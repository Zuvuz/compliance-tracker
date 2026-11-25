# Compliance-tracker – Faser og status

Senest opdateret: 2026-11-25

Projektet blev planlagt i fire faser. Fase 1 (MVP'en) blev bygget først og gjort færdig, som planlagt i [B33](beslutningslog.md). Efter MVP'en blev stories prioriteret efter, hvad der giver mest værdi i hverdagen for en lille virksomhed og for en revision, ikke længere fase for fase ([B165](beslutningslog.md)). Derfor er dele af fase 2, 3 og 4 bygget side om side.

Tabellen viser, hvad der er bygget i hver fase, og hvad der mangler.

| Fase | Tema | Status |
|---|---|---|
| 1 | Kernen: krav, evidens og AI-mapping | Færdig 2026-11-05 (B33) |
| 2 | Revision og dokumentation | Næsten færdig (to stories mangler) |
| 3 | Sårbarheder og risiko | Næsten færdig (Jira mangler) |
| 4 | Bredere dækning og klogere AI | Delvist |
| 5 | Hele ISMS'et og hverdagsbrugen | I gang: US-208, US-214, US-212, US-209 og US-211 bygget |

## Fase 1 – MVP

**Mål:** En lille udviklingsvirksomhed kan for hver regulering se, hvilke krav der er opfyldt, og bevise det over for en auditor.

- **Bygget:** de stories, der har labelen `mvp` (B33):
  - AI-forslag og overblik (US-044)
  - evidens pr. krav (US-057)
  - sporbarhedsmatrix (US-052)
  - Docker (US-017)
  - backup med testet gendannelse (US-014)
  - projektets overblik (US-048)
  - unikt CVR (US-015)
- **Udvidet med sikkerhedens grundlag:** isolation mellem organisationer (US-086), sikkerheds-headers (US-087), sessioner, der udløber (US-088), og MFA for Admins (US-089).
- **Udskudt og senere fravalgt:** `Kontrol`, `Kravvurdering` og `Release`. Deres opgaver løses på en anden måde ([B165](beslutningslog.md), [domænemodellen](domaenemodel.md#ikke-bygget)).

## Fase 2 – Revision og dokumentation

**Mål:** Kunne gennemføre og dokumentere en audit, og bevise hvem der ændrede hvad.

| Punkt | Status | Stories |
|---|---|---|
| Audit og fund | Bygget | US-134 |
| Låste rapporter med fingeraftryk | Bygget | US-050, US-131, US-132 |
| Revisionspakke til en ekstern revisor | Bygget | US-076, US-178 til US-181, US-197, US-198 |
| Tidsbegrænset adgang for revisoren | Bygget | US-182, US-196 |
| Påmindelser før frister | Bygget | US-183 |
| Ændringshistorik | Bygget som aktivitetslog | US-102 |
| Statement of Applicability | Bygget | US-053 |
| Underskrift med MitID Erhverv | Ikke bygget | US-133 |
| Vandmærke i revisionspakken | Ikke bygget | US-162 |

## Fase 3 – Sårbarheder og risiko

**Mål:** Dække CRA's krav om sårbarhedshåndtering og forbinde kravene med de risici, de skal mindske.

| Punkt | Status | Stories |
|---|---|---|
| SBOM pr. produktversion | Bygget, pr. versionsnummer i stedet for release (B87) | US-079, US-148 |
| Scanninger som evidens | Bygget (SARIF fra hånden, pipelinen og GitHub) | US-078, US-122, US-160 |
| Sikkerhedshændelser og indberetningsfrister | Bygget | US-073, US-127, US-128 |
| Risikoregister og behandling | Bygget, inkl. AI-udkast og restrisiko | US-042, US-111, US-112, US-165 |
| Azure DevOps ved siden af GitHub | Bygget | US-030 |
| Jira | Ikke bygget | – |

## Fase 4 – Bredere dækning og klogere AI

**Mål:** Flere frameworks og bedre AI-støtte, bygget på data fra de tidligere faser.

| Punkt | Status | Stories |
|---|---|---|
| NIS2 | Bygget, inkl. gap-analyse og leverandører | US-142, US-188 til US-191 |
| GDPR | Delvist: trackerens egen håndtering af persondata, ikke som framework | US-095 |
| ISO/IEC 42001, OWASP ASVS og MDR | Ikke indbygget; de kan uploades som organisationens egne kataloger | US-023, US-081 |
| Versionering af frameworks | Bygget | US-035, US-110 |
| Bedre forslag | Bygget: reranking, måling af kvalitet og eksport af træningsdata | US-116, US-118, US-123, US-205 |
| Gap-analyse | Bygget | US-108, US-119, US-155 |
| Krav uden eget værktøj | Bygget som upload af kravspecifikationer og use cases | US-032 til US-034, US-104 |
| Single sign-on | Bygget med Keycloak | US-004 |
| Roller pr. projekt | Ikke bygget; rollerne gælder organisationen | – |
| Spørgsmål til egne data | Sat på pause | US-039 |

## Fase 5 – Hele ISMS'et og hverdagsbrugen

**Mål:** At trackeren kan drive hele organisationens ISMS efter ISO/IEC 27001 kapitel 4–10, og at det er nemt at bruge hver dag.

Fasen er besluttet som stories, men ikke bygget:

- scope og kontekst (US-208), **bygget** (B166)
- mål og målinger (US-209), **bygget** (B169)
- ledelsens gennemgang (US-210)
- internt auditprogram (US-211), **bygget** (B170)
- afvigelser (US-212), **bygget** (B168)
- kompetencer og awareness (US-213)
- aktivfortegnelse (US-214), **bygget** (B167)
- årshjul (US-215)
- ISMS-forsiden (US-216)

Politikkerne er US-136. Begreberne står som planlagte i [domænemodellen](domaenemodel.md).

## Rækkefølge og afhængigheder

- Fase 2 blev bygget oven på fase 1, som planlagt.
- SBOM'en i fase 3 skulle have krævet releases. I stedet hører den til et versionsnummer (B87), så den kunne bygges uden `Release`.
- Bedre AI i fase 4 bygger på de godkendte og afviste forslag fra fase 1–3 (US-116, US-123).
- Fase 5 starter med scope og kontekst (US-208), fordi de andre ISMS-stories bygger på den.
