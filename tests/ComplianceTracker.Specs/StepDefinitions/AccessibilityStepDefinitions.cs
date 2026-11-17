using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-185: every page of a seeded organisation, checked for the WCAG rules a page can be read for: a field has a label
    // tied to it (R1), a button or link has a name (R2), an image has alt text (R3), and the shared dialog can be used with
    // the keyboard (R4). A new page that breaks a rule fails the build and names the page and the element (R5).
    [Binding]
    public class AccessibilityStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly List<(string Path, IDocument Page)> _pages = [];

        public AccessibilityStepDefinitions(WebScenario web) => _web = web;

        [When("I open every page of {string}")]
        public async Task WhenIOpenEveryPage(string organisation)
        {
            Guid projectId, riskId, areaId;
            await using (var db = _web.CreateDbContext())
            {
                var org = await db.Organizations.SingleAsync(o => o.Name == organisation);
                projectId = await db.Projects.Where(p => p.OrganizationId == org.Id && p.Kind != Domain.Projects.ProjectKind.Organisation).Select(p => p.Id).FirstAsync();
                riskId = await db.Risks.Where(r => r.ProjectId == projectId).Select(r => r.Id).FirstAsync();
            }

            await _web.OpenAsync("/Organisation"); // makes the organisation area
            await using (var db = _web.CreateDbContext())
            {
                areaId = await db.Projects.Where(p => p.Kind == Domain.Projects.ProjectKind.Organisation).Select(p => p.Id).SingleAsync();
            }

            var paths = new List<string>
            {
                "/Projects", "/Projects/Create", "/Frameworks", "/Frameworks/BuiltIn", "/Documents", "/Users", "/Activity", "/Settings",
                "/Organisation/Profile", "/Review", "/Search?q=risk", "/Suppliers", "/Checklists", "/GapAnalysis", "/Reminders", "/Help",
                $"/Risks/Details/{riskId}", $"/Audits?projectId={projectId}", $"/Projects/Edit/{projectId}",
                $"/Projects/Details/{projectId}/compliance/cra/I.2(d)", $"/Projects/TestResults/{projectId}"
            };
            foreach (var tab in new[] { "overview", "frameworks", "compliance", "evidence", "risks", "security-events", "scans", "product-requirements", "suggestions", "activity", "reports" })
            {
                paths.Add($"/Projects/Details/{projectId}/{tab}");
            }

            paths.Add($"/Projects/Details/{areaId}/nis2-scope");

            foreach (var path in paths)
            {
                await _web.OpenAsync(path);
                Assert.True(_web.Response!.IsSuccessStatusCode, $"{path} answered {(int)_web.Response.StatusCode}.");
                _pages.Add((path, _web.Page!));
            }
        }

        [Then("every field has a label a screen reader can read")]
        public void ThenEveryFieldHasALabel()
        {
            var problems = _pages.SelectMany(p => Problems(p.Page).Select(problem => $"{p.Path}: {problem}")).Distinct().ToList();
            Assert.True(problems.Count == 0, "Not usable with a screen reader:\n" + string.Join("\n", problems.Take(60)));
        }

        // R1-R3: what a screen reader would miss on one page
        public static IEnumerable<string> Problems(IDocument page)
        {
            foreach (var field in page.QuerySelectorAll("input, select, textarea"))
            {
                var type = field.GetAttribute("type")?.ToLowerInvariant();
                if (type is "hidden" or "submit" or "button" or "reset" or "image")
                {
                    continue;
                }

                if (!HasLabel(page, field))
                {
                    yield return $"the field {Describe(field)} has no label";
                }
            }

            foreach (var button in page.QuerySelectorAll("button, input[type=submit], input[type=button], a[href], summary"))
            {
                if (string.IsNullOrWhiteSpace(Name(page, button)))
                {
                    yield return $"the {button.LocalName} {Describe(button)} has no name";
                }
            }

            foreach (var image in page.QuerySelectorAll("img"))
            {
                if (!image.HasAttribute("alt"))
                {
                    yield return $"the image {Describe(image)} has no alt text";
                }
            }
        }

        private static bool HasLabel(IDocument page, IElement field)
        {
            if (!string.IsNullOrWhiteSpace(field.GetAttribute("aria-label")) || !string.IsNullOrWhiteSpace(field.GetAttribute("title")))
            {
                return true;
            }

            if (field.GetAttribute("aria-labelledby") is { } ids
                && ids.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(id => !string.IsNullOrWhiteSpace(page.GetElementById(id)?.TextContent)))
            {
                return true;
            }

            if (field.Closest("label") is { } wrapping && !string.IsNullOrWhiteSpace(wrapping.TextContent))
            {
                return true;
            }

            return field.Id is { Length: > 0 } id
                   && page.QuerySelectorAll("label").Any(l => l.GetAttribute("for") == id && !string.IsNullOrWhiteSpace(l.TextContent));
        }

        private static string Name(IDocument page, IElement element)
        {
            var aria = element.GetAttribute("aria-label");
            if (!string.IsNullOrWhiteSpace(aria))
            {
                return aria;
            }

            if (element.GetAttribute("aria-labelledby") is { } ids)
            {
                return string.Join(" ", ids.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(id => page.GetElementById(id)?.TextContent ?? ""));
            }

            if (element.LocalName == "input")
            {
                return element.GetAttribute("value") ?? "";
            }

            var text = element.TextContent;
            var alt = string.Join(" ", element.QuerySelectorAll("img[alt]").Select(i => i.GetAttribute("alt")));
            return (text + " " + alt + " " + (element.GetAttribute("title") ?? "")).Trim();
        }

        private static string Describe(IElement element)
        {
            var html = element.OuterHtml.Replace("\n", " ");
            var end = html.IndexOf('>');
            return end > 0 && end < 160 ? html[..(end + 1)] : html[..Math.Min(160, html.Length)];
        }
    }
}
