# Designregler for siderne

US-168. Alle sider bygges på samme måde, så man genkender, hvor tingene er, og hvad der er vigtigst. Reglerne gælder nye sider og ændringer. Komponenterne ligger i `src/ComplianceTracker.Web/TagHelpers` og `Models/StatusBadges.cs`.

## 1. Sidehovedet

Hver hovedside starter med det fælles sidehoved `<page-header>`:

- et link tilbage ("← Risks of Acme Portal"), når siden ligger under en anden;
- sidens titel som `h1` med dens status ved siden af;
- sidens handlinger til højre: højst én primær knap, resten som outline, og "More" til det, der sjældent bruges.

```cshtml
<page-header heading="@risk.Title" heading-id="risk-title" status="@risk.Level.ToString()" status-id="risk-level"
             back-href="…" back-text="Risks of Acme Portal">
    <a class="btn btn-outline-primary" asp-action="Edit" asp-route-id="@risk.Id">Edit</a>
</page-header>
```

Projektets faner og kravsiden ligger under projektets sidehoved. Derfor er kravets titel en `h2` med sin egen header (`.requirement-header`).

## 2. Knapper og links

| Hvad | Hvordan |
| --- | --- |
| Det vigtigste på siden eller i et område | `btn btn-primary`, højst én pr. område |
| Andre handlinger | `btn btn-outline-primary` eller `btn-outline-secondary` |
| Fjerne, lukke, slette | rød (`btn-outline-danger`/`btn-danger`), aldrig andet |
| At gå til et andet sted | et link, ikke en knap |
| Mange sjældne handlinger | under "More" eller "⋯" |

"Tilføj" står over den liste, der tilføjes til (#52).

## 3. Status har faste farver

En status vises altid med `<status-badge status="…" />` og har den samme farve på alle sider og i rapporterne:

| Farve | Status |
| --- | --- |
| Grøn | Covered, Done, Low, Treated, Approved, Inside |
| Gul | Covered, needs review, Partly, Needs review, Medium, Not approved, Changed since approval, Not decided |
| Orange | Needs action, High, Due soon |
| Rød | Not covered, Missing, Critical, Expired, Overdue |
| Grå | Not applicable, Turned off, Closed, Accepted, Later, Archived, Outside |
| Teal | AI-generated, AI-drafted |
| Lys | navne og etiketter uden en vurdering, fx en fase |

En ny status tilføjes i `StatusBadges` og i tabellen her, ikke i en view.

## 4. Overskrifter

Én skala: sidens titel er `h1`, et afsnit er `h2` (ofte med `class="h5"` eller `h6` af hensyn til størrelsen), et underafsnit `h3`. Et afsnit springer ikke et niveau over.

## 5. Bredde og ombrydning

Intet gør en side bredere end vinduet (US-167): en række af felter og knapper ombrydes (`flex-wrap`), et felt eller en select bliver aldrig bredere end sin række, og en tabel, der er for bred på en smal skærm, scroller inde i sig selv.

## 6. Test-id'er

Komponenterne beholder sidernes id'er og klasser, fx `#risk-title`, `#risk-level` og `.requirement-status`, så testene kan finde dem.
