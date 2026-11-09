using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Web.Models
{
    // US-142 R8: the language requirement texts are shown in. The pages themselves are English for now; when they can
    // be shown in other languages (later), this is where the choice is made. Until then the browser's preferred
    // language decides, among the languages the tracker has texts in. English otherwise.
    public static class PageLanguage
    {
        public static readonly IReadOnlyList<string> Supported = [Requirement.DefaultLanguage, "da"];

        public static string From(HttpRequest request)
        {
            var wanted = request.GetTypedHeaders().AcceptLanguage
                .OrderByDescending(l => l.Quality ?? 1)
                .Select(l => RequirementText.Normalize(l.Value.Value));
            return wanted.FirstOrDefault(l => l is not null && Supported.Contains(l)) ?? Requirement.DefaultLanguage;
        }
    }
}
