# Compliance-tracker – Beslutningslog

Senest opdateret: 2026-10-28

Beslutninger truffet under arbejdet med [domænemodellen](domaenemodel.md). Hver beslutning har en begrundelse og det, der blev fravalgt, så det senere er tydeligt, *hvorfor* modellen ser ud, som den gør. Status er **Vedtaget** for beslutninger, der er taget, og **Anbefaling** for råd, der er værd at bekræfte senere.

## Scope og ansvar

### B01 – Trackeren styrer compliance, ikke produktkrav

- **Status:** Vedtaget
- **Beslutning:** User stories og use cases oprettes og vedligeholdes i teamets eget værktøj (GitHub, Azure DevOps, Jira). Trackeren gemmer kun en henvisning (`Arbejdselement`).
- **Begrundelse:** Ét hjem per ting. Udviklere skifter ikke værktøj for et compliance-system, og at bygge sin egen backlog ville gøre projektet til en Jira-konkurrent.
- **Fravalgt:** Stories og use cases oprettet direkte i trackeren. Kan senere understøttes som `kildesystem = intern` uden at ændre modellen.

### B02 – Udviklingsmetoden modelleres ikke

- **Status:** Vedtaget
- **Beslutning:** Om teamet arbejder agilt eller med vandfald, er ikke en del af modellen. I stedet har `Projekt` en `livscyklusfase`, og `Arbejdselement` har en `type` (user story, use case, opgave).
- **Begrundelse:** CRA og NIST SSDF er metodeneutrale. Det, der betyder noget for compliance, er hvor produktet er i sit liv, ikke hvordan det styres.

### B03 – AI opretter ikke acceptkriterier

- **Status:** Vedtaget
- **Beslutning:** AI'en mapper kun eksisterende stories og deres acceptkriterier til krav. Den foreslår ikke nye acceptkriterier og skriver ikke tilbage til kildesystemet.
- **Begrundelse:** Ansvaret for, hvad produktet skal kunne, ligger hos udviklerne. Det holder scope og ansvar rent.
- **Tilladt:** AI'ens begrundelse må nævne, hvis en story kun dækker et krav delvist. Det er en observation, ikke et nyt kriterie.
- **Fravalgt:** En generaliseret `AiForslag` med typen "acceptkriterium".

### B04 – Ingen vision på projektet

- **Status:** Vedtaget
- **Beslutning:** `Projekt` har `beskrivelse`, `tilsigtetFormål` og `scope`, men ingen vision. `Organisation` har `sektor` og `størrelse`.
- **Begrundelse:** Systemet bruger ikke en vision til noget. Tilsigtet formål er derimod et juridisk begreb i både CRA og AI Act og afgør, hvilke krav der gælder. Overlappende tekstfelter giver støj for AI'en.

## Struktur i modellen

### B05 – Kontroller ejes af organisationen

- **Status:** Vedtaget
- **Beslutning:** `Kontrol` hører til organisationen og kobles mange-til-mange til `Krav`.
- **Begrundelse:** Én kontrol (fx MFA) kan opfylde krav fra flere frameworks. Det er grundlaget for overlap-detektion mellem fx ISO 27001 og CRA.

### B06 – Frameworks og krav er fælles referencedata

- **Status:** Vedtaget
- **Beslutning:** `Framework` og `Krav` indlæses én gang og deles af alle organisationer, som vælger dem.
- **Begrundelse:** Et regelsæt er det samme for alle, ligesom en lovsamling.

### B07 – Release er et selvstændigt begreb

- **Status:** Vedtaget
- **Beslutning:** `Projekt` har releases. Evidens kan knyttes til en release, og arbejdselementer leveres i en release.
- **Begrundelse:** CRA tænker i produktversioner. En SBOM eller scanning gælder for en bestemt version, og en opdatering kan bryde et krav, der før var opfyldt.

### B08 – Kravvurdering per release eller organisation

- **Status:** Vedtaget
- **Beslutning:** Projektframeworks (CRA, AI Act) vurderes per release. Organisationsframeworks (ISO 27001) vurderes på organisationen.
- **Begrundelse:** En ny version kan ændre status, mens ISO 27001 gælder hele organisationen uafhængigt af versioner.

### B09 – Begrænsninger vises som noter

- **Status:** Vedtaget
- **Beslutning:** Regler, som multipliciteter ikke kan udtrykke (xor, mindst én), vises som noter i domænemodellen.
- **Begrundelse:** En abstrakt overklasse som `Mappingmål` findes ikke i den virkelige verden og hører ikke hjemme i en domænemodel. Om den er værd det i koden, afgøres i klassediagrammet.

