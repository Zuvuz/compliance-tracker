using ComplianceTracker.Specs.Support;
using Npgsql;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-084: TLS between the app and the database, against a PostgreSQL started like "postgres-full".
    // The app's own connection string uses SSL Mode=VerifyFull with the local CA (docker-compose.yml), as here.
    [Binding]
    public sealed class DatabaseTlsStepDefinitions
    {
        private TlsPostgres? _database;
        private bool? _encrypted;
        private Exception? _error;

        [Given("a PostgreSQL that only accepts TLS with a certificate from a test CA")]
        public async Task GivenAPostgreSqlThatOnlyAcceptsTls()
            => _database = await TlsPostgres.StartAsync();

        [When("the app connects with the test CA")]
        public Task WhenTheAppConnectsWithTheTestCa()
            => ConnectAsync(_database!.ConnectionString(SslMode.VerifyFull, _database.CaFile));

        [When("a client connects without TLS")]
        public Task WhenAClientConnectsWithoutTls()
            => ConnectAsync(_database!.ConnectionString(SslMode.Disable));

        [When("the app connects with another CA")]
        public async Task WhenTheAppConnectsWithAnotherCa()
        {
            var other = TlsPostgres.OtherCaFile();
            try
            {
                await ConnectAsync(_database!.ConnectionString(SslMode.VerifyFull, other));
            }
            finally
            {
                File.Delete(other);
            }
        }

        [Then("the connection is encrypted")]
        public void ThenTheConnectionIsEncrypted()
        {
            Assert.Null(_error);
            Assert.True(_encrypted, "PostgreSQL says the connection does not use TLS.");
        }

        [Then("the connection is refused")]
        public void ThenTheConnectionIsRefused()
        {
            Assert.NotNull(_error);
            Assert.Null(_encrypted);
        }

        // Opens a connection and asks PostgreSQL itself whether it uses TLS
        private async Task ConnectAsync(string connectionString)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand("select ssl from pg_stat_ssl where pid = pg_backend_pid()", connection);
                _encrypted = (bool)(await command.ExecuteScalarAsync())!;
            }
            catch (Exception error) when (error is NpgsqlException or System.Security.Authentication.AuthenticationException)
            {
                _error = error;
            }
        }

        [AfterScenario("US-084")]
        public async Task StopTheDatabaseAsync()
        {
            if (_database is not null)
            {
                await _database.DisposeAsync();
            }
        }
    }
}
