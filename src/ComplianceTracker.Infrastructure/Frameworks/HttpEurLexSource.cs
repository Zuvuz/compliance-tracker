using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Common;
using Microsoft.Extensions.Configuration;

namespace ComplianceTracker.Infrastructure.Frameworks
{
    // US-137 R1: fetches an EU act's Formex 4 files from the EU Publications Office (CELLAR), the source behind EUR-Lex.
    // It asks for the act by its CELEX number as a zip of Formex XML in English. Off until the operator sets
    // EurLex:Enabled, since it needs internet; EurLex:BaseAddress can point at a mirror.
    public class HttpEurLexSource : IEurLexSource
    {
        // Limits, so a wrong answer cannot fill the memory (zip bomb)
        private const long MaxDownload = 40 * 1024 * 1024;
        private const long MaxUnpacked = 200 * 1024 * 1024;
        private const int MaxFiles = 500;

        private readonly HttpClient _http;
        private readonly IConfiguration _configuration;

        public HttpEurLexSource(HttpClient http, IConfiguration configuration)
        {
            _http = http;
            _configuration = configuration;
        }

        public bool IsEnabled => _configuration.GetValue<bool>("EurLex:Enabled");

        private Uri BaseAddress => new(_configuration["EurLex:BaseAddress"] is { Length: > 0 } address ? address : "https://publications.europa.eu/");

        public async Task<EurLexAct> FetchAsync(string celex, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseAddress, $"resource/celex/{Uri.EscapeDataString(celex)}"));
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/zip;mtype=fmx4"));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("eng"));

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (HttpRequestException)
            {
                throw new DomainException("EUR-Lex cannot be reached right now. Try again later.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new DomainException("EUR-Lex cannot be reached right now. Try again later.");
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new DomainException($"EUR-Lex has no act with the CELEX number {celex} in Formex");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new DomainException("EUR-Lex cannot be reached right now. Try again later.");
                }

                var package = await ReadLimitedAsync(response, cancellationToken);
                return new EurLexAct(celex, package, Unpack(package));
            }
        }

        private static async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.Content.Headers.ContentLength > MaxDownload)
            {
                throw new DomainException("The act from EUR-Lex is too large");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxDownload)
                {
                    throw new DomainException("The act from EUR-Lex is too large");
                }
            }

            return buffer.ToArray();
        }

        // The Formex files of the package: every .xml entry, read with a size limit
        public static IReadOnlyList<string> Unpack(byte[] package)
        {
            try
            {
                using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
                var files = new List<string>();
                long unpacked = 0;
                foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).Take(MaxFiles))
                {
                    unpacked += entry.Length;
                    if (unpacked > MaxUnpacked)
                    {
                        throw new DomainException("The act from EUR-Lex is too large");
                    }

                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                    files.Add(reader.ReadToEnd());
                }

                return files.Count > 0 ? files : throw new DomainException("The answer from EUR-Lex holds no Formex files");
            }
            catch (InvalidDataException)
            {
                throw new DomainException("The answer from EUR-Lex is not a Formex package");
            }
        }
    }
}
