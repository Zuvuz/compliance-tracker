using ComplianceTracker.Infrastructure.Reports;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-197: the seal on the PDFs the audit package makes, checked like a PDF reader does (US-132's PdfSigner.Verify)
    [Binding]
    public class SealedDossierStepDefinitions
    {
        private readonly AuditPackageStepDefinitions _packages;

        public SealedDossierStepDefinitions(AuditPackageStepDefinitions packages)
        {
            _packages = packages;
        }

        [Then("the package's file {string} has a valid seal by {string}")]
        public async Task ThenHasAValidSeal(string path, string organisation)
        {
            var (valid, covers, signer) = PdfSigner.Verify(await _packages.PackageFileAsync(path));
            Assert.True(valid, $"{path} has no valid seal.");
            Assert.True(covers, $"The seal of {path} does not cover the whole file.");
            Assert.Contains(organisation, signer!.Subject, StringComparison.Ordinal);
        }

        [Then("the package's file {string} with one byte changed has no valid seal")]
        public async Task ThenAChangedFileHasNoValidSeal(string path)
        {
            var file = (await _packages.PackageFileAsync(path)).ToArray();
            // A byte of the page content, before the signature's place near the end
            file[file.Length / 3] ^= 0x01;
            Assert.False(SafeVerify(file), $"{path} still has a valid seal after a change.");
        }

        [Then("the package's file {string} is not sealed")]
        public async Task ThenIsNotSealed(string path)
            => Assert.False(SafeVerify(await _packages.PackageFileAsync(path)), $"{path} is sealed.");

        private static bool SafeVerify(byte[] file)
        {
            try
            {
                return PdfSigner.Verify(file).Valid;
            }
            catch (Exception)
            {
                return false; // no signature, or one that cannot be read, is not a valid seal
            }
        }
    }
}
