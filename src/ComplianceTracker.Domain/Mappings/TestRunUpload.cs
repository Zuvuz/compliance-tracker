namespace ComplianceTracker.Domain.Mappings
{
    // #52 (US-058, US-177): one upload of test results (TRX or JUnit) to a project, kept whatever it led to, so the upload can
    // be seen afterwards also when it made no suggestions, and the reason is shown: tags already suggested from the same
    // file, tags with a code the framework does not have, or tags of frameworks the project does not follow.
    public class TestRunUpload
    {
        public const int MaxListLength = 2000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public string FileName { get; private set; } = "";
        public string Sha256 { get; private set; } = "";
        public Guid UploadedById { get; private set; }
        public DateTime UploadedAt { get; private set; }

        public int Passed { get; private set; }
        public int Failed { get; private set; }

        // New suggestions made, and of those the ones blocked by a failing test
        public int Suggested { get; private set; }
        public int Blocked { get; private set; }

        // Requirements that already had a suggestion from the same file (pending, blocked, accepted or rejected)
        public int AlreadySuggested { get; private set; }

        // e.g. "@cra-13 @cra-annexVII-1a": a framework the project follows, but a code it does not have
        public string UnknownTags { get; private set; } = "";

        // e.g. "iso27001 aiact": tags of frameworks the project does not follow
        public string OtherFrameworks { get; private set; } = "";

        // For EF Core only
        private TestRunUpload() { }

        public TestRunUpload(
            Guid projectId, string fileName, string sha256, Guid uploadedById, DateTime uploadedAt, int passed, int failed,
            int suggested, int blocked, int alreadySuggested, IEnumerable<string> unknownTags, IEnumerable<string> otherFrameworks)
        {
            ProjectId = projectId;
            FileName = fileName.Length <= 200 ? fileName : fileName[..200];
            Sha256 = sha256;
            UploadedById = uploadedById;
            UploadedAt = uploadedAt;
            Passed = passed;
            Failed = failed;
            Suggested = suggested;
            Blocked = blocked;
            AlreadySuggested = alreadySuggested;
            UnknownTags = Cut(string.Join(" ", unknownTags));
            OtherFrameworks = Cut(string.Join(" ", otherFrameworks));
        }

        // What the upload led to, in one line, e.g. "203 passed · 12 suggested · 3 already suggested from this file"
        public string Summary
        {
            get
            {
                var parts = new List<string> { $"{Passed} passed" };
                if (Failed > 0)
                {
                    parts.Add($"{Failed} failed");
                }

                parts.Add($"{Suggested - Blocked} suggested");
                if (Blocked > 0)
                {
                    parts.Add($"{Blocked} blocked by failing tests");
                }

                if (AlreadySuggested > 0)
                {
                    parts.Add($"{AlreadySuggested} already suggested from this file");
                }

                return string.Join(" · ", parts);
            }
        }

        // Why an upload made no new suggestions, or null when it made some
        public string? WhyNothing => Suggested > 0 ? null
            : AlreadySuggested > 0 ? "The same file was uploaded before; its suggestions are listed under it."
            : OtherFrameworks.Length > 0 && UnknownTags.Length == 0 ? $"The tags name frameworks this project does not follow: {OtherFrameworks}. Add the framework to the project, or check the tags."
            : UnknownTags.Length > 0 ? $"No tag matched a requirement. Unknown codes: {UnknownTags}."
            : "No test had a requirement tag such as @cra-I.2(d).";

        private static string Cut(string text) => text.Length <= MaxListLength ? text : text[..MaxListLength];
    }
}
