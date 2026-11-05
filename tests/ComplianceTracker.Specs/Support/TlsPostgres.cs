using Npgsql;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Testcontainers.PostgreSql;

namespace ComplianceTracker.Specs.Support
{
    // US-084: a PostgreSQL that only accepts TLS, started with the same entrypoint script and pg_hba file as the
    // "postgres-full" service in docker-compose.yml. Its certificate comes from a test CA made here, so no keys
    // are ever stored. One container per scenario, apart from the shared test database.
    public sealed class TlsPostgres : IAsyncDisposable
    {
        private const string Password = "tls-test-only";

        private readonly PostgreSqlContainer _container;

        // The CA's certificate as a file, for SSL Mode=VerifyFull
        public string CaFile { get; }

        private TlsPostgres(PostgreSqlContainer container, string caFile)
        {
            _container = container;
            CaFile = caFile;
        }

        public static async Task<TlsPostgres> StartAsync()
        {
            using var ca = Ca("Test CA");
            var (certificate, key) = ServerCertificate(ca, "localhost");

            var scripts = Path.Combine(AppContext.BaseDirectory, "scripts");
            var container = new PostgreSqlBuilder("postgres:17")
                .WithPassword(Password)
                .WithResourceMapping(Encoding.ASCII.GetBytes(certificate), "/certs/db.crt")
                .WithResourceMapping(Encoding.ASCII.GetBytes(key), "/certs/db.key")
                .WithResourceMapping(new FileInfo(Path.Combine(scripts, "postgres-tls-entrypoint.sh")), "/scripts/")
                .WithResourceMapping(new FileInfo(Path.Combine(scripts, "pg_hba_tls.conf")), "/etc/postgresql/")
                .WithEntrypoint("sh", "/scripts/postgres-tls-entrypoint.sh")
                .WithCommand("postgres")
                .Build();
            await container.StartAsync();

            var caFile = Path.Combine(Path.GetTempPath(), $"test-ca-{Guid.NewGuid():N}.crt");
            await File.WriteAllTextAsync(caFile, ca.ExportCertificatePem());
            return new TlsPostgres(container, caFile);
        }

        // A connection string to the container from the test, with the given TLS settings
        public string ConnectionString(SslMode mode, string? rootCertificate = null)
            => new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
            {
                SslMode = mode,
                RootCertificate = rootCertificate,
                Pooling = false
            }.ConnectionString;

        // Another CA's certificate, which the database's certificate is not signed by
        public static string OtherCaFile()
        {
            using var other = Ca("Another CA");
            var file = Path.Combine(Path.GetTempPath(), $"other-ca-{Guid.NewGuid():N}.crt");
            File.WriteAllText(file, other.ExportCertificatePem());
            return file;
        }

        public async ValueTask DisposeAsync()
        {
            await _container.DisposeAsync();
            File.Delete(CaFile);
        }

        private static X509Certificate2 Ca(string name)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            // Valid a day longer than the server certificate, which must end before its CA does
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddDays(2));
        }

        // The database's certificate, signed by the CA, for the host name the test connects to
        private static (string Certificate, string Key) ServerCertificate(X509Certificate2 ca, string host)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest($"CN={host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(host);
            names.AddIpAddress(System.Net.IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));

            using var certificate = request.Create(ca, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
            return (certificate.ExportCertificatePem(), key.ExportPkcs8PrivateKeyPem());
        }
    }
}
