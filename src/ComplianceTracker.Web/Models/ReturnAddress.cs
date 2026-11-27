using Microsoft.AspNetCore.Routing;

namespace ComplianceTracker.Web.Models
{
    // US-228: the project pages a form can come from and go back to (R1)
    public enum ReturnPage
    {
        Compliance,  // a framework's requirements on the Compliance tab, maybe filtered
        Requirement, // one requirement's own page (US-153)
        Link,        // the Link page with a requirement chosen (US-220)
        Evidence     // the Evidence tab, or one piece of evidence on it (US-154)
    }

    // US-228: where a form on a project page goes back to, after the action and after a broken rule (R1, R2). The form
    // carries it as four named hidden fields (_ReturnAddress.cshtml): back (the page), sub, req and show. The server builds
    // the address from known pages only; nothing in the form is used as a URL, so it cannot send anyone elsewhere (R3).
    public sealed record ReturnAddress(ReturnPage Page, string? Sub = null, string? Req = null, string? Show = null)
    {
        public const string BackField = "back";
        public const string SubField = "sub";
        public const string ReqField = "req";
        public const string ShowField = "show";

        // A framework slug or an evidence id is short; anything longer is not one
        public const int MaxSubLength = 100;

        public static ReturnAddress ForCompliance(string? sub, string? show) => Of(ReturnPage.Compliance, sub, null, show)!;

        public static ReturnAddress ForRequirement(string? sub, string code) => Of(ReturnPage.Requirement, sub, code, null)!;

        public static ReturnAddress ForLink(string? sub, string? code, string? show) => Of(ReturnPage.Link, sub, code, show)!;

        public static ReturnAddress ForEvidence(Guid? evidenceId = null) => new(ReturnPage.Evidence, evidenceId?.ToString());

        // R3: the address in the form's fields, or in the query of a GET (the confirmation of an AI suggestion, US-221).
        // Null when the request names no known page.
        public static ReturnAddress? From(HttpRequest request)
        {
            string? Field(string name)
                => request.HasFormContentType && request.Form.TryGetValue(name, out var formValue) ? formValue.ToString()
                    : request.Query.TryGetValue(name, out var queryValue) ? queryValue.ToString()
                    : null;

            return Parse(Field(BackField), Field(SubField), Field(ReqField), Field(ShowField));
        }

        // R3: an unknown page is no address; each field is checked for the page it belongs to
        public static ReturnAddress? Parse(string? back, string? sub, string? req, string? show)
        {
            var page = PageOf(back);
            return page is null ? null : Of(page.Value, sub, req, show);
        }

        // For forms that do not say where they came from (older forms, and tests that post the fields themselves): the
        // rule as it was before, so nothing changes for them. A requirement code means its page, otherwise the
        // framework's list on the Compliance tab.
        public static ReturnAddress FallbackToCompliance(string? sub, string? req, string? show)
            => RequirementPages.Clean(req) is { } code ? ForRequirement(sub, code) : ForCompliance(sub, show);

        // The values for RedirectToAction(Details) or Url.Action(Details) of the project
        public RouteValueDictionary RouteValues(Guid projectId) => Page switch
        {
            ReturnPage.Requirement => new()
            {
                ["id"] = projectId, ["tab"] = ProjectTabs.Compliance, ["sub"] = Sub ?? ComplianceFilters.AllSub, ["req"] = Req
            },
            ReturnPage.Link => new()
            {
                ["id"] = projectId, ["tab"] = ProjectTabs.Link, ["sub"] = Sub == ComplianceFilters.AllSub ? null : Sub, ["req"] = Req, ["show"] = Show
            },
            ReturnPage.Evidence => new() { ["id"] = projectId, ["tab"] = ProjectTabs.Evidence, ["sub"] = Sub },
            _ => new() { ["id"] = projectId, ["tab"] = ProjectTabs.Compliance, ["sub"] = Sub, ["show"] = Show }
        };

        // The four fields as the form sends them, without the empty ones; for the hidden fields, a confirmation's query
        // and an Undo (R4, R5)
        public IReadOnlyDictionary<string, string> Fields()
        {
            var fields = new Dictionary<string, string> { [BackField] = Slug(Page) };
            if (Sub is not null)
            {
                fields[SubField] = Sub;
            }

            if (Req is not null)
            {
                fields[ReqField] = Req;
            }

            if (Show is not null)
            {
                fields[ShowField] = Show;
            }

            return fields;
        }

        // R5: the Undo after an action goes back where the action was made; it replaces any fields of an older address
        public void AddTo(IDictionary<string, string> values)
        {
            foreach (var name in new[] { BackField, SubField, ReqField, ShowField })
            {
                values.Remove(name);
            }

            foreach (var (name, value) in Fields())
            {
                values[name] = value;
            }
        }

        public static string Slug(ReturnPage page) => page switch
        {
            ReturnPage.Requirement => "requirement",
            ReturnPage.Link => ProjectTabs.Link,
            ReturnPage.Evidence => ProjectTabs.Evidence,
            _ => ProjectTabs.Compliance
        };

        private static ReturnPage? PageOf(string? back)
            => Enum.GetValues<ReturnPage>().Cast<ReturnPage?>()
                .FirstOrDefault(p => string.Equals(Slug(p!.Value), back?.Trim(), StringComparison.OrdinalIgnoreCase));

        private static ReturnAddress? Of(ReturnPage page, string? sub, string? req, string? show)
        {
            var cleanSub = Clean(sub);
            var code = RequirementPages.Clean(req);
            var filter = string.IsNullOrWhiteSpace(show) ? null : show.Trim();
            return page switch
            {
                // A requirement's page without a code is the framework's list
                ReturnPage.Requirement => code is null ? new ReturnAddress(ReturnPage.Compliance, cleanSub) : new ReturnAddress(page, cleanSub, code),
                // The Link page's own filters only (US-220 R6)
                ReturnPage.Link => new ReturnAddress(page, cleanSub, code, filter is null ? null : LinkPage.FilterOf(filter)),
                // An evidence page is an id; anything else is the list
                ReturnPage.Evidence => new ReturnAddress(page, Guid.TryParse(cleanSub, out var id) ? id.ToString() : null),
                // The Compliance tab's filters; "all" and unknown ones are no filter (US-044 R2)
                _ => new ReturnAddress(page, cleanSub, null, KnownComplianceFilter(filter))
            };
        }

        private static string? KnownComplianceFilter(string? filter)
        {
            var known = filter is null ? ComplianceFilters.All : ComplianceFilters.Find(filter);
            return known == ComplianceFilters.All ? null : known;
        }

        private static string? Clean(string? sub)
        {
            var trimmed = sub?.Trim();
            return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxSubLength ? null : trimmed;
        }
    }
}
