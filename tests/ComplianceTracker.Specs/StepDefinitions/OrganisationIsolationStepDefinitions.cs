using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Specs.Support;
using ComplianceTracker.Web.Hubs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Net;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-086: calls every controller action as a member of another organisation, with Acme ApS's ids.
    // The actions are found from the app's own list (IActionDescriptorCollectionProvider), so a new action is
    // tested by itself. Ids are filled in by the parameter's name; an id that cannot be filled fails the test (R6).
    [Binding]
    public class OrganisationIsolationStepDefinitions
    {
        // R3: what is not called, and why. Everything else with an id is called.
        private static readonly Dictionary<string, string> NotCalled = new()
        {
            ["Account"] = "Signing in and out; no data",
            ["Home"] = "Open to everyone; no data",
            ["Invitations"] = "Reached by a secret token from the link, not by an id (US-009)"
        };

        // Ids the fixture has no data for; a random id is used, which can only prove "not found"
        private static readonly Dictionary<string, string> NoDataFor = new()
        {
            ["suggestionId"] = "The fixture has no AI suggestion (that needs the pretend AI's ranking)",
            ["draftId"] = "The fixture has no story draft (that needs the pretend AI's writing); AskTheAi.feature tests it (US-106)",
            ["mappingId"] = "The fixture has no cross-mapping (that needs the pretend AI's vectors and two frameworks); CrossMapping.feature tests it (US-037)",
            ["markId"] = "The fixture has no \"needs action\" mark (that needs the pretend AI's answer); NeedsAction.feature tests it (US-155)",
            ["checkId"] = "The fixture has no evidence check (that needs the pretend AI's answer); EvidenceCheck.feature tests it (US-038)",
            ["requestId"] = "The fixture has no framework activation waiting for approval (that needs an import from EUR-Lex); CompleteRequirements.feature tests it (US-137)"
        };

        // Pages the own member gets "not found" for, because the fixture has nothing there yet, and why. They are still
        // called by the other organisation, where "not found" is the right answer.
        private static readonly Dictionary<string, string> EmptyForOwn = new()
        {
            ["Frameworks.CrossMapping"] = "The page only takes versions the organisation's projects use, and the fixture's framework is used by none (US-037), tested in CrossMapping.feature",
            ["Projects.SealedReport"] = "Only a sealed report has this file; sealing needs a seal certificate and two signers (US-132), tested in SigningReports.feature",
            ["Projects.StatementOfApplicability"] = "Only an organisation with its own ISO/IEC 27001 has one; the fixture's one framework is a policy (US-053), tested in StatementOfApplicability.feature"
        };

        private readonly WebScenario _web;
        private readonly DatabaseScenario _database;
        private readonly RiskRegisterStepDefinitions _risks;
        private Ids? _ids;
        private readonly List<string> _wrongResults = new();
        private readonly List<string> _changed = new();
        private readonly List<string> _unfilled = new();
        private readonly List<string> _called = new();
        private Exception? _hubError;

        public OrganisationIsolationStepDefinitions(WebScenario web, DatabaseScenario database, RiskRegisterStepDefinitions risks)
        {
            _web = web;
            _database = database;
            _risks = risks;
        }

        // ---------------------------------------------------------- Given

        [Given("the risk {string} has the measure {string}")]
        public Task GivenTheRiskHasTheMeasure(string risk, string measure)
            => _risks.WhenILinkTheRisk(risk, measure);

        // ---------------------------------------------------------- When

        [When("{string} calls every action with the ids of {string}")]
        public async Task WhenCallsEveryAction(string userName, string organisation)
        {
            _ids = await IdsAsync(organisation);
            using var client = _web.CreateClient(signedInAs: userName, followRedirects: false);
            var token = await AntiforgeryTokenAsync(client);
            var tables = await TablesAsync();

            foreach (var action in Actions())
            {
                var request = Request(action);
                if (request is null)
                {
                    continue;
                }

                _called.Add($"{request.Method} {request.Path}");
                if (request.Method == "GET")
                {
                    var response = await client.GetAsync(request.Path + request.Query);
                    if (!Refused(response))
                    {
                        _wrongResults.Add($"{Name(action)}: GET gave {(int)response.StatusCode} {response.Headers.Location}");
                    }
                }
                else
                {
                    var before = await ChecksumAsync(tables);
                    var form = new Dictionary<string, string>(request.Form) { ["__RequestVerificationToken"] = token };
                    var response = await client.PostAsync(request.Path, new FormUrlEncodedContent(form));
                    if ((int)response.StatusCode >= 500)
                    {
                        _wrongResults.Add($"{Name(action)}: POST gave {(int)response.StatusCode}");
                    }

                    var after = await ChecksumAsync(tables);
                    _changed.AddRange(after.Where(t => before[t.Key] != t.Value).Select(t => $"{Name(action)} changed \"{t.Key}\""));
                }
            }
        }

        // The same pages and files, as the organisation's own member: proves the ids and addresses are real
        [When("{string} opens every page and file with the ids of {string}")]
        public async Task WhenOpensEveryPage(string userName, string organisation)
        {
            _ids = await IdsAsync(organisation);
            using var client = _web.CreateClient(signedInAs: userName, followRedirects: false);
            foreach (var action in Actions().Where(a => Method(a) == "GET"))
            {
                if (Request(action) is not { } request || action.ControllerName == "System"
                    || EmptyForOwn.ContainsKey($"{action.ControllerName}.{action.ActionName}"))
                {
                    continue; // the system pages are for the system admin only (US-010); EmptyForOwn says why for the rest
                }

                var response = await client.GetAsync(request.Path + request.Query);
                _called.Add(Name(action));
                if ((int)response.StatusCode >= 400)
                {
                    _wrongResults.Add($"{Name(action)}: GET gave {(int)response.StatusCode} for the organisation's own member");
                }
            }
        }

        [When("{string} follows the live updates of {string}")]
        public async Task WhenFollowsTheLiveUpdates(string userName, string projectName)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();

            var server = _web.App.Factory.Server;
            await using var hub = new HubConnectionBuilder()
                .WithUrl(new Uri(server.BaseAddress, WorkItemsHub.Path), options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.Headers[TestAuthHandler.UserHeader] = userName;
                })
                .Build();
            await hub.StartAsync();
            try
            {
                await hub.InvokeAsync("Follow", projectId);
            }
            catch (HubException error)
            {
                _hubError = error;
            }
        }

        // ---------------------------------------------------------- Then

        [Then("every page and file gives {string} or {string}")]
        public void ThenEveryPageGivesNotFoundOrForbidden(string notFound, string forbidden)
        {
            Assert.NotEmpty(_called);
            Console.WriteLine($"Called {_called.Count} actions with Acme's ids:\n" + string.Join("\n", _called));
            Assert.True(_wrongResults.Count == 0,
                $"These were not refused with {notFound} or {forbidden}:\n" + string.Join("\n", _wrongResults));
        }

        [Then("no change changed anything in the database")]
        public void ThenNoChangeChangedAnything()
            => Assert.True(_changed.Count == 0, "Data changed by another organisation:\n" + string.Join("\n", _changed));

        [Then("every id the actions take was one of Acme ApS's")]
        public void ThenEveryIdWasAcmes()
            => Assert.True(_unfilled.Count == 0,
                "No id of Acme ApS for these parameters; add one to the fixture or to NoDataFor with a reason:\n" + string.Join("\n", _unfilled));

        [Then("they all work")]
        public void ThenTheyAllWork()
        {
            Assert.NotEmpty(_called);
            Assert.True(_wrongResults.Count == 0, string.Join("\n", _wrongResults));
        }

        [Then("the hub refuses")]
        public void ThenTheHubRefuses()
            => Assert.NotNull(_hubError);

        // ---------------------------------------------------------- Actions and their requests

        private IEnumerable<ControllerActionDescriptor> Actions()
            => _web.App.Factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
                .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
                .Where(a => !NotCalled.ContainsKey(a.ControllerName))
                .OrderBy(Name);

        private static string Name(ControllerActionDescriptor action) => $"{action.ControllerName}.{action.ActionName} ({Method(action)})";

        private static string Method(ActionDescriptor action)
            => action.ActionConstraints?.OfType<HttpMethodActionConstraint>().FirstOrDefault()?.HttpMethods.First() ?? "GET";

        private sealed record ActionRequest(string Method, string Path, string Query, Dictionary<string, string> Form);

        // The request for an action, with Acme's ids; null when the action takes no id (it only acts on the caller's own organisation)
        private ActionRequest? Request(ControllerActionDescriptor action)
        {
            var values = new Dictionary<string, string>();
            var hasId = false;
            foreach (var parameter in action.Parameters)
            {
                var type = parameter.ParameterType;
                if (type == typeof(CancellationToken) || type == typeof(IFormFile))
                {
                    continue;
                }

                if (IsSimple(type))
                {
                    hasId |= Add(values, action, parameter.Name, type);
                    continue;
                }

                // US-059: a list of ids as its own parameter, e.g. requirementIds
                if (type == typeof(List<Guid>))
                {
                    hasId |= Add(values, action, parameter.Name, typeof(Guid));
                    continue;
                }

                // A form model: its properties are the fields
                foreach (var property in type.GetProperties().Where(p => p.CanWrite))
                {
                    if (property.PropertyType == typeof(List<Guid>))
                    {
                        if (Id(action, property.Name) is { } listId)
                        {
                            values[property.Name] = listId.ToString();
                            hasId = true;
                        }

                        continue;
                    }

                    if (IsSimple(property.PropertyType))
                    {
                        hasId |= Add(values, action, property.Name, property.PropertyType);
                    }
                }
            }

            if (!hasId)
            {
                return null;
            }

            var method = Method(action);

            // A page only needs its ids; its other inputs (tab, filters) are optional and have their own defaults
            if (method == "GET")
            {
                foreach (var key in values.Keys.Where(k => !Guid.TryParse(values[k], out _)).ToList())
                {
                    values.Remove(key);
                }
            }

            var links = _web.App.Factory.Services.GetRequiredService<LinkGenerator>();
            var routeValues = new RouteValueDictionary();
            foreach (var name in action.AttributeRouteInfo?.Template?.Split('{', '}', '?', ':').ToHashSet() ?? ["id"])
            {
                if (values.TryGetValue(name, out var value))
                {
                    routeValues[name] = value;
                }
            }

            var path = links.GetPathByAction(action.ActionName, action.ControllerName, routeValues)
                ?? throw new Xunit.Sdk.XunitException($"No address for {Name(action)}");
            var query = method == "GET"
                ? QueryString.Create(values.Where(v => !routeValues.ContainsKey(v.Key)).Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).ToString()
                : "";
            return new ActionRequest(method, path, query, values);
        }

        // Adds the parameter's value; returns true when it is an id
        private bool Add(Dictionary<string, string> values, ControllerActionDescriptor action, string name, Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying == typeof(Guid))
            {
                if (Id(action, name) is { } id)
                {
                    values[name] = id.ToString();
                }
                else if (NoDataFor.ContainsKey(name))
                {
                    values[name] = Guid.NewGuid().ToString();
                }
                else
                {
                    _unfilled.Add($"{Name(action)}: {name}");
                    values[name] = Guid.NewGuid().ToString();
                }

                return true;
            }

            values[name] = underlying switch
            {
                _ when underlying == typeof(string) => "x",
                _ when underlying == typeof(int) => "1",
                _ when underlying == typeof(bool) => "false",
                _ when underlying == typeof(DateOnly) => "2026-01-01",
                _ when underlying.IsEnum => Enum.GetNames(underlying)[0],
                _ => ""
            };
            return false;
        }

        private static bool IsSimple(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(string) || underlying == typeof(Guid)
                   || underlying == typeof(DateOnly) || underlying == typeof(DateTime) || underlying == typeof(decimal);
        }

        // Acme's id for a parameter, by its name; "id" depends on the controller
        private Guid? Id(ControllerActionDescriptor action, string name)
        {
            var ids = _ids!;
            var frameworks = action.ControllerName == "Frameworks";
            var documents = action.ControllerName == "Documents"; // US-136
            return name.ToLowerInvariant() switch
            {
                "id" => action.ControllerName switch
                {
                    "Projects" or "ScanUploadApi" => ids.Project, // US-122: the API's project
                    "Risks" => ids.Risk,
                    "Frameworks" => action.ActionName == "Versions" ? ids.Framework : ids.FrameworkVersion,
                    "Organizations" or "System" => ids.Organization,
                    "Approvals" => ids.ApprovalRequest, // US-135
                    "Documents" => ids.OrganisationDocument,
                    _ => null
                },
                "projectid" => ids.Project,
                "riskid" => ids.Risk,
                "requirementid" => frameworks ? ids.FrameworkRequirement : ids.CraRequirement,
                "workitemid" => ids.WorkItem,
                "linkid" => documents ? ids.OrganisationDocumentLink : ids.Link,
                "evidenceid" => ids.Evidence,
                "versionid" => frameworks ? ids.FrameworkVersion : ids.DocumentVersion,
                "frameworkid" => ids.Framework,
                "frameworkversionid" => ids.FrameworkVersion,
                "from" or "to" => ids.FrameworkVersion, // US-037: the Cross-mapping page's two versions
                "memberid" or "ownerid" => ids.Member,
                "invitationid" => ids.Invitation,
                "measureid" => ids.Measure,
                "eventid" => ids.SecurityEvent, // US-073
                "actionid" => ids.RiskAction, // US-111
                "scanid" => ids.Scan, // US-078
                "sbomid" => ids.Sbom, // US-079
                "importid" => ids.RiskImport, // US-049
                "auditid" or "correctsauditid" => ids.Audit, // US-134
                "findingid" => ids.Finding,
                "tokenid" => ids.UploadToken, // US-122
                "snapshotid" => ids.ReportSnapshot, // US-050
                "documentid" => documents ? ids.OrganisationDocument : ids.Document,
                "frameworkids" => ids.Framework,
                "requirementids" => ids.CraRequirement,
                _ => null
            };
        }

        // GET: "not found", "forbidden", or a redirect that does not lead to Acme's data
        private bool Refused(HttpResponseMessage response)
            => response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden
                   or HttpStatusCode.Unauthorized // US-122: the upload API without a valid token
               || (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.Redirect
                   && !_ids!.All.Any(id => response.Headers.Location?.ToString().Contains(id.ToString(), StringComparison.OrdinalIgnoreCase) == true));

        // ---------------------------------------------------------- Data

        private sealed record Ids(
            Guid Organization, Guid Project, Guid Risk, Guid Measure, Guid Evidence, Guid WorkItem, Guid Link,
            Guid Document, Guid DocumentVersion, Guid Framework, Guid FrameworkVersion, Guid FrameworkRequirement,
            Guid CraRequirement, Guid Member, Guid Invitation, Guid SecurityEvent, Guid RiskAction, Guid Scan, Guid Finding, Guid UploadToken,
            Guid ApprovalRequest, Guid OrganisationDocument, Guid OrganisationDocumentLink, Guid ReportSnapshot, Guid Sbom, Guid RiskImport, Guid Audit)
        {
            public IEnumerable<Guid> All => [Organization, Project, Risk, Measure, Evidence, WorkItem, Link, Document,
                DocumentVersion, Framework, FrameworkVersion, FrameworkRequirement, Member, Invitation, SecurityEvent, RiskAction, Scan, Finding, UploadToken, ApprovalRequest, OrganisationDocument, OrganisationDocumentLink, ReportSnapshot, Sbom, RiskImport, Audit];
        }

        private async Task<Ids> IdsAsync(string organisation)
        {
            await using var db = _web.CreateDbContext();
            var org = await db.Organizations.SingleAsync(o => o.Name == organisation);
            var project = await db.Projects.SingleAsync(p => p.OrganizationId == org.Id);
            var risk = await db.Risks.SingleAsync(r => r.ProjectId == project.Id);
            var document = await db.RequirementDocuments.SingleAsync(d => d.ProjectId == project.Id);
            var framework = await db.Frameworks.SingleAsync(f => f.OwnerOrganizationId == org.Id);
            var version = await db.FrameworkVersions.SingleAsync(v => v.FrameworkId == framework.Id);
            var link = await db.RequirementLinks.SingleAsync(l => l.ProjectId == project.Id);

            return new Ids(
                org.Id,
                project.Id,
                risk.Id,
                risk.Measures.Single().Id,
                await db.Evidence.Where(e => e.ProjectId == project.Id && e.OrganisationDocumentId == null).Select(e => e.Id).SingleAsync(),
                link.WorkItemId,
                link.Id,
                document.Id,
                document.Versions.Single().Id,
                framework.Id,
                version.Id,
                await db.Requirements.Where(r => r.FrameworkVersionId == version.Id).Select(r => r.Id).FirstAsync(),
                link.RequirementId,
                org.Users.Single(u => u.Name == "Carl").Id,
                await db.Invitations.Where(i => i.OrganizationId == org.Id).OrderByDescending(i => i.CreatedAt).Select(i => i.Id).FirstAsync(), // the open one, not Carl's used one
                await db.SecurityEvents.Where(e => e.ProjectId == project.Id).Select(e => e.Id).SingleAsync(), // US-073
                risk.Actions.Single().Id, // US-111
                await db.Scans.Where(s => s.ProjectId == project.Id).Select(s => s.Id).SingleAsync(), // US-078
                await db.Findings.Where(f => f.ProjectId == project.Id).OrderBy(f => f.RuleId).Select(f => f.Id).FirstAsync(),
                await db.ScanUploadTokens.Where(t => t.ProjectId == project.Id).Select(t => t.Id).SingleAsync(), // US-122
                await db.ApprovalRequests.Where(r => r.ProjectId == project.Id).Select(r => r.Id).SingleAsync(), // US-135
                await db.OrganisationDocuments.Where(d => d.OrganizationId == org.Id).Select(d => d.Id).SingleAsync(), // US-136
                await db.OrganisationDocuments.Where(d => d.OrganizationId == org.Id).SelectMany(d => d.Links).Select(l => l.Id).SingleAsync(), // US-136
                await db.ReportSnapshots.Where(s => s.ProjectId == project.Id).Select(s => s.Id).SingleAsync(), // US-050
                await db.Sboms.Where(s => s.ProjectId == project.Id).Select(s => s.Id).SingleAsync(), // US-079
                await db.RiskImports.Where(i => i.ProjectId == project.Id).Select(i => i.Id).SingleAsync(), // US-049
                await db.Audits.Where(a => a.ProjectId == project.Id).Select(a => a.Id).SingleAsync()); // US-134
        }

        // A form token for the caller, from a page they may open (their own Users page)
        private static async Task<string> AntiforgeryTokenAsync(HttpClient client)
        {
            var page = await WebScenario.ParseAsync(await client.GetAsync("/Users"));
            return page.QuerySelector<IHtmlInputElement>("input[name='__RequestVerificationToken']")?.Value
                   ?? throw new Xunit.Sdk.XunitException("No form token on the caller's own Users page.");
        }

        // Every table but the Data Protection keys (a new key is a normal side effect of a form token) and the migrations
        private async Task<List<string>> TablesAsync()
        {
            await using var connection = new NpgsqlConnection(_database.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "select table_name from information_schema.tables where table_schema = 'public' and table_type = 'BASE TABLE' " +
                "and table_name not in ('DataProtectionKeys', '__EFMigrationsHistory') order by table_name", connection);
            var tables = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }

        // A fingerprint of each table's rows
        private async Task<Dictionary<string, string>> ChecksumAsync(List<string> tables)
        {
            await using var connection = new NpgsqlConnection(_database.ConnectionString);
            await connection.OpenAsync();
            var sums = new Dictionary<string, string>();
            foreach (var table in tables)
            {
                await using var command = new NpgsqlCommand(
                    $"select md5(coalesce(string_agg(t::text, '|' order by t::text), '')) from \"{table}\" t", connection);
                sums[table] = (string)(await command.ExecuteScalarAsync())!;
            }

            return sums;
        }
    }
}