### B10 – Audit, fund og rapporter er fase 2

- **Status:** Vedtaget
- **Beslutning:** `Audit`, `Fund` og `Rapportsnapshot` er med i domænemodellen, markeret «fase 2», men bygges ikke i første fase.
- **Begrundelse:** Strukturen er tænkt igennem fra start, så fase 2 bliver en udbygning og ikke en ombygning, mens MVP'en holdes overskuelig.

## Integration og AI

### B11 – Integration via adapter-mønster

- **Status:** Vedtaget
- **Beslutning:** Eksterne systemer tilgås gennem et fælles interface (fx `IArbejdselementKilde`) med én adapter per system. `Arbejdselement` får `kildesystem`.
- **Begrundelse:** Trackeren og AI'en arbejder kun på det normaliserede arbejdselement og er ligeglade med, hvor det kommer fra. Nye systemer kræver kun en ny adapter.

### B12 – Kun GitHub i første fase, ingen tovejssynkronisering

- **Status:** Anbefaling
- **Beslutning:** Første fase understøtter manuelle links og en GitHub-adapter, der kun læser. Udviklere kan tagge issues med krav-ID'et som label.
- **Begrundelse:** Envejs-læsning giver det meste af værdien. Tovejssynkronisering skaber konflikter og dobbeltdata.

### B13 – AI foreslår, mennesket beslutter

- **Status:** Vedtaget
- **Beslutning:** AI'en laver `Mappingforslag` med score og begrundelse. Et menneske godkender eller afviser, og det registreres, hvem der besluttede.
- **Begrundelse:** Ved compliance skal nogen stå inde for koblingen. Det matcher også AI Act's tankegang om menneskeligt tilsyn.

### B14 – Afvisninger logges til forbedring

- **Status:** Vedtaget
- **Beslutning:** Ved afvisning registreres en fast `afvisningsårsag` og en fri `brugerkommentar`.
- **Begrundelse:** Kategorierne kan måles, kommentaren giver nuance. Godkendte og afviste par kan senere forbedre forslagene.
- **Forbehold:** Brug af kunders data til træning kræver samtykke.

## Login og adgang

### B15 – Login via Keycloak med OpenID Connect

- **Status:** Vedtaget
- **Beslutning:** Trackeren har ingen egne kodeord. Login sker hos Keycloak via OpenID Connect (authorization code flow med PKCE). Brugere genkendes på Keycloaks `sub`-claim, og personer kan selv registrere sig i Keycloak. (US-004)
- **Begrundelse:** Kodeord, MFA og kontolåsning håndteres af et dedikeret og afprøvet system i stedet for egen kode (ISO 27001 A.8.5). `sub` kan ikke genbruges af en anden person, hvilket navn og e-mail kan (ASVS V10.5.2). Keycloak kører i Docker og i Testcontainers, så både udvikling og CI virker uden konti hos eksterne tjenester.
- **Fravalgt:** ASP.NET Core Identity med egne kodeord (mere sikkerhedskritisk kode at vedligeholde). Microsoft Entra ID og Auth0 (kræver konti og er svære at teste automatisk).

### B16 – Én organisation pr. bruger, indtil invitationer findes

- **Status:** Vedtaget
- **Beslutning:** Første gang en bruger logger ind, opretter brugeren sin organisation og bliver Admin. En bruger kan kun være medlem af én organisation og kun se den. (US-004)
- **Begrundelse:** Holder adgangsreglerne enkle, indtil invitationer (US-009) og roller (US-010) er bygget.

## Kataloger

### B17 – Kataloger uploades som PDF

- **Status:** Vedtaget
- **Beslutning:** Organisationen uploader sine standarder og politikker som PDF. Hver upload bliver en ny version; de gamle bevares, og den nyeste gælder. Hver version får et SHA-256-fingeraftryk. Kravene trækkes først ud af PDF'en i en senere story (US-007). (US-005)
- **Begrundelse:** Standarder som ISO 27001 købes som PDF med vandmærke. At kræve et struktureret katalog (fx CSV) ville betyde, at nogen skulle skrive kravene af i hånden. Fingeraftrykket viser en auditor, at en version ikke er ændret efter upload.
- **Fravalgt:** CSV-skabelon med krav. Overvejes igen, hvis udtræk fra PDF viser sig upålideligt.
- **Kendt risiko:** ISO-standarder er ofte licenseret til én bruger. Organisationen skal selv sikre, at licensen tillader deling med alle medlemmer.

