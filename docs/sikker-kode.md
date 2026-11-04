# Sikker kode: regler mod injection

US-094. Disse regler tjekkes ved hvert build af `InjectionGuardrails.feature`, som læser appens egen kildekode i `src/`. Kommentarer, migreringer, `bin`, `obj` og `wwwroot/lib` læses ikke.

## Reglerne

| Regel | Forbudt | Brug i stedet | Krav |
|---|---|---|---|
| R1 SQL | `FromSqlRaw`, `ExecuteSqlRaw`, `SqlQueryRaw`, `CommandText` | LINQ, eller `FromSql`/`ExecuteSql` med en interpoleret streng, som EF Core laver om til parametre | ASVS V5.3.4 |
| R2 HTML | `Html.Raw`, `HtmlString` i views | Almindelig `@værdi`, som Razor koder | ASVS V5.3.3 |
| R3 Processer | `Process.Start`, `ProcessStartInfo` | Ingen. Trackeren starter ikke andre programmer | ASVS V5.3.8 |
| R4 Redirects | `Redirect(...)`, `RedirectPermanent(...)`, `new RedirectResult(...)` | `RedirectToAction` eller `LocalRedirect` | ASVS V5.1.5 |
| R5 Filer | `File.*`, `Directory.*`, `FileStream`, `Path.Combine` i appen | Filer gemmes i databasen efter id. Brugerens filnavn bruges kun til visning og download | ASVS V12.3.1 |
| R6 Scripts | `eval`, `new Function`, `document.write`, `innerHTML`/`outerHTML`/`insertAdjacentHTML` | `textContent` og DOM-metoder | ASVS V5.3.3 |

## Gennemgåede undtagelser

Listen ligger i `InjectionGuardrailsStepDefinitions.Reviewed`. En ny undtagelse kræver en begrundelse både dér og her.

| Fil | Regel | Hvorfor det er sikkert |
|---|---|---|
| `src/ComplianceTracker.Web/wwwroot/js/work-items.js` | `innerHTML` | Indsætter listen over work items fra trackerens egen side (`/Projects/WorkItems`), som Razor allerede har kodet (US-018 R7) |

## R7: Prompt injection

Tekst fra brugerne (user stories, krav og use cases) sendes til den lokale AI og kan forsøge at styre den. Det er acceptabelt, fordi:

- AI'en handler aldrig selv. Den laver forslag, og et menneske godkender eller afviser hvert forslag (B32).
- AI'ens svar vises kodet som almindelig tekst og bliver aldrig kørt.
- AI'en har ingen værktøjer eller adgang til andre data end den tekst, den får.

Hvis en af de tre ændres, skal reglen vurderes igen.
