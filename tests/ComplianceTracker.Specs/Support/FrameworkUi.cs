namespace ComplianceTracker.Specs.Support
{
    // Shared UI actions on a framework version's page (US-007), used by several stories' steps.
    // Since US-007 an upload is a draft, and projects can only use it after it has been activated.
    public static class FrameworkUi
    {
        private const string VersionPath = "/Frameworks/Version/";

        // The id of the version whose page is open (from the address, because an active version's page has no forms)
        public static string OpenVersionId(WebScenario web)
            => web.CurrentPath.StartsWith(VersionPath, StringComparison.OrdinalIgnoreCase)
                ? web.CurrentPath[VersionPath.Length..]
                : throw new Xunit.Sdk.XunitException($"The page {web.CurrentPath} is not a framework version page.");

        // Opens a version's page
        public static Task OpenVersionAsync(WebScenario web, string versionId)
            => web.OpenAsync($"/Frameworks/Version/{versionId}");

        // Adds a requirement on the open draft page, like typing it into the "Add requirement" row
        public static async Task AddRequirementAsync(WebScenario web, string versionId, string code, string title)
            => await web.PostFormAsync("/Frameworks/AddRequirement", new Dictionary<string, string>
            {
                ["versionId"] = versionId,
                ["code"] = code,
                ["title"] = title,
                ["description"] = ""
            });

        public static Task ActivateAsync(WebScenario web, string versionId)
            => web.PostFormAsync("/Frameworks/Activate", new Dictionary<string, string> { ["versionId"] = versionId });

        // The page right after an upload is the new draft. A test PDF has no text, so one requirement
        // is added by hand (R5) and the version is activated (R3).
        public static async Task ActivateUploadedDraftAsync(WebScenario web)
        {
            var versionId = OpenVersionId(web);
            await AddRequirementAsync(web, versionId, "1.1", "Test requirement");
            await ActivateAsync(web, versionId);

            if (web.Page!.QuerySelector("#version-status")?.TextContent.Trim() != "Active")
            {
                throw new Xunit.Sdk.XunitException("The uploaded version could not be activated.");
            }
        }
    }
}