### B18 – Frameworks kan ejes af en organisation

- **Status:** Vedtaget
- **Beslutning:** Ud over de fælles, indbyggede frameworks (US-006) kan en organisation oprette sine egne, fx "Security policy". De ejes af præcis én organisation, og navnet er unikt inden for organisationen uden hensyn til store og små bogstaver. (US-005)
- **Begrundelse:** Organisationer skal kunne følge interne politikker på samme måde som officielle standarder.
- **Ændrer:** B06, som sagde, at frameworks altid er fælles referencedata.

## Projekter

### B19 – Fire livscyklusfaser for et projekt

- **Status:** Vedtaget
- **Beslutning:** Et projekt har én af fire faser: `Planning`, `Development`, `Released` og `EndOfSupport`. Fasen er valgfri ved oprettelse og starter som `Planning`. (US-008)
- **Begrundelse:** De fire faser dækker et produkts hele levetid fra idé til ophør af support, som CRA tænker i. Færre faser end det første forslag (`Design`, `Development`, `Released`, `Maintenance`, `Retired`) gør valget enkelt for brugeren; vedligeholdelse er en del af `Released`.
- **Bygger på:** B02, som siger, at livscyklusfasen erstatter udviklingsmetoden i modellen.

### B20 – CRA og AI Act er indbyggede frameworks uden krav

- **Status:** Vedtaget
- **Beslutning:** CRA (forordning (EU) 2024/2847) og AI Act (forordning (EU) 2024/1689) seedes af en databasemigration som indbyggede frameworks uden ejer. Hver har én version med et link til den officielle tekst på EUR-Lex og ingen PDF. En organisation slår dem til og fra; historikken bevares. Kravene seedes ikke nu, men i en senere story. (US-006)
- **Begrundelse:** EU-forordninger er frit tilgængelige, så alle organisationer kan bruge dem uden at uploade noget. En migration med faste id'er giver samme data i alle miljøer og i testene. At udskyde kravene holder US-006 lille, så US-011 (frameworks på projekter) kan bygges ovenpå.
- **Bygger på:** B18, som skelner mellem fælles og organisationens egne frameworks.

### B21 – Projekter vælger selv deres frameworkversion

- **Status:** Vedtaget
- **Beslutning:** Hvert projekt peger på én bestemt version af hvert framework, det følger. Når en ny version uploades, vælger brugeren, hvilke projekter der flytter med; resten beholder deres version. Organisationssiden viser hver version i brug med dens projekter og markerer projekter, der ikke er på nyeste version. Den nyeste version vises altid, også uden projekter ("no projects yet"). Et indbygget framework kan ikke slås fra, mens projekter bruger det. (US-011)
- **Begrundelse:** Et projekt kan ikke altid flytte til en ny version samme dag. Ved at gemme den præcise version kan en auditor se, hvilke regler et produkt blev tjekket imod, og organisationen kan se, hvilke projekter der halter efter (ISO 27001 7.5.3).
- **Fravalgt:** At alle projekter automatisk flytter til nyeste version.
- **Bygger på:** B20 (indbyggede frameworks) og B19 (projekter).

## Test og kvalitet

### B22 – Hver regel skal have en automatisk test

- **Status:** Vedtaget
- **Beslutning:** Hver regel i en færdig story skal have en automatisk test. Regler uden test samles i US-016 og testes der. Logposternes tidspunkt testes præcist: alle services læser tiden fra .NET's `TimeProvider`, som testene erstatter med et fast tidspunkt. Hemmeligheder i repoet fanges af gitleaks i CI, der scanner hele historikken. (US-016)
- **Begrundelse:** En regel, vi siger, vi overholder, skal have bevis (ISO 27001 A.8.29). Et fast tidspunkt gør det muligt at teste "logges med tid" uden tilfældige fejl. En scanning i CI er mere pålidelig end at huske ikke at committe `.env`.
- **Fravalgt:** Automatiske tests af Docker-opsætningen og en arkitekturtest for "ingen forretningsregler i controllere" (US-003 R6). De kan tages op senere.

## Åbne spørgsmål til klassediagrammet

- Skal `Mappingforslag` pege på en fælles overklasse, eller valideres xor-reglen i konstruktøren?
- Hvilke attributter bliver enums (status, type, niveau)? Livscyklusfasen er afgjort i B19.
- Skal mange-til-mange-koblingerne (fx Krav–Kontrol) have egne data, som en begrundelse for koblingen?
- Kører AI-modellen lokalt eller hos en udbyder? En lokal model beskytter fortrolige stories bedre.
