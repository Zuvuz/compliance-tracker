namespace ComplianceTracker.Application.Frameworks
{
    // US-041: short names and addresses of frameworks, e.g. "CRA – Regulation (EU) 2024/2847" -> "CRA" -> "cra".
    // US-058: the same slug starts a requirement's test tag, e.g. "@cra-I.2(d)".
    public static class FrameworkSlugs
    {
        // The built-in frameworks are called e.g. "CRA – Regulation (EU) 2024/2847": the part before " – " is the short name
        public static string Short(string name) => name.Split(" – ")[0].Trim();

        // "Security policy" -> "security-policy"
        public static string Slug(string name)
        {
            var chars = Short(name).ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
            var slug = new string(chars);
            while (slug.Contains("--"))
            {
                slug = slug.Replace("--", "-");
            }

            return slug.Trim('-');
        }
    }
}
