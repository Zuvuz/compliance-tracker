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
- **Opdateret (US-009, US-010):** Flere personer kan nu blive medlem gennem en invitation. En person kan stadig kun være medlem af én organisation.
- **Opdateret (US-187):** Se B121. En person kan nu være medlem af flere organisationer, men kun med en ekstern rolle (Consultant eller Auditor).

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
- **Opdateret (US-043):** CRA har nu sine krav. Bilag I (22 krav) seedes på version 1 med den officielle tekst. AI Act har stadig ingen krav.
- **Opdateret (US-062):** AI Act har nu 31 krav på version 1 (B58).

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

### B23 – Dependency injection og faste mønstre i hele koden

- **Status:** Vedtaget
- **Beslutning:** (US-029)
  - Den indloggede bruger og organisation findes ét sted (`ICurrentUser`, `CurrentMember`) og injiceres i use cases og queries.
  - Data ændres af use cases (`...Service`) og læses af queries (`...Queries`). Controllere bruger kun disse.
  - Logposter skrives kun af `IActivityLogger`.
  - Hvert lag registrerer sig selv (`AddApplication`, `AddInfrastructure`).
  - Indstillinger er typede Options, der tjekkes ved opstart.
  - Brudte domæneregler vises af ét fælles filter.
- **Begrundelse:**
  - Hver ting findes ét sted, så nye stories kan bygges uden at gentage opslag, logning og fejlhåndtering.
  - Reglen "kun din egen organisations data" kan ikke glemmes i en ny controller.
  - Fejl i opsætningen opdages ved opstart i stedet for hos brugeren.
- **Fravalgt:**
  - Domain events, fordi `IActivityLogger` dækker behovet nu. Det kan tages op, når flere ting skal ske ved samme hændelse.
  - Fuld CQRS med separate læsemodeller.
  - MediatR og lignende biblioteker, fordi almindelig DI er nok og nemmere at forklare.

### B24 – En upload er en kladde, der skal aktiveres

- **Status:** Vedtaget
- **Beslutning:** (US-007)
  - En ny frameworkversion starter som kladde med de krav, der blev fundet i PDF'en.
  - Kladden kan rettes: krav kan ændres, slettes og tilføjes i hånden. Aktivering låser den og kræver mindst ét krav.
  - Projekter kan kun bruge aktive versioner. Den gældende version er den senest aktiverede.
  - En upload flytter ikke længere projekter. De flyttes med "Use newest", når den nye version er aktiveret. Checkboksene "Move …" er fjernet (punkt i #52).
  - Versioner fra før US-007 og de indbyggede versioner blev markeret som aktive i migrationen, så ingen projekter gik i stykker.
  - Udtrækningen bruger PdfPig og en fast regel (linjer, der starter med en nummereret kode). Der bruges ikke AI (domænemodellens regel 9).
- **Begrundelse:** Et projekt skal vurderes mod en tekst, der ikke kan ændre sig bagefter (ISO 27001 7.5.3). En automatisk udtrækning er aldrig perfekt, så et menneske skal godkende den først.
- **Fravalgt:**
  - At flytte projekter ved upload, fordi kladden kan ændre sig.
  - OCR af scannede PDF'er.
  - En "kategori" på kravet. Koden viser allerede kategorien (fx `A.5`).

### B25 – User stories fra GitHub synkroniseres af systemet

- **Status:** Vedtaget
- **Beslutning:** (US-018)
  - Et projekt kan linkes til et offentligt GitHub-repository (`owner/name`) og en label (standard `user-story`). Trackeren læser kun.
  - Issues med labelen, der ikke følger BDD-skabelonen (ingen "Rules"), springes over og vises med grunden.
  - Work items slettes aldrig. Et lukket issue bliver `Closed`, og et issue, der ikke længere kommer med, bliver `Removed`.
  - Ud over "Sync now" tjekker en baggrundsjob alle linkede repositories hvert 15. minut (`WorkItemSync:IntervalMinutes`). GitHub tillader 60 forespørgsler i timen uden nøgle.
  - En planlagt sync logges kun, når den fandt ændringer. Den logges uden bruger: `ActivityLogEntry.UserId` er null og betyder "systemet". En sync startet af et medlem logges altid med medlemmet.
  - Åbne projektsider opdateres med SignalR. Hubben tjekker, at brugeren hører til projektets organisation.
- **Begrundelse:** En handling må ikke skrives på en person, der ikke udførte den (ISO 27001 A.8.15). Derfor er systemet sin egen "bruger" i loggen.
- **Fravalgt:**
  - Webhooks fra GitHub, fordi de kræver en offentlig adresse. De kan erstatte baggrundsjobbet, når trackeren hostes.
  - At logge hver planlagt sync uden ændringer, fordi loggen så ville fyldes med ét indslag pr. kvarter pr. projekt.

### B26 – Adgangsnøgler gemmes krypteret pr. projekt

- **Status:** Vedtaget
- **Beslutning:** (US-031, genbruges af US-030)
  - Et projekt kan få en read-only nøgle til sit GitHub-repository. Nøglen tjekkes hos GitHub, før den gemmes.
  - Nøglen gemmes krypteret med ASP.NET Core Data Protection (`ISecretProtector`). Den vises aldrig igen og kommer aldrig i loggen, i fejlbeskeder eller i sidens HTML. Den kan skiftes og fjernes.
  - Data Protection-nøglerne ligger i databasen (`DataProtectionKeys`), så krypterede nøgler og login-cookies stadig kan læses efter en genstart eller i en ny container.
  - Skifter projektet repository, slettes nøglen, fordi den hører til det gamle repository.
- **Begrundelse:** En nøgle i klartekst kunne læses af alle med adgang til databasen eller en backup (ISO 27001 A.8.24, A.5.17).
- **Kendt begrænsning:** Data Protection-nøglerne ligger ukrypteret i den samme database. Før trackeren hostes, skal de beskyttes med et certifikat eller en key vault (`ProtectKeysWith...`).
- **Fravalgt:**
  - Én fælles nøgle til hele trackeren i serverens konfiguration, fordi alle organisationer så ville dele adgang.
  - En GitHub App eller OAuth, fordi det kræver en offentlig adresse og en app-registrering.

### B27 – Versioner gælder fra en dato

- **Status:** Vedtaget (afløser B24's regel om, at den senest aktiverede version er den gældende)
- **Beslutning:** (US-035)
  - En version kan få en dato, den gælder fra. Den sættes på kladden og låses ved aktivering.
  - Den gældende version er den aktive version, der senest begyndte at gælde. Indtil datoen er den nye version "Upcoming".
  - Har et framework kun versioner, der ikke er startet endnu, bruges den første af dem (fx CRA, der gælder fra 11-12-2027).
  - Projekter flyttes ikke på datoen. De får "Newer version available" og flytter med "Use newest". De kan også vælge en kommende version tidligt for at forberede sig.
  - Nye versioner af de indbyggede regulativer kommer med en opdatering af trackeren (en migration) med deres startdato.
  - "I dag" er datoen i serverens lokale tidszone, fra appens ur.
- **Begrundelse:** Regulativer bliver vedtaget længe før, de gælder. Organisationen skal kunne forberede sig uden, at den gældende version skifter for tidligt.
- **Fravalgt:**
  - Regulativer, der gælder i etaper (fx AI Act's artikler fra forskellige datoer). Én dato pr. version indtil videre.
  - Automatisk flytning af projekter på datoen.

### B28 – CRA's krav som seed-data, og "Not applicable" pr. projekt

- **Status:** Vedtaget
- **Beslutning:** (US-043)
  - Bilag I i CRA (del I punkt 1 og 2(a)–(m), del II punkt 1–8) seedes på den eksisterende CRA-version 1 med faste id'er.
  - Beskrivelsen er den officielle engelske tekst fra EUR-Lex, med kilden som link, og titlen er vores egne ord. EU-lovtekst må genbruges med kildeangivelse (Kommissionens afgørelse 2011/833/EU).
  - Fælles indledninger ("... shall:") sættes foran hvert punkt, så hvert krav kan læses alene.
  - Et krav kan markeres "Not applicable" for et projekt med en påkrævet begrundelse. Det tæller så ikke med i dækningen. Beslutningen kan trækkes tilbage, og historikken gemmes. Det gælder alle frameworks.
- **Begrundelse:** CRA's væsentlige krav gælder "where applicable" ud fra risikovurderingen (art. 13(2)–(3)), så en ærlig dækning skal kunne udelade krav med en begrundelse. Kravene lægges på version 1, fordi lovteksten ikke er ændret.
- **Fravalgt:** AI Act's krav (egen story), en dansk oversættelse, og krav uden for Bilag I.

### B29 – Lokal AI i to trin, som altid kan skiftes ud

- **Status:** Vedtaget (uddyber B13 og B14)
- **Beslutning:** (US-020, US-036)
  - AI'en kører lokalt (Ollama som standard). Der sendes ingen tekst ud af organisationens egen infrastruktur.
  - **Trin 1:** en embedding-model (standard `nomic-embed-text`) scorer alle relevante krav mod hver user story (cosine similarity). De 4 bedste pr. story bliver forslag, og "Show more" giver de næste 4.
  - **Trin 2:** en lille chat-model (standard `qwen2.5:3b`) skriver en kort begrundelse i baggrunden. Siden opdateres via SignalR.
  - **Modellen kan altid skiftes:** Application kender kun `IEmbeddingModel` og `IRationaleWriter`. Provider, adresse og modelnavne står i konfigurationen (`Ai:Provider`, `Ai:BaseUrl`, `Ai:EmbeddingModel`, `Ai:ChatModel`). Der er adaptere til Ollama og til OpenAI-kompatible servere (vLLM, LM Studio, llama.cpp). En ny slags server kræver kun en ny adapter.
  - **Sporbarhed:** hvert forslag gemmer modellernes navne og prompt-versionen (`RationalePrompt.Version`), så resultater fra forskellige modeller kan sammenlignes. Embeddings gemmes ikke, men beregnes ved hver kørsel, så en ny model aldrig blandes med gamle vektorer.
  - **Data:** alle forslag og beslutninger gemmes altid som revisionsspor. Eksport til forbedring af AI'en kræver, at en Admin har slået deling til (slået fra som standard). Eksporten indeholder ikke, hvem der besluttede.
  - **Opdateret (US-130, B63):** deling er nu slået til som standard, og organisationens egen eksport er fjernet.
  - Krav markeret "Not applicable" (US-043) foreslås ikke.
- **Begrundelse:** Udviklingsmaskinen har ingen GPU. En chat-model, der vurderede alle par, ville tage timer, mens embeddings tager sekunder. Når trackeren kører på en server med GPU, kan en større model indsættes uden kodeændringer, og de gemte beslutninger viser, om den er bedre.
- **Fravalgt:**
  - At gemme embeddings (pgvector), indtil der er så mange krav, at genberegning bliver langsom.
  - At sende data til en sky-AI.
  - En fast tærskel for score (B13's "under 0,5 vises ikke"), fordi de 4 bedste giver mere data at lære af.

### B30 – Risikoregister pr. projekt

- **Status:** Vedtaget
- **Beslutning:** (US-042)
  - Hvert projekt har sit eget risikoregister. Det er produktets cybersikkerheds-risikovurdering (CRA art. 13(2), bilag VII punkt 3).
  - Sandsynlighed og konsekvens går fra 1 til 5. Niveauet er produktet af dem: 1–4 Low, 5–9 Medium, 10–16 High, 20–25 Critical. Restrisiko er to valgfri felter.
  - Behandlingerne er Reduce, Accept, Avoid og Transfer. En accept kræver en begrundelse og gemmer, hvem der accepterede. Indtil roller kommer (US-010), må alle medlemmer acceptere.
  - En risiko, der skal mindskes, kobles til projektets krav og user stories. Uden koblinger vises "No measures yet", og med mindst én vises status "Treated".
  - Review er påkrævet hver 12. måned ("Review due"). En risiko slettes aldrig, den lukkes med en begrundelse.
  - Hver ændring logges med gammel og ny værdi.
- **Begrundelse:** CRA kræver en risikovurdering, der holdes opdateret i hele produktets levetid, og ISO 27001 kræver faste kriterier og regelmæssig gentagelse (6.1.2, 8.2). Koblingen til krav og stories viser, hvorfor et tiltag findes.
- **Fravalgt:** organisationens egne risici (kun produktets indtil videre), trusselsmodellering (STRIDE), AI-forslag til risici og påmindelser pr. e-mail.

### B31 – Roller, invitationer og en system-admin, der ikke ser kundedata

- **Status:** Vedtaget
- **Beslutning:** (US-009, US-010)
  - **Fire roller i en organisation:**
    - Organisation admin (den, der opretter organisationen): alt, også medlemmer, roller, invitationer og AI-deling.
    - Compliance manager: frameworks, "not applicable", godkende/afvise AI-forslag, acceptere og lukke risici, plus alt, hvad en Developer må.
    - Developer: projekter, GitHub, koblinger, AI-forslag og risici, men ikke at godkende dem.
    - Auditor: kun læse, også loggen.
  - Rettighederne står i én tabel (`RolePermissions`). De tjekkes på serveren i hvert brugsscenarie (`CurrentMember.RequireAsync(Permission)`), og knapper, rollen ikke må bruge, skjules.
  - En organisation har altid mindst én organisation admin.
  - **En rolleændring ændrer ikke fortiden.** Hver beslutning gemmer den rolle, personen havde, da beslutningen blev truffet: logposter, AI-beslutninger, koblinger, "not applicable", accepterede risici og aktiverede versioner. Eksisterende data fik ved migrationen den rolle, personen havde (alle var organisation admin).
  - **Invitationer:** organisation admin laver et link med en rolle. Det kan bruges én gang, udløber efter 7 dage og kan trækkes tilbage. Kun SHA-256 af tokenet gemmes.
  - **System-admin** er ejeren af trackeren og gives som realm-rollen `system-admin` i Keycloak, aldrig i appen. System-admin ser alle organisationer og deres medlemmer og kan ændre roller, fx når en organisation har låst sig ude. System-admin kan ikke åbne organisationernes compliance-data. Ændringerne logges i organisationens log som systemet, med system-admins navn.
- **Begrundelse:** Adgang efter behov (ISO 27001 A.5.15, A.5.18). Et revisionsspor skal vise, med hvilken myndighed en beslutning blev truffet, også efter en rolle er ændret. Kunder skal kunne stole på, at ejeren af systemet ikke kigger i deres data.
- **Fravalgt:**
  - Invitation til et bestemt projekt, fordi der ikke findes projektmedlemskab.
  - At fjerne medlemmer (egen story).
  - At system-admin kan se data.

### B32 – AI-forslagene besluttes på Compliance-fanen

- **Status:** Vedtaget
- **Beslutning:** (US-044)
  - **Ét sted til forslagene:** de ventende AI-forslag vises under det krav, de handler om, på Compliance-fanen, og der godkendes eller afvises de. User Stories-fanen viser kun et mærke ("2 AI suggestions"), som linker til Compliance-fanen filtreret på "Has suggestions". "Suggest mappings" og "Show more" pr. story bliver på User Stories-fanen.
  - **Overblik øverst:** pr. regulering vises dækket / ikke dækket / AI-forslag / ikke relevant og en bjælke. Tallene er de samme som i US-043 R6: krav, der ikke er relevante, tæller ikke med.
  - **Filtre er links** (`?show=not-covered`, `has-suggestions`, `not-applicable`), så en filtreret visning kan deles.
  - **"Suggest for not covered"** spørger kun om den åbne regulerings krav, der hverken er dækket eller markeret ikke relevante. For hvert af dem bliver de 3 bedste stories til forslag. Ventende forslag til de krav erstattes. Par, der er koblet, godkendt eller afvist, foreslås aldrig igen.
  - Domænet er uændret. En afvisning kræver stadig en årsag (US-020 R7).
- **Begrundelse:** Hullerne lukkes pr. regulering, så det er dér, man har brug for forslagene (CRA art. 13, ISO 27001 5.31 og 9.1). Med beslutningen ét sted er man ikke i tvivl om, hvor et forslag blev afgjort.
- **Fravalgt:**
  - At godkende flere forslag på én gang.
  - At køre "Suggest for not covered" automatisk efter en GitHub-synk.
  - At vise "dækket af åbne stories" i overblikket.

### B33 – MVP'en for fase 1

- **Status:** Vedtaget
- **Beslutning:**
  - **Målet:** en lille udviklingsvirksomhed kan for hver regulering se, hvilke krav der er opfyldt, og bevise det over for en auditor.
  - **MVP'en er de stories, der har labelen `mvp`:**
    - US-044: AI-forslag og overblik på Compliance-fanen (bygget).
    - US-057: evidens pr. krav.
    - US-052: sporbarhedsmatrix som CSV, rykket frem fra fase 2.
    - US-017: Docker.
    - US-014: backup med en testet gendannelse.
    - US-048: projektets overblik.
    - US-015: samme CVR gemt på samme tid.
  - **Udskudt fra fase 1 til efter MVP'en:** `Kontrol`, `Kravvurdering` og `Release` fra domænemodellen. I MVP'en viser koblede stories og evidens, hvordan et krav opfyldes. Koblingen sammen med "ikke relevant" og dens begrundelse (US-043) fungerer som vurderingen.
  - Alle andre åbne stories bygges efter MVP'en. Nye idéer skrives som stories, men får ikke labelen `mvp`, medmindre de erstatter en af de syv.
  - **Udvidet 04-11-2026:** de syv er bygget. Før en rigtig kunde får adgang, skal sikkerhedens grundlag også være på plads, og CRA-kravene skal være komplette. Derfor har disse også fået `mvp`:
    - US-086: ingen organisation kan nå en andens data, testet på alle handlinger.
    - US-087: sikkerheds-headers.
    - US-088: sessioner udløber.
    - US-089: Keycloak stopper gætteri på passwords og kræver MFA for Admins.
    - US-090: et medlem kan fjernes.
    - US-094: tests, der holder injection ude.
    - US-084: TLS til databasen.
    - US-068: dækningsliste for de indbyggede regulativer.
    - US-096: kom-godt-i-gang-tjekliste.
  - **Fase 1 efter MVP'en** har labelen `phase:1`: US-069, US-075, US-083, US-091, US-092, US-093 og US-095. Resten har `phase:2` eller ingen fase endnu.
- **Begrundelse:** Uden evidens kan trackeren vise *hvem* der skal opfylde et krav, men ikke *at* det er gjort. Det er det, en auditor spørger om (ISO 27001 9.2, CRA bilag VII). En kunde skal kunne køre trackeren selv og ikke miste data (DOK-01 F3). Kontroller er mest vigtige for ISO 27001 på organisationsniveau, og releases bliver først nødvendige med SBOM (fase 3).
- **Fravalgt:**
  - At bygge kontroller og releases nu.
  - Flere rapporter (fase 2).
  - Flere AI-funktioner (US-037 til US-040).
  - Forbedringer af faser og risici (US-046, US-047, US-049) før MVP'en.

### B34 – Backup med pg_dump hver nat, 30 dage, uden for containeren

- **Status:** Vedtaget
- **Beslutning:** (US-014)
  - En egen service `backup` i Docker-stakken laver hver nat kl. 02:00 en `pg_dump` (custom format) af hele databasen til `./backups` på værtsmaskinen og sletter backupper ældre end 30 dage.
  - Gendannelse sker med `./scripts/restore.sh <fil>` i én transaktion. En fil, der ikke er en backup, afvises først.
  - Gendannelsen testes i CI med de samme scripts.
- **Begrundelse:** ISO 27001 A.8.13 kræver backup og test af gendannelse. `pg_dump` er PostgreSQLs eget værktøj, kræver ingen ekstra software og tager også filerne i databasen med (PDF'er, evidens, nøglerne til GitHub-tokens). En test i CI viser hver gang, at en backup faktisk kan gendannes.
- **Fravalgt indtil videre:**
  - Kryptering af backupperne.
  - En kopi et andet sted.
  - Point-in-time recovery.
  - At vise seneste backup i appen.

### B35 – Kravspecifikationer importeres som versioner og synkroniseres ikke

- **Status:** Vedtaget
- **Beslutning:** (US-032, US-033, US-034)
  - Et projekt kan få en eller flere kravspecifikationer (`RequirementDocument`) uploadet som Excel, CSV eller Word. Et Word-dokument kan også være et use case-dokument (Unified Process). Hver upload er en ny version (`RequirementDocumentVersion`), og filen gemmes i databasen.
  - Hvert krav bliver et `WorkItem` med `Source = Document` og `Type = Requirement`, så det vises og kan kobles til regler på samme måde som en user story fra GitHub. En use case bliver `Type = UseCase` med sine afsnit (primær aktør, forudsætninger, hovedforløb, udvidelser og resultat) gemt som JSON.
  - En use case's hovedforløb og resultat er dens acceptkriterier, så AI-mappingen (US-020) læser den som en user story.
  - Kravene matches på deres ID inden for samme kravspecifikation. Nye tilføjes, ændrede opdateres, og krav, der ikke længere er i filen, markeres "Removed in version N". Intet slettes.
  - En fil med et ID to gange afvises helt, så en halv import aldrig sker.
  - En ny version vælges i formularen, ellers bruges kravspecifikationen med samme filnavn.
  - I Word starter et krav ved en overskrift eller et afsnit, der begynder med et ID. Hvordan et ID ser ud, er et regulært udtryk pr. kravspecifikation (standard `[A-Z]{2,10}-\d+`). Et ID midt i teksten starter ikke et nyt krav. Udtrykket får højst ét sekund pr. afsnit.
- **Begrundelse:** Projekter, der følger vandfaldsmodellen, har deres krav i et dokument og ikke i et issue-system. Et dokument har ingen API at synkronisere med, så en ny version kommer kun, når nogen uploader den. Det er også det, ISO 27001 A.8.32 beder om: hver ændring er en version, man kan se og hente igen.
- **Fravalgt indtil videre:**
  - Gamle `.xls`-filer, Google Sheets og Numbers.
  - At vælge kolonnenavne ved upload.
  - Flere ark i samme fil (kun det første læses).
  - Krav i Word-tabeller, billeder og PDF.
  - Use case-diagrammer og "include"/"extend" som andet end tekst.
  - Et preview før importen.

### B36 – Factories, hvor der vælges mellem flere implementeringer

- **Status:** Vedtaget
- **Beslutning:** (US-083)
  - Der bruges en factory, hvor der findes flere implementeringer af det samme, og valget først træffes, mens appen kører:
    - dokumentlæsere (filtype og dokumenttype)
    - AI-udbydere (`Ai:Provider`)
    - senere også scanner- og SBOM-importører, rapportgeneratorer, kilder til work items og brancheprofiler
  - Implementeringerne registreres som *keyed services* i .NET, fx `"UseCases:.docx"` eller `"Ollama"`. Factory'en henter én ud fra nøglen.
  - Factory'ens interface ligger dér, hvor valget træffes:
    - Vælger en use case ud fra brugerens input, ligger interfacet i Application (`IRequirementDocumentReaderFactory`).
    - Er valget konfiguration, ligger factory'en i Infrastructure (`AiProviders`), og Application spørger bare efter `IEmbeddingModel`.
  - Application og Domain bruger aldrig `IServiceProvider` (ingen *service locator*). En arkitekturtest fejler, hvis det sker.
  - Hvor der kun er én implementering (repositories og use cases), er DI-containeren selv factory'en. Der skrives ingen factory-klasse.
  - En ukendt nøgle giver en klar besked: en ukendt filtype afvises med de filtyper, der virker, og en ukendt `Ai:Provider` stopper appen ved start.
- **Begrundelse:** En ny filtype, scanner eller udbyder bliver én ny klasse og én registrering, uden ændringer i use cases eller andre steder (ISO 27001 A.8.27, A.8.32). Det er nemt at teste hver del for sig. En factory gør ikke systemet hurtigere i sig selv; gevinsten er, at det er nemt at udvide uden at ændre det, der virker.
- **Fravalgt:** factories overalt, og et plugin-system, der indlæser assemblies, mens appen kører.

### B37 – Kryptering i lag: først TLS til databasen

- **Status:** Vedtaget
- **Beslutning:** (US-084)
  - Hele stakken (`--profile full`) får sin egen database, `postgres-full`. Den tager kun imod forbindelser over TLS (`hostssl` i `pg_hba_tls.conf`), og kun med password (SCRAM).
  - Webappen, migreringerne og backuppen godkender certifikatet (`SSL Mode=VerifyFull`, `sslmode=verify-full`). Certifikatet udstedes af den lokale CA, som `certs`-servicen laver.
  - Udviklingsdatabasen (port 5433, Visual Studio) er uændret og kører uden TLS.
- **Begrundelse:** TLS beskytter data på vej mellem webappen og databasen og er billigt at indføre. Det er nødvendigt, så snart databasen ligger på en anden maskine eller i skyen (ISO 27001 A.8.24, ASVS V9.2.2). En separat database til stakken betyder, at din udviklingsdatabase ikke ændres, og stakken ligner produktion.
- **Senere lag, i den rækkefølge:**
  - Krypteret disk eller volume (drift).
  - Krypterede backupper (genbesøger B34).
  - Kryptering af filer og hemmeligheder i appen med nøgler uden for databasen.
- **Fravalgt:** at slå TLS til på den fælles database, fordi det ville ændre udviklingsmiljøet og kræve certifikater i Visual Studio.

### B39 – Engangskode (MFA) for alle brugere

- **Status:** Vedtaget
- **Beslutning:** (US-089)
  - Alle brugere skal sætte en authenticator-app (TOTP) op ved første log-ind, og Keycloak beder om koden ved hvert log-ind.
  - Brute force-beskyttelse er slået til med en midlertidig lås.
  - Passwords skal have mindst 12 tegn.
  - Det hele står i realm-filen. En eksisterende Keycloak sættes op efter `docs/keycloak-sikkerhed.md`.
- **Begrundelse:** Keycloak kender ikke trackerens roller (de ligger i trackeren, US-010), så "kun Admins" ville kræve en ny integration, der holder en Keycloak-gruppe opdateret. Alle brugere kan se fortrolige compliance-data, og et stjålet password må ikke være nok (ISO 27001 A.8.5).
- **Fravalgt:**
  - MFA kun for Admins.
  - Passkeys/WebAuthn (senere).
  - Log-ind med kundens egen identitetsudbyder (senere).

### B38 – Trackeren følger producentens og udbyderens pligter

- **Status:** Vedtaget
- **Beslutning:** (US-068)
  - For hvert indbygget regulativ fører `docs/daekning-af-regulativer.md` en liste over alle artikler og bilag med status "I trackeren", "Mangler" (med issue) eller "Udeladt" (med grund).
  - Trackeren indbygger de pligter, der ligger hos producenten (CRA) eller hos udbyderen og idriftsætteren (AI Act).
  - Pligter for myndigheder, bemyndigede organer, Kommissionen, importører, distributører og open source-forvaltere udelades, og det samme gør frivillige ordninger og pligter, der kun opstår, når en myndighed beder om noget.
  - En test holder listen og de indbyggede krav i takt begge veje.
- **Begrundelse:** Trackerens brugere er dem, der laver produkterne. En liste med grunde viser en auditor, at intet er glemt, og hvorfor noget mangler (ISO 27001 A.5.31). Gennemgangen viste, at CRA's dokumentation og overensstemmelse også står i artikel 28–32, ikke kun i artikel 13.
- **Fravalgt:** at indbygge hele regulativet, også myndighedernes pligter.

### B40 – Aktivitetsloggen kan ikke ændres

- **Status:** Vedtaget
- **Beslutning:** (US-091)
  - Databasen nægter `UPDATE`, `DELETE` og `TRUNCATE` på `ActivityLog` med triggere, også fra SQL uden om trackeren.
  - Hver post får et fingeraftryk (SHA-256) af sit indhold og af den forrige posts fingeraftryk i samme organisation. Kæden lægges, når posten gemmes, i én transaktion med en lås pr. organisation, så to samtidige requests ikke deler kæden.
  - Admins og Auditors kan på `/LogCheck` få kæden gennemgået. Siden viser den første post, der ikke passer.
  - Poster fra før kæden har intet fingeraftryk og godtages kun i starten af loggen.
- **Begrundelse:** Loggen er bevis over for en auditor (ISO 27001 A.8.15, CRA bilag I del I (2)(l)). Triggerne stopper fejl og almindelige brugere. Kæden opdager det, triggerne ikke kan stoppe: en ejer af tabellen kan slå triggerne fra, men kan ikke ændre en post uden at bryde kæden fra det punkt.
- **Fravalgt:**
  - At gemme fingeraftrykket uden for databasen (fx i en ekstern tidsstempeltjeneste). Senere, hvis en auditor kræver det, for en ejer kan stadig skrive hele kæden om.
  - En særskilt databasebruger uden rettigheder til at ændre tabellen. Det kræver ændringer i udviklingsmiljøet og deployment.

### B41 – CRA version 2, og hvad der følger med, når et projekt skifter version

- **Status:** Vedtaget
- **Beslutning:** (US-069)
  - CRA version 2 har bilag I uændret (samme koder, titler og tekster) plus producentens pligter i art. 13, 14 og 28–32 og bilag II og VII. Version 1 ændres ikke.
  - Version 2 gælder fra 11-09-2026, hvor art. 14 begynder at gælde (art. 71). Derfor er det den version, nye projekter får. Et projekt på version 1 ser "Newer version available".
  - Når et projekt skifter version, følger links, "ikke relevant"-afgørelser og evidens med til kravet med samme kode. Hvem der besluttede det, og hvornår, bevares. En evidensfil kopieres. En kode, der ikke findes i den nye version, bliver på den gamle som historik. Loggen siger, hvor meget der fulgte med.
  - Bilag II og VII får koderne `Annex II.n` og `Annex VII.n`, fordi `II.n` allerede er bilag I, del II.
- **Begrundelse:** En version har én startdato, men CRA gælder i trin. Den tidligste dato gør de nye pligter synlige, mens art. 14 allerede gælder. Uden overførsel ville et versionsskifte nulstille projektets dækning og bevismateriale (ISO 27001 A.8.32).
- **Fravalgt:**
  - Startdato pr. krav (ville kræve en ændring af domænemodellen fra US-035).
  - At lade links pege på en kode i stedet for et krav-id.

### B42 – Feedback fra brugerne bliver issues i trackerens eget repository

- **Status:** Vedtaget
- **Beslutning:** (US-063)
  - Alle medlemmer kan sende feedback fra sidefoden, formet som en user story, med et valgfrit skærmbillede (PNG eller JPG, højst 5 MB, tjekket på indholdet).
  - Kun systemadministratoren ser feedbacken. Det er det eneste fra en organisation, systemadministratoren kan se (undtagelse fra B31), fordi brugeren selv vælger at sende den.
  - Systemadministratoren kan rette teksten og oprette et issue i trackerens eget repository (`Feedback:Repository`, som standard `Zuvuz/compliance-tracker`) med labels `user-story`, `status:draft` og `feedback`. Issuet indeholder aldrig afsenderen, organisationen eller skærmbilledet. Det siger kun, om der er et skærmbillede i trackeren.
  - Det er det eneste sted, trackeren skriver til GitHub (undtagelse fra B12). Det bruger sit eget fine-grained token med kun "Issues: read and write" på det ene repository, fra miljøvariablen `Feedback__GitHubToken`.
  - Højst 10 feedbacks i timen pr. bruger.
- **Begrundelse:** CRA bilag I, del II (5)–(6): producenten skal kunne modtage rapporter fra brugerne. Skærmbilleder kan indeholde kundedata, og repositoryet kan være offentligt, så de bliver i trackeren (ISO 27001 A.5.34).
- **Fravalgt:**
  - At uploade skærmbilledet til GitHub.
  - At bruge projekternes GitHub-tokens (de må kun læse).
  - En liste over brugerens egen feedback (senere).

### B43 – Udkast til CRA's tekniske dokumentation som DOCX

- **Status:** Vedtaget
- **Beslutning:** (US-056)
  - Trackeren laver et udkast med de otte punkter fra CRA bilag VII som DOCX, så det kan gøres færdigt i Word eller LibreOffice. Udkastet kan laves igen når som helst (art. 31, stk. 2).
  - Trackeren udfylder: projektets navn, beskrivelse, fase, scope og tilsigtede formål (punkt 1), user stories (punkt 2(a)), risikoregistret og hvordan hvert krav i bilag I er opfyldt eller hvorfor det ikke gælder (punkt 3), og de andre rammeværker projektet følger (punkt 5).
  - Resten står som "To be completed" med en kort vejledning: supportperioden, softwareversioner, billeder, brugerinformation, arkitektur, sårbarhedshåndtering, produktion, standarder, testrapporter, overensstemmelseserklæringen og SBOM.
  - Kun projekter, der følger CRA, kan lave udkastet. Linket står under "Reports" på projektets Overview-fane.
- **Begrundelse:** Producenten skal udarbejde den tekniske dokumentation før markedsføring (art. 13, stk. 12, og art. 31). Et udkast fra trackerens data sparer arbejde og holder dokumentet i takt med trackeren. DOCX er det format, de fleste kan redigere videre i.
- **Fravalgt:**
  - At vente på US-050, US-051 og US-054. Risikoafsnittet bygges direkte fra risikoregistret, og låsning kommer med US-050.
  - Udkastet pr. release i stedet for pr. projekt (senere, når releases findes, B07).
  - AI-skrevet tekst i udkastet.

### B44 – Evidens hører til projektet og kobles til mange krav

- **Status:** Vedtaget
- **Beslutning:** (US-059)
  - `Evidence` hører til projektet og gemmes én gang. En ny `EvidenceLink` siger, hvilke krav den beviser (mange-til-mange), med hvem der koblede den, rolle og tidspunkt. Afkobling gemmes som historik.
  - Fanen "Evidence" på projektet: upload én gang, kobl til krav, fjern fra alle krav på én gang. Alle i organisationen ser fanen, også Auditor. Kun medlemmer med `LinkWork` kan ændre.
  - Efter upload foreslår den lokale AI de krav, evidensen mest sandsynligt beviser (4 ad gangen, "Show more"), med score og en begrundelse mærket "AI-generated". Et forslag bliver først et link, når en person godkender det. Afviste og koblede par foreslås aldrig igen (B13). Uden AI'en virker alt andet.
  - AI'en læser tekst fra PDF, TXT, CSV og Markdown. Billeder og links matches kun på titlen.
  - På Compliance-fanen kan "+ Evidence" stadig uploade ny evidens, og man kan vælge eksisterende evidens. Krydset tager evidensen af det ene krav. Var det det eneste krav, den beviste, fjernes evidensen også (som i US-057).
  - Migreringen giver hver evidens fra US-057 ét link til sit krav, med samme hvem og hvornår.
  - Når et projekt skifter version (US-069), flyttes kun linkene. Filen kopieres ikke længere.
- **Begrundelse:** Ét dokument (fx en adgangspolitik) beviser ofte flere krav. Det skal gemmes én gang med én SHA-256, så en auditor kan se pr. dokument, hvad det beviser, og pr. krav, hvilke dokumenter der er (ISO 27001 7.5.3 og 9.2).
- **Fravalgt:**
  - Evidens delt mellem projekter eller på organisationsniveau (senere, med kontroller, B33).
  - At AI'en kun foreslår, når man trykker på en knap. Upload er det øjeblik, hvor man forventer forslagene.

### B45 – Evidensforslag fra tests og pull requests

- **Status:** Vedtaget
- **Beslutning:** (US-058)
  - Et scenarie tagget `@<regulativ>-<kode>` (fx `@cra-I.2(d)`) er bevis for kravet. Regulativ-delen er den korte adresse fra Compliance-fanen (`cra`), og koden skrives præcis som i trackeren, med parenteser.
  - Testresultater uploades på Evidence-fanen som TRX eller JUnit XML (højst 10 MB). Filens SHA-256 gemmes. Ét forslag pr. krav og testkørsel. Et fejlende scenarie med samme tag blokerer forslaget og vises som "1 failing test". Ukendte tags vises og ignoreres.
  - Tags læses fra `TestCategory` (TRX), `property name="Category"` (JUnit) og en linje `Requirement tags: …` i testens output. xUnit skriver ingen tags i TRX-filen, så trackerens egne specs skriver linjen med en hook (`RequirementTagsHook`).
  - Når en lukket story er koblet til et krav, foreslås den mergede pull request, der refererer til issuet i GitHub's tidslinje ("cross-referenced"). Det sker ved hver synkronisering, højst 20 stories ad gangen, kun med læseadgang.
  - Intet bliver evidens, før en person med `LinkWork` accepterer. En pull request bliver et link. En testkørsel bliver en Markdown-fil med de beståede scenarier og resultatfilens SHA-256. Afvisning kræver en grund, og samme kilde foreslås aldrig igen. Krav, der ikke gælder, får ingen forslag.
  - Ingen AI: tags og links afgør det. AI-resuméet (R7, valgfrit) er ikke bygget.
- **Begrundelse:** Beståede tests og mergede pull requests er det bevis, teamet allerede har (ISO 27001 8.29, CRA bilag VII (2) og (6)). Tags er entydige, så der er intet at gætte.
- **Fravalgt:**
  - `@cra-I.2.d` uden parenteser. Reqnroll tillader parenteser, og koden er så den samme som i trackeren.
  - At hente testresultater fra GitHub Actions automatisk (kræver `actions: read` på tokenet; senere).
  - At gemme hele resultatfilen som evidens. Filtyperne fra US-057 tillader ikke XML, og en kort Markdown-fil er lettere at læse for en auditor.

### B46 – Fanen "Product requirements" og projektets metoder

- **Status:** Vedtaget
- **Beslutning:** (US-104)
  - Fanen "User stories" hedder "Product requirements" (`/product-requirements`), fordi den rummer alle måder at beskrive produktets krav på. Den gamle adresse `/user-stories` sender videre (301), så delte links virker.
  - Projektet gemmer, hvilke metoder det bruger: GitHub user stories, kravspecifikation og use cases. Det er en eller flere, valgt med afkrydsningsfelter af roller med `EditProjects`, og ændringen logges med før og efter.
  - Kun de valgte metoders afsnit og elementer vises på fanen. At slå en metode fra sletter intet: links, dækning og evidens bliver og vises stadig på Compliance-fanen.
  - Et nyt projekt bruger alle tre, indtil nogen vælger. Mindst én metode skal altid være valgt. Eksisterende projekter får de metoder, de allerede bruger (et GitHub-repository, en uploadet kravspecifikation eller use cases); et projekt, der ikke bruger nogen, får alle tre.
- **Begrundelse:** Holdene arbejder forskelligt (B02), og fanen skal kun vise det, holdet bruger. Valget gemmes på projektet, så alle ser det samme, og det kan senere bruges af den tekniske dokumentation (CRA bilag VII (2)(a)) og af AI-udkast til nye krav (US-106).
- **Fravalgt:**
  - At et nyt projekt starter uden metoder og spørger først (som issuet foreslog). Det ville give en tom fane, før man kan komme i gang.
  - Et valg pr. bruger i browseren. Så ville to personer på samme projekt se forskellige faner.
  - At omdøbe `WorkItem` i koden.

### B47 – Minimumsscore for AI-forslag

- **Status:** Vedtaget
- **Beslutning:** (US-105)
  - Et AI-forslag under en minimumsscore laves ikke og vises ikke. Story-forslag (US-020, US-044) og evidensforslag (US-059) har hver sin grænse: `Ai:MinimumScoreForStories` og `Ai:MinimumScoreForEvidence`, begge 0,5 som udgangspunkt.
  - Driften sætter grænserne i indstillingerne, ikke organisationens admin i appen. Hvert forslag gemmer den grænse, det blev lavet med (sammen med model og prompt-version), så gamle og nye forslag kan sammenlignes (AI Act art. 12).
  - Når intet når grænsen, siger Evidence-fanen "No requirement looks like a match; link it by hand." Grænsen står over forslagene.
  - Svage forslag skjules helt; de foldes ikke væk.
  - Én embeddings-model og én chat-model bruges stadig til alt. En egen model til udkast kommer med US-106, hvor den første opgave har brug for det.
- **Begrundelse:** Forslag med score omkring 0,45 viste sig at være støj: AI'ens egen begrundelse sagde, at evidensen ikke beviste kravet. Et menneske skal kun bruge tid på forslag, der kan være rigtige (AI Act art. 14).
- **Fravalgt:**
  - At lade godkendte og afviste forslag justere scoren automatisk (læring). Senere, når der er data nok.
  - At logge ændringer af grænserne i appen: de ændres i driftens indstillinger, og hvert forslag gemmer grænsen.
  - Startværdien er et skøn. Den justeres ud fra scorerne i US-036's eksport af godkendte og afviste forslag.

### B48 – Supportperiode på projektet

- **Status:** Vedtaget
- **Beslutning:** (US-025)
  - Et projekt kan ikke skifte til "Released" uden en slutdato for supporten (CRA art. 13, stk. 8). Datoen gives i samme formular som fasen. Projektet husker udgivelsesdagen, og de fem år regnes fra den.
  - En slutdato under fem år efter udgivelsen kræver en skriftlig grund. Datoen kan kun flyttes senere, aldrig tidligere. Hver ændring logges som `SupportPeriodSet` med gammel og ny dato.
  - Headeren viser "Supported until dd-MM-yyyy" (art. 13, stk. 19). Overview-fanen har et kort til at flytte datoen og viser "No support end date" under "Needs attention" for et udgivet projekt uden dato (fx et, der blev udgivet før US-025).
  - Udkastet til den tekniske dokumentation (US-056) viser datoen og en eventuel grund under bilag VII (1) og (4).
- **Begrundelse:** Supportperioden er en pligt i CRA, og brugerne skal kende slutdatoen ved køb. At den ikke kan flyttes tidligere, beskytter brugerne mod kortere support, end de blev lovet.
- **Fravalgt:**
  - At flytte et projekt til "EndOfSupport" automatisk på slutdatoen (issuets åbne spørgsmål; det kommer med US-074).
  - Påmindelser før supporten slutter.
  - At kræve en dato, når et projekt oprettes direkte som "Released". Det vises i stedet under "Needs attention".

### B49 – Sikkerhedshændelser og fristerne i CRA art. 14

- **Status:** Vedtaget
- **Beslutning:** (US-073)
  - En aktivt udnyttet sårbarhed eller en alvorlig hændelse registreres på projektets fane "Security events" med tidspunktet, hvor producenten fik kendskab til den. Fristerne regnes derfra, tjekket mod den officielle tekst (EUR-Lex, 05-11-2026):
    - Early warning: 24 timer (art. 14, stk. 2, litra a, og stk. 4, litra a).
    - Notifikation: 72 timer (stk. 2, litra b, og stk. 4, litra b).
    - Slutrapport for en sårbarhed: 14 dage efter, at en korrigerende eller afhjælpende foranstaltning er tilgængelig (stk. 2, litra c).
    - Slutrapport for en hændelse: én måned efter, at notifikationen er indsendt (stk. 4, litra c).
  - Trackeren sender ikke rapporterne. En person markerer hver som sendt med tidspunkt og den reference, platformen gav. En rapport sendt efter fristen vises som "Sent late".
  - Overview viser frister inden for 24 timer og overskredne frister (i rødt) under "Needs attention", og supportperiodens slutning 90 dage før.
  - En hændelse kan kun lukkes, når alle tre rapporter er sendt. Et projekt kan ikke gå til "End of support", mens en hændelse er åben.
  - Tider gemmes i UTC og vises i lokal tid.
  - Samme roller som risikoregistret (`ManageRisks`) kan registrere og markere. Alle i organisationen kan se fanen.
- **Begrundelse:** Art. 14 har gældet siden 11-09-2026, og fristerne er korte. En overskredet frist skal kunne ses med det samme (ISO 27001 A.5.24–A.5.26).
- **Fravalgt:**
  - At sende rapporterne til ENISA's fælles platform eller en CSIRT (uden for scope).
  - At give brugerne besked (art. 14, stk. 8) fra trackeren; kravet ligger på Compliance-fanen.
  - Porte mellem faserne (US-071, US-072). Kun "End of support"-reglen er med her.

### B50 – Klassificering af et projekt under hvert regulativ

- **Status:** Vedtaget
- **Beslutning:** (US-026)
  - Et projekt klassificeres under hvert indbygget regulativ, det følger. Hvert regulativ har sine egne klasser plus "Not applicable":
    - CRA: Default, Important class I, Important class II, Critical (art. 7–8, bilag III–IV).
    - AI Act: Prohibited, High-risk, Limited risk, Minimal risk (art. 5, 6, 50, bilag I og III).
  - En begrundelse er påkrævet. En ny klassificering erstatter den gamle; alle gemmes og kan ses med hvem, hvornår og hvorfor. Hver klassificering logges som `ProjectClassified`.
  - Klassificeringen står på Overview-fanen med en kort vejledning pr. regulativ, der nævner de artikler og bilag, som afgør klassen. Kun roller med `DecideCompliance` kan klassificere.
  - Klasserne står i koden (`RegulationClasses`, fundet ud fra regulativets korte navn), ikke som seed-data, fordi de følger forordningens tekst og ikke ændres af brugerne.
  - `Project.craClass` i klassediagrammet er erstattet af `ProjectClassification`.
- **Begrundelse:** Klassen afgør, hvilken overensstemmelsesprocedure der gælder (CRA art. 32) og hvilke krav der gælder (AI Act). En auditor skal kunne se, hvorfor (ISO 27001 A.5.31).
- **Fravalgt:**
  - MDR (klasse I, IIa, IIb, III). Den kommer med US-023, når MDR bliver et indbygget regulativ.
  - At klassen styrer, hvilke krav der vises. Det kommer med US-061 og US-080.

### B51 – End of support og opbevaring

- **Status:** Vedtaget
- **Beslutning:** (US-074)
  - Når et projekt går til "End of support", angiver man den sidste dag, produktet blev bragt i omsætning (som standard i dag). Projektet og dets dokumentation opbevares derefter mindst 10 år efter den dag, eller til supportperiodens slutning, hvis den er senere (CRA art. 13, stk. 13, tjekket mod EUR-Lex 05-11-2026). En senere dato kan vælges, aldrig en tidligere.
  - Headeren viser "End of support since … · kept until …". Fasen logges med opbevaringsdatoen.
  - Projektet er skrivebeskyttet (US-024), men kan stadig læses og eksporteres af alle i organisationen, også Auditor.
  - `Project.EnsureMayBeRemoved` afviser arkivering og sletning før opbevaringsdatoen. Arkivering kommer med US-028, som skal bruge reglen.
  - Evidensfiler og kravdokumenter slettes aldrig (de var allerede kun markeret som fjernet).
- **Begrundelse:** Markedsovervågningen kan bede om dokumentationen i årevis efter, at produktet er udgået (ISO 27001 A.5.33, A.8.10).
- **Fravalgt:**
  - At kræve en låst teknisk dokumentation (US-050) før end of support. Udkastet (US-056) kan altid laves; låsning kommer med US-050.
  - At slette projekter efter opbevaringsdatoen (en senere story).

### B52 – Et projekt kan kobles til ét GitHub Project i en organisation

- **Status:** Vedtaget
- **Beslutning:** (US-107)
  - Linkformularen tager en GitHub-adresse: et repository (som i dag), et GitHub Project (`github.com/orgs/<org>/projects/<nummer>`) eller en organisation (`github.com/<org>`), hvor man derefter vælger et af organisationens projekter.
  - Et GitHub Project læses med GitHub's GraphQL-API og kun med et token. Kun det koblede projekts issues læses, også når de ligger i flere repositories. Organisationens andre projekter og repositories læses aldrig.
  - Et tracker-projekt er koblet til højst ét repository eller GitHub Project. Ved et skift markeres de gamle stories som fjernet ved næste synkronisering. To tracker-projekter i samme organisation kan ikke koble sig til samme GitHub Project.
  - Referencen er stadig `ejer/repo#nummer`, så sporbarhed, PR-forslag (US-058) og links virker uændret.
  - Trackeren læser stadig kun (B12).
- **Begrundelse:** En organisation med flere produkter i samme GitHub må ikke få dem blandet sammen; hvert tracker-projekts dækning skal komme fra dets egne stories (ISO 27001 A.5.15, A.8.25).
- **Fravalgt:**
  - Personlige (brugerejede) GitHub Projects.
  - Flere GitHub Projects til ét tracker-projekt.
  - **Kendt begrænsning:** GitHub kan ikke begrænse et token til ét Project; et token med læseadgang til projects kan læse alle organisationens projects. README anbefaler en maskinbruger, der kun er medlem af det ene projekt.

### B53 – Gennemgang af links, når kravets tekst ændres i en ny version

- **Status:** Vedtaget
- **Beslutning:** (US-110)
  - Alt følger stadig med til kravet med samme kode, når et projekt skifter version (B41): links, "ikke relevant"-afgørelser og evidens.
  - Er kravets titel eller tekst ændret (ses bort fra forskelle i mellemrum), markeres det overførte "Needs review". Uændrede krav overføres uden markering.
  - Et link eller en evidens, der venter på gennemgang, tæller ikke. Kravet står som "Covered, needs review", til nogen har set på det.
  - Rækken viser den gamle tekst overstreget over den nye. "Still correct" godkender alt på kravet på én gang og logges med hvem og hvornår. Det, der ikke passer længere, fjernes med det eksisterende kryds.
  - Links og evidens kræver `LinkWork`. En "ikke relevant"-afgørelse kræver også `DecideCompliance`.
  - Compliance-fanen har filteret "Needs review", og Overview viser antallet under "Needs attention".
- **Begrundelse:** Et link, der var rigtigt for den gamle tekst, beviser ikke nødvendigvis den nye. Dækningen skal ikke se bedre ud, end den er (ISO 27001 A.8.32, 7.5.3).
- **Fravalgt:**
  - At gennemgå alt, der overføres. Det giver meget at klikke igennem for krav, der ikke er ændret.
  - At AI'en vurderer, om linket stadig passer (kan komme med US-038).

### B54 – Handlinger på risici, sporet til user stories og evidens

- **Status:** Vedtaget
- **Beslutning:** (US-111)
  - En risiko kan have handlinger med titel, ejer og en valgfri frist. De ligger på risikoens side under "Treatment actions" sammen med de user stories og den evidens, der udfører dem. Det er sporet fra risiko over handling til bevis.
  - Handlingen er en del af risikoen, ligesom de eksisterende measures, som bliver ved med at vise, hvilke krav og stories der mindsker risikoen.
  - En handling er "Planned", bliver "In progress", når noget kobles på, og kan kun markeres "Done", når en koblet user story er lukket eller en koblet evidens findes. Hvem og hvornår gemmes.
  - Handlinger efter fristen vises på Risks-fanen og under "Needs attention" på Overview.
  - Samme rolle som risikoregistret (`ManageRisks`). Alt logges.
- **Begrundelse:** ISO 27001 6.1.3 og 8.3 kræver en plan for risikohåndtering og bevis for, at den udføres. CRA art. 13(2) kræver, at risikovurderingen holdes opdateret. Kravet om bevis forhindrer, at en handling står som udført uden spor.
- **Fravalgt:**
  - Behandlingsform pr. handling. Risikoen har allerede sin behandling (Reduce, Accept …).
  - At residualniveauet kun kan sættes, når alle handlinger er udført. Det ville ændre eksisterende risici; det kan komme senere.
  - At genåbne en handling, når en koblet story genåbnes.

### B55 – Hvad AI'en får at vide om projektet, og "ikke relevant" som forslag

- **Status:** Vedtaget
- **Beslutning:** (US-060, US-061)
  - AI'ens begrundelse for et forslag får projektets formål, scope og klassificering under kravets eget regulativ. Den får også de åbne risici, hvis measures nævner kravet. Tomme felter skrives som "(not given)", så modellen ikke gætter.
  - Hvert forslag gemmer, hvilken kontekst der blev brugt ("Context used: purpose, classification, risk 'Stolen sessions'"). Prompten hedder nu `mapping-v3`, så gamle og nye forslag kan sammenlignes (US-036).
  - Krav, som en åben High- eller Critical-risiko nævner, får mærket "High risk", og deres forslag vises først.
  - "Suggest not applicable" spørger den lokale chatmodel om de ikke-dækkede krav i ét regulativ, højst 20 pr. tryk. Kun et klart "NOT APPLICABLE: <grund>" bliver et forslag. Et krav med en åben risiko foreslås aldrig.
  - Et forslag om "ikke relevant" bliver kun en afgørelse, når en person med `DecideCompliance` accepterer det. Grunden kan rettes først. Accepterede og afviste forslag gemmes med rolle.
  - Ændres klassificeringen, erstattes ventende forslag for det regulativ. Afgørelser, der blev accepteret ud fra den gamle klassificering, vises med "Based on an earlier classification" og under "Needs attention", til nogen trykker "Still not applicable" eller gør kravet relevant igen.
- **Begrundelse:** CRA art. 13(2)–(3) lader risikovurderingen afgøre, hvilke krav der gælder, og AI Act art. 6 gør det samme for klassen. AI'en kan foreslå, men et menneske beslutter (AI Act art. 14), og det skal kunne ses, hvad AI'en byggede på (art. 13).
- **Fravalgt:**
  - At sende konteksten til en ekstern model. Den bliver i organisationens egen infrastruktur (US-020 R2).
  - At ændre selve embedding-scoren efter risiko. Kun rækkefølgen ændres.

### B56 – En model pr. AI-opgave

- **Status:** Vedtaget
- **Beslutning:** (US-117)
  - AI'ens opgaver har hver sit navn: Embedding, Ranking, Rationale, EvidenceCheck, GapDrafts, RiskDrafts og ImportFill. Driften kan give hver opgave sin egen model i indstillingerne, `Ai:Tasks:<Opgave>:Model`. Den kan også få en anden server, `Ai:Tasks:<Opgave>:BaseUrl`, men stadig i organisationens egen infrastruktur.
  - En opgave uden egen indstilling bruger standardmodellerne (`Ai:EmbeddingModel` og `Ai:ChatModel`), så intet ændrer sig, før driften vælger.
  - I dag bruger forslag til user stories og evidens opgaven Embedding, begrundelser for story-forslag Rationale, og begrundelser for evidensforslag EvidenceCheck. De andre opgaver får deres model, når deres stories bygges.
  - Hvert forslag gemmer stadig, hvilken model der lavede det.
  - Kan en opgaves egen model ikke nås, siger fejlen hvilken ("Embedding is unavailable: the model … cannot be reached"). Opgaven falder ikke tilbage til en anden model, så en svagere model aldrig bruges, uden at nogen ved det.
  - Systemsiden viser, hvilken model hver opgave bruger.
  - Embeddings gemmes ikke. De beregnes, hver gang der foreslås, så et modelskift kræver ingen genberegning.
- **Begrundelse:** De mange små kald (søgning) skal være hurtige, mens de få, der skriver tekst til mennesker, har gavn af en større model. Det skal kunne ses og måles (US-116), hvilken model der gjorde hvad (AI Act art. 12, art. 15).
- **Fravalgt:**
  - At organisationens admin vælger modeller i appen. Driften styrer modellerne, ligesom grænserne i B47.
  - At falde tilbage til standardmodellen, når en opgaves egen model er nede.

### B57 – Måling af AI'ens kvalitet på egne beslutninger

- **Status:** Vedtaget
- **Beslutning:** (US-116)
  - Driften (systemadministratoren) har en side "AI quality" under System. Den måler AI'en på de forslag, folk har godkendt og afvist (US-036), i de organisationer, der har slået deling til. Intet forlader infrastrukturen, og en måling ændrer ingen forslag eller links.
  - Story-matching måles automatisk med opgaven Embeddings aktuelle model (US-117). For hvert godkendt forslag måles, hvor ofte kravet er blandt de første 1, 3 og 10 af alle krav i versionen. For hvert afvist forslag måles, om det stadig scorer over grænsen.
  - Begrundelser vurderes af et menneske: op til 20 af de nyeste begrundelser får en karakter fra 1 til 5, og gennemsnittet gemmes.
  - Hver kørsel gemmes med trin, model, prompt-version, antal beslutninger og hvem der kørte den. Siden sammenligner den nyeste story-matching med den forrige ("Top 3: 71 → 78 (better)").
  - Under `Ai:MinimumDecisionsForEvaluation` beslutninger (standard 50) siger siden "Too few decisions to measure".
- **Begrundelse:** Et modelskift skal besluttes ud fra målinger på organisationens egne data, ikke ud fra et gæt om, at en større model er bedre (AI Act art. 15, art. 17; ISO 27001 A.8.32).
- **Fravalgt:**
  - Måling af evidens-matching. Den følger samme mønster og kan komme senere.
  - At skifte model automatisk ud fra resultatet.
  - Offentlige benchmarks.

### B58 – AI Act's krav i trackeren

- **Status:** Vedtaget
- **Beslutning:** (US-062)
  - Den indbyggede AI Act version 1 får 31 krav i fem grupper:
    - AI literacy: art. 4.
    - Forbudte praksisser: art. 5, stk. 1, litra a–h.
    - Krav til højrisiko-systemer: art. 9–15.
    - Udbyderens pligter: art. 16–20, 43, 47–49, 72 og 73.
    - Gennemsigtighed: art. 50, stk. 1–4.
  - Koderne følger forordningen (`Art. 9`, `Art. 5(1)(c)`, `Art. 50(2)`). Kravene er seed-data med faste id'er og vises med det samme for projekter, der allerede bruger AI Act.
  - **Titel og beskrivelse er et resumé med egne ord, ikke den officielle tekst** (en afvigelse fra US-062 R2). Hvert krav linker til sin artikel på EUR-Lex, hvor den bindende tekst står. Beskrivelsen starter med, hvornår kravet gælder: art. 4 og 5 fra 2. februar 2025, højrisiko-kravene fra 2. august 2026 (2. august 2027 for systemer under bilag I) og art. 50 fra 2. august 2026.
  - Pligterne for AI-modeller til almen brug (kapitel V) er ikke med. De gælder modeludbydere, ikke de produktteams, trackeren er lavet til.
  - B20 er opdateret: AI Act har nu krav.
- **Begrundelse:** Uden krav kunne et projekt følge AI Act, men ikke dække eller bevise noget. Resuméer på egne ord er korte nok til at matche med user stories, og de undgår at gengive den officielle tekst forkert fra hukommelsen. Linket til EUR-Lex giver altid den bindende tekst.
- **Fravalgt:**
  - Den officielle tekst som beskrivelse. Den kan komme i en ny version, når teksten hentes fra EUR-Lex og kontrolleres.
  - Senere ændringer af forordningen (fx udskudte frister). De bliver en ny version (US-035 R7).
  - Kapitel V (AI-modeller til almen brug).

### B59 – Scanningsresultater (SARIF) og fanen "Scans"

- **Status:** Vedtaget
- **Beslutning:** (US-078)
  - Et projekt har sin egen fane "Scans". Den har upload af SARIF 2.1.0-filer (højst 50 MB), den seneste scanning pr. værktøj, de åbne fund efter alvor og en liste over løste og accepterede fund.
  - Formatet genkendes på indholdet (`version` 2.1.0 og `runs`), ikke kun på filnavnet. Andre filer afvises med "Only SARIF files can be uploaded". Importen sker bag `IScanImporter`, så andre formater kan komme til (B36).
  - Alvor tages fra `security-severity` (9+ Critical, 7+ High, 4+ Medium, ellers Low), som Trivy, Grype og CodeQL skriver. Ellers bruges SARIF's level (error = High, warning = Medium).
  - Fund følges mellem scanninger fra samme værktøj via værktøjets eget fingeraftryk, eller via regel, placering og besked. Nye fund tilføjes. Fund, der ikke længere rapporteres, bliver "Fixed in scan of <dato>", og fund, der kommer igen, er åbne igen. Accepterede fund forbliver accepterede.
  - Et fund kan accepteres med en grund. En scanning kan bruges som evidens for et krav og gemmes så som evidensfil med SHA-256 (SARIF er nu en tilladt evidenstype).
  - Overview viser åbne fund med alvor High eller Critical under "Needs attention".
  - Det kræver `LinkWork` at uploade og acceptere. Auditor kan læse. Hver import logges med resultatet.
- **Begrundelse:** CRA bilag I kræver, at produktet leveres uden kendte udnyttelige sårbarheder og testes regelmæssigt. ISO 27001 A.8.8 kræver styring af tekniske sårbarheder. SARIF er det fælles format, som de fleste scannere skriver.
- **Fravalgt:**
  - At køre scannere fra trackeren.
  - SBOM-import (US-079).
  - At koble et fund til en user story eller en risiko. Det kommer med US-119's Suggestions-fane.

### B60 – Sporbarhedsmatrixen som Excel eller CSV, til team, auditor eller ledelse

- **Status:** Vedtaget
- **Beslutning:** (US-120)
  - Compliance-fanens download har tre valg: hvem filen er til (team, auditor eller ledelse), formatet (Excel eller CSV) og regulativerne (det åbne eller alle). Excel og team er standard.
  - Excel-filen laves med OpenXml, som projektet allerede bruger til Word, så der kom ingen ny pakke. Den har et ark "Summary" med projekt, fase, regulativer og versioner, læser, hvem der hentede den og hvornår, og dækningen pr. regulativ.
  - Der er ét ark pr. regulativ med fed og frosset overskrift, filter, kolonnebredder efter indholdet og ombrudt tekst. Hver story, evidens eller AI-forslag får sin egen række. Status er farvet: grøn for dækket, rød for ikke dækket, grå for ikke relevant og gul for "mangler evidens" og "skal gennemses".
  - **Team** får det, der skal gøres: de relevante krav med de ikke-dækkede først, og stories, evidens og ventende AI-forslag.
  - **Auditor** får hele sporet: alle krav inkl. "ikke relevant" med grund, og for hver kobling og evidens kilde, hvem der besluttede, rolle, dato, SHA-256 og gyldighed. Ingen ventende forslag.
  - **Ledelse** får kun Summary, udvidet med ikke-dækkede, dækkede uden evidens, åbne høje risici, høje og kritiske scanningsfund, åbne sikkerhedshændelser og supportslutdato.
  - Hver celle er tekst (inline string), så intet kan køre som formel. Hver download logges med format, læser og regulativer.
  - CSV beholder US-052's kolonner, så værktøjer, der læser den, virker uændret. Ledelsesudgaven findes kun som Excel.
- **Begrundelse:** En rå CSV er svær at læse i Excel. Team, auditor og ledelse har brug for forskelligt indhold (ISO 27001 7.5.3, 9.1–9.3; CRA bilag VII).
- **Fravalgt:**
  - At CSV får én række pr. kobling og læserens kolonner (US-120 R7). Det ville ændre et format, andre værktøjer kan læse.
  - PDF (US-051, US-082) og låsning af rapporten med fingeraftryk (US-050).

### B61 – Sikkerhedsalarmer hentes fra GitHub

- **Status:** Vedtaget
- **Beslutning:** (US-121)
  - Et projekt, der er koblet til et GitHub-repository, kan hente repositoryets code scanning-alarmer (CodeQL og andre værktøjer, der uploader SARIF til GitHub) og Dependabot-alarmer. Det sker med knappen "Fetch from GitHub" på Scans-fanen og med den planlagte synkronisering. Der bruges samme token som til user stories, og trackeren kun læser (B12).
  - Alarmerne laves om til en SARIF-fil og importeres som en upload (US-078). Hvert værktøj bliver sin egen scanning med kilden "GitHub code scanning" eller "GitHub Dependabot". Fingeraftrykket er alarmens nummer i repositoryet, så den samme alarm følges mellem hentninger.
  - En alarm, der er løst i GitHub, forsvinder fra listen og bliver "Fixed". En alarm, der er afvist i GitHub, bliver "Accepted" med GitHubs grund ("Dismissed in GitHub: false positive"). Den markeres ikke som accepteret af en person.
  - Kan en kilde ikke læses (Dependabot slået fra, code scanning ikke sat op, ingen adgang), siger fanen det i ord, og den anden kilde hentes stadig.
  - En scanning hentet af den planlagte synkronisering har ingen bruger.
- **Begrundelse:** Fundene findes allerede i GitHub for de fleste projekter. At hente dem kræver ingen ny rettighed og giver Scans-fanen data uden manuelle uploads (CRA bilag I, ISO 27001 A.8.8).
- **Fravalgt:**
  - At starte en scanning eller et workflow i GitHub (kræver skriveadgang).
  - Secret scanning-alarmer (kan komme på samme måde).
  - Repositories uden for GitHub (US-122).

### B62 – Den røde tråd fra risiko til krav og evidens

- **Status:** Vedtaget
- **Beslutning:** (US-124)
  - Risikosiden har en sektion "Trace", der viser tråden som et træ: risikoens measures (krav og stories) og handlinger → de user stories, der udfører dem → de krav, stories dækker → evidensen på kravene. Evidens koblet direkte til en handling vises under handlingen.
  - Compliance-fanen viser på hver krav-række de åbne risici, der når kravet. En risiko når et krav direkte som measure, eller gennem en story, der er measure eller koblet til en af risikoens handlinger. Højeste risiko står først, og hver risiko nævnes én gang.
  - Lukkede risici vises ikke på kravene, men deres egen side har stadig tråden. Fjernede stories og links vises ikke.
  - Excel-rapporten (US-120) har en "Risks"-kolonne for team og auditor. CSV'en er uændret (B60).
  - Tråden bygges af data, der allerede findes (`RiskTraces`). Intet nyt gemmes, og intet kan ændres fra tråden.
- **Begrundelse:** Det skal kunne ses, hvorfor et stykke arbejde findes, og hvad det beviser: risikovurderingen bestemmer kravene, og kravene bevises af arbejde og evidens (CRA art. 13(2)–(3), bilag VII; ISO 27001 6.1.3, 8.3; AI Act art. 9).
- **Fravalgt:**
  - Et grafisk diagram. Træet er nok til at starte med.
  - Sikkerhedshændelser og scanningsfund i tråden. De kan komme, når US-119 kobler dem til risici.

### B63 – AI-beslutninger deles som standard, og kun systemejeren træner

- **Status:** Vedtaget
- **Beslutning:** (US-130)
  - Nye organisationer deler som standard deres beslutninger om AI-forslag: de tekster, AI'en sammenlignede, og om forslaget blev godkendt. Navne og e-mails deles aldrig. Det fortælles på siden, hvor organisationen oprettes, og på Settings-siden.
  - Organisationens admin kan slå delingen fra og til. Den bruger, der må redigere projekter, kan udelade et fortroligt projekt fra AI-forbedring på projektets Edit-side. Begge dele logges.
  - Kun beslutninger, der er truffet efter delingen sidst blev slået til, og som ikke kommer fra et udeladt projekt, bruges til måling (US-116) og træning (US-123). Reglen står ét sted (`AiSharing.MayUse`).
  - Organisationernes egen "Export AI feedback" (US-036) er fjernet. Settings-siden viser i stedet, hvor mange beslutninger der deles. Kun systemejeren vælger modeller (B47, B56) og trækker træningsdata ud. AI'en trænes aldrig i realtid.
  - Ved migreringen bliver eksisterende organisationer slået til fra nu af, undtagen dem, hvis admin bevidst har slået delingen fra. Deres tidligere beslutninger bruges ikke. Migreringen skriver ikke i aktivitetsloggen, så loggens fingeraftryk ikke brydes.
- **Begrundelse:** Systemejeren træner modellerne til formålet og skal have data nok. Mellem virksomheder kan det bygge på legitim interesse med ret til at sige fra (GDPR art. 6(1)(f), art. 21), når det fortælles tydeligt (art. 13). Fortrolige projekter kan holdes ude (art. 25).
- **Fravalgt:**
  - At organisationer bruger deres egne modeller eller eksporterer til dem. En svagere model ville få produktet til at se dårligere ud, end det er.
  - At bruge beslutninger fra en tidligere periode med deling, efter delingen har været slået fra. Det er enklere og mere forsigtigt at starte forfra.
- **Åbent:** vilkårene og databehandleraftalen skal sige det samme, før det går i drift.

### B64 – Sikkerhedshændelser, der ikke skal indberettes (endnu)

- **Status:** Vedtaget
- **Beslutning:** (US-127, udvider B49)
  - En sikkerhedshændelse kan også være "Vulnerability, not exploited" eller "Incident, not severe". De har ingen indberetninger og ingen frister. Fanen siger det, og status er `NotRequired`.
  - En sårbarhed, der ikke udnyttes, følges til rettelsen (CRA bilag I, del II): rettelsen markeres som tilgængelig og sårbarheden som offentliggjort, tidligst ved rettelsen og ikke i fremtiden. Den lukkes, når begge dele er sket, eller med en grund (fx at komponenten er fjernet). En hændelse, der ikke er alvorlig, kan lukkes med det samme.
  - Bliver den udnyttet aktivt eller alvorlig, opgraderes den med tidspunktet og en grund. Tidspunktet skal ligge mellem, da man blev opmærksom, og nu. Fristerne i CRA art. 14 regnes fra det tidspunkt, og det første opmærksomhedstidspunkt gemmes. En hændelse kan kun opgraderes én gang og aldrig nedgraderes.
  - En opgraderet hændelse kræver alle indberetninger, før den kan lukkes. En grund erstatter dem ikke.
  - Opgradering og offentliggørelse logges i gruppen Security events.
- **Begrundelse:** Kun aktivt udnyttede sårbarheder og alvorlige hændelser skal indberettes (CRA art. 14(1), (3)). De andre skal stadig håndteres og rettes (bilag I, del II), og de kan skifte status senere. Fristen løber fra, man ved, at den er indberetningspligtig.
- **Fravalgt:**
  - At nedgradere en hændelse, der først er indberetningspligtig. Indberetningerne er sendt eller skulle have været.
  - Forslag til hændelser ud fra fund og lister over udnyttede sårbarheder. Det kommer med US-128.

### B65 – Alle rapporter på én fane

- **Status:** Vedtaget
- **Beslutning:** (US-129)
  - Projektet har en fane "Reports" efter "Activity". Den viser hver rapport som et kort med navn, hvem den er til, hvad den indeholder og formatet.
  - Sporbarhedsrapporten har de samme valg som på Compliance-fanen (læser, format og regulativ, B60), men her er "All regulations" standard. Udkastet til teknisk dokumentation (US-056) hentes som Word.
  - En rapport, der ikke kan laves, står der stadig og siger hvorfor, fx "Add a framework to the project to make a traceability report". Reglen ligger i `ProjectReports` i Application, så nye rapporter kommer til samme sted.
  - Overview's kort "Reports" viser, hvilke rapporter der kan laves, og linker til fanen. Den forkerte tekst "Traceability matrix (CSV)" er væk.
  - Compliance-fanen beholder sin download for det åbne regulativ. Alle roller kan hente, og hver download logges som før.
- **Begrundelse:** Rapporterne lå forskellige steder. Den, der skal aflevere en rapport til en auditor eller ledelsen, skal kunne finde den ét sted (ISO 27001 7.5.3, 9.1, 9.3; CRA bilag VII).
- **Fravalgt:**
  - At fanen husker de sidste valg pr. bruger.
  - Siden for eksterne auditorer. Den hører til US-076.

### B66 – Forslag til sikkerhedshændelser fra fund og lister over udnyttede sårbarheder

- **Status:** Vedtaget
- **Beslutning:** (US-128)
  - Trackeren foreslår sikkerhedshændelser. Den opretter dem aldrig selv. En person registrerer eller afviser hvert forslag.
  - **Udnyttede sårbarheder:**
    - Åbne fund matches på CVE-nummer mod de offentlige lister over udnyttede sårbarheder: CISA's Known Exploited Vulnerabilities (KEV) og EU's sårbarhedsdatabase (EUVD, ENISA).
    - CVE-nummeret findes i fundets regel-id eller besked, uanset store og små bogstaver.
    - Et match foreslår "Actively exploited vulnerability" én gang pr. CVE og projekt, også når flere fund har samme CVE.
  - **Kritiske fund:** et nyt kritisk fund uden match foreslår "Vulnerability, not exploited" (B64). Det gælder også et fund uden CVE, fx en CodeQL-regel.
  - **Når der allerede er en hændelse:**
    - En CVE, der allerede har en hændelse, får ikke et nyt forslag.
    - Er hændelsen en åben "not exploited", foreslås en opgradering i stedet.
    - Et ventende "not exploited"-forslag trækkes tilbage, når CVE'en bliver udnyttet.
  - **Registrering:** opmærksomhedstidspunktet er, da trackeren lærte det, og det kan ændres. Fristen for den tidlige advarsel løber altså fra det tidspunkt.
  - **Afvisning:** et afvist forslag beholder sin grund. Det kommer kun igen, hvis CVE'en dukker op i et nyt fund.
  - **Hvornår der matches:** efter hver scanningsimport, og når listerne hentes.
  - **Listerne:**
    - Listerne hentes kun, når operatøren slår dem til (`Security:ExploitedLists:Enabled`, fx `["KEV", "EUVD"]`). De hentes ved start og derefter hver 24. time.
    - Den sidst hentede udgave gemmes. Hvis hentningen fejler, viser fanen, hvornår listen sidst blev hentet, og intet andet ændres.
    - En tracker uden internet virker som før.
  - Forslagene vises øverst på fanen Security events og under "Needs attention", hvor en udnyttet CVE står først. Det kræver `ManageRisks` at beslutte, som i US-073. Forslag, registrering og afvisning logges.
- **Begrundelse:** CRA art. 14(2)(a) giver 24 timer til den tidlige advarsel fra, man ved, at en sårbarhed udnyttes aktivt. Listerne er det offentlige bevis for det, og produktets fund viser, at sårbarheden er i produktet (ISO 27001 A.5.7, A.8.8). Trackeren gætter ikke på alvoren alene. Mennesket beslutter (AI Act art. 14 som princip).
- **Fravalgt:**
  - GitHub's private security advisories som kilde. De kommer, når US-121 (GitHub-alarmer) er flettet.
  - EPSS-scorer (sandsynligheden for udnyttelse).
  - Hændelser fra drift og logs (US-098).
  - At trackeren selv opretter hændelser.

### B67 – Byggepipelinen uploader scanninger med et token for projektet

- **Status:** Vedtaget
- **Beslutning:** (US-122, udvider B59 og B61)
  - **API'et:**
    - Trackeren får sit første indgående API: `POST /api/projects/{id}/scans` med SARIF-filen og valgfri `commit` og `version`.
    - Det kræver `Authorization: Bearer <token>` og bruger ingen cookies og intet antiforgery-token. Uden for udvikling kræves https.
  - **Tokenet:**
    - Et token hører til ét projekt og kan kun uploade dertil. Det kan ikke læse noget.
    - Det er 32 tilfældige bytes med præfikset `ctu_`. Det vises én gang, og kun dets SHA-256 gemmes (unikt indeks).
    - Det kan spærres med det samme. Et forkert, spærret eller andet projekts token giver samme svar, 401, så man ikke kan gætte, hvilke tokens der findes.
    - Admin og Compliance manager (`EditProjects`) opretter og spærrer tokens på fanen Scans.
  - **Uploadet** følger US-078: SARIF 2.1.0, højst 50 MB og match på fingeraftryk.
    - Scanningen får kilden "CI upload" og gemmer commit (en Git-SHA på 7–40 hextegn), version og tokenet.
    - Filen læses først, når tokenet og størrelsen er godkendt.
  - **Grænse:** højst 30 uploads i timen pr. token, ellers 429 med `Retry-After`. Grænsen tælles ud fra tokenets scanninger i databasen, så den gælder også efter en genstart og på flere servere.
  - **Log:** oprettelse og spærring logges med tokenets navn, aldrig selve tokenet. Hver upload logges af systemet som en scanningsimport med tokenets navn, commit og version.
  - Scanninger uden en bruger (fra en pipeline eller den planlagte GitHub-hentning) logges nu som systemet. Før krævede loggen en logget ind bruger.
- **Begrundelse:** CRA bilag I, del II (3) og ISO 27001 A.8.29 kræver regelmæssige sikkerhedstest. Når hver build sender sin scanning med commit og version, ligger testene ét sted og kan knyttes til det, der blev bygget. Tokenet følger ASVS V2 og V13: kun det nødvendige, gemt som hash og kan spærres.
- **Fravalgt:**
  - At læse gennem API'et.
  - Andre formater, fx SBOM (US-079).
  - En generel ratebegrænser (US-097).
  - At koble versionen til en release (et åbent spørgsmål til US-072).

### B68 – Systemejeren eksporterer træningsdata

- **Status:** Vedtaget
- **Beslutning:** (US-123, udvider B63)
  - **Ingen læring i realtid:** modellerne trænes aldrig i realtid. Intet i trackeren ændrer en model, en prompt eller en score ud fra beslutningerne af sig selv.
  - **Eksporten er den eneste vej ud:** systemejeren eksporterer beslutningerne på System → AI quality og træner eller tilpasser en model uden for trackeren. En ny model måles (US-116), før den tages i brug (US-117).
  - Kun systemejeren kan eksportere. Organisationerne har ingen eksport (B63).
  - **Hvad der tages med:** kun organisationer, der deler, og kun beslutninger fra efter, at deling sidst blev slået til, uden projekter, der er holdt udenfor. Reglen er `AiSharing.MayUse`, den samme som til målingen.
  - **Filen** er JSON Lines med én linje pr. beslutning. Den skrives, mens den laves, så en stor eksport ikke fylder hukommelsen.
  - **Typerne:**
    - Story ↔ krav (US-020)
    - Evidens ↔ krav (US-059)
    - "Ikke relevant" (US-060)
  - **En linje** har typen, de to tekster AI'en sammenlignede, beslutningen, grund og kommentar, score og grænse, modellerne, begrundelsen, promptversionen og datoerne.
    - Den har ingen navne, e-mails eller bruger-id'er.
    - Organisationer og projekter er numre (`org-1`, `org-1/project-2`), de samme inden for én eksport.
    - Medlemmernes navne og alle e-mailadresser i teksterne erstattes med `[person]` og `[e-mail]`. Hele ord, så "Bo" ikke rører "Board".
  - **Udvalg:** eksporten kan afgrænses til en periode (hele dage, begge med) og til typer.
  - **Log:** hver eksport logges af systemet i hver medtaget organisations log med antal og periode, så organisationen kan se det.
- **Begrundelse:** AI Act art. 10 kræver styring af træningsdata: hvor de kommer fra, og hvilken model og prompt der lavede forslaget. Art. 12 og 15 kræver, at en ny model trænes bevidst og måles. GDPR art. 5(1)(c) og 25 og ISO 27001 A.5.34 og A.8.11 kræver dataminimering og maskering.
- **Fravalgt:**
  - Træning eller automatisk læring i trackeren.
  - At eksportere evidensfiler. Evidens er kun med som titel.
  - Karaktererne fra US-116's vurdering af begrundelser.
  - At maskere andre navne end medlemmernes. Fri tekst kan stadig nævne fx en kunde.

### B69 – Bekræftelse af vigtige handlinger og fire-øjne

- **Status:** Vedtaget
- **Beslutning:** (US-135)
  - **Bekræftelse:** vigtige handlinger åbner en dialog, der siger, hvad der sker, og om det kan fortrydes. Det gælder fx at fjerne evidens, en ramme eller et medlem, at tilbagekalde et token eller en invitation, at lukke en risiko eller hændelse, at acceptere et fund, at ændre en rolle og at ændre AI-deling. Dialogen er en `data-confirm` på formularen. Uden JavaScript sendes formularen som før, og serveren tjekker alt.
  - **Fire-øjne** for fire handlinger:
    - at acceptere en risiko, der er High eller Critical (det resterende niveau, når det er givet, ellers niveauet)
    - at registrere en sikkerhedshændelse som ikke indberetningspligtig (US-127), også fra et forslag (US-128)
    - at sætte et projekt til end of support
    - at signere og låse rapporter, som kommer med US-050 og US-132
  - Handlingen gemmes som en `ApprovalRequest` med det, den skal bruge, og sker først, når en anden person godkender. Godkenderen skal have den rettighed, handlingen selv kræver. Ingen kan godkende sin egen anmodning.
  - Handlingen udføres af den samme kode, som når den ikke venter. Den logges som udført af den, der bad om den, med "(approved by …)". Godkendelsen logges for sig.
  - Anmodningen tjekkes, før den laves, så der aldrig bedes om noget, der ville fejle. Den annulleres, hvis emnet er ændret inden godkendelsen (risikoens eller projektets version), og udløber efter 7 dage.
  - Den, der bad, kan trække anmodningen tilbage. En afvisning kræver en grund.
  - Anmodningerne vises på projektets Overview under "Waiting for approval" og under "Needs attention" for dem, der må godkende.
  - **Kun ét medlem med rettigheden:** anmodningen laves stadig, og siden siger, at ingen andre må godkende. Organisationen må invitere en.
- **Begrundelse:** ISO 27001 A.5.3 kræver adskillelse af opgaver, og A.8.32 kræver styrede ændringer. CRA art. 14 gør det alvorligt at lade være med at indberette. Det må aldrig være én persons beslutning. OWASP ASVS V2.3 og V11.1 kræver bekræftelse af følsomme handlinger.
- **Fravalgt:**
  - At organisationen selv vælger, hvilke handlinger der kræver fire-øjne. Det kan komme senere.
  - En ny "godkender"-rolle. Rettighederne findes allerede.
  - At lade en eneste administrator godkende sig selv.

### B70 – Tjek af udtrækningen fra en PDF

- **Status:** Vedtaget
- **Beslutning:** (US-070)
  - Udtrækningen læser nu PDF'en side for side, og en ren parser (`RequirementParser`, uden AI) finder både kravene og det, der ikke blev brugt. Resultatet gemmes som JSON på kladden (`ExtractionCheck`).
  - **Kladdens side viser** antal krav, sider uden krav, hvor stor en del af teksten der er dækket, og en advarsel over 20 % sider uden krav.
  - **Den viser også:**
    - tekst, der ikke blev til et krav, pr. side og afkortet til 300 tegn
    - huller i nummereringen ("5.15 mangler måske")
    - koder, der findes to gange
    - punkter i indholdsfortegnelsen, der ikke blev fundet
    - "shall/must"-sætninger uden for et krav
  - **En side uden krav** er en side, hvor intet krav starter. Dens tekst vises, også når den er lagt til kravet før, for det kan være et krav uden nummer.
  - **Ikke vist:** sidetal, indholdsfortegnelse, og sidehoveder og -fødder (en linje, der står på mindst halvdelen af siderne, dog mindst tre).
  - **EU-retsakter:** "Article 14" med stykker "1." og litra "(a)", og "ANNEX I" og "Part II" med "(1)" og "(a)" giver koder som "Art. 14(2)(a)" og "Annex I Part II (1)". Det fineste niveau med en selvstændig forpligtelse bliver kravet, og overskrifter over det gør ikke. Betragtningerne før første artikel er ikke krav.
  - **Krav tilføjet i hånden** fjerner den tekst, de dækker, fra listen. Kun krav, der ikke blev fundet ved udtrækningen, tæller.
  - Versionen kan stadig aktiveres med en advarsel. Tallene skrives i loggens `CatalogActivated`.
- **Begrundelse:** ISO 27001 A.5.31 kræver, at alle krav er identificeret, og A.8.32 kræver styrede ændringer. En udtrækning, der stille springer krav over, ville give en falsk dækning. US-137 henter EU-retsakter fra EUR-Lex og bruger dette tjek for PDF'er.
- **Fravalgt:**
  - OCR af scannede PDF'er.
  - AI, der foreslår krav ud fra den tekst, der er tilbage.
  - At blokere aktivering ved en advarsel.

### B71 – Organisationens dokumenter som fælles evidens

- **Status:** Vedtaget
- **Beslutning:** (US-136)
  - Politikker, procedurer og risikotabeller lægges én gang på organisationens side "Documents". De har en type, ejer, version og næste review-dato.
  - **Links:** et dokument kobles til krav på ramme og kode, ikke på et bestemt krav-id. Linket holder derfor, når rammen får en ny version med samme kode.
  - **Evidens i projekterne:** hvert projekt, der følger kravet, får dokumentet som almindelig evidens, markeret "From the organisation".
    - Det er en kopi af posten, men filen gemmes kun én gang, hos organisationen.
    - Så virker compliance, rapporter, isolation og fanerne uden ændringer, fordi en politik er evidens.
    - Projekterne opdateres, når dokumentet ændres, og når et projekt begynder at følge en rammeversion.
  - **Ændringer:** evidens fra organisationen kan ikke fjernes i et projekt. Den ændres på Documents-siden.
  - **Ny version:** den erstatter den gamle i alle links, og den gamle fil gemmes.
  - **Review:** et dokument, hvis review-dato er passeret, vises under "Needs attention" på Documents-siden og på projekternes Overview.
  - **Fjernelse:** at fjerne et dokument kræver bekræftelse, og dialogen nævner de krav, der mister evidensen. Projekterne mister den samtidig.
  - Ændringer kræver `ManageFrameworks`, og alt logges.
- **Begrundelse:** ISO 27001 5.2 og A.5.1 kræver politikker, og 7.5.2–7.5.3 kræver styring af dokumenter. En politik gælder for hele organisationen, så den skal ikke lægges op i hvert projekt.
- **Fravalgt (endnu):**
  - AI-forslag til links (R2). Når de kommer, er de forslag, som en person godkender, som for evidens (US-059).
  - At importere en risikotabel i risikoregistret (US-113).
  - At et projekt fravælger et organisationsdokument.

### B72 – Alle rapporter som PDF

- **Status:** Vedtaget
- **Beslutning:** (US-131)
  - Sporbarhedsrapporten (US-120) og udkastet til den tekniske dokumentation (US-056) kan hentes som PDF på Reports-fanen. PDF'en laves fra den samme rapportmodel som Excel- og Word-filen, så tallene er de samme i alle formater.
  - **Bibliotek:** PdfPig (Apache-2.0), som trackeren allerede bruger til at læse rammernes PDF'er. Det er gratis til kommerciel brug, og der skal ikke hentes noget nyt. PDFsharp/MigraDoc og QuestPDF er fravalgt.
  - **Hver side** viser organisation, projekt, rapport, hvornår og af hvem den er lavet, og "Page x of y".
  - **Tabeller:**
    - Kolonnebredden følger indholdet. Tekst ombrydes, så intet skæres af.
    - En tabel, der fortsætter på næste side, gentager sin overskrift.
    - Mange kolonner udskrives på tværs (landskab).
  - **Tegn:** PDF'en bruger standardfonten Helvetica (Windows-1252), så æ, ø og å virker. Pile bliver til "->", og andre tegn, fonten ikke kan vise, bliver til "?", så en rapport aldrig fejler.
  - **Log:** hver download logges som `ReportDownloaded` med formatet, nu også for den tekniske dokumentation.
- **Begrundelse:** ISO 27001 7.5.2 og 9.3 kræver dokumenteret information i et passende format til ledelse og auditor. PDF er også grundlaget for låsning (US-050) og signatur (US-132).
- **Fravalgt:**
  - PDF/A. Det kan komme med låsningen.
  - Indlejrede fonte, som ville give flere tegn men større filer.

### B73 – Låste rapporter med fingeraftryk

- **Status:** Vedtaget
- **Beslutning:** (US-050)
  - En rapport låses som PDF (US-131) på Reports-fanen. Låsningen gemmer filen, typen, titlen, tidspunktet, hvem der låste den og med hvilken rolle, og filens SHA-256-fingeraftryk (`ReportSnapshot`).
  - **Samme kode:** den låste PDF laves af den samme kode som en download (`ReportFiles`), så det er præcis den rapport, et medlem ville hente.
  - **Aldrig ændret:** en låst rapport kan aldrig ændres eller slettes. Databasen har triggere, der afviser UPDATE, DELETE og TRUNCATE, som for aktivitetsloggen (B40, US-091). En ny version er en ny låsning.
  - **Lagring:** filen gemmes i databasen (`ReportSnapshotFiles`). Det er det enkleste til Docker-installationen og svarer på issuets åbne spørgsmål.
  - **Adgang:** alle i organisationen, også auditoren, kan hente en låst rapport og se fingeraftrykket. Kun roller med `DecideCompliance` kan låse.
  - **Tjek af fingeraftryk:** man uploader en fil, og trackeren siger, om den matcher en låst rapport i organisationen og hvornår den blev låst.
  - Låsningen logges med fingeraftrykket (`ReportLocked`) og bekræftes i en dialog (US-135).
- **Begrundelse:** ISO 27001 7.5.3 og A.5.33 kræver beskyttede optegnelser. CRA bilag VII kræver, at dokumentationen for hver version gemmes. En auditor skal kunne stole på, at rapporten er den, der blev lavet.
- **Fravalgt (endnu):**
  - At knytte låsningen til en release. Det kommer, når releases findes.
  - Signatur, som er US-132.
  - Låsning af Excel og Word. Kun PDF, så filen ikke ændrer sig ved åbning.

### B74 – Signering i trackeren og virksomhedens segl

- **Status:** Vedtaget
- **Beslutning:** (US-132)
  - **Hvem signerer:** en låst rapport (US-050) signeres af en compliance-ansvarlig og en leder. Det skal være to forskellige personer, og hver rolle signerer én gang. Det giver i sig selv fire-øjne (US-135).
    - Compliance-ansvarlig er et medlem med `DecideCompliance` (Admin eller Compliance manager).
    - Leder er et medlem, som administratoren har markeret "signs for the management" på Users-siden. Det er en rettighed på personen, ikke en ny rolle.
  - **Frisk login:** man skal have logget ind inden for de sidste 10 minutter. Ellers sendes man til Keycloak med `prompt=login` og tilbage igen. Login-tidspunktet stemples på hvert Keycloak-login.
  - **Kun den låste fil:** hver signatur gælder netop den låste fil (dens SHA-256). Rapporten ændres aldrig, og en ny version er en ny låsning med nye signaturer.
  - **Forsegling:** når begge har signeret, laves en forseglet PDF:
    - den låste rapports sider uændret
    - en side med underskriverne, deres roller og tidspunkter og den låste rapports fingeraftryk
    - en digital signatur over hele filen med organisationens certifikat. Signaturen er en PDF-signatur (ISO 32000, `adbe.pkcs7.detached`, SHA-256) og tilføjes som en inkrementel opdatering.
  - **Certifikatet:** organisationens administrator uploader det som .pfx på Settings. Filen og adgangskoden gemmes krypteret og vises aldrig igen. Et udløbet eller endnu ikke gyldigt certifikat kan ikke forsegle. Er der intet gyldigt segl, er rapporten signeret men ikke forseglet, og den kan forsegles senere.
  - **Værktøjer:** signaturen laves med .NET's egen `System.Security.Cryptography.Pkcs` og PdfPig. Der bruges ingen betalt tjeneste eller ekstern komponent.
  - Alt logges.
- **Begrundelse:** ISO 27001 5.1 og 9.3 kræver, at ledelsen godkender, og 7.5.3 og A.5.33 kræver beskyttede optegnelser. CRA art. 28 kræver, at producenten underskriver. Efter eIDAS er dette en avanceret elektronisk signatur og et segl. Den kvalificerede signatur er US-133.
- **Fravalgt:**
  - At organisationen vælger roller pr. rapporttype. Begge roller kræves altid.
  - Online tjek af tilbagekaldelse (CRL/OCSP), som kræver netværk.
  - PAdES-baseline med tidsstempel fra en ekstern tjeneste.
  - At kræve et certifikat fra en betroet udsteder. Organisationens eget certifikat accepteres.

### B75 – NIS2 på organisationsniveau (US-142)

- **Beslutning:**
  - **Organisationsområdet** er et projekt af typen `Organisation`, ét pr. organisation. Det oprettes første gang nogen åbner "Organisation" i topbaren. Det genbruger dækning, evidens, risici, rapporter, låsning og signering fra projekterne. Det har ingen livscyklus, ingen supportperiode og ingen user stories, og det vises ikke blandt projekterne.
  - **Niveau på regelsæt:** et regelsæt er enten for et produkt (CRA, AI Act) eller for organisationen (NIS2). Et regelsæt på organisationsniveau kan kun bruges i organisationsområdet, og omvendt. Uploadede regelsæt er for produkter.
  - **Dækning i organisationsområdet:** der er ingen user stories, så evidens, der tæller (ikke udløbet og ikke til gennemgang), dækker et krav. En politik fra Documents (US-136) dækker altså et NIS2-krav.
  - **Nationalt lag:** et regelsæt kan gennemføre et andet i et land, f.eks. den danske NIS2-lov (lov nr. 434 af 6. maj 2025, "DK"), der gennemfører NIS2. Hvert krav siger, hvilket krav i direktivet det gennemfører. Når begge følges, dækker de hinanden, hvor de passer sammen, og det vises som "through NIS2 Art. 21(2)(b)". Kun egen dækning gives videre, så det aldrig går i ring. Gennemførelsesforordning (EU) 2024/2690 er lagt ind på samme måde, som detaljer til NIS2 art. 21.
  - **Sprog og land er to ting:** en oversættelse er tekst på et andet sprog (`RequirementText`, f.eks. "da"), mens en anden lands lov er et nyt nationalt lag. Engelsk er altid reserven. Siden viser teksten på browserens sprog, når den findes, ellers engelsk med "Not translated yet". Et nyt sprog kræver kun tekster, ingen kode.
  - **NIS2-tjekket** følger art. 2 og 3 med bilag I og II. Størrelsen følger Kommissionens henstilling 2003/361/EF: mellemstor fra 50 ansatte, stor fra 250, eller når både omsætning og balance er over grænsen (10/10 og 50/43 mio. euro). Særlige tjenester (DNS, topdomæner, kvalificerede tillidstjenester, offentlig elektronisk kommunikation, offentlig forvaltning) er omfattet uanset størrelse. Hvert tjek gemmes; det nyeste gælder.
  - Er virksomheden omfattet, slås NIS2 til, og den danske lov, når landet er DK, og 2024/2690 for de digitale udbydere, den gælder for. Intet slås fra automatisk. NIS2 kan slås til og fra med en knap i organisationsområdet (med bekræftelse), også uden at virksomheden er omfattet. Slås NIS2 fra, følges den danske lov og 2024/2690 heller ikke længere; koblinger og evidens gemmes og kommer tilbage, når NIS2 slås til igen.
  - Alle med `DecideCompliance` kan lave tjekket. Landet er en indstilling (standard DK), som administratoren ændrer under Settings.
  - Kravteksterne er resuméer med egne ord, som for AI Act (B58), med link til den bindende tekst på EUR-Lex og retsinformation.dk. EU-retsakter og danske love må genbruges frit.
- **Begrundelse:** NIS2 gælder virksomheden, ikke et produkt. At genbruge projektets struktur giver det samme flow uden en ny model for dækning og rapporter. Et nationalt lag gør det muligt senere at tilføje f.eks. den tyske NIS2-lov uden at ændre koden.
- **Fravalgt:**
  - En særskilt model for organisationens dækning, som ville kopiere projektets kode.
  - At slå regelsæt fra automatisk, når et nyt tjek siger noget andet.
  - At tjekke CVR-registret for landet.
  - Indberetning af NIS2-hændelser, ledelsens godkendelse (art. 20) og forslag fra ISO 27001-evidens: senere stories.

### B76 – AI-forslag til organisationens dokumenter (US-136 R2)

- **Beslutning:**
  - Når en politik, procedure eller risikotabel uploades (eller får en ny version), foreslår den lokale AI, hvilke krav den beviser. Det sker på samme måde som for evidens på et projekt (US-059): samme embedding-model, samme minimumsscore og højst 4 ad gangen, med "spørg om flere".
  - Kandidaterne er kravene i den nyeste aktive version af alle regelsæt, organisationen bruger, både på organisationsniveau (NIS2) og for produkter (CRA). Det er de samme krav, man kan koble til i hånden.
  - **Forslaget hører til dokumentet,** ikke til hver kopi i projekterne. Det afgøres derfor én gang. Godkendes det, kobles dokumentet til kravet (framework og kode), og det bliver evidens alle steder, hvor kravet følges. Afvises det med en grund, foreslås parret aldrig igen.
  - En ny version erstatter de ventende forslag, fordi teksten er ændret. Afgjorte forslag foreslås ikke igen.
  - Uden AI gemmes dokumentet alligevel, og siden siger hvorfor der ingen forslag er.
  - Ventende forslag vises på Documents-siden og under "Needs attention" for dem, der må afgøre dem (`ManageFrameworks`).
- **Begrundelse:** Ét flow for AI-forslag i hele trackeren, og én beslutning pr. dokument i stedet for én pr. projekt.
- **Fravalgt:**
  - At genbruge `EvidenceLinkSuggestion`, som er bundet til ét projekt og ét krav-id.
  - En begrundelse skrevet af AI'en (som på projekterne) og deling af beslutningerne til træning: senere.

### B77 – Ledelsesrapporten viser det vigtigste først (US-141)

- **Beslutning:**
  - Ledelsesrapporten har seks afsnit i fast rækkefølge: status pr. regulering, "What management must know now", nøgletal, de største mangler, åbne høje risici og til sidst detaljerne. PDF og Excel laves af samme kode og samme model, og hvert afsnit er et ark i Excel.
  - **Trafiklys** ud fra den afrundede procent, der vises: rød under 50 %, gul 50–79 %, grøn fra 80 %. En regulering uden krav får ingen farve.
  - **"Siden sidst"** sammenligner med den nyeste låste ledelsesrapport med præcis de samme reguleringer. Den låste rapport gemmer sine tal (dækning pr. regulering som JSON i `ReportSnapshot.Figures`), så den gamle PDF aldrig skal læses.
  - **Højst 5 punkter**, mest alvorlige først: åbne sikkerhedshændelser, åbne alvorlige scanningsfund, høje risici uden ejer, manglende supportperiode (kun med CRA), røde reguleringer, åbne høje risici og gule reguleringer.
  - **Højst 10 mangler pr. regulering:** først dem, der er koblet til en åben Critical- og derefter High-risiko, så krav med en frist, og til sidst resten i katalogets rækkefølge.
  - **"Har en frist"** gælder CRA's indberetningspligter i Art. 14 (24 timer, 72 timer og 14 dage). Det afgøres ud fra koden, så der ikke kræves et nyt felt.
- **Begrundelse:** Ledelsen skal kunne forstå status på et minut. Faste regler gør rapporten forudsigelig og let at teste.
- **Fravalgt:**
  - Grafer over tid; kun ændringen siden sidste låste rapport vises.
  - Et frist-felt på hvert krav. Det kan komme, hvis andre regelsæt får frister.

### B78 – Testdækning i hvert build, med minimumskrav (US-140)

- **Beslutning:**
  - **Værktøjer:** coverlet (MIT) måler dækningen i alle tre testprojekter i samme kørsel. Kun gratis værktøjer, ingen betalte tjenester.
  - **Samlet tal:** et lille Python-script (`build/coverage_gate.py`, kun standardbiblioteket) lægger kørslerne sammen. En linje tæller som dækket, hvis bare én af testene kører den, så kode, som kun UI-scenarierne når, også tæller.
  - **Filnavne:** en fil identificeres ved sin fulde sti. Hver kørsel skriver filnavne i forhold til sin egen `<sources>`-mappe, så uden fuld sti ville den samme fil tælle to gange.
  - **HTML-rapport:** ReportGenerator (Apache-2.0) laver den som lokalt dotnet-værktøj, og den gemmes som artifact i 30 dage. Dækningen pr. projekt står i PR'ens job summary.
  - **Udeladt:** migrationer, genererede filer (Razor-views, `.feature.cs`, `*.g.cs`), `Program.cs` og testprojekterne selv.
  - **Minimumskrav** gælder kun linjedækning og er dagens tal rundet ned til nærmeste 5 %:

    | Projekt | I dag | Minimum |
    |---|---:|---:|
    | Application | 93,2 % | 90 % |
    | Domain | 94,9 % | 90 % |
    | Infrastructure | 93,3 % | 90 % |
    | Web | 94,2 % | 90 % |

    Grenedækningen vises, men har intet minimum endnu.
  - Minimumskravene hæves over tid og sænkes kun med en begrundelse her i loggen.
- **Begrundelse:** Det skal kunne vises over for en auditor, at testene er grundige, og en PR må ikke stille og roligt sænke dækningen (ISO 27001 8.25 og 8.29, CRA bilag I del II (3)).
- **Fravalgt:**
  - Codecov, Coveralls og SonarCloud (betalte planer).
  - Mutationstest (Stryker.NET): en senere story.
  - At en PR fejler, når den sænker dækningen af den kode, den ændrer. Det kræver en sammenligning med main.

### B79 – Standarder i alle former og strukturer (US-143)

- **Beslutning:**
  - Ingen ISO-standard bygges ind i trackeren. Organisationen uploader sin egen licenserede kopi, og filen og kravene tilhører kun den organisation.
  - **Formater:** PDF med tekst, Word (.docx), Excel (.xlsx), CSV og HTML gemt fra ISO's Online Browsing Platform.
    - Både filendelse og indhold tjekkes: PDF-signatur, en zip-pakke med `word/document.xml` eller `xl/workbook.xml`, og tekst uden NUL-bytes.
    - En HTML-fil sendes altid tilbage som tekst, så den aldrig kan køre som en side i trackeren.
    - Gamle .doc- og .xls-filer læses ikke.
  - **Scannet PDF:** en PDF, der kan læses men ikke har bogstaver på nogen side, afvises med en besked om at uploade en PDF med tekst eller Word/Excel. En PDF, der slet ikke kan læses, giver som før en tom kladde.
  - **Struktur:** hovedafsnit ("4 Context …"), underafsnit, punkter med bogstav ("6.1.2 a)"), annekser ("Annex A", "C.1") og kvalitetsmodeller. Hvert krav kender sin forælder (`Requirement.ParentId`).
    - En overskrift uden egen tekst, men med krav under sig, udelades. Det, der lå under den, rykker et niveau op. Det er samme regel som for EU-retsakter (US-137 R2).
  - **Udeladt:**
    - forord, indledning og indholdsfortegnelse
    - afsnit 1–3 (scope, normative referencer, termer)
    - NOTEs og EXAMPLEs
    - informative annekser og bibliografien
    - licens-vandmærker og ©-linjer
  - **Type** (`RequirementKind`):
    - "shall" (også "shall not", og når "should" står samme sted) giver Requirement
    - kun "should" giver Recommendation
    - i en kvalitetsmodel bliver et punkt uden nogen af dem en Characteristic; ellers forbliver det Requirement
    - typen kan ændres, mens versionen er en kladde
    - EU-retsakter læses som før, uden træ og typer
  - **Anbefalinger** tæller aldrig i dækningen. De har deres egen tæller ("3 of 9 recommendations followed"), et gråt "Recommended"-mærke og et filter. "Not covered" viser dem ikke.
  - **Excel og CSV:** admin vælger kolonnerne for kode, titel og tekst (og eventuelt forælder og type) ud fra overskriftsrækken.
    - Valget huskes på frameworket, så næste version læses med det samme.
    - En række uden kode springes over. En kode, der står to gange, beholdes kun første gang og vises i tjekket.
    - CSV kan bruge komma, semikolon eller tabulator, med anførselstegn.
  - Test bruger kun opdigtede filer, der efterligner ISO's layout. Kun nummereringen (koder og antal) svarer til de rigtige standarder.
- **Begrundelse:** Organisationer har deres standarder i mange former, og ISO-tekst må ikke følge med trackeren. Med ét fælles kladde-flow virker udtræks- og fuldstændighedstjekket for alle formater.
- **Fravalgt:**
  - OCR af scannede PDF'er (kræver ekstra software).
  - Rettelsesblade (amendments) oven på en version: en senere story.
  - At tilbyde informative annekser som anbefalinger: et åbent spørgsmål.

### B80 – Komplette krav fra hvert regelsæt (US-137)

- **Beslutning:**
  - **Import fra EUR-Lex:** en EU-retsakt importeres via sit CELEX-nummer (fx `32024R2847`) fra EU's Publikationskontor (CELLAR). Trackeren beder om Formex 4 på engelsk (`application/zip;mtype=fmx4`) og læser strukturen fra XML'en, ikke fra en PDF.
    - Pakken gemmes som versionens fil, og versionen peger på EUR-Lex.
    - Funktionen er slået fra, indtil driften sætter `EurLex:Enabled`, fordi den kræver internet. `EurLex:BaseAddress` kan pege på et spejl.
    - Svaret begrænses i størrelse (zip-bomber), og XML'en læses uden DTD'er og eksterne entiteter.
  - **Opdeling:** et krav deles ned til det fineste niveau med sin egen forpligtelse. Et stykke med litra bliver til sine litra ("Art. 13(2)(a)"), og underlitra deles også ("(a)(i)"). Bilagenes dele beholdes ("Annex I Part I (2)(a)"). Koderne er de samme som PDF-læserens.
  - **Udeladt med begrundelse:** alle andre dele vises med en årsag:
    - betragtninger: "Recital"
    - definitioner: "Definition"
    - myndigheders pligter: "An authority's duty". Det gælder, når stykket begynder med fx "Member States shall" eller "The Commission is empowered".
    - ophævede bestemmelser: "Deleted"
    - ikrafttræden og ændringer af andre retsakter: "Final provision"
  - **Profiler:** de første er ISO/IEC 27001:2022 (30 krav-afsnit og 93 kontroller i Annex A) og ISO/IEC 27002:2022 (de samme 93 kontroller uden "A.").
    - En profil indeholder kun koder og antal, aldrig standardens tekst.
    - Profilen findes ud fra frameworkets navn og bruges, når en uploadet fil har givet krav.
    - Siden viser fx "92 of 93 controls found" og "A.5.23 is missing" samt koder, der ligner kontroller, men ikke findes i standarden.
  - **Fire øjne:** en version importeret fra EUR-Lex aktiveres først, når en anden med `ManageFrameworks` har godkendt det. Det bruger samme godkendelser som US-135.
    - En uploadet standard, der er tjekket mod en profil, viser tjekket (også det, der mangler), men aktiveres af én person. En organisation følger ofte kun en del af en standard, og ellers ville enhver upload med "27001" i navnet kræve to personer.
    - Godkendelsen kan være organisationens egen og knyttes så ikke til et projekt (`ApprovalRequest.ProjectId` kan være tom).
    - Ændres kravene efter anmodningen, bliver den forældet.
    - Aktiveringen logges med tjekket og navnet på den, der godkendte.
- **Begrundelse:** Alle forpligtelser i en retsakt skal med, ikke kun dem, en PDF-læser finder. En kendt liste over koder viser præcis, hvad der mangler, før nogen bygger på versionen.
- **Fravalgt:**
  - At trackeren selv opdager en ny konsolideret version (R6). Den kan importeres som ny version via sit CELEX-nummer (fx `02016R0679-20160504`), men der søges ikke automatisk. Det kræver EUR-Lex' søge-API og bliver en senere story.
  - At erstatte de indbyggede CRA- og AI Act-kataloger (US-068) med importen: et åbent spørgsmål.
  - Profiler for IEC 62443-4-1 og andre standarder: de kan tilføjes, når organisationerne har brug for dem.
  - HTTP-adapteren er kun testet mod en efterlignet CELLAR. Den første rigtige import bør tjekkes af driften.

### B81 – En overskuelig kravrække (US-144)

- **Beslutning:**
  - **Grupper:** "Covered by" har tre grupper med overskrift og antal: user stories, evidens og "Waiting for your decision". En tom gruppe vises ikke.
  - **Kilde på hver linje:**
    - stories: "GitHub" eller "Specification", "linked by" eller "AI suggestion approved by", plus dato
    - evidens: "Uploaded by", "Link added by", "From a test run", "From a merged pull request" eller "From the Documents tab"
    - Evidens fra en test eller en PR genkendes på det accepterede forslag (`EvidenceSuggestion.EvidenceId`), så der kræves intet nyt felt på evidensen.
  - **Ventende forslag** (AI-forslag til stories, foreslået evidens og "not applicable") står samlet i én farvet boks, med Accept og Reject ved siden af hinanden.
  - **Handlinger og fjern:** handlingerne er små knapper nederst. Fjern ligger i en lille "⋯"-menu og spørger først.
  - **Stories:** en lukket story hedder "Done" og er grøn.
  - **Foldet række:** én kort linje med kode, titel, antal, et "waiting"-mærke, status og "Open". Foldning huskes stadig kun i browseren (#52). "Fold all" folder ikke rækker, der venter på en beslutning.
  - **Egen adresse:** hvert krav har sin egen adresse (`…/compliance/{framework}/{kode}`). Den viser rækken udfoldet og markeret og ruller hen til den. Det erstatter det sidepanel, US-046 beskrev.
  - **"All"-fane:** findes, når projektet følger mere end ét regelsæt. Kravene grupperes pr. regelsæt, og tallene lægges sammen.
  - **Not applicable:** kan nævne en af projektets risici som begrundelse (`RequirementApplicability.RiskId`, CRA Art. 13(3)). Risikoen følger med til næste frameworkversion.
  - **Ubrugte regelsæt:** regelsæt, der er slået til i organisationen, men ikke bruges af projektet, vises under tabellen med "Add to project".
  - **Story i flere regelsæt:** når en story dækker krav i flere regelsæt, vises regelsættets navn foran koden, fx "CRA I.2(d)".
- **Begrundelse:** Rækken var en lang, ens liste, hvor man ikke kunne se, hvad der var hvad, eller hvor det kom fra. Grupper, kilder og en kompakt foldning giver overblik uden at skjule noget.
- **Fravalgt:**
  - Et separat sidepanel pr. krav: den udfoldede række med egen adresse giver det samme med mindre kode.
  - Kildeteksterne på dansk: et åbent spørgsmål (US-142 R8).

### B82 – Én tråd fra risiko til bevis (US-146)

- **Beslutning:**
  - **Kravet forbliver midtpunktet.** Der gemmes kun to nye ting:
    - en kobling fra evidens til en user story eller product requirement (`EvidenceStoryLink`)
    - AI'ens forslag til sådanne koblinger (`EvidenceStorySuggestion`)
  - **Ingen direkte kobling til risici, scanninger eller rapporter.** Risici findes via deres tiltag (krav eller stories), via deres handlinger og via handlinger, der nævner evidensen (US-124).
  - **"Trace" på evidens-kortet** står altid i rækkefølgen: risici → krav → stories. Hver linje siger, hvordan den hænger sammen:
    - "via I.2(b)"
    - "linked to the evidence"
    - "AI suggestion approved"
    - "via the action …"
  - En risiko, der nås ad flere veje, vises én gang med alle veje. Den højeste risiko står først.
  - **Begge veje:** en story viser sin evidens (direkte eller "via" kravene), og en risikos side viser evidens, der er koblet direkte til dens stories.
  - **AI-forslag:** AI'en foreslår stories til evidens med samme embedding og minimumsscore som for krav (US-059, US-105). Det sker ved upload og ved "Show more", højst 4 ad gangen. Et afvist par foreslås ikke igen.
  - **Låst ledelsesrapport som evidens:** kun en låst ledelsesrapport (US-050) kan bruges som evidens. Det sker for ét krav, som en person vælger. Filen kopieres som evidens med rapportens SHA-256 i loggen.
  - **Organisationens dokumenter:** deres kopier i projekterne er evidens, så tråden ses på hvert projekts Evidence-fane.
- **Begrundelse:** Det gav ikke et sammenhængende billede, når evidens kun pegede på kravkoder. Når det, der allerede er gemt, bruges til at vise tråden, er der intet ekstra at vedligeholde.
- **Fravalgt:**
  - Direkte koblinger fra evidens til risici, scanninger eller almindelige rapporter.
  - En graf over hele kæden: en senere story.

### B83 – Spørg AI'en om ét krav, én evidens, én story eller én risiko (US-106)

- **Beslutning:**
  - **"Ask the AI" på et krav** kigger kun på det ene krav:
    - Først foreslås projektets stories og evidens, der passer, højst 3 af hver, med samme embedding og minimumsscore som ellers (US-105).
    - En score på 0 tæller aldrig som et match.
    - Et par, der er koblet, godkendt eller afvist, foreslås ikke igen.
    - Spørger man igen, erstattes de ventende forslag, men aldrig de afgjorte.
    - Er der intet, der passer, skriver chatmodellen (opgaven `GapDrafts`, US-117) et udkast til en user story. Udkastet indeholder titel, rolle, story, regler og eksempler, ud fra kravets tekst og projektets formål. Promptens version gemmes med udkastet.
  - **Udkastet** er mærket "AI-generated" og bliver først til en story, når en person har læst det, eventuelt rettet det og trykket "Create story".
    - Trackeren skriver ikke til GitHub (B12). I stedet åbner den GitHubs "new issue"-side udfyldt med udkastet.
    - Når issuet kommer ind ved næste synkronisering (samme titel), kobles det til kravet med kilden "AI-drafted", som den person, der accepterede.
    - Et udkast kan kasseres med en begrundelse og gemmes som historik.
  - **Evidens og stories** havde allerede "Suggest requirements" (US-059, US-020). Med US-146 foreslår evidens også stories.
  - **"Suggest measures" på en risiko** foreslår krav og stories, der kan mindske den. Først når en person godkender, bliver forslaget et tiltag (US-124).
  - Alle forslag lander i "Waiting for your decision"-boksen (US-144) og viser, at de kommer fra AI'en.
  - Auditoren ser forslag og udkast, men kan ikke spørge eller beslutte.
- **Begrundelse:** Det er hurtigt og billigt at spørge om én ting ad gangen, og et udkast giver en konkret vej videre for et krav, som intet dækker.
- **Fravalgt:**
  - At trackeren selv opretter issues i GitHub. Det kræver skriveadgang og en undtagelse fra B12 som B42.
  - At et udkast bliver et krav i trackerens egen liste for projekter uden GitHub. Teksten kan kopieres; det kan blive en senere story.

### B84 – Suggestions-fanen med mangler pr. kategori (US-119)

- **Beslutning:**
  - **Fire kategorier:** Requirements, Security, Risk og Scanning. Manglerne findes med enkle tjek uden AI. De beregnes, når fanen åbnes, og gemmes ikke. Kun afvisningerne gemmes (`GapDismissal`).
  - **Requirements:** et krav, der gælder, men ikke er dækket, eller er dækket uden evidens. Anbefalinger tæller ikke.
  - **Risk:** en risiko, der skal mindskes, men ikke har handlinger; handlinger over fristen; alle handlinger udført, men intet restniveau sat.
  - **Scanning:** åbne fund på High eller Critical. SBOM (US-079) findes ikke endnu, og fanen siger det.
  - **Security:** en åben hændelse, hvor en rapport ikke er sendt, og fristen er overskredet (CRA Art. 14).
  - **Handlinger på en mangel:**
    - "Ask the AI" for et krav (US-106; det er det udkast til arbejde, som US-108 beskrev)
    - "Add a risk" med titel og beskrivelse udfyldt
    - "Open the risk"
    - "Open" kilden
    - "Dismiss" med en begrundelse
  - Intet oprettes uden en person.
  - **Afvisning med "stempel":** en afvist mangel bærer et stempel af kildens tilstand, fx antallet af handlinger eller den seneste scanning. Den vises igen, når kilden ændrer sig.
  - **AI-forslag andre steder:** AI'ens forslag til stories og evidens bliver, hvor de er. Fanen tæller dem og linker til dem.
  - Auditoren ser manglerne, men kan ikke handle på dem.
- **Begrundelse:** Ét sted at se, hvad projektet mangler. Tjekkene kræver ingen AI og er derfor hurtige og forudsigelige.
- **Fravalgt:**
  - At AI'en grupperer og formulerer manglerne: en senere udvidelse.
  - Mangler i SBOM (US-079) og manglende sårbarhedskontakt (US-103): de kommer, når de stories er bygget.

### B85 – En afvisning gælder, indtil det vurderede ændrer sig (US-145)

- **Beslutning:**
  - **Fingeraftryk:** når en person afviser et AI-forslag, gemmes et kort fingeraftryk af begge sider ("emne|krav"). Det er en SHA-256 af teksten, hvor store/små bogstaver og mellemrum ikke tæller.
    - Krav: kode, titel og beskrivelse.
    - Story eller produktkrav: titel og tekst.
    - Evidens: filens SHA-256, ellers titel og link.
    - Organisationsdokument: den aktuelle fils SHA-256.
    - Risiko: titel og beskrivelse.
  - **Afvisningen holder,** så længe ingen af siderne har ændret sig. Ændrer en side sig, må AI'en foreslå parret igen.
  - **Noten:** et forslag, der kommer igen, siger "Rejected before by Anna on 03-11-2026 (NotRelevant). The document has changed since." Den gamle afvisning bliver stående som historik.
  - **Fortryd:** en person, der må beslutte, kan fortryde en afvisning ("Undo rejection"). Status bliver `RejectionUndone`, og handlingen logges som `SuggestionRejectionUndone`.
  - **Gamle afvisninger** fra før US-145 har intet fingeraftryk og holder, indtil en person fortryder dem.
  - Det gælder for alle slags forslag: story ↔ krav, evidens ↔ krav, evidens ↔ story, dokument ↔ krav og tiltag for en risiko.
- **Begrundelse:** En afvisning var et svar på det, personen så. Når teksten eller filen ændrer sig, kan svaret være et andet. Noten viser, at nogen har sagt nej før, så personen ikke skal starte forfra.
- **Fravalgt:**
  - At afvisninger udløber efter en fast tid: det siger intet om, hvorvidt noget har ændret sig.
  - At gemme hele teksten for at vise forskellen: fingeraftrykket er nok til at se, at noget har ændret sig, og gemmer ingen fortrolig tekst to gange.

### B86 – Sårbarhedskontakt pr. projekt (US-103)

- **Beslutning:**
  - Hvert projekt har én adresse til rapporter om sårbarheder i produktet (CRA Art. 13(17)).
  - **Standarden beregnes og gemmes ikke:** organisationens Compliance Manager (den første efter navn), ellers den første Admin. Fjernes personen, overtager den næste af sig selv.
  - En adresse kan sættes i hånden, fx `security@acme.dk`. Et tomt felt går tilbage til standarden.
  - Adressen skal være én gyldig e-mail på højst 320 tegn.
  - Overview viser adressen og hvor den kommer fra, fx "anna@acme.dk (Compliance Manager)".
  - Ændringen logges som `ProjectUpdated` med gammel og ny værdi.
- **Begrundelse:** CRA kræver ét kontaktpunkt. Når standarden beregnes, er den altid en person, der stadig er i organisationen.
- **Fravalgt:**
  - At modtage eller videresende rapporter i trackeren.
  - En `security.txt` pr. produkt.
### B87 – SBOM pr. produktversion (US-079)

- **Beslutning:**
  - **Formater:** CycloneDX JSON (1.4 eller nyere) og SPDX 2.3 JSON kan uploades på Scans-fanen. Formatet findes ud fra indholdet, med én importer pr. format som for scanninger (B36).
  - **Pr. version:** en SBOM gælder én produktversion, fx "2.1.0". En ny SBOM for samme version erstatter den viste, og den gamle gemmes.
  - **Komponenter:** hver komponent vises med navn, version, package URL og licens. Den samme komponent vises kun én gang, og listen kan søges.
  - **Åbne fund:** en komponent markeres, når et åbent fund nævner dens navn som helt ord og dens version, sådan som Trivy skriver det ("Package: openssl", "Installed Version: 3.0.1"). En komponent, der ikke kan matches, markeres ikke.
  - **Evidens:** en SBOM kan bruges som evidens, fx for CRA bilag I del II (1). Derfor er `.json` nu en tilladt evidenstype, og indholdet tjekkes som UTF-8-tekst.
  - Importen logges som `SbomImported`.
- **Begrundelse:** CRA kræver, at komponenterne er kendt og dokumenteret. Fund fra scanninger findes allerede i trackeren, så de kan vises ved komponenterne uden opslag i eksterne databaser.
- **Fravalgt:**
  - Opslag i OSV eller NVD: kun scanninger bruges.
  - XML-formater: de kan komme senere som en ny importer.
  - Tjek af licenser.

### B88 – Import af risici fra et regneark (US-049)

- **Beslutning:**
  - **Filtyper:** CSV med `;` eller `,` og Excel-projektmapper (.xlsx). Separatoren findes ud fra overskriftsrækken. Excel læses med `DocumentFormat.OpenXml`, som projektet allerede bruger.
  - **To trin:** filen gemmes først (`RiskImport`), og en forhåndsvisning viser hver række med niveau og fejl. Først når personen bekræfter, bliver rækkerne til risici. Importen gemmes, så loggen og risiciene kan pege på filen.
  - **Alt eller intet:** én fejl i én række stopper hele importen. Fejlen nævner rækkens nummer og feltet, fx "Row 2: Likelihood must be 1 to 5".
  - **Dubletter:** en titel, der allerede findes i projektet, springes over, medmindre personen vælger "Import anyway". Den samme titel to gange i filen er en fejl.
  - **Ejer:** en ejer, der ikke er medlem, gemmes som tekst på risikoen. Den, der importerer, bliver ejer.
  - **Accept:** en accepteret risiko kræver en begrundelse. En High- eller Critical-risiko kræver stadig en anden person (US-135), så den forbliver åben med behandlingen Accept.
  - "Last reviewed" sætter datoen for seneste gennemgang, ellers bruges importdatoen.
  - **Grænser:** højst 1 MB og 500 rækker, og kun roller med `ManageRisks` kan importere. Importen logges én gang som `RisksImported`.
- **Begrundelse:** Organisationer har ofte et risikoregister i Excel allerede. At tjekke alle rækker først og importere alt eller intet giver ingen halve registre.
- **Fravalgt:**
  - Import fra andre værktøjers formater (Jira, ServiceNow).
  - Den nye risikoside: den er delt ud i US-147.

### B89 – Faseporte advarer og kræver en begrundelse (US-071)

- **Beslutning:**
  - **Gaten:** før et projekt går fra Planning til Development, viser Overview gaten "Ready for Development" med fire tjek:
    - formål og scope er beskrevet
    - projektet følger mindst ét framework
    - projektet er klassificeret under hver forordning, det følger (US-026)
    - der er mindst én risiko i registret
  - **Trackeren beregner selv hvert tjek,** og et tjek bliver Met af sig selv, når det er på plads. Tjeklisterne fra US-047 er ikke nødvendige og kan senere tilføje manuelle punkter.
  - **Gaten blokerer ikke.** Hvis et tjek ikke er opfyldt, kræver faseskiftet en begrundelse.
  - **Log:** begrundelsen gemmes i aktivitetsloggen sammen med skiftet, fx "Phase: Planning → Development, 1 check not met: Risk workshop next week". Er alle tjek opfyldt, står der "all checks met". Der er ingen ny tabel, fordi loggen er den uforanderlige registrering af skiftet.
  - **Kun fremad:** at gå tilbage til en tidligere fase har ingen gate.
  - Samme mekanisme (`PhaseGate`) bruges til gaten før Released (US-072).
- **Begrundelse:** Et team skal kunne gå videre, når der er en god grund. En begrundelse i loggen giver auditoren sporbarheden, uden at trackeren står i vejen.
- **Fravalgt:**
  - Porte, der blokerer.
  - Godkendelse af en anden person (four-eyes som US-135).

### B90 – Gaten før Released (US-072)

- **Beslutning:**
  - **Gaten:** før et produkt går til Released, viser Overview gaten "Ready for release" med fem tjek:
    - **Dækning:** hvert gældende krav er dækket. Anbefalinger tæller ikke (US-143).
    - **Evidens:** hvert dækket krav har gyldig evidens.
    - **Fund:** der er ingen åbne critical- eller high-fund fra scanninger, og der er mindst én scanning.
    - **SBOM:** der er uploadet en SBOM (US-079).
    - **Support:** der er en slutdato for support (US-025).
  - **Antal:** hvert tjek viser, hvor meget der mangler, fx "3 requirements without evidence", og linker til, hvor det rettes.
  - **Som i B89:** gaten blokerer ikke, men kræver en begrundelse, som logges med faseskiftet.
  - **Rækkefølge:** gaten gælder også, når et projekt springer direkte fra Planning til Released. Domænets egne regler kommer først, fx "Set the support end date before release". Gaten tjekkes derefter, og intet gemmes, hvis den afviser.
  - En supportdato, der sendes med selve frigivelsen, tæller som opfyldt.
  - "Not available yet" fra issuet bruges ikke, fordi scanninger og SBOM nu findes.
- **Begrundelse:** CRA kræver, at et produkt ikke markedsføres med kendte udnyttelige sårbarheder, at komponenterne er kendt, og at dokumentationen er på plads. Gaten samler det ét sted, lige før det gælder.
- **Fravalgt:**
  - Selve overensstemmelsesvurderingen og CE-mærkning.
  - At gaten blokerer.

### B91 – Eksport af aktivitetsloggen (US-055)

- **Beslutning:**
  - **Hvor:** eksporten ligger på organisationens Activity-side (/Activity) som knapperne "CSV" og "PDF" og bruger de filtre, siden viser: søgning, periode, gruppe, person og projekt.
  - **Periode:** uden datoer de seneste 90 dage. Begge datoer er med.
  - **Hvem:** de samme som må se hele loggen (US-102 R6), altså Admin og Auditor. Issuet sagde "alle i organisationen", men det ville give andre roller adgang til en log, de ikke må se på siden.
  - **Kolonner:**
    - tid i UTC (ISO 8601)
    - handling
    - projekt
    - hvad den ellers handler om, fx et medlem
    - person (eller "System")
    - rolle på tidspunktet
    - detaljer
  - **Én CSV-skriver** (`CsvFile`) for alle eksporter: UTF-8 med BOM, `;`, og ingen celle kan køre som en formel. Traceability-matrixen bruger den nu også.
  - Eksporten logges som `ActivityLogExported` med format, periode og antal.
- **Begrundelse:** En auditor vil tjekke beslutningerne i sine egne værktøjer. Samme adgang som siden giver ingen nye huller.
- **Fravalgt:**
  - At låse eksporten (US-050). Den kommer i den låste auditpakke (US-076).
  - Serverens egne logs.

### B92 – Rapport over risikovurderingen (US-054)

- **Beslutning:**
  - **Hvor:** "Risk assessment" er en rapport på projektets Reports-fane som Excel, CSV og PDF. Den kan låses som de andre (US-050).
  - **Fire afsnit i fast rækkefølge:**
    - **Criteria:** matrixen med niveauet for hver sandsynlighed × konsekvens.
    - **Risks:** åbne og accepterede risici efter niveau, score og titel. Hver viser tiltag, handlinger, ejer, seneste gennemgang og næste gennemgang, eller "Review due".
    - **Accepted risks:** hvem der accepterede, deres rolle, hvornår og hvorfor.
    - **Closed risks:** med dato og begrundelse.
  - **CSV** indeholder kun afsnittet Risks, så værktøjer kan læse én tabel.
  - **Kun den nuværende tilstand.** Historikken findes i aktivitetsloggen (US-055).
  - **PDF-tekst:** standardfonten mangler omkring 45 tegn, som Windows-1252 ellers har, fx "å", "ö", "×" og "€". De gav en fejl i alle PDF-rapporter. De skrives nu i en nær form ("aa", "o", "x", "EUR"). En test skriver hvert tegn i overskrift og celle.
- **Begrundelse:** En markedsovervågningsmyndighed eller en auditor beder om risikovurderingen som ét dokument (CRA Art. 13(2), ISO 27001 6.1.2 og 8.2). Rapporten genbruger rapportmodellen, så Excel og PDF har samme indhold.
- **Fravalgt:**
  - Historik pr. risiko.
  - Trusselsmodellering.
  - Et indlejret font, så alle tegn kan vises. Det er større og kan komme senere.

### B93 – Statement of Applicability fra projekternes data (US-053)

- **Beslutning:**
  - **Katalog:** ISO/IEC 27001's tekst er ISO's og bygges ikke ind. SoA'en bruger det ISO/IEC 27001, som organisationen selv har uploadet: et framework, hvis navn starter med "ISO/IEC 27001" eller "ISO 27001".
  - **Kontroller:** kun Annex A-kontrollerne (koder, der starter med "A."), når de findes, ellers alle krav. De står i standardens rækkefølge fra den nyeste version, projekterne bruger.
  - **Hvor:** på organisationsområdets Reports-fane som Excel, CSV og PDF. Den kan låses (US-050).
  - **Gælder / gælder ikke:** en kontrol er "Not applicable", kun når alle projekter, der følger standarden, har markeret den sådan (US-043). Organisationsområdet tæller som et projekt. Begrundelserne vises pr. projekt.
  - **Implementering beregnes, sættes ikke i hånden:**
    - **Implemented:** dækket med gyldig evidens i alle projekter, hvor kontrollen gælder.
    - **Partly implemented:** dækket et sted.
    - **Not implemented:** dækket intet sted.
  - **"How it is met"** viser pr. projekt de stories og den gyldige evidens, der dækker kontrollen.
  - Uden ISO/IEC 27001 siger rapportkortet, hvad der mangler.
- **Begrundelse:** SoA'en skal passe med det, projekterne faktisk gør. En status sat i hånden ville kunne afvige fra dækningen og evidensen.
- **Fravalgt:**
  - At organisationen sætter "not applicable" for hele organisationen et sted for sig. Organisationsområdet kan allerede markere sine egne kontroller.
  - En manuel implementeringsstatus.

### B94 – Audits, fund og certifikater (US-134)

- **Beslutning:**
  - **Hvem registrerer:** compliance-ansvarlige (DecideCompliance) registrerer en afholdt audit. De angiver art, scope, dato, auditor, resultat og uploader auditorens underskrevne rapport, som gemmes med SHA-256. Den eksterne auditor får intet eget login til det; læsning er Auditor-rollen (US-010).
  - **Ingen four-eyes:** en registreret audit ændres aldrig og rettes kun ved en ny registrering, der henviser til den. Den gamle vises som "Corrected by …". Derfor tilføjer en anden person kun lidt.
  - **Organisationens audits** registreres på organisationsområdet. Dens certifikat vises også i hvert projekts header.
  - **Certifikat:** et badge, fx "ISO/IEC 27001 certified until 30-06-2027".
    - 90 dage før udløb står det under "Needs attention" ("Certificate DK-1234 expires in 48 days").
    - Efter udløb siger badget "expired".
    - Pr. standard vises det nyeste certifikat, der ikke er rettet.
  - **Fund:** et fund har en ejer og en frist og står under "Needs attention", til det lukkes. Det lukkes én gang med en begrundelse, som alle med LinkWork kan give. Evidens kan knyttes til, men kræves ikke.
  - **Ingen log ved udløb:** "CertificateExpiring" logges ikke, fordi der intet sker på det tidspunkt. Det ses under "Needs attention".
  - Registrering og lukning logges som `AuditRecorded` og `AuditFindingClosed`.
- **Begrundelse:** ISO 27001 9.2 og 10.2 kræver, at audits og deres fund følges op, og CRA Art. 32 kan kræve et bemyndiget organs vurdering. Med auditorens egen rapport og en uforanderlig registrering kan man stole på historikken.
- **Fravalgt:**
  - Auditorens eget login og signatur i trackeren.
  - Certifikatet i PDF-rapporterne (US-131). Det kommer i auditpakken (US-076).

### B95 – Auditpakken gemmes som en låst rapport (US-076)

- **Beslutning:**
  - **Indhold:** pakken er en ZIP med:
    - **Indholdsfortegnelse:** en tekstfil, der lister hver fil med SHA-256 og evidens, der er et link, og siger, hvad der ikke kom med og hvorfor (R6).
    - **Rapporter:**
      - traceability for en auditor pr. valgt framework
      - ledelsesrapporten som compliance-status
      - SoA, hvis organisationen har ISO/IEC 27001
      - risikovurderingen
      - projektets aktivitetslog som CSV for perioden
    - **Filer:**
      - hver evidensfil
      - den nyeste version af hvert kravdokument
      - den nyeste SBOM pr. version
      - audits med auditorens rapport og deres fund
  - **Låsning:** pakken gemmes som en låst rapport (US-050, `ReportSnapshot`) og ikke i en ny tabel. Den står derfor under "Locked reports" med sit fingeraftryk, kan hentes og tjekkes og ændres aldrig. Signering (US-132) tilbydes kun for PDF'er.
  - **Fingeraftryk:** pakkens eget SHA-256 vises i trackeren og i loggen (`AuditPackageCreated`). Indholdsfortegnelsen kan ikke indeholde pakkens eget fingeraftryk, men har hver fils.
  - **Valg:** brugeren vælger frameworks og periode. Uden datoer er perioden de seneste 12 måneder.
  - **Hvem:** alle i organisationen, også Auditoren (R4). Pakken læser kun data og gemmer en låst kopi.
  - **Samme pakke:** filerne skrives i fast rækkefølge med fast tidsstempel, så de samme dele giver samme ZIP.
- **Begrundelse:** Auditoren får alt i ét, og fingeraftrykket viser, at intet er ændret. Låste rapporter har allerede listen, download og tjek af fingeraftryk, så der er ingen grund til en ny mekanisme.
- **Fravalgt:**
  - At dele pakken uden for trackeren.
  - En signatur med certifikat på ZIP'en.

### B96 – Grænse for databasekald pr. side (US-139)

- **Beslutning:**
  - **Tælling:** EF Core's egen `DbCommandInterceptor` tæller kaldene i hver request og logger et kald over 500 ms som en advarsel i alle miljøer. Advarslen nævner siden, men aldrig parameterværdier. De 500 ms er indstillingen `Database:SlowQueryMilliseconds`.
  - **Testene:** et scenarie åbner de vigtige sider først med lidt data, derefter med 200 stories, 60 risici, 100 evidens og 15 projekter mere. Hver side skal holde sig under sin grænse og må ikke vokse med data (±2).
  - **Målingen den 12-11-2026:** ingen side voksede med data, så der var ingen N+1-kald at rette. Antallet af kald var:
    - Dashboard: 9
    - Projects: 9
    - Overview: 47
    - Compliance: 49
    - Evidence: 31
    - Risks: 10
    - Work items: 26
    - Reports: 25
    - Project activity: 11
    - Frameworks: 2
    - Organisation activity: 5
    - Traceability download: 46
  - **Grænserne** ligger lige over tallene. En grænse hæves kun i samme PR som den ændring, der kræver det, og med begrundelsen.
- **Begrundelse:** En side, der bliver langsom, når data vokser, fanges i testene i stedet for hos en kunde. Kun gratis værktøjer, ingen profiler.
- **Fravalgt:**
  - At sænke Overview og Compliance nu. De laver mange kald, men et fast antal. Det kan blive en egen forbedring, hvis lasttesten (US-138) viser, at de er langsomme.

### B97 – Driftslogs med reference og sikkerhedshændelser (US-098)

- **Beslutning:**
  - **Format:** logs skrives som JSON til stdout med .NET's egen JSON-logger, så der kommer ingen ny pakke. Development bruger stadig den læselige konsol. Indstillingen `Logging:Format` vælger.
  - **Reference og bruger:** hver loglinje i en request bærer referencen (W3C trace id) og brugerens Keycloak-id ("sub"). Fejlsiden viser samme reference og ingen tekniske detaljer.
  - **Ingen organisation i hver linje:** organisationens id står ikke i hver linje, fordi det ville koste et databasekald pr. request (US-139). Brugerens id finder organisationen.
  - **Sikkerhedshændelser (Warning) med faste EventId'er:**
    - adgang nægtet: 403, eller 404 på en side for en logget ind bruger
    - en ramt grænse for requests (429)
    - et mislykket login, hvor kun typen af fejl logges og aldrig token eller besked
  - **Fejlrate:** mere end 10 fejl (5xx) inden for 5 minutter giver én "Error rate high"-linje, som et logsystem kan alarmere på.
  - `docs/drift-og-logs.md` beskriver felterne og hændelserne.
- **Begrundelse:** Operatøren skal kunne finde en fejl ud fra den reference, en bruger giver, og blive advaret, før kunderne opdager det, uden at logs bliver en kilde til persondata.
- **Fravalgt:**
  - At drive et logsystem.
  - Advarsler på e-mail eller chat.
  - Serilog.

### B98 – Grænser for tunge handlinger (US-097)

- **Beslutning:**
  - **Værktøj:** ASP.NET Core's egen rate limiter, så der kommer ingen ny pakke.
  - **Grænser:**
    - **Ændringer:** 120 POST i minuttet pr. bruger (Keycloaks "sub", ellers IP-adressen).
    - **Uploads:** en formular med en fil tæller som upload, 20 i minuttet.
    - **AI:** "Ask the AI", "Suggest", "Show more" og forslag for "not covered", "not applicable" og tiltag er begrænset til 10 i minuttet pr. organisation. Organisationen findes via et filter, fordi den ikke står i login-cookien.
  - **Aldrig begrænset:** GET, `/health` og statiske filer. Pipelinens upload-API (`/api/…`) har sin egen grænse pr. token (US-122 R6) og tælles ikke med her.
  - **Når en grænse rammes:** svaret er 429 med "Too many requests. Try again in a minute" og `Retry-After: 60`. Handlingen kører ikke, så intet ændres, og hændelsen logges som en sikkerhedshændelse (US-098).
  - **Indstillinger:** grænserne er `RateLimits:*`.
- **Begrundelse:** Én bruger eller et script må ikke kunne gøre trackeren eller den lokale AI langsom for alle andre. Grænsen pr. organisation for AI deler AI'en retfærdigt mellem kunderne.
- **Fravalgt:**
  - Grænser på tværs af flere servere, fordi der kun er én instans.
  - Grænser for login, som Keycloak tager sig af (US-089).

### B99 – Persondata: indsigt og sletning som pseudonym (US-095)

- **Beslutning:**
  - **Privatlivssiden** siger, hvilke persondata trackeren gemmer, hvorfor, på hvilket grundlag, hvor længe og hvem man kontakter. Den juridiske ordlyd skal stadig læses af en, der kender GDPR.
  - **"My data":** hvert medlem kan hente sine egne data som JSON: profil, organisation, rolle og de logposter, medlemmet selv har lavet.
  - **Sletning:** Admin kan slette persondata for et medlem, der er fjernet (US-090).
    - Navn og e-mail bliver et pseudonym, fx "Former member 7F3A".
    - Forbindelsen til Keycloak skæres over.
    - Medlemmets id bevares, så alt det, medlemmet gjorde, nu står under pseudonymet.
    - Et aktivt medlem kan ikke slettes, og sletning sker kun én gang.
    - Kontoen i Keycloak slettes af operatøren dér.
  - **Aktivitetsloggen ændres ikke.** Den er kædet med hashes (US-091), så detaljer skrevet før sletningen, fx et navn i "MemberRemoved", bliver stående. Navnet ved siden af hver post kommer fra medlemmets id og viser derfor pseudonymet.
  - **Egen tekst:** det, medlemmer selv skrev, ændres ikke, og privatlivssiden siger det.
  - **Log:** eksport og sletning logges uden selve persondataene (`PersonalDataExported`, `PersonalDataErased`).
- **Begrundelse:** GDPR giver ret til indsigt og sletning. Et compliance-system skal samtidig kunne vise, hvem der besluttede hvad, og at loggen ikke er ændret. Et pseudonym opfylder begge dele.
- **Fravalgt:**
  - At slette logposter.
  - Persondata i kundernes eget indhold.
  - En databehandleraftale, som er juridisk tekst og ikke kode.

### B100 – Last- og stresstest med k6, startet i hånden (US-138)

- **Beslutning:**
  - **Værktøj:** k6 (open source) fra Docker-imaget. Den nye workflow `load-test.yml` køres kun med `workflow_dispatch`, så den ikke bruger minutter af sig selv.
  - **Systemet:** hele systemet startes med `docker compose --profile full`. En lille override (`tests/load/docker-compose.load.yml`) gør testdatabasen tilgængelig for seed-værktøjet.
  - **Login:** k6 går gennem Keycloaks egen login-formular med testbrugere, som workflowen laver i test-realmen med et tilfældigt kodeord for den ene kørsel. Kravet om engangskode fjernes kun for dem. Realm-filen ændres ikke.
  - **Data:** `tests/load/seed` (C#) fylder en tom testdatabase med domænets egne regler. Loggen forsegles som en hash-kæde, og værktøjet nægter at køre mod en database med data (R6). Et scenarie kører det mod testdatabasen og tjekker antal, kæden og at en bruger kan se projekterne.
  - **Grænser:** p95 under 1 s for sider og under 5 s for rapporter, og under 1 % fejl.
    - Stresstesten går op til fire gange så mange brugere og stopper ikke ved grænserne.
    - Et minut efter tjekker en smoke-test, at systemet kommer sig.
  - **Resultater** gemmes i 90 dage.
  - **Langsomme sider** bliver egne issues, medmindre rettelsen er lille.
- **Begrundelse:** Kun gratis værktøjer, og kun når nogen vil vide det. Testen viser, hvor mange brugere trackeren kan klare, før en kunde opdager det.
- **Fravalgt:**
  - En betalt cloud-lasttest.
  - En fast kørsel hver uge.
  - Test af den lokale AI-server og Keycloak selv.
  - Soak-tests over mange timer.

### B101 – AI-tjek af evidens, og en afvist evidens tæller ikke (US-038)

- **Beslutning:**
  - **Tjekket:** "Check with AI" ved en evidens på en kravrække sender kravets tekst og evidensens tekst til den lokale AI med opgavens model (`EvidenceCheck`, US-117). For en PDF eller tekstfil er det de første sider, og for et link kun titlen.
  - **Svaret:** AI'en svarer med en vurdering (Covers, Partly covers, Does not cover), hvad der mangler, og citater.
  - **Kun på klik,** aldrig af sig selv.
  - **Beslutning:** vurderingen er "AI-generated", indtil en person med DecideCompliance bekræfter den eller tilsidesætter den med en anden vurdering og en begrundelse.
  - **Afvist evidens:** en bekræftet eller tilsidesat "Does not cover" gør, at evidensen ikke længere tæller for kravet. Det ser også gaten før Released (US-072) og SoA'en (US-053). Issuet foreslog det modsatte; det er valgt den 13-11-2026.
  - **Historik:** tjekket hører til evidensens fingeraftryk. Et nyt tjek af samme par vises i stedet for det gamle, og begge gemmes.
  - **Uden AI:** siden siger det, og intet gemmes.
  - Tjek og beslutninger logges.
- **Begrundelse:** Mangler skal findes, før en auditor finder dem, men det er stadig en person, der beslutter. Evidens, som en person har afvist, kan ikke stå som bevis.
- **Fravalgt:**
  - Automatisk tjek ved upload.
  - At hente siderne bag links.
  - OCR af scannede PDF'er.

### B102 – En AI-agent laver kun udkast til risici (US-112)

- **Beslutning:**
  - **Knappen:** "Draft risks with AI" på Risks-fanen sender projektets fakta til den lokale AI med opgavens model (`RiskDrafts`, US-117) og beder om højst 10 risici med begrundelse. Faktaene er:
    - formål og scope
    - klassifikation
    - op til 15 ikke-dækkede krav
    - åbne sikkerhedshændelser
    - åbne high/critical-fund
    - registrets risici
  - **Agenten må kun lave udkast.** Et udkast bliver aldrig til en risiko, før en person med ManageRisks accepterer det. Personen kan rette titel, beskrivelse, skala, ejer og behandling først. Risikoen mærkes "AI-drafted".
  - **Dubletter:** et udkast, som en eksisterende risiko, et åbent udkast eller et kasseret udkast allerede dækker, kommer ikke med. Siden siger, hvilket der dækker det. To titler regnes for den samme risiko, når den ene indeholder den anden, eller når mindst halvdelen af ordene er fælles.
  - **Kassering:** et udkast kasseres med en begrundelse og foreslås ikke igen.
  - **Uden AI:** siden siger det, og intet gemmes.
- **Begrundelse:** Teamet starter fra en begrundet liste i stedet for en tom side, mens hver risiko stadig er en persons beslutning (AI Act Art. 14).
- **Fravalgt:**
  - Agenter, der handler af sig selv efter en tidsplan.
  - Udkast til handlinger (US-111).

### B103 – Cross-mapping mellem frameworks foreslås af AI og godkendes af en person (US-037)

- **Beslutning:**
  - **Siden:** "Cross-mapping between frameworks" under Frameworks. Man vælger to versioner, som organisationens projekter bruger, og ser forslag og godkendte matches. Alle medlemmer kan se siden; at foreslå og beslutte kræver ManageFrameworks.
  - **Forslagene:** hvert krav i den ene version sammenlignes med hvert krav i den anden via tekstens vektor (Embedding-opgavens model). De bedste 4 pr. krav over grænsen bliver forslag, mærket "AI-generated". Grænsen er historie-grænsen (US-105), dog mindst 0,5, så svage ligheder ikke bliver støj.
  - **Styrken følger scoren:** Same fra 0,85, Partly fra 0,7, ellers Related.
  - **Begrundelsen er en skabelon** med styrke, de to krav og scoren, ikke et kald til en tekstmodel, fordi der kan være hundredvis af par.
  - **Beslutning:** godkend, eller afvis med en grund fra den faste liste (B14). Et afgjort par foreslås ikke igen; at spørge igen erstatter kun de ventende.
  - **Dækning:** et godkendt Same lader dækningen gå videre til det ikke-dækkede krav, vist som "through CRA I.2(d)" (samme felt som US-142). Kun egen dækning går videre, så det aldrig går i ring. Partly og Related er kun hints.
  - **Isolation:** et match hører til organisationen og præcis de to versioner. En version, organisationen ikke følger, giver "not found".
- **Begrundelse:** Arbejde for ét framework tæller også for et andet, uden at teamet skal mappe hundredvis af krav i hånden, mens hver match stadig er en persons beslutning (AI Act Art. 14).
- **Fravalgt:**
  - At Partly også tæller som dækning.
  - En begrundelse fra tekstmodellen for hvert par.

### B104 – En reranker afgør rækkefølgen af AI'ens forslag (US-118)

- **Beslutning:**
  - **Tre trin:** forslag til stories (US-020, US-044), til evidens (US-059) og fra evidens til stories (US-146) laves sådan:
    - søgning med vektorer finder de bedste 20 kandidater, uanset deres søgescore
    - en reranker læser kravet og hver kandidat sammen og giver en ny score
    - US-105's grænse gælder for rerank-scoren, og kun de beholdte får skrevet en begrundelse
  - **Begge scorer gemmes** sammen med rerank-modellen, og forslaget viser "score 0.82 (search 0.61)".
  - **Rerankeren kaldes som en ny klient** mod organisationens egen AI-server i formatet `/v1/rerank` (TEI, vLLM, Jina). Modellen sættes med `Ai:Tasks:Ranking:Model`, og `Ai:Tasks:Ranking:BaseUrl`, når den kører på en anden server.
  - **Uden en model til Ranking-opgaven** bevares dagens opførsel, og AI-oversigten viser "Off (search score only)".
  - **Svar:** et svar uden præcis én score pr. kandidat afvises. Kan rerankeren ikke nås, gemmes intet, og siden siger det.
- **Begrundelse:** En cross-encoder læser teksterne sammen og rammer derfor oftere rigtigt end to vektorer (AI Act Art. 15). Begge scorer vises, så man kan se, hvad rerankeren ændrede (Art. 13).
- **Fravalgt:**
  - Reranking i gap-finderen (US-119 kan bruge den senere).
  - Forslag til cross-mapping (US-037). Her er der hundredvis af par, og godkendelsen er alligevel en persons.

### B105 – Industriprofilen foreslår, en branche ad gangen (US-080)

- **Beslutning:**
  - **Valget:** en Admin vælger på Settings, hvilken branche organisationen laver produkter til: Software, Medtech, Fintech eller Other. Det er også et trin i "Get started".
  - **Profilen foreslår kun.** Hvert forslag siger hvorfor. Et indbygget framework slås til med en knap, NIS2 går til scope-tjekket (US-142), og ISO/IEC 27001 er organisationens egen kopi (US-143). Intet slås til eller fra af sig selv, heller ikke når branchen skiftes.
  - **AI Act** foreslås kun, når "Our products use AI" er krydset af.
  - **Profilen viser også** de scanninger og rapporter, branchen forventes at have.
  - **En branche ad gangen:** Software først. Medtech (software og hardware) og Fintech kan vælges, men siger, at deres egne forslag kommer, og viser indtil da software-forslagene.
  - **Én klasse pr. branche** (`IIndustryProfile`), valgt gennem `IndustryProfiles`, så en ny branche er en ny klasse.
- **Begrundelse:** En ny organisation starter med det, der typisk gælder for den, i stedet for at læse alle regler selv, mens beslutningen stadig er Adminens (ISO/IEC 27001 4.2, A.5.31).
- **Fravalgt:**
  - At slå frameworks til automatisk ud fra branchen.
  - At bygge alle tre brancher på én gang.

### B106 – Diagrammer læses som tekst, uden AI og uden at køre noget (US-149)

- **Beslutning:**
  - **Formater:** draw.io (`.drawio`, også komprimerede sider), Mermaid (`.mmd`, `.mermaid`), PlantUML (`.puml`, `.plantuml`), BPMN 2.0 (`.bpmn`) og SVG (`.svg`) kan uploades som evidens. Hvert format har sin egen læser bag `IDiagramReader`.
  - **Det, der læses:** elementer (navn og slags), pile med deres labels og grupper. En SVG giver sin tekst; en SVG eksporteret fra draw.io giver hele diagrammet, den bærer.
  - **Sikkerhed:** intet i filen køres. XML læses med DTD'er og eksterne entiteter slået fra; scripts og styles i en SVG læses aldrig. Filen hentes altid som download med `nosniff`.
  - **Brug:** det læste gemmes på evidensen, når den tilføjes, og vises under filen ("Diagram: 3 elements, 2 links"). Det er også evidensens tekst for AI'ens forslag, evidens-tjekket og søgningen, ligesom en PDF's tekst.
  - **Grænser:** højst 2.000 elementer og pile; ellers står der, hvor mange der blev læst.
  - **Fejl:** et diagram, der ikke kan læses, gemmes stadig som evidens med årsagen.
  - **Billeder** (PNG, JPG, PDF) gemmes som i dag; at læse dem kræver en vision-model (US-151).
- **Begrundelse:** Arkitektur-, dataflow- og procesdiagrammer er dokumentation, CRA (Annex VII) og ISO/IEC 27001 (A.8.27) kræver. Tekstformaterne kan læses præcist uden AI, så resultatet er det samme hver gang.
- **Fravalgt:**
  - At tegne eller rette diagrammer i trackeren.
  - At læse `.xml` som draw.io, da endelsen også bruges af mange andre formater.

### B107 – Compliance-fanen er en let oversigt; hvert krav har sin egen side (US-153)

- **Beslutning:**
  - **Oversigten** viser hvert krav som én let række: kode, titel, hvad der er linket ("1 story · 2 evidence"), hvad der venter, og status. Rækkerne har ingen formularer eller vælgere.
  - **Kravets side** ligger på kravets adresse fra US-144 R12, fx `/Projects/Details/{id}/compliance/cra/I.2(d)`. Den har:
    - en tilbage-link til oversigten;
    - et hoved med kode, titel, status og én primær handling ("Add evidence"), mens "Ask the AI" og "Not applicable" ligger under "More";
    - kravets tekst;
    - user stories, evidens og det, der venter på en beslutning.
  - **Efter en handling** vises samme side igen. Formularerne sender kravets kode som `req`, og en fejl vises også på siden.
  - **Kun kravets side** henter projektets evidens og risici til vælgerne.
  - **Folde-rækkerne** fra US-144 (R9, R10) er fjernet. Træet fra US-143 bliver.
- **Begrundelse:** Compliance-fanen var 1,35 MB med 436 formularer og 219 vælgere for 72 krav, så den var tung at hente og svær at overskue. Nu ser man status for alle krav på én gang og handler ét sted pr. krav. Det giver også én adresse at linke til fra dokumentation og reviews.
- **Fravalgt:**
  - Et sidepanel ved siden af listen: det kræver mere JavaScript og har ingen egen adresse.
  - At beholde formularerne skjult i rækkerne, fordi siden så stadig ville være lige så tung.

### B108 – AI'en markerer krav, der kræver handling (US-155)

- **Beslutning:**
  - **Én gennemgang:** "Find what needs action" spørger den lokale model om hvert gældende, ikke-dækket krav i ét framework, højst 20 pr. tryk. Svaret er enten "needs action" med prioritet (High, Medium, Low) og begrundelse, "not applicable" (bliver det sædvanlige forslag fra US-060) eller "unsure", som ikke markerer noget.
  - **Grundlaget** er det samme som i US-060: produktets beskrivelse, formål, scope, klassifikation, branche og risici.
  - **Risici:** en åben høj eller kritisk risiko, hvis tiltag peger på kravet, gør altid markeringen High, og siden siger det. Et krav, en åben risiko peger på, foreslås aldrig som "not applicable".
  - **Markeringen** er et forslag med mærket "AI-generated". Kravet vises som "Needs action" i oversigten, i et eget filter og under "Needs attention" på overblikket.
  - **En person beslutter:**
    - "Create story" bekræfter markeringen og beder AI'en om et udkast til en story (US-106).
    - "Not needed" kræver en begrundelse og gør kravet "not applicable", eventuelt med en risiko som begrundelse.
    - Kun de, der må træffe compliance-beslutninger, kan gøre det.
  - **Når kravet bliver dækket**, vises markeringen ikke længere, uanset dens status.
  - **Egen tabel:** markeringen gemmes i `NeedsActionSuggestions` med model, promptversion og beslutning, ligesom de andre AI-forslag, og alt logges.
- **Begrundelse:** "Not covered" siger ikke, om kravet overhovedet gælder produktet. Med markeringen ser man, hvad der faktisk mangler, og hvad der haster, mens beslutningen stadig er en persons (AI Act art. 14, CRA art. 13).
- **Fravalgt:**
  - At udvide `ApplicabilitySuggestion`. "Gælder ikke" og "mangler arbejde" har forskellige beslutninger og følger.
  - At oprette issues i GitHub direkte. Det kræver skriverettigheder; linket til GitHubs "new issue" bruges som i US-106.
  - Automatisk genvurdering ved ændringer, som kommer i US-156.

### B109 – Risikoagenten spørger én runde pr. trusselsområde (US-157)

- **Beslutning:**
  - **Runder:** agenten spørger i én runde pr. tema i stedet for én stor prompt. De faste temaer er adgang og login, data og privatliv, opdateringer og forsyningskæde samt tilgængelighed. "Requirements not met" kommer med, når der er ikke-dækkede krav, og "Events and findings", når der er åbne hændelser eller alvorlige fund.
  - **Antal:** højst 3 forslag pr. tema og 15 i alt.
  - **Dubletter:** de sorteres fra på tværs af temaerne med samme regel som før (US-112). Det første tema vinder.
  - **Fejl:** en runde, der fejler eller ikke giver noget brugbart, stopper ikke de andre. Beskeden siger, hvilket tema der intet gav. Først når ingen runde svarer, er AI'en "ikke tilgængelig".
  - **Visning:** hvert udkast gemmer sit tema (`RiskDrafts.Theme`), og fanen Risici viser udkastene grupperet efter tema. Promptversionen er `risk-drafts-v2`.
- **Begrundelse:** En lille lokal model skriver typisk kun én eller to risici, når den bliver bedt om op til 10 på én gang. Små, fokuserede spørgsmål giver flere og bredere forslag, og temaerne gør det lettere at se, hvad der mangler (CRA art. 13, ISO/IEC 27001 6.1.2).
- **Fravalgt:**
  - At spørge den samme prompt igen, til der er nok forslag. Det giver gentagelser.
  - Temaer pr. branche, som kommer senere med brancheprofilerne.

### B110 – Risikoregistret viser, hvad der står bag hver risiko (US-159)

- **Beslutning:**
  - **Kolonnen "Linked":** hver risiko i registret viser "2 measures · 1 story · 3 evidence". Tallene tælles ud fra risikoens spor (US-124):
    - stories, der er linket til et krav, som er et tiltag;
    - en story, der selv er et tiltag;
    - stories og evidens på risikoens handlinger.
  - **Optælling:** hver story og hver evidens tælles én gang. Udløbet evidens og fjernede stories tæller ikke.
  - **"Nothing behind it"** markerer en åben høj eller kritisk risiko, der skal reduceres, og som ikke har tiltag, eller hvis tiltag hverken har en story eller evidens. Accepterede, overførte, undgåede og lukkede risici markeres aldrig.
  - **Filter og overblik:** fanen Risici har et filter "Nothing behind it", og overblikkets "Needs attention" tæller disse risici i stedet for "uden tiltag".
  - **Forespørgsler:** fanen Risici henter koblingerne med et fast antal forespørgsler (krav-links, evidens, evidens-links, story-evidens og stories). Overblikket regner det ud fra de compliance-data, det allerede har.
  - **Mærket "No measures yet"** fra US-042 bliver stående ved siden af.
- **Begrundelse:** Med flere AI-foreslåede risici (US-157) skal man kunne se, hvor behandlingen mangler, uden at åbne hver risiko (ISO/IEC 27001 6.1.3 og 8.3, CRA art. 13).
- **Fravalgt:**
  - At hente hele compliance-oversigten på fanen Risici. Det er mange flere forespørgsler.
  - At ændre risikoens egen side, som allerede viser sporet.
### B111 – Et AI-foreslået tiltag kan godkendes med evidens i ét trin (US-158)

- **Beslutning:**
  - **Valgfri evidens:** godkendelsesformularen for et foreslået tiltag har en sektion "+ Evidence" med titel, link eller fil og "Valid until". Uden evidens virker godkendelsen som før.
  - **Hvor evidensen havner:**
    - Er tiltaget et krav, lægges evidensen på kravet med de sædvanlige regler (US-057).
    - Er tiltaget en story, lægges evidensen på storyen (US-146).
  - **Rækkefølge:** forslaget og evidensen tjekkes først, og evidensen gemmes, før tiltaget godkendes. Bliver evidensen afvist, fx fordi der både er link og fil, godkendes intet, og fejlen vises på risikoens side.
  - **Rettigheder:** evidens kræver `LinkWork`.
  - **Mangler evidens:** et godkendt tiltag, hvis krav ikke har gyldig evidens, får linket "Add evidence" til kravets side (US-153).
- **Begrundelse:** Tiltaget og beviset for, at det er udført, hører sammen (ISO/IEC 27001 6.1.3 og 8.3). Ét trin sparer en tur rundt om kravet.
- **Fravalgt:**
  - Evidens direkte på risikoen. Evidens hører til krav og stories, så sporet (US-124) bliver ved med at være ét.
  - At beholde de indtastede værdier efter en fejl. Formularen er kort, og fejlen vises tydeligt.
### B112 – "Needs action" vurderes igen, når produktet ændres (US-156)

- **Beslutning:**
  - **Hvad starter det:** en ændring af beskrivelse, formål eller scope, en ny klassifikation, en ny branche (for alle organisationens projekter) eller et nyt framework. Ændringen gemmer en `NeedsActionReassessment` sammen med sig selv.
  - **Én vurdering:** ændringer inden for 5 minutter giver én vurdering med alle årsagerne, fx "intended purpose changed; scope changed".
  - **Baggrundsjob:** et job kører hvert halve minut og vurderer de forfaldne projekter, hvert i sit eget scope. Det sker aldrig under en request.
  - **Hvad ændres:**
    - Ventende markeringer og ventende "not applicable"-forslag erstattes.
    - En persons beslutning beholdes. "Not needed" markeres "Based on an earlier description".
  - **Uden AI:** intet af vurderingen gemmes. Den venter og prøves igen, og Compliance-fanen siger, at den venter på AI'en.
  - **Lukkede projekter** vurderes ikke.
  - **Log og visning:** aktivitetsloggen får `NeedsActionReassessed` (af systemet) med årsag og resultat, og Compliance-fanen viser status for den seneste vurdering.
  - **Test:** jobbet slås fra med `NeedsActionReassessment:Enabled`. Testene kører det selv efter at have flyttet uret.
- **Begrundelse:** Risikovurderingen skal følge produktet (CRA art. 13, ISO/IEC 27001 8.2), uden at nogen skal huske at trykke på knappen. Forsinkelsen undgår, at fem små ændringer giver fem langsomme AI-kørsler.
- **Fravalgt:**
  - At vurdere i requesten, fordi det er for langsomt med en lokal model.
  - At vurdere ved hver sidevisning.
  - At ændre en persons beslutning.

### B118 – CI kører testene parallelt, og Keycloak-testene om natten

- **Beslutning:**
  - **Parallel kørsel:** Reqnroll-scenarierne køres i 4 parallelle jobs med lige mange scenarier hver. Fordelingen laves af `build/test_shards.py` ud fra de genererede feature-klasser, så den er ens hver gang.
  - **Egne jobs:** domæne- og unit-testene kører i deres eget job, og de hurtige tjek (hemmeligheder, docker-compose, sårbare pakker) i et tredje.
  - **Samling:** et sidste job, `build-and-test` (det navn branch protection kræver), henter dækningen fra alle jobs, laver rapporten og tjekker minimumsgrænserne som før.
  - **Keycloak om natten:** en PR springer de langsomme tests mod en rigtig Keycloak over (`@keycloak`). Main og den natlige kørsel kl. 02 kører det hele.
  - **Pakker og afbrydelse:** NuGet-pakkerne gemmes mellem kørsler, og et nyt push på samme PR afbryder den kørsel, der er i gang.
  - **Fil-overvågning:** grænsen for overvågere hæves, og testene poller i stedet, fordi Ubuntu-maskinen ellers løb tør (128) og alle sider svarede 500.
- **Begrundelse:** Én kørsel af alle tests tog 25-30 minutter. Parallelt tager den omtrent en fjerdedel, og det er gratis, fordi repoet er offentligt.
- **Fravalgt:**
  - Betalte, hurtigere runners.
  - At springe dækningsmålingen over på PR'er.
### B113 – Ingen side må være bredere end vinduet (US-167)

- **Beslutning:**
  - **Topbaren:** den foldede sig ud fra 576 px, men skal bruge cirka 880 px, mere med System og brugermenuen, og det gjorde hele siden bredere. Den folder sig nu ind i sin knap under 992 px (`navbar-expand-lg`), og punkterne ombrydes.
  - **Rækker med vælgere** skal kunne ombryde (`flex-wrap`). En test i `ViewLayoutTests` finder de rækker, der ikke kan.
  - **Vælgere og felter** vokser aldrig ud over deres række, og lange ord brydes.
  - **Tabeller**, der er for brede på smalle skærme, scroller inde i sig selv og ikke i siden.
  - **Årsagen rettes:** siden skjuler ikke det, der stikker ud (ingen `overflow-x: hidden`).
- **Begrundelse:** En vandret scrollbar på hver side gør siderne rodede og skjuler knapper til højre.
- **Fravalgt:** at skjule scrollbaren, for så bliver indholdet klippet af.
### B114 – AI-knapper viser, at de arbejder (US-170)

- **Beslutning:**
  - **Markering:** alle formularer, der spørger AI'en (handlingerne med `[AiRateLimit]`), får `data-busy="Working… this can take a minute"`, og uploads får `data-busy="Uploading…"`.
  - **Scriptet** (`site.js`) viser teksten på formularens egne knapper og låser dem efter første tryk. Resten af siden kan stadig bruges, og knapperne virker igen, når man kommer tilbage til siden.
  - **Uden JavaScript** virker alt som før, og serverens grænse for AI-kald (US-097) stopper stadig gentagelser.
  - **Test:** `BusyFormTests` finder en AI- eller upload-formular uden markering.
- **Begrundelse:** En lokal model er sekunder til minutter om at svare. Uden besked trykker man igen eller tror, den er gået i stå.
- **Fravalgt:** at køre AI-kaldene i baggrunden, som er en større ændring til senere.
### B115 – Lette sider: under 150 KB med rigtige data (US-154)

- **Beslutning:**
  - **Evidens-fanen** gentager ikke længere en vælger med alle krav og alle stories på hvert kort. Det gav 488 KB. Vælgerne ligger på evidensens egen adresse, `/Projects/Details/{id}/evidence/{evidenceId}`, ligesom kravsiden (US-153).
  - **Suggestions-fanen** viser de første 15 huller pr. kategori med "Show all n". Den var 187 KB.
  - **Test:** et scenarie åbner alle sider og faner med realistiske data (30 stories, 12 risici, 20 evidens) og kræver under 150 KB pr. side og ingen lange vælgere gentaget pr. række.
- **Begrundelse:** Siderne skal være hurtige for alle og blive ved med at være det, når et projekt vokser (ISO/IEC 27001 A.8.6).
- **Fravalgt:** at hente vælgerne med JavaScript, når de åbnes. En egen adresse virker uden JavaScript og kan linkes til.
### B116 – Fejl rettes ved at trække tilbage, ikke ved at slette (US-176)

- **Beslutning:**
  - **Risici og handlinger:**
    - En risiko, der er oprettet ved en fejl, kan trækkes tilbage med en begrundelse, mens den er åben. Den lukkes og markeres "Withdrawn", forsvinder fra registret og risikorapporten og bliver i historikken.
    - En accepteret eller lukket risiko kan genåbnes med en begrundelse og samme rettighed som at acceptere. En accepteret risiko skal så reduceres igen.
    - En handling på en risiko kan fjernes, og en story eller evidens kan tages af den, så længe handlingen ikke er udført.
  - **Scanninger:** en accepteret finding kan fortrydes og er åben igen.
  - **Security events:**
    - En security event, der er registreret ved en fejl, lukkes som "Recorded in error" med en begrundelse og bliver i tidslinjen.
    - Når en rapport er sendt til CSIRT, er det ikke muligt længere. Så tilføjes en rettelse i stedet.
  - **Låst:** låste rapporter og audit-pakker ændres aldrig.
  - **Log:** alt logges (`RiskWithdrawn`, `RiskReopened`, `RiskActionRemoved`, `RiskActionUnlinked`, `FindingAcceptanceUndone`, `SecurityEventRecordedInError`).
- **Begrundelse:** Man skal kunne rette en fejl uden en admin og uden at efterlade forkerte data i rapporterne. Historikken bevares (ISO/IEC 27001 A.5.33 og A.8.15, CRA art. 14).
- **Afvigelse:** at genåbne en accepteret risiko kræver ikke fire-øjne. Det gør risikoen åben og synlig igen og sænker ikke opmærksomheden.
- **Senere:** at trække en scanning, en SBOM, testresultater eller en kravspecifikations-version tilbage. Det kræver, at fundene rulles tilbage pr. scanning, og ligger i US-177.
### B117 – Enklere navigation og en "To review"-indbakke (US-169)

- **Beslutning:**
  - **Topbaren** har Projects, Frameworks, Organisation og "To review" (plus System for ejeren). Organisation er en menu med organisationens sider: Overview, Documents, Users, Activity og Settings.
  - **Projektets 11 faner** ligger i 6 grupper:
    - Overview (med NIS2 scope, Suggestions og Activity);
    - Requirements (Compliance, Product requirements og Frameworks);
    - Evidence, Risks og Reports;
    - Security (Security events og Scans).
  - **En gruppe med flere faner** er en menu og viser sine faner som en række under sig, når man står i den. Alle faner beholder deres adresse, så gamle links virker.
  - **"To review"** viser pr. projekt alt, der venter på medlemmets beslutning, og kun det, medlemmet må beslutte. Det gælder AI-forslag, markeringer, udkast, tjek og godkendelser. Hver linje linker til der, hvor det besluttes. Lukkede projekter er udeladt.
- **Begrundelse:** 11 faner og 8 menupunkter gjorde siderne svære at overskue. AI-forslagene var spredt på flere faner, så man nemt overså noget, der ventede på en person (AI Act art. 14).
- **Fravalgt:**
  - Antallet i topbaren, fordi det ville koste forespørgsler på hver side. Det står på "To review"-siden.
  - At flytte fanernes indhold, som kommer med designreglerne (US-168).

### B119 – Audit-pakken tjekkes, før den laves (US-178)

- **Beslutning:**
  - **Tjeklisten:** "Check the package first" på fanen Rapporter viser pr. framework, hvad en auditor typisk beder om, og om det findes.
    - **ISO/IEC 27001:** 4.3 scope, 5.2 politik, 6.1.2 risikovurdering, 6.1.3 risikobehandling og SoA, 9.2 intern audit og 9.3 ledelsens gennemgang. Hvert punkt findes som gyldig evidens på kravet med den kode i organisationens kopi. 9.2 er også dækket af en audit inden for 12 måneder.
    - **CRA:** SBOM, supportperiode, kontakt for sårbarheder og risikovurdering (risikoregistret).
    - **Alle frameworks:**
      - gældende krav uden gyldig evidens (de første 10 nævnes, resten tælles);
      - evidens, der udløber inden for 60 dage;
      - ændringer, der venter på gennemsyn.
  - **"Fix":** hvert punkt, der mangler, linker til der, hvor det rettes.
  - **Pakken laves alligevel.** Tjekket ligger i den som `00-check.txt`.
  - **Ikke relevante krav** tæller ikke som manglende, men står med begrundelsen.
- **Begrundelse:** Hullerne skal findes før auditten og ikke under den (ISO/IEC 27001 9.2, CRA Annex VII).
- **Afvigelse:** "udløber i audit-perioden" er blevet til "udløber inden for 60 dage". Pakkens periode slutter senest i dag, så det er de kommende uger, der betyder noget.
- **Fravalgt:**
  - At stoppe pakken, når noget mangler, fordi en auditor også skal se hullerne.
  - At læse indholdet af dokumenterne. Det gør AI'ens evidens-tjek (US-038).

### B120 – Audit-pakken sorteres pr. krav (US-179)

- **Beslutning:**
  - **En mappe pr. krav:** hvert krav i de valgte frameworks får mappen `<framework>/<kode>/`, fx `CRA/I.2(d)/`, med en `README.txt`. Den viser kravets tekst, status, koblede user stories, fejlende tests og hver evidens med sti i pakken (eller link), SHA-256 og kilde.
  - **Sikre navne:** tegn, som Windows, macOS eller Linux ikke tillader (`/ \ : * ? " < > |`), bliver til `-`. Parenteser beholdes. To koder, der giver samme mappe, får et nummer.
  - **Filer gemmes én gang** under `evidence/`. Kravenes README'er peger på dem, så pakken ikke vokser, når en fil dækker flere krav. To filer med samme navn får et nummer.
  - **Indeks:** `index.html` (virker offline, ingen scripts, relative links) og `index.xlsx` med en række pr. evidens.
  - **Fingeraftrykket** dækker alle filer, også mapperne og indekset.
- **Begrundelse:** En auditor arbejder krav for krav. Med en mappe pr. krav og et indeks finder hen beviset uden at søge i én lang liste.
- **Fravalgt:**
  - At kopiere filerne ind i hver kravmappe. Det gør pakken større, og samme fil får flere stier.
  - Genveje (symlinks) i ZIP-filen. Windows' egen udpakning håndterer dem ikke.

### B121 – En konsulent kan arbejde for flere kunder (US-187)

- **Beslutning:**
  - **Ny rolle "Consultant":** må det samme som en Compliance manager, men ikke styre medlemmer eller AI-deling.
  - **Flere organisationer:** en person, der allerede er medlem et sted, kan kun komme med i en anden organisation som Consultant eller Auditor. En medarbejder hører stadig kun til én organisation (B16).
  - **Valgt organisation:** gemmes i cookien `ct_org` (HttpOnly). Serveren bruger den kun, når personen er medlem dér. En ændret cookie viser derfor aldrig fremmede data, og skift til en fremmed organisation giver 404.
  - **Standard:** personens egen organisation (en rolle, der ikke er ekstern), ellers den første efter navn.
  - **Adgang og log:** kun kundens organisation-admin kan invitere og fjerne konsulenten. Kunden kan gøre konsulenten læse-kun ved at skifte rollen til Auditor. Alt, konsulenten gør, logges i kundens log med rollen. Login og logud logges i hver organisation.
  - **"My customers":** viser hver organisation med dækning, åbne huller og næste udløb af evidens. Hver organisation læses i sit eget scope med de samme forespørgsler og tjek som på dens egne sider.
- **Begrundelse:** En konsulent laver gap-analyser for mange kunder og skal ikke have en konto pr. kunde (ISO/IEC 27001 A.5.15, A.5.18).
- **Afvigelse:** "Next deadline" er blevet til "næste udløb af evidens". Fristerne for fund og indberetninger kommer med påmindelserne (US-183).
- **Fravalgt:** at konsulenten selv opretter kunder. Det står som åbent spørgsmål i issuet.

### B122 – NIS2 gap-analyse fra organisationens dokumenter (US-188)

- **Beslutning:**
  - **Siden "NIS2 gap analysis"** ligger under Organisation i topmenuen. US-189 og US-190 bygger videre på den.
  - **Upload:** en ZIP eller flere filer, højst 200 filer og 200 MB ad gangen. Mapper i ZIP'en flades ud, så kun filnavnet bruges. Systemets egne filer (`__MACOSX`, `.DS_Store`) springes over.
  - **Hver læsbar fil** bliver et af organisationens dokumenter (US-136): den gemmes én gang med SHA-256, har version v1, og typen gættes ud fra filnavnet (politik, procedure, risikotabel eller andet).
  - **Word og Excel** (.docx og .xlsx) accepteres nu også som evidens. Filens indhold skal stemme overens med endelsen, og teksten læses med OpenXml.
  - **Filer, der ikke kan læses,** står på listen med grunden: forkert format, over 10 MB, en ZIP i en ZIP eller en ødelagt ZIP.
  - **Samme fil igen** genkendes på SHA-256 og gemmes ikke en gang til.
  - **AI'ens forslag** laves som for dokumenter i dag (embeddings). Til hvert forslag gemmes den sætning fra dokumentet, der deler flest ord med kravet, og den vises som citat.
  - **Kun forslag** tæller ikke. Et krav er først dækket, når en person godkender forslaget.
- **Begrundelse:** Det meste af en gap-analyse er at finde ud af, hvilke dokumenter kunden allerede har. Det kan AI'en forberede, mens mennesket beslutter (AI Act art. 14).
- **Fravalgt:**
  - At lade sprogmodellen skrive begrundelsen. Det er langsomt med den lille lokale model, og et citat fra dokumentet kan efterprøves.
  - At læse billeder med OCR.

### B123 – Spørgsmål til det, dokumenterne ikke viser (US-189)

- **Beslutning:**
  - **16 faste spørgsmål** i almindeligt sprog: et for hver NIS2-foranstaltning i art. 20, art. 21(2)(a)–(j), 21(3), 21(4) og art. 23. Spørgsmålene er bygget ind ligesom NIS2-kravene og gemmes ikke i databasen.
  - **Modenhed 0–4:** 0 ingen, 1 ad hoc, 2 defineret, 3 implementeret, 4 målt og forbedret. Et svar er en vurdering og kræver derfor DecideCompliance. Hvert svar logges.
  - **Spørgsmål, der allerede er dækket** af godkendt evidens eller links i organisationsområdet, foldes væk, men kan stadig åbnes.
  - **"Needs evidence":** et svar på 3 eller mere, uden at noget godkendt beviser det.
  - **Svar tilføjes kun.** Et nyt svar erstatter det gamle, som gemmes med tidspunktet, hvor det blev erstattet (tabellen `GapAnswers`).
- **Begrundelse:** Ikke alt står i dokumenter. Resten skal besvares på samme måde hver gang, så næste vurdering kan sammenlignes (ISO/IEC 27001 9.1).
- **Fravalgt:** at sende spørgeskemaet til personer uden for trackeren.

### B124 – NIS2 gap-rapport med handlingsplan (US-190)

- **Beslutning:**
  - **Status pr. foranstaltning:**
    - **Opfyldt (Met):** godkendt evidens og modenhed 3 eller mere. Er spørgsmålet ikke besvaret, fordi evidensen var nok, tæller det også som opfyldt.
    - **Delvist opfyldt (Partly met):** evidens med lavere modenhed, eller modenhed 2 eller mere uden evidens.
    - **Mangler (Missing):** resten.
  - **Handlingsplan:** et forslag til første handling for hver foranstaltning. Prioriteten er "High", når foranstaltningen mangler, eller når en åben høj risiko rammer kravet. Ellers er den "Medium". Kolonnerne til ansvarlig og frist står tomme, så de kan udfyldes.
  - **Rapporten** er en låst PDF med SHA-256 (rapporttypen `nis2-gap`). Første side har felter til ledelsens godkendelse (navn, dato og underskrift, NIS2 art. 20(1)). Alle medlemmer kan lave den, også en Auditor.
  - **Ændringer:** status for hver foranstaltning gemmes i rapportens Figures. Næste rapport og siden viser, hvad der er blevet bedre, det samme eller værre.
  - **"Make it a risk":** åbner risikoformularen i organisationsområdet med titel og beskrivelse udfyldt.
- **Afvigelse:** en handling kan ikke blive til en user story. Organisationsområdet har ingen user stories, så det bliver en risiko i stedet.
- **Fravalgt:** rapporten som Word-fil. Det står som åbent spørgsmål i issuet.

### B125 – Underleverandører, og at de overholder reglerne (US-191)

- **Beslutning:**
  - **Siden "Suppliers"** ligger under Organisation. For hver underleverandør registreres:
    - navn og hvad den leverer;
    - kritikalitet (High, Medium eller Low);
    - kontakt;
    - de regler, den skal overholde for os. De vælges fra en fast liste: NIS2 art. 21(2)(d), GDPR art. 28, ISO/IEC 27001 A.5.19–A.5.22, CRA art. 13(5) og DORA art. 28.
  - **Evidens:** certifikat, databehandleraftale, sikkerhedsspørgeskema, auditrapport, kontrakt eller andet, med eller uden slutdato. Filen kontrolleres som al anden evidens og gemmes i sin egen tabel.
  - **Status:**
    - **Low** kræver ingen evidens.
    - **High og Medium** er "Missing" uden gyldig evidens. Udløbet evidens nævnes ved navn.
    - **"Expires soon"**, når al gyldig evidens slutter inden for 30 dage.
  - **CRA:** antallet af komponenter i projekternes nyeste SBOM'er, hvis leverandørfelt nævner underleverandøren.
  - **Gap-rapporten** (US-190) får et ark med underleverandørerne og deres status.
  - **Rettigheder:** at registrere og tilføje evidens kræver DecideCompliance. Alle medlemmer kan se listen.
- **Begrundelse:** NIS2 art. 21(2)(d) og 21(3) kræver, at forsyningskæden vurderes. ISO/IEC 27001 A.5.19–A.5.22 kræver aftaler og opfølgning.
- **Afvigelse:** der sendes endnu ingen påmindelse før en slutdato. Det kommer med påmindelserne i US-183.
- **Fravalgt:** en portal, hvor underleverandøren selv uploader.

### B126 – Påmindelser på e-mail før frister (US-183)

- **Beslutning:**
  - **Typer af påmindelser:**
    - evidens, der udløber;
    - audit-fund, der nærmer sig fristen (til fundets ansvarlige);
    - godkendelser, der venter (til dem, der må godkende, undtagen den, der bad om det);
    - frister for at indberette efter CRA og NIS2 (early warning, notification og final report);
    - evidens fra underleverandører, der udløber (US-191).
  - **Modtagere:** medlemmer med DecideCompliance. Ved indberetninger får den, der registrerede hændelsen, også besked.
  - **Hvornår:** et baggrundsjob kører hver time, så en frist på 24 timer ikke overses. Påmindelser sendes et antal dage før fristen. Standard er 30 dage; organisation-admin vælger mellem 1 og 90. En frist, der allerede er overskredet, giver én påmindelse.
  - **Én påmindelse pr. person, type, emne og frist** (tabellen `Reminders`). En ændret frist giver en ny.
  - **Mailen** nævner kun typen, fristen, organisationen, projektet og et link. Den indeholder aldrig titler, indhold eller tekst fra user stories.
  - **Når en mail fejler,** gemmes fejlen, og der prøves igen ved næste kørsel, højst 5 gange. Jobbet logger antallet af fejl.
  - **Uden mailserver** (`Mail:Host` og `Mail:From`) laves og sendes intet, og siden "Reminders" siger det.
  - **Mailserveren:** .NET's egen SMTP-klient, så der ikke skal installeres en ekstra pakke, og enhver SMTP-server kan bruges. Adgangskoden sættes uden for repo'et, fx `Mail__Password`.
  - **Den enkeltes valg:** hver person vælger selv sine typer på siden "Reminders". Frister for indberetning kan ikke slås fra.
- **Begrundelse:** Udløbet evidens, glemte fund og overskredne frister for indberetning er de huller, der koster mest i en audit. CRA art. 14 og NIS2 art. 23 kræver indberetning inden for 24 og 72 timer.
- **Fravalgt:**
  - SMS, Teams og Slack.
  - At indberette til CSIRT/ENISA fra trackeren.

### B127 – Seks livsfaser med tjeklister pr. regulativ (US-047)

- **Beslutning:**
  - **Seks faser:** Planning, Design, Development, Verification, Released og EndOfSupport. Design og Verification er nye. Vedligehold hører under Released, fordi det er perioden, hvor produktet er på markedet og supporteres. Faserne gemmes som tekst, så eksisterende projekter beholder deres fase.
  - **Tjekliste pr. fase:** et indbygget katalog med punkter for CRA, ISO/IEC 27001 og AI Act. Et punkt vises, når projektet følger dets regulativ. Hvert punkt har et mærke med regulativ og reference, fx "CRA Annex I Part II(1)". Punkter, der ligner hinanden i to regulativer, vises begge og afkrydses hver for sig.
  - **To slags punkter:**
    - **Automatiske**, som trackeren selv afgør ud fra sine data: scope, klassificering, risici, dækning, evidens, scanning, SBOM, supportperiode og opbevaring.
    - **Manuelle**, som en person, der må redigere projektet, krydser af med en kommentar og et link til evidens.
  - **Til- og fravalg:** et punkt kan slås fra med en begrundelse, og så tæller det ikke som manglende. Valgfrie tests (pentest, fuzzing, SAST, DAST og load test) er slået fra, indtil projektet vælger dem.
  - **Faseskift med mangler** er tilladt. Formularen advarer tydeligt, og skiftet logges som `PhaseChangedWithItemsMissing` med det, der manglede. De eksisterende faseporte fra US-071 og US-072 virker som før (B89) og kræver stadig en begrundelse for deres egne tjek.
  - **Rettigheder:** at ændre punkter kræver EditProjects.
  - **Historik:** hver ændring logges som `ChecklistItemChanged`. Tabellen `ProjectChecklistEntries` gemmer den nuværende stand.
  - **Forespørgsler:** oversigten henter kun de fakta, den aktuelle fase og porten skal bruge (US-139).
- **Begrundelse:** CRA art. 13 og ISO/IEC 27001 A.8.25–A.8.29 stiller krav i hver fase. Med et mærke på hvert punkt kan man se, hvilket regulativ det kommer fra.
- **Fravalgt:** at blokere et faseskift.

### B128 – Organisationens egne tjeklistepunkter (US-192)

- **Beslutning:**
  - **Siden "Checklists"** ligger under Organisation. Den viser de indbyggede punkter pr. fase, som kun kan læses, og organisationens egne punkter.
  - **Et eget punkt** har en fase, en tekst og en kilde: enten "Own policy" eller et af organisationens frameworks. Det kan have en reference, fx "ISMS-PRO-12".
  - **Hvor punktet vises:** et punkt med "Own policy" vises i alle projekter i fasen. Et punkt bundet til et framework vises kun i de projekter, der følger det framework.
  - **Afkrydsning:** egne punkter krydses af ligesom de indbyggede manuelle punkter (US-047). De tæller med i advarslen ved faseskift.
  - **Fjernede punkter** vises ikke længere, men afkrydsninger og log bevares.
  - **Rettigheder:** at tilføje, rette og fjerne kræver ManageFrameworks. Alt logges.
  - **Forespørgsler:** oversigten laver én fast forespørgsel mere for de egne punkter, så grænsen er hævet fra 52 til 53 (US-139).
- **Begrundelse:** Organisationens egne regler for sikker udvikling (ISO/IEC 27001 A.5.1, A.8.25) skal følges i hvert projekt på samme måde som regulativernes.

### B129 – Livsfasen og tjeklisten i rapporterne (US-194)

- **Beslutning:**
  - **Sporbarhedsrapporten:** resuméet viser fasen og "x af y gjort" i den nuværende fase, også i ledelsesrapporten. Team- og auditor-udgaven får to nye ark:
    - **"Lifecycle checklist":** hvert punkt i hver fase med regulativ, status og hvem, hvornår eller hvorfor.
    - **"Phase changes":** hvert faseskift med det, der manglede på det tidspunkt.
  - **Den tekniske dokumentation:** fasen, tjeklisterne og faseskiftene står i afsnit 2 (design, udvikling og håndtering af sårbarheder). Afsnit 1–8 følger stadig CRA Annex VII.
  - **Audit-pakken** får `lifecycle.txt`.
  - **Audit-tjekket** (US-178) får en gruppe "Lifecycle" med de punkter, der mangler i den nuværende fase, og et link til tjeklisten.
  - **Faseskiftene** læses fra aktivitetsloggen, hvor hvert `ProjectPhaseChanged` parres med den `PhaseChangedWithItemsMissing`, der blev logget samtidig.
  - **Valgfrie tests,** som ingen har valgt, kommer ikke med.
  - **Forespørgsler:** sporbarhedsrapporten genbruger den overholdelse, den allerede har slået op. Resten er faste forespørgsler, så grænsen er hævet fra 53 til 63 (US-139).
- **Begrundelse:** Ledelse og auditor skal kunne se, hvor produktet er i sin livscyklus, og hvad der manglede, da det gik videre (CRA Annex VII, ISO/IEC 27001 9.2 og 9.3).

### B130 – Standarddatoer for gennemgang og gyldighed (US-195)

- **Beslutning:**
  - **Standardværdier pr. slags dato** ligger på organisationen i måneder: evidens 12, politikker og procedurer 12, evidens fra underleverandører 12 og audit-certifikater 36. Organisation-admin retter dem under Settings, fra 1 til 60 måneder. En ændring rører ikke datoer, der allerede er sat.
  - **Formularerne** udfylder datoen som i dag plus standarden og viser standarden som hint. Det gælder evidens på evidensfanen og på kravets side, nye dokumenter og gennemgang af dokumenter samt evidens fra underleverandører. Certifikatet på en audit er valgfrit, så dets dato vises kun som hint og udfyldes ikke.
  - **Uden dato:** man kan godt gemme uden dato, men får advarslen "No review date: it will never remind anyone".
  - **Overblik:** "To review" har en linje "Without a date" pr. projekt. I organisationsområdet tæller den også dokumenter og evidens fra underleverandører med kritikalitet High eller Medium. Audit-tjekket har linjen "Evidence has an end date".
  - **Gennemgangsdato i fortiden:** en sådan dato for et dokument afvises, fordi den aldrig ville minde nogen om noget.
  - **Rettelse i tjeklisten (US-047):** punktet "Every covered requirement has evidence" tæller først som gjort, når mindst ét krav er dækket.
- **Begrundelse:** Påmindelserne (US-183) virker kun med en dato. ISO/IEC 27001 kræver gennemgang med planlagte mellemrum (5.1, 7.5.3, 8.2).
- **Afvigelse:** risici har ingen datoformular. Deres gennemgang følger stadig de faste 12 måneder (US-042).

### B131 – Tidsbegrænset læseadgang for en ekstern auditor (US-182)

- **Beslutning:**
  - **Tildeling:** et medlem med DecideCompliance giver adgang på projektets Reports-fane. Adgangen gælder ét projekt, fra i dag eller senere, og højst 30 dage.
  - **Invitationen:** tildelingen sker gennem en invitation (US-009) med rollen Auditor. Linket vises én gang og sendes også med mail, når en mailserver er sat op (US-183). Når auditoren tager imod, bliver medlemskabet låst til projektet og perioden (`OnlyProjectId`, `AccessFrom`, `AccessUntil` på brugeren).
  - **Kun i perioden:** medlemskabet tæller kun i perioden. Bagefter siger trackeren "Your access to … has ended".
  - **Låsningen:** et globalt filter i web-laget tillader kun GET-sider og -downloads på Projects og Audits med det tildelte projekts id. Forsiden og projektlisten fører til projektet, og alt andet giver 403. Hver side og download logges som `AuditorAccessUsed`. Rollen Auditor har i forvejen ingen rettigheder, så heller ikke use cases tillader ændringer.
  - **Tilbagekaldelse** virker med det samme: medlemmet fjernes, og en invitation, der ikke er brugt, tilbagekaldes.
  - **Topmenuen:** en låst auditor ser kun "Read-only audit access" i topmenuen.
- **Begrundelse:** Auditoren kan se de levende data i stedet for ZIP-filer, og adgangen er begrænset, udløber af sig selv og logges (ISO/IEC 27001 A.5.15, A.5.18, A.8.15).
- **Afvigelse:** beskeden efter udløb nævner organisationen, ikke projektet.

### B132 – Eksport af alle data og lukning af organisationen (US-184)

- **Beslutning:**
  - **Eksport:** "Export everything" under Settings, kun for organisation-admin. Det giver én ZIP-fil:
    - hver tabel, der hører til organisationen, som JSON (`data/`);
    - hver gemt fil for sig (`files/<tabel>/<id>-<kolonne>`);
    - en indholdsside med SHA-256 for hver fil.
    Tabellerne findes ud fra EF-modellen: en række hører til organisationen gennem dens id, et af dens projekter eller en fremmednøgle til en række, der gør. Så glemmes en ny tabel aldrig. En nøgle til Users bruges kun, når intet andet fører til organisationen.
  - **Lukning:** organisation-admin skriver organisationens navn for at bekræfte. Medlemmerne får en mail, når en mailserver er sat op. Alle medlemmer ser en rød besked med datoen. Lukningen kan fortrydes i 30 dage.
  - **Efter de 30 dage** sletter det timelige job alle organisationens data i én transaktion. Organisationens række og medlemmerne anonymiseres i stedet for at blive slettet, fordi den beskyttede aktivitetslog (US-091) peger på dem og bevares uden navne.
  - **Låste rapporter** (US-050) kan stadig aldrig ændres. Triggeren tillader nu kun sletning, når transaktionen navngiver netop den organisation, der lukkes (`ct.closing_organisation`).
  - **Indbyggede frameworks og andre organisationer** røres aldrig.
- **Begrundelse:** GDPR art. 17 og 20 og ISO/IEC 27001 A.5.34 og A.8.10. En kunde, der stopper, skal kunne få sine data med og have dem slettet.
- **Afvigelse:** mens lukningen står på, kan der stadig ændres i data. Den røde besked advarer om lukningen.
- **Backups:** se docs/backup-og-gendannelse.md.

### B133 – Tjeklistepunkter for frameworks, trackeren ikke kender i forvejen (US-193)

- **Beslutning:**
  - **Forslag:** for et framework, som organisationen selv har uploadet, sammenligner AI'en hvert krav i den aktive version med en kort beskrivelse af hver af de seks faser. Den bruger de samme embeddings som de øvrige forslag. Den bedste fase over grænsen for evidens-forslag (US-105) bliver et forslag med kravets titel som tekst. Krav, der ikke passer til nogen fase, springes over.
  - **Godkendelse:** et menneske godkender med den fase og tekst, det vælger, eller afviser (AI Act art. 14). Et godkendt forslag bliver et af organisationens egne punkter (US-192) med framework og kravkode som kilde, fx "Customer requirements SR-12". Det vises i de projekter, der følger frameworket.
  - **Ny version:** når man beder om forslag igen, fjernes de punkter, hvis krav er forsvundet i den nyeste version, og kun nye krav foreslås. Et afvist forslag foreslås ikke igen.
  - **Indbyggede frameworks** (CRA, ISO/IEC 27001, AI Act) har deres egne punkter fra US-047 og får ikke forslag.
  - **Siden:** alt ligger på Checklists-siden.
- **Begrundelse:** Alle frameworks, et projekt følger, skal kunne komme med i faserne (ISO/IEC 27001 A.5.31 og A.8.25).
- **Fravalgt:**
  - At lade sprogmodellen formulere punktet. Kravets titel kan rettes ved godkendelsen.
  - At foreslå automatisk ved hver upload. Man beder selv om forslag.

### B134 – Invitation med e-mail og slutdato fra Users-siden (US-196)

- **Beslutning:**
  - **Ét sted:** alle invitationer laves på Users-siden med e-mail og rolle. For konsulent og revisor kan man sætte en slutdato, og for revisor også ét projekt. Linket sendes med det samme, når der er en mailserver (US-183), og vises altid én gang for admin.
  - **Kun den inviterede:** en invitation med e-mail kan kun bruges af den, der er logget ind med den e-mail. E-mailen gemmes med små bogstaver.
  - **Adgangen slutter af sig selv:** den starter den dag, invitationen accepteres, og slutter efter slutdatoen, som revisoradgangen i US-182. Admin kan flytte slutdatoen på Users-siden; ændringen logges. En medarbejderrolle har ingen slutdato, og et rolleskift til en medarbejderrolle fjerner den.
  - **Samme person to gange:** en ny invitation til en e-mail, der allerede har en åben invitation, afvises, indtil den første er trukket tilbage.
  - **Gamle links:** invitationer uden e-mail (fra før US-196) virker som før. Formularen kræver e-mail.
- **Begrundelse:** Adgang gives ét sted, til en navngiven person, og en ekstern persons adgang slutter af sig selv (ISO/IEC 27001 A.5.15, A.5.16, A.5.18 og A.5.19).
- **Fravalgt:**
  - At gøre e-mail obligatorisk i domænet. Det ville ugyldiggøre åbne links fra før US-196.
  - At lade medarbejdere få en slutdato. Det står som et åbent spørgsmål i US-196.
### B135 – Revisionspakken låser sine rapporter og sammenligner med den forrige pakke (US-198)

- **Beslutning:**
  - **Låst med pakken:** når en revisionspakke laves, låses hver rapport i den også (US-050) med sit fingeraftryk. Det gælder sporbarhed pr. framework, ledelsesrapporten, Statement of Applicability og risikovurderingen. Rapporten peger på pakken og vises under den på Reports-fanen, ikke igen for sig selv.
  - **Since last:** ledelsesrapporten i pakken sammenligner med den nyeste tidligere låste ledelsesrapport, der deler mindst ét regulativ. Det kan også være en fra en tidligere pakke. Uden en tidligere står der "First package: nothing to compare with".
  - **Nyt og fjernet:** et framework, der er kommet til siden sidst, står som "New". Et, der ikke længere følges, står som "No longer followed".
  - **Uden for pakken** gælder US-141 som før: kun en rapport med præcis de samme regulativer sammenlignes.
- **Begrundelse:** Ændringen siden sidste revision vises uden ekstra trin, og hver rapport i en pakke bevares uændret (ISO/IEC 27001 9.2, 9.3 og A.5.33).
- **Fravalgt:**
  - At sammenligne evidensfiler mellem pakker. Det står uden for US-198.

### B136 – Revisionspakken som ét forseglet PDF-dossier pr. framework (US-197)

- **Beslutning:**
  - **Ét dossier pr. framework:** pakken har fx `CRA-dossier.pdf` med hvert krav: tekst, status, user stories, fejlende tests og evidens med titel, sti i pakken, SHA-256 og kilde. Til sidst kommer frameworkets punkter fra livscyklus-tjeklisten. Mapperne med `README.txt` (US-179) udgår; `index.html` og `index.xlsx` bliver, og koden i indekset åbner dossieret.
  - **Pakkens egne tekster er PDF:** `00-contents.pdf`, `00-check.pdf` og `lifecycle.pdf` i stedet for `.txt`.
  - **Segl:** har organisationen et gyldigt segl (US-132), forsegles hver PDF, pakken selv laver: dossierne, rapporterne, indholdsfortegnelsen, tjekket og livscyklussen. Det sker, før indholdsfortegnelsen skriver deres SHA-256. Uden segl laves de uforseglede, og indholdsfortegnelsen og dossieret siger det.
  - **Evidensfilerne** lægges som de blev uploadet, under `evidence/`, med deres SHA-256.
  - **Låste rapporter (US-198)** gemmes uforseglede i trackeren; pakken har de forseglede.
- **Begrundelse:** En revisor læser ét dokument pr. framework og kan se, at det ikke er ændret, efter det blev lavet (ISO/IEC 27001 7.5.3 og A.5.33, CRA bilag VII, eIDAS art. 35–36).
- **Fravalgt:**
  - At signere selve ZIP-filen. Det er US-180.
  - At konvertere de uploadede evidensfiler til PDF. Originalerne bevares.

### B137 – En signeret revisionspakke, som modtageren kan kontrollere (US-180)

- **Beslutning:**
  - **Manifest:** hver pakke har `manifest.json` med hver fils sti og SHA-256 samt pakken, organisationen, projektet, hvem der lavede den og hvornår. Stierne er dem, filerne har i ZIP-filen.
  - **Signatur:** har organisationen et gyldigt segl (US-132), signeres manifestet som en løsrevet CMS-signatur (PKCS #7, SHA-256) i `manifest.json.p7s`. Den kan også kontrolleres med OpenSSL uden trackeren. Uden segl er pakken usigneret, og indholdsfortegnelsen siger det.
  - **Kontrolsiden:** "Verify an audit package" kræver ikke login. Filen læses i hukommelsen og gemmes ikke. Pakker over 200 MB, og pakker der pakker sig ud til mere end 2 GB, afvises. Den fælles grænse for uploads pr. IP-adresse gælder (US-097).
  - **Resultat:** "Unchanged" kun når signaturen er gyldig og alle filer passer. Ellers vises hver fil, der er ændret, mangler eller er kommet til. Siden viser, hvem der har signeret, efter certifikatet, ikke efter manifestets eget navn.
- **Begrundelse:** En revisor eller myndighed kan stole på pakken uden at spørge organisationen om hver fil (ISO/IEC 27001 A.5.33 og A.8.24, CRA art. 13 og bilag VII).
- **Fravalgt:**
  - Tidsstempel fra en ekstern tjeneste. Det står uden for US-180.
  - At kontrollere certifikatets kæde. Seglet kan være organisationens eget certifikat.

### B138 – CRA's tekniske dokumentation og EU-overensstemmelseserklæring i revisionspakken (US-181)

- **Beslutning:**
  - **Hvornår:** en pakke, der dækker CRA, har `CRA-technical-documentation.pdf` (udkastet fra US-056) og `CRA-declaration-of-conformity.pdf`. Andre pakker har ingen af dem. Den tekniske dokumentation låses også med pakken (US-198).
  - **Erklæringen** følger bilag V punkt 1–8. Den udfyldes med produktets navn, version (den nyeste SBOM), formål, producentens navn, CVR og land, klassifikationen og de øvrige frameworks, projektet følger. Det, trackeren ikke kender, står som "To be completed", fx adressen, der ikke findes i trackeren endnu. Pakkens tjek (`00-check.pdf`) lister de manglende felter.
  - **Procedure efter klasse (art. 32):**
    - Default: modul A, eller B+C eller H.
    - Vigtig klasse I: B+C eller H, eller harmoniserede standarder med modul A.
    - Vigtig klasse II: B+C eller H, eller en europæisk cybersikkerhedscertificeringsordning.
    - Kritisk: et europæisk cybersikkerhedscertifikat, hvor art. 8, stk. 1 kræver det; ellers som klasse II.
    - Uden klassifikation: "To be completed: classify the product".
  - **Aldrig signeret:** begge er markeret som udkast, der skal gennemgås og signeres af producenten, og signaturfeltet siger "not signed (draft)".
- **Begrundelse:** En revisor eller myndighed får de dokumenter, CRA nævner, og producenten ser, hvad der mangler (CRA art. 28 og 31, bilag V og VII).
- **Fravalgt:**
  - At signere erklæringen i trackeren. Producenten gør det uden for trackeren.
  - At gætte på adresse eller bemyndiget organ.
### B139 – Restrisiko efter foranstaltningerne (US-165)

- **Beslutning:**
  - **Niveauet før foranstaltninger** (sandsynlighed × konsekvens) ændres aldrig af foranstaltningerne. Restrisikoen sættes af et menneske på risikosiden med "Set residual risk", og ændringen logges som `RiskResidualSet` med gammel og ny værdi.
  - **Ikke højere:** restrisikoen kan ikke være højere end risikoen før foranstaltningerne. Det gælder også i redigeringsformularen.
  - **Registeret** viser niveauet før og efter side om side, fx "High → Medium", og "–" uden restrisiko.
  - **"Residual to set":** en åben risiko, der skal reduceres, og hvor hver foranstaltning har en user story eller evidens bag sig (US-159), markeres, så længe ingen restrisiko er sat. Den tælles under "Needs attention".
  - **"Residual to review":** fjernes en foranstaltning, efter restrisikoen er sat, markeres den til gennemsyn, indtil den sættes igen.
  - **AI-forslag (R6) er ikke bygget endnu.** Trackeren har ingen generel sprogmodel-port til det, og det må bygges som en story for sig.
- **Begrundelse:** Risikoejeren tager stilling til restrisikoen, når behandlingen er på plads, og registeret viser, hvor meget den har sænket risikoen (ISO/IEC 27001 6.1.3 f og 8.3, CRA art. 13, stk. 2–3).
- **Fravalgt:**
  - At lade niveauet falde af sig selv, når foranstaltninger får evidens. En foranstaltning kan være dækket uden at virke.
### B140 – SBOM'en kommer af sig selv fra pipelinen eller GitHub (US-148)

- **Beslutning:**
  - **Pipeline:** `POST /api/projects/{id}/sboms` med projektets upload-token fra US-122. Det samme token virker til scanninger og SBOM'er og har sin egen grænse på 30 pr. time for SBOM'er. Versionen er påkrævet, og commit er valgfri. Et forkert eller spærret token giver 401, og intet gemmes.
  - **GitHub:** "Fetch SBOM from GitHub" på fanen Scans læser `GET /repos/{owner}/{repo}/dependency-graph/sbom` (SPDX 2.3) med projektets GitHub-token, kun læsning (B12). Den gemmes under versionen "GitHub (default branch)", så en ny hentning erstatter den viste (US-079 R2). Den planlagte synkronisering henter den højst én gang i døgnet pr. projekt. Har repositoriet ingen dependency graph, siger fanen det, og intet ændres.
  - **Kilde:** hver SBOM har sin kilde (Upload, Pipeline med tokenets navn, eller GitHub), og en pipelines commit gemmes. Importen logges som `SbomImported`, af systemet, når den kommer fra en pipeline eller fra den planlagte synkronisering.
- **Begrundelse:** Hver udgivelse får en opdateret SBOM uden manuelle trin (CRA bilag I, del II, punkt 1, og bilag VII; ISO/IEC 27001 A.5.21 og A.8.25).
- **Fravalgt:**
  - Et eget token kun til SBOM'er. Det samme token er lettere at drive, og det kan stadig kun uploade.

### B141 – Flere læsende kilder pr. projekt: GitHub, Azure DevOps og dokumenter (US-030, afløser B12)

- **Beslutning:**
  - **Kilder:** et projekt kan læse user stories fra en eller flere kilder på samme tid: GitHub, Azure DevOps (dev.azure.com) og uploadede dokumenter (US-032 til US-034). Hver work item viser sin kilde.
  - **Kun læsning:** trackeren skriver aldrig tilbage. For Azure DevOps sendes kun GET og WIQL-forespørgsler, som kun læser.
  - **Token:** tokens er skrivebeskyttede (Azure: "Work Items: Read") og gemmes krypteret pr. projekt med Data Protection (US-031). De vises aldrig igen og logges aldrig. Et token tjekkes med kilden, før noget gemmes.
  - **Azure DevOps læses** med en WIQL-forespørgsel på typerne User Story, Product Backlog Item og Requirement, eventuelt under en area path, og derefter feltlæsning i portioner på 200. Acceptkriterierne og beskrivelsen er HTML og gøres til ren tekst. En story uden acceptkriterier springes over med årsagen. Closed, Done, Resolved og Removed tæller som lukket.
  - **"Sync now"** og den planlagte synkronisering læser alle kilder, projektet er koblet til. Afviser Azure tokenet, beholdes work items, og siden siger "Sync failed: The token was refused by Azure DevOps".
- **Begrundelse:** Teams bruger forskellige værktøjer, og mange har både en agil backlog og en kravspecifikation. Adaptermønstret (B11) gør hver ny kilde til en ny adapter (ISO/IEC 27001 A.8.26, A.8.24, A.5.17 og A.8.15).
- **Fravalgt:**
  - Tovejssynkronisering.
  - Ét fælles token for hele trackeren.
  - Azure DevOps Server on-premises og Entra ID-login. De står uden for US-030.
### B142 – Fælles sidehoved og faste farver for status (US-168)

- **Beslutning:**
  - **Sidehovedet:** hovedsiderne bygges med `<page-header>`: et link tilbage, titlen som h1 med status, og handlingerne til højre med højst én primær knap. Projekt, risiko og dokumenter bruger det; kravsiden har sin egen header under projektets.
  - **Status:** en status vises med `<status-badge>` og har faste farver fra ét sted (`StatusBadges`). Grøn er dækket eller færdig, gul er review, orange er "Needs action" og "High", rød er "Not covered", "Missing" og "Critical", grå er "Not applicable", og teal er AI. Orange er den eneste nye farve og har en kontrast på 10,5:1.
  - **Reglerne** står i `docs/design-rules.md` og er linket fra README.
- **Begrundelse:** Siderne genkendes, og det vigtigste ses først. Det mindsker risikoen for forkerte beslutninger og klik (ISO/IEC 27001 A.8.25 og A.8.26).
- **Fravalgt:**
  - Et nyt tema eller nye farver ud over orange. Det står uden for US-168.
  - At ændre test-id'erne. Komponenterne beholder sidernes id'er og klasser.

### B143 – Følg ét krav til ende (US-200)

- **Beslutning:**
  - **Trinlisten:** kravets side viser de trin, kravet mangler, i rækkefølge. For hver koblet story: at linket er bekræftet (efter en ny version), at storyen er færdig, og at den har evidens. Derefter at testene består, og at kravet selv har evidens. Hvert trin står som "Done" eller "Open", med hvad der mangler.
  - **Fremdrift:** siden siger fx "2 of 6 steps done". Når alle trin er færdige, står der "Requirement complete".
  - **Næste trin:** det første åbne trin, som kan klares i trackeren, er mærket "Next step". Et link ud til GitHub eller Azure DevOps bliver kun næste trin, når intet andet er åbent.
  - **Én primær knap:** "Add evidence" i kravets header forbliver den eneste primære knap (B142, US-153). Næste trin og "Next requirement" er markerede, men ikke primære.
  - **Næste krav:** "Next requirement" følger rammens rækkefølge og det filter, siden blev åbnet med. Knappen fremhæves først, når kravet er færdigt.
  - **Krav, der ikke gælder,** har ingen trin. I organisationsområdet er evidensen det eneste trin.
- **Begrundelse:** Med flere stories, tests og evidens på ét krav skal det være tydeligt, at alt kan gøres færdigt, før man går videre. Det mindsker risikoen for huller i dokumentationen (ISO/IEC 27001 A.8.25).
- **Fravalgt:**
  - At gemme trinnene. De beregnes af det, der allerede findes.
  - At lukke en story fra trackeren. Storyen lukkes i sin kilde.

### B144 – "My tasks": frister og beslutninger på én side (US-099, ændrer B117)

- **Beslutning:**
  - **Én side:** "To review" (US-169) bliver til "My tasks". Øverst står frister på tværs af projekterne: evidens, der er udløbet eller udløber inden for 7 dage, risici, der skal reviewes, mine egne risici, rapporteringsfrister for sikkerhedshændelser og mine audit-fund. Nedenunder står det, der venter på min beslutning, som før.
  - **Rækkefølge og filter:** fristerne sorteres efter dato, den tidligste først. Siden kan filtreres på projekt og type. Hver linje siger fx "Overdue by 3 days" og linker til der, hvor opgaven løses. En risiko åbner i sidepanelet (US-147).
  - **Rettigheder:** en opgave, som medlemmets rolle ikke kan løse, vises uden link med "Your role can only read this". Det gælder fx auditoren. Lukkede projekter er udeladt, og et medlem med adgang til ét projekt (US-182) ser kun det.
  - **Tallet i topbaren:** "My tasks (3)" tæller det, der er overskredet eller forfalder inden for 7 dage. Tallet hentes, efter siden er vist, og gemmes i fanen i 5 minutter. Så venter ingen side på det, og der spørges ikke ved hvert sideskift. Det ændrer fravalget i B117.
- **Begrundelse:** Den, der arbejder på flere projekter, skal ikke åbne hvert projekt for at finde det, der haster. Overskredne reviews og frister ses og handles på (ISO/IEC 27001 9.1 og A.5.1, CRA bilag I, del II (2)).
- **Fravalgt:**
  - Mails. De sendes af påmindelserne (US-183).
  - At tildele opgaver til andre medlemmer. Det står uden for US-099.
  - En ny side ved siden af "To review". To lister med næsten det samme ville forvirre.

### B145 – Søgning med PostgreSQL's fuldtekstsøgning (US-100)

- **Beslutning:**
  - **Søgefeltet** står i topbaren. Søgningen finder krav, work items, evidens, risici og projekter i medlemmets egen organisation og viser dem grupperet efter type. Hvert resultat viser projektet eller rammen og har et link. En risiko åbner i sidepanelet (US-147), og et krav åbner på rammens side.
  - **Teknik:** PostgreSQL's fuldtekstsøgning med den engelske konfiguration, så "log" finder "logging". Den bruger GIN-indeks på de søgte kolonner (migrationen Search). Koder og id'er som "I.2(d)" og "US-004" findes også med en almindelig "indeholder"-søgning, fordi fuldtekstsøgningen deler dem op.
  - **Sikkerhed:** organisationen tjekkes først. Kun egne projekter og de rammer, organisationen bruger, søges i, og et medlem med adgang til ét projekt (US-182) søger kun i det. Søgeordet sendes altid som parameter og aldrig som SQL-tekst (US-094). %, _ og \ i ordet søges som tegn.
  - **Grænser:** højst 50 resultater pr. type med "Show more", og et søgeord på højst 200 tegn.
- **Begrundelse:** Man skal hurtigt kunne finde alt om fx "MFA" på tværs af projekterne. Søgningen må kun finde det, medlemmet må se (ISO/IEC 27001 A.5.15 og A.8.28).
- **Fravalgt:**
  - En separat søgemaskine som Elasticsearch. Det ville betyde endnu en tjeneste at drive, og PostgreSQL er nok her.
  - Søgning i uploadede filer og danske ordformer. Det står uden for US-100.

### B146 – Organisationens oplysninger rettes på Organisation → Profile (US-027)

- **Beslutning:**
  - **Siden:** Organisation-menuen har punktet "Profile" med organisationens navn, CVR, sektor og størrelse. Admin retter navn, sektor og størrelse i én formular med de samme regler som ved oprettelsen (US-001). Navnet trimmes og må højst være 200 tegn.
  - **CVR** vises, men kan ikke rettes, fordi det identificerer virksomheden.
  - **Rettigheder:** kun Admin kan rette. Domænet og use casen tjekker det begge. Andre ser oplysningerne uden formular.
  - **Log:** hver ændring logges som `OrganizationUpdated` med gammel og ny værdi, fx "Size: Small → Medium". Uden ændringer gemmes og logges intet.
- **Begrundelse:** Sektor og størrelse afgør senere, hvilke regler der gælder (fx NIS2), så de skal kunne holdes opdateret og spores (ISO/IEC 27001 4.1 og A.8.15).
- **Fravalgt:**
  - Formularen på organisationens Overview-fane, som issuen foreslog. Siden Profile er der, hvor US-163 også samler branche, land og AI. Så findes organisationens profil ét sted.

### B147 – Én profil for organisationen; Settings er kun indstillinger (US-163, ændrer US-045 R2)

- **Beslutning:**
  - **Profilen:** Organisation → Profile samler navn, CVR, NIS2-sektor og størrelse (US-027), land samt branche og "Our products use AI". Hvert felt siger, hvad det ændrer: sektoren afgør, om NIS2 gælder; branchen afgør, hvad trackeren foreslår; landet afgør de nationale love. Branchens forslag til regler, scanninger og rapporter vises også her.
  - **Sektor foreslår branche:** når en sektor vælges, viser siden fx "Suggested industry: Medtech". Health giver Medtech, Banking og finansiel infrastruktur giver Fintech, og de digitale sektorer giver Software. Andre sektorer giver Other, og sektoren Other giver intet forslag. Intet gemmes, før admin gemmer.
  - **Alle fire brancher** kan vælges. En branche, hvis egne forslag ikke er klar endnu, står med "(own suggestions coming)".
  - **Settings** siger øverst "For the organisation's admins" og har kun indstillingerne (AI-feedback, revisionsdatoer, segl, eksport og lukning). Den linker til profilen. Organisationens egne medlemmer ser Settings skrivebeskyttet. En auditor og et medlem med adgang til ét projekt (US-182) får stadig "forbidden". Det ændrer US-045 R2.
  - **Gamle adresser:** /Settings#industry-profile og #organisation-country fører til profilen. Formularer, der sendes til de gamle POST-adresser, virker stadig og fører også dertil.
- **Begrundelse:** Branchelisten på Settings og sektorlisten under Organisation fik folk til at undre sig over forskellen. Ét sted med en forklaring gør organisationens kontekst tydelig (ISO/IEC 27001 4.1 og 4.3, NIS2 art. 3).
- **Fravalgt:**
  - At gemme alt i én formular. Navn, sektor og størrelse, landet og branchen har hver sin regel og log. Derfor har hver sin formular på samme side.
  - At ændre branchen automatisk, når sektoren skifter. Branchen ændres kun, når admin selv gemmer den.

### B148 – Et projekt arkiveres i stedet for at blive slettet (US-028)

- **Beslutning:**
  - **Arkivering:** et projekt kan arkiveres, men aldrig slettes. Det kan også arkiveres ved end of support. Arkiveringen gemmer hvem og hvornår (`ArchivedAt`, `ArchivedById`, migrationen ProjectArchived) og logges som `ProjectArchived`. Gendannelse logges som `ProjectRestored`.
  - **Skrivebeskyttet:** et arkiveret projekt tæller som lukket (`IsClosed`), ligesom ved end of support. Alle ændringer afvises derfor med "An archived project cannot be changed". Siden viser "Archived on dd-MM-yyyy by …" og en Restore-knap.
  - **Lister:** projektsiden viser kun arkiverede projekter efter "Show archived projects (n)", og de er så mærket "Archived". Valglister som invitationer til ét projekt viser dem ikke. "My tasks" og påmindelserne udelader dem som andre lukkede projekter.
  - **Rammer:** arkiverede projekter hindrer ikke, at en indbygget ramme slås fra (US-011 R8). Gendannes projektet senere, følger det stadig rammen. Det ses på projektets Frameworks-fane.
- **Begrundelse:** Listerne skal være korte, mens dokumentationen og historikken bevares for auditorer. CRA kræver, at den tekniske dokumentation gemmes i ti år (ISO/IEC 27001 7.5.3 og A.8.15, CRA art. 13(13)).
- **Fravalgt:**
  - En Delete-knap. Data slettes kun, når hele organisationen lukkes (US-184).
  - Et eget skrivebeskyttet flag ved siden af `IsClosed`. Så skulle alle de steder, der allerede skjuler formularer for lukkede projekter, kende to flag.

### B149 – En ramme kan tilføjes fra projektet og slås til samtidig (US-164)

- **Beslutning:**
  - **Valglisten** på projektets Frameworks-fane har to grupper: "Switched on" og "Not switched on yet (also switches it on for the organisation)". Den sidste gruppe har de indbyggede rammer, organisationen ikke har slået til, og den vises kun for medlemmer med ManageFrameworks. Andre ser listen som før.
  - **Ét trin:** vælges en ramme fra den anden gruppe, slås den til for organisationen med den aktuelle version (som i US-006/US-007) og tilføjes projektet. Begge dele gemmes i samme transaktion og logges som `FrameworkEnabled` og `ProjectFrameworkAdded`. Fejler et af trinene, fx fordi versionen ikke er aktiv, gemmes ingenting.
  - **Sikkerhed:** serveren tjekker ManageFrameworks igen. Uploadede rammer findes kun i deres egen organisation.
- **Begrundelse:** En regulering, der gælder et produkt, sættes op der, hvor arbejdet foregår, uden en omvej (ISO/IEC 27001 4.2 og A.5.31, CRA art. 13).
- **Fravalgt:**
  - At slå rammen til uden at spørge. Gruppen siger tydeligt, at den også slås til for organisationen.

### B150 – En upload, der er lavet ved en fejl, trækkes tilbage, ikke slettes (US-177)

- **Beslutning:**
  - **Fælles:** scanninger, SBOM'er, testresultater og versioner af kravspecifikationer kan trækkes tilbage via "⋯" ved siden af uploaden. Det kræver en begrundelse på højst 500 tegn og den samme ret som uploaden. Det tilbagetrukne gemmes med hvem, hvornår og hvorfor, logges og udelades af lister, dækning, forslag og nye rapporter.
  - **Scanning:** kun den nyeste scanning af et værktøj kan trækkes tilbage. Fund, den så første gang, bliver "Withdrawn". Hvert fund husker sin tilstand fra før den seneste scanning, der ændrede det. Fund, scanningen lukkede, er derfor åbne igen, og fund, den genåbnede, er lukkede igen. En accept efter scanningen består. Ventende forslag til sikkerhedshændelser, der kun bygger på de tilbagetrukne fund, trækkes også tilbage. For scanninger fra før US-177 vides kun, at et lukket fund var åbent før.
  - **SBOM:** den forrige SBOM for samme version er den gældende igen. Det er ikke muligt, hvis en låst rapport blev låst, mens SBOM'en var den nyeste for sin version: "Used by a locked report; upload a corrected SBOM instead".
  - **Testresultater:** forslag, der venter eller er blokeret, trækkes tilbage. Accepterede forslag er blevet til evidens og bliver stående.
  - **Kravspecifikation:** kun den nyeste version kan trækkes tilbage, og ikke hvis en låst rapport brugte den. Stories, som kun den version bragte ind (læst første gang efter den forrige versions upload), markeres som fjernet. Stories, den fjernede, er tilbage. Stories, den ændrede, hører til den forrige version igen, men beholder den tekst, de har nu, fordi den gamle tekst ikke er gemt. Den rigtige version uploades derefter igen.
- **Begrundelse:** En fejl-upload må ikke stå som projektets data eller produktets SBOM, men historikken skal bevares (ISO/IEC 27001 A.5.33 og A.8.15, CRA bilag I, del II (1)).
- **Fravalgt:**
  - At slette uploaden. Data slettes kun, når organisationen lukkes (US-184).
  - At trække en ældre scanning tilbage. Fundene kan så ikke sættes præcist tilbage.
### B151 – Alle sider tjekkes for skærmlæser og tastatur i CI (US-185)

- **Beslutning:**
  - **Tjekket:** et Reqnroll-scenarie åbner alle sider og faner i en organisation med data, deriblandt projekter, risici, evidens og en uploadet ramme. Det fejler, hvis et felt mangler en label, der er bundet til det (med `for`/`id`, en omsluttende label, `aria-label`, `aria-labelledby` eller `title`), hvis en knap, et link eller en `summary` ikke har et navn, eller hvis et billede mangler alt-tekst. Fejlen nævner siden og elementet. Tjekket bruger AngleSharp, som allerede er med, så der er intet nyt værktøj.
  - **Rettet:** audit-formularens labels er nu bundet til felterne, og filfeltet "Upload new version" på Frameworks har et navn.
  - **Tastatur:** unit-tests sikrer, at det fælles dialogvindue (US-166) er en navngivet modal dialog, som Esc lukker. De sikrer også, at fokus altid er synligt (en regel, der fjerner `outline`, skal give en fokusring i stedet), og at ingen side bruger en positiv `tabindex`.
- **Begrundelse:** Den, der bruger skærmlæser eller kun tastatur, skal kunne arbejde som alle andre. EU-kunder kan kræve det (WCAG 2.1 AA 1.3.1, 2.1.1, 2.4.7 og 4.1.2, EN 301 549, European Accessibility Act).
- **Fravalgt:**
  - Værktøjer som axe eller Lighthouse i CI. De kræver en browser og installation, og de vigtigste regler kan læses direkte af siden.
  - Kontrastberegning. Farverne kommer fra designreglerne (B142).

### B152 – En risiko viser, hvad den er gået fra og til, med sin historik (US-203)

- **Beslutning:**
  - **Registret:** "L × I" viser både før og efter tiltag, fx "3 × 4 = 12 → 2 × 3 = 6". Uden residual vises kun det første.
  - **Matrixen:** "Before measures" og "After measures". Efter tiltag placeres en risiko efter sin residual; en risiko uden residual står, hvor den stod, og noten siger, hvor mange der mangler en. Valget ligger i adressen (`matrix=after`), huskes i browseren (US-175), og en celle filtrerer efter den position, der vises.
  - **Historikken** på risikosiden læses fra aktivitetsloggen, nyeste først. Loggen siger, hvilken risiko en linje handler om, ved at starte teksten med risikoens titel; en omdøbning ("Title: Gammel → Ny") følges bagud, så linjer skrevet under den gamle titel også kommer med.
- **Begrundelse:** ISO 27001 8.2 og 8.3 kræver, at resultatet af risikovurdering og -behandling gemmes. Ændringerne blev allerede logget, men de var svære at finde, og registret og matrixen viste kun risikoen før tiltag.
- **Fravalgt:**
  - En ny tabel med risikoens historik. Aktivitetsloggen er allerede append-only og kædet (US-091), og en ny tabel ville ikke have de ændringer, der er sket indtil nu.
  - At give logposten et id for risikoen. Det ville ændre loggens format og fingeraftryk; titlen er nok, fordi to risici i samme projekt ikke kan have samme titel ved import, og omdøbninger følges.
### B153 – Evidens grupperes efter type i grupper, der kan foldes (US-201)

- **Beslutning:**
  - **Grupperne** på Evidence-fanen er Files, Links, Test results, SBOMs, Scans, Reports, Diagrams og Organisation documents, i den rækkefølge og med antal. Tomme grupper vises ikke.
  - **Hver evidens er i én gruppe.** Hvor den kom fra, vinder over hvad den er: et dokument fra organisationen, testresultater, en SBOM, en scanning og en låst rapport har hver deres gruppe; ellers et diagram, en fil eller et link.
  - **Evidens får en kilde** (`Evidence.Source`), som sættes, når den laves. Migrationen mærker den eksisterende evidens: testresultater via det godkendte forslag, SBOM'er, scanninger og rapporter via filens SHA-256 i samme projekt eller den titel, appen gav dem.
  - **Foldet eller åben:** en gruppe starter foldet, medmindre noget i den venter på en beslutning eller er udløbet. Browseren husker, hvad medlemmet har åbnet eller foldet, pr. projekt; uden JavaScript gælder serverens valg.
- **Begrundelse:** En lang liste med evidens er svær at overskue i daglig brug og i en demo. ISO 27001 7.5.3 kræver, at dokumenteret information er til at finde.
- **Fravalgt:**
  - Egne kategorier, som brugeren navngiver. Det kræver vedligehold, og typen er nok til at finde tingene.
  - Gruppering efter ramme. En evidens, der beviser krav i flere rammer, ville stå flere steder.

### B154 – Ét sted på risikosiden for det, der mindsker risikoen (US-202)

- **Beslutning:**
  - **Én sektion,** "What reduces this risk", i stedet for "Measures", "Treatment actions" og "Trace". Først kommer risikoens measures (krav og user stories), hver med de handlinger, der udfører den, og "+ Add action". Derefter "Other actions" for handlinger, der ikke hører til en measure.
  - **En handling kan høre til en measure** (`RiskAction.MeasureId`) og flyttes til en anden measure eller til "Other actions". En measure med handlinger kan ikke fjernes, før handlingerne er flyttet eller fjernet.
  - **De mest relevante krav kommer først,** når man vælger en measure: op til fem krav, der deler ord med risikoens titel og beskrivelse. AI-forslagene (US-106) er uændrede.
  - **Den fulde tråd** (US-124) ligger foldet inde i sektionen i stedet for som en sektion for sig.
  - **En risiko, der ikke mindskes** (accepteret, overført eller undgået), siger, at der ikke er brug for handlinger.
- **Begrundelse:** Measures og handlinger svarede begge på "hvad mindsker risikoen", uden at de var koblet sammen, og "Trace" gentog handlingerne. ISO 27001 6.1.3 og 8.3: de valgte kontroller og planen, der udfører dem, hører sammen.
- **Afveget fra historien:**
  - User stories forbliver measures i stedet for at blive lavet om til handlinger (R8). En handling kan ligge under en story-measure lige så vel som under et krav, så der er intet at flytte, og ingen data ændres.
  - Fejlbeskeden ved fjernelse nævner ikke kravets kode ("Move or remove the 1 action under the measure first"), fordi domænet ikke kender koderne.
  - Kravlisten er stadig en dropdown med en gruppe "Most relevant" øverst, ikke et søgefelt.

### B155 – Dokumenteret er ikke det samme som bevist (US-204)

- **Beslutning:**
  - **Evidens er dokumentation eller bevis** (`Evidence.Kind`). Dokumentation beskriver, hvordan vi arbejder: politikker, procedurer og diagrammer. Bevis viser, at det er sket: filer, links, testresultater, scanninger, SBOM'er og låste rapporter. Organisationens dokumenter og diagrammer er dokumentation, alt andet er bevis, indtil en person ændrer det på Evidence-fanen. Kun personens valg gemmes (`ChosenKind`), så et diagram, der læses senere, stadig får standarden.
  - **To tilstande for et krav i et produkt:** "Covered" kommer stadig fra en koblet user story. Ved siden af står "Proven" eller "Documented – proof missing". I organisationsområdet, som ikke har stories, står "Documented – proof missing" i stedet for "Covered", når der kun er dokumentation.
  - **"Documentation is enough"** gælder for hele organisationen (`DocumentationDecision`) og kræver en begrundelse. Den nyeste beslutning gælder; rækkerne ændres aldrig.
  - **Indbyggede mærker** ligger i koden (`DocumentationRules`), ikke i seed-data: NIS2 Art. 21(2)(a) og (f) og de krav i den danske lov og 2024/2690, der gennemfører dem; CRA II.5 og Art.31; AI Act Art. 11 og Art. 17. Begrundelsen er "The requirement asks for the document itself". En organisation kan fjerne mærket med sin egen begrundelse.
  - **Udløbet bevis tæller ikke,** så et krav med gyldig dokumentation og kun udløbet bevis er "Documented – proof missing".
  - **Rapporter, auditpakken og trinlisten** bruger bevis i stedet for "har evidens". SoA'ens "Implemented", hullerne og fasegaten spørger, om kravet er bevist.
  - **NIS2-gapanalysen tæller stadig dokumentation** som evidens for, at en foranstaltning er på plads. Compliance-fanen viser samtidig "Documented – proof missing".
- **Begrundelse:** En godkendt procedure gjorde et krav dækket og "bevist", selvom intet viste, at proceduren blev fulgt. ISO 27001 9.1 og NIS2 Art. 21(2)(f) skelner mellem at have en foranstaltning og at vise, at den virker.
- **Fravalgt:**
  - Mærket pr. projekt. En politik gælder hele organisationen, så beslutningen gør det også.
  - En kolonne på `Requirement` til de indbyggede mærker. Det ville kræve en migration med data for hver version af hvert katalog.
### B156 – Kontaktoplysninger til leverandører (US-207)

- **Beslutning:**
  - **Firmaet:** CVR- eller momsnummer, adresse, land, hjemmeside og en e-mail til sikkerhedssager. Alt er frivilligt. En hjemmeside uden `https://` får det sat foran.
  - **Kontaktpersoner** (`SupplierContact`) med navn, rolle, e-mail og telefon. Den første er primær, og en ny primær tager over fra den gamle. Fjernes den primære, bliver den næste primær. En rolle kan vælges fra en liste eller skrives.
  - **"Write"** er et `mailto:`-link med et emne, fx "Security – Demo ApS" eller "Updated evidence – Demo ApS", når evidens mangler eller snart udløber. Trackeren sender intet selv.
  - **"No security contact"** vises på en leverandør med høj kritikalitet, når hverken en sikkerhedskontakt med e-mail eller en sikkerheds-e-mail er givet. En sikkerhedskontakt uden e-mail tæller ikke, for man kan ikke skrive til den.
  - **Loggen** nævner, hvad der ændrede sig, og personens navn og rolle, aldrig e-mail eller telefon.
  - **Det gamle fritekstfelt "Contact"** vises som en note, indtil den første kontaktperson er tilføjet.
  - **Påmindelsen om udløbende evidens** linker til leverandørens række, hvor den primære kontakt kan spørges. Selve mailen nævner ingen navne, som før (US-183 R3).
- **Begrundelse:** NIS2 Art. 21(2)(d) og ISO 27001 A.5.19–A.5.22 kræver, at organisationen kan følge op på sine leverandører. Det kræver, at man ved, hvem man skriver til.
- **Fravalgt:**
  - At sende mails fra trackeren. Medlemmets eget mailprogram er nok og kræver ingen mailopsætning.
  - Opslag i CVR-registeret. Det kommer som en historie for sig (besluttet 18-11-2026).
### B157 – Cross-mapping på tværs af flere rammeværker, for valgte projekter (US-206)

- **Beslutning:**
  - **Flere rammeværker på én gang** (vej 1, valgt af brugeren 18-11-2026). Man vælger 2–5 versioner. AI'en sammenligner hvert par i den valgte rækkefølge, (A, B), (A, C), (B, C), ud fra én indlejring af alle teksterne. Forslagene gemmes pr. par som før, så siden for to rammeværker virker uændret.
  - **Grupperet pr. krav:** siden viser hvert krav med dets matches i de andre valgte rammeværker. "Approve all" og "Reject all" afgør dem alle på én gang. Et enkelt match kan stadig afgøres for sig.
  - **Et godkendt match gælder for valgte projekter** (`RequirementMapping.ProjectIds`). Tom liste betyder alle projekter, der følger begge sider. Kun projekter, der følger begge sider, kan vælges, og ændringen logges som `MappingProjectsChanged`.
  - **Kun den, der styrer rammeværker** (`ManageFrameworks`), vælger projekterne. Det svarer på historiens åbne spørgsmål: et projekt kan ikke selv slå et match fra.
  - **Matches er ikke tjekket endnu (R6):** indtil AI'en læser teksterne (US-205), siger siden, at et match kun betyder, at kravene er formuleret ens. "Same" kan stadig godkendes, så US-037 virker som før.
- **Begrundelse:** Med tre rammeværker skulle man før køre og afgøre tre par hver for sig, og et match gjaldt i alle produkter. ISO 27001 6.1.3 d) beder om at sammenligne de valgte kontroller med flere kilder.
- **Fravalgt:**
  - En fælles liste over organisationens egne kontroller (vej 2). Den er uden for denne historie.
  - Flere end fem rammeværker på én gang. Det giver for mange par for den lokale AI.

### B158 – Organisationens egen compliance, arvet af projekterne (US-115)

- **Beslutning:**
  - **Hvor et krav opfyldes** (`RequirementScope`): et krav, som organisationen selv følger, er enten organisationsdækkende eller udføres i projekterne. ISO 27001's udviklings- og ændringskontroller (A.8.25–A.8.32) udføres i projekterne som standard, og alt andet er organisationsdækkende. En person med `DecideCompliance` ændrer det med en begrundelse på kravets side i organisationsområdet. Rækkerne ændres aldrig, og den nyeste gælder.
  - **"Ned":** et produkt har siden "From the organisation" (linket fra Compliance-fanen). Den viser organisationens krav og deres status, mærket "Inherited from the organisation". Produktet skal ikke selv følge rammeværket.
  - **Fravalg (R4, besluttet af brugeren 19-11-2026):** et produkt kan markere et arvet krav "Not applicable here" med en begrundelse, fx uden for ISMS'ets scope. Det er en almindelig `RequirementApplicability` på produktet, så historikken og loggen er de samme som for andre krav.
  - **"Op" (begge retninger nu, besluttet af brugeren 19-11-2026):** for et krav, der udføres i projekterne, lægger hvert produkt sit eget bevis til på siden "From the organisation". Organisationens krav viser "Proven in …" og "No proof yet: …" og er dækket, når alle produkter, der ikke har fravalgt det, har bevis. Udløbet bevis og dokumentation alene tæller ikke (B155).
  - **Egen side i stedet for en fane på Compliance-fanen,** så produktets sider ikke laver flere databaseforespørgsler.
- **Begrundelse:** ISO 27001 er et ledelsessystem for organisationen, men nogle kontroller udføres i produkternes arbejde. Uden arv skulle hvert produkt uploade de samme politikker igen, og organisationen kunne ikke se, hvilke produkter der mangler bevis.
- **Fravalgt:**
  - At produktet automatisk følger organisationens rammeværker. Så blandes organisationens krav sammen med produktets egne.
  - Flere juridiske enheder med hver sit ISMS. Det er uden for historien.

### B159 – AI-forslag, der har læst dokumentet (US-205)

- **Beslutning:**
  - **Tre trin for dokumenter og projektets evidens:**
    1. **Shortlist:** dokumentet deles i afsnit på højst 1.200 tegn, og et krav får sit bedste afsnits score.
    2. **Rerank:** rerankeren (US-118) ordner kandidaterne, når den er sat op.
    3. **Tjek:** chatmodellen for opgaven `EvidenceCheck` læser kravet og de tre bedste afsnit. Den svarer "Meets", "Partly meets" eller "Does not meet", med et citat og hvad der mangler.

    Kun "Meets" og "Partly" bliver forslag, og citatet skal stå ordret i dokumentet. Mellemrum og store/små bogstaver må afvige.
  - **Uden tjek (R7):** når modellen ikke kan nås, svarer i et forkert format, eller dokumentet ikke har tekst, laves forslaget som før og mærkes "Not checked".
  - **Hvert forslag viser** dommen, citatet, hvad der mangler, og hvilken model der tjekkede det (R2, R10).
  - **Cross-mapping (R13, R14):**
    - Kun de to bedste matches pr. krav, og intet under "Partly" ud fra teksternes lighed.
    - Modellen læser begge krav og svarer "Same", "Partly" eller "Related" med det, der adskiller dem. "Related" foreslås ikke.
    - Kun et tjekket "Same" giver dækning.
    - "Reject all not checked" afviser resten på én gang med én begrundelse.
  - **Evidens gennem stories (R11):** evidenskortet viser de krav, evidensen når gennem sine stories, fx "Through [US-004] Sign in: CRA I.2(d)", uden at spørge AI'en, og tilbyder "Link directly".
  - **Evalueringen (R9)** måles på folks beslutninger, ligesom US-116, på AI-kvalitetssiden. Den viser:
    - andelen af tjekkede forslag, der blev godkendt
    - den samme andel for forslag uden tjek (før/efter)
    - andelen af de rigtige links, AI'en fandt, regnet som godkendte mod dem, der blev lavet i hånden
  - **Modellen vælges af systemejeren** med `Ai:Tasks:EvidenceCheck:Model`, fx en 14B-model i Ollama. Kun lokale modeller (R6).
- **Begrundelse:** Indlejring alene svarer på "samme emne?", ikke på "står der det, kravet beder om?". I demoen gav en hændelsesprocedure høje scorer på alle hændelseskrav, og CRA × ISO-demoen gav 288 forslag omkring 0,65.
- **Åbne spørgsmål fra historien, besvaret:**
  - Story-forslagene (US-020, US-118) tjekkes ikke endnu. De har allerede reranker og begrundelse, og et ekstra kald pr. forslag gør dem langsomme.
  - Evidenstjekket (US-038) bruger allerede `EvidenceCheck`-modellen. Citatreglen gælder endnu ikke der.
  - Hvilken modelstørrelse der er bedst, afgør evalueringen.
- **Fravalgt:**
  - Et fast testsæt af dokumenter i repoet til evalueringen. Folks egne beslutninger er et bedre og ærligere testsæt, og standarderne må ikke ligge i repoet.
  - At ændre forslagene til en eksisterende `MappingSuggestion`. Kun dokumenter, projektets evidens og cross-mapping er med.

### B160 – Evidens foreslået, når en foranstaltning godkendes (US-161)

- **Beslutning:**
  - **Forslagene regnes, når risikosiden vises,** og gemmes ikke. Det sker kun, når der er foreslåede foranstaltninger, der venter, og projektet har evidens, og det koster ét kald til indlejringsmodellen. Rerankeren (US-118) bruges, når den er sat op, og grænsen for evidensforslag (US-059) gælder.
  - **Projektets evidens sammenlignes på titlen** med foranstaltningens krav eller story. Evidens, der allerede ligger på kravet eller storyen, udløbet evidens og fjernet evidens foreslås ikke.
  - **De tre bedste vises under "+ Evidence"** som valg. Et valg og "Approve with evidence" kobler evidensen til kravet eller storyen og godkender foranstaltningen i ét trin. Ny evidens kan stadig lægges op som før (US-158), men ikke samtidig med et valg.
  - **Uden AI** står der "No suggestions: the AI is not available", og upload og link virker som før.
- **Begrundelse:** Det er ofte den samme politik eller rapport, der beviser flere foranstaltninger. At finde den i samme trin sparer en ny upload af det samme dokument.
- **Fravalgt:** at gemme forslagene som `EvidenceLinkSuggestion`. De gælder kun i godkendelsesøjeblikket, og projektets egne evidensforslag (US-059) dækker resten.

### B161 – AI'en skriver arbejde til hullerne (US-108)

- **Beslutning:**
  - **"Draft for the gaps"** ligger under "Ask the AI…" på en regulerings Compliance-fane. Den tager op til 10 krav ad gangen: de krav, der gælder, ikke er dækket, ikke har et AI-forslag til en story, og ikke allerede har et åbent udkast.
  - **Formen følger projektets metode** (US-104): user stories, use cases eller krav. Med flere metoder vælger man formen.
  - **Et udkast dækker højst 3 krav** (besluttet af brugeren 19-11-2026). Det fortæller i én linje, hvordan det passer til produktets formål.
  - **Et krav, der ikke passer til formålet,** får et "not applicable"-forslag (US-060) i stedet for et udkast.
  - **US-106's `StoryDraft` er udvidet** med form, de ekstra krav, "passer til" og en use cases postconditions, så de to historier deler udkastet.
  - **Et oprettet udkast:**
    - En user story åbner GitHubs "new issue"-side udfyldt, som US-106. Trackeren skriver ikke til GitHub (B12, besluttet af brugeren 19-11-2026).
    - En use case eller et krav står som tekst at kopiere ind i teamets dokument (besluttet af brugeren 19-11-2026).
    - Når issuet eller den næste version af dokumentet kommer ind, kobles det til alle de krav, udkastet dækker, med kilden "AI-drafted". Titlen skal matche, også med dokumentets ID foran, fx "UC-12 …".
  - **Et kasseret udkast** gemmer begrundelsen, og de samme krav skrives ikke igen i samme form.
  - **Siden "Drafts"** viser de åbne udkast efter form og dem, der venter på at komme ind. Auditoren ser dem uden knapper.
  - **Modellen er opgaven `GapDrafts`'s** (US-117), så systemejeren kan give den en større model.
- **Begrundelse:** En liste over artikler er svær at handle på. Et udkast i teamets egen form og med produktets egne ord gør hullet til arbejde.
- **Fravalgt:**
  - At trackeren selv opretter issues eller skriver nye versioner af teamets dokumenter.
  - At skrive udkast til alle reguleringer på én gang. En ad gangen kan gennemgås.

### B162 – Definition of Done og en audit af færdigt arbejde (US-109)

- **Beslutning:**
  - **Pr. projekt** (besluttet af brugeren 19-11-2026): én liste kriterier for hver slags arbejdselement (user stories, use cases og krav fra en kravspec). Et projekt uden egen liste bruger standarden (version 0), og den første ændring gemmer den som version 1.
  - **Kriterier, trackeren tjekker selv:** skabelonen er udfyldt, der er koblet et krav, hvert koblet krav har bevis (US-204), test er bestået (US-058), og intet AI-forslag venter. De kan ikke krydses af i hånden.
  - **Manuelle kriterier,** fx "Code reviewed", krydses af af en med `LinkWork`, med hvem og hvornår. Når fluebenet fjernes, bliver rækken stående.
  - **Færdig:** en user story, når den er lukket i GitHub. En use case eller et krav, når en person markerer det "Done", for de har ingen tilstand selv.
  - **Genåbnes en story i GitHub, tæller de manuelle flueben fra før ikke længere** (besluttet af brugeren 19-11-2026). Det sker ved at sammenligne med tidspunktet for genåbningen, så historikken bevares.
  - **Auditten bruger altid den nuværende Definition of Done** (besluttet af brugeren 19-11-2026). CSV-eksporten skriver versionen på hver linje, og ændringer logges med gamle og nye kriterier.
  - **Siden "Definition of Done"** ligger under Product requirements og viser færdigt arbejde, filteret "Done, but not meeting it", det, der ikke er færdigt endnu, og kriterierne. Overview viser antallet under "Needs attention".
- **Begrundelse:** En auditor skal kunne se, at "done" betyder det samme hver gang, og at intet kaldes færdigt med huller. Det følger ISO 27001 A.8.25 og 9.2.
- **Fravalgt:**
  - At gemme et øjebliksbillede pr. færdigt element.
  - Mærket "no compliance impact" på en story. Det findes ikke endnu, så en story uden krav mangler "Linked".
  - At vise kriterierne på hver story på fanen Work items. Det ville gøre den fane dyrere, og auditsiden viser dem allerede.

### B163 – Sikkerhedshændelser foreslået fra GitHub-alarmer og overvågning (US-160)

- **Beslutning:**
  - **GitHub-alarmer bliver forslag, ikke hændelser.** Når projektets alarmer hentes (US-121), foreslås en kritisk eller høj Dependabot- eller code scanning-alarm som en sårbarhed. En Dependabot-alarm nævner pakken. Fra andre scannere foreslås stadig kun kritiske fund (US-128).
  - **Secret scanning hentes også.** Kun åbne alarmer hentes, og de er altid kritiske. En lækket hemmelighed foreslås som en hændelse (`IncidentNotSevere`), og selve hemmeligheden gemmes aldrig, kun dens type.
  - **Overvågningen sender til `POST /api/projects/{id}/alerts`** med projektets upload-token (US-122). Både en enkel form og Prometheus Alertmanagers webhook forstås. En kritisk eller høj alarm, der fyrer, foreslås én gang, så længe den er åben, og "resolved" lukker et forslag, ingen har taget stilling til, med "Resolved at the source".
  - **En alarm over 256 KB eller i en ukendt form afvises** med 400 og logges som `SecurityAlertRefused` uden indholdet. Et forkert eller spærret token giver 401, og intet gemmes.
  - **En person registrerer eller afviser hvert forslag** som før (US-128). En hændelse, der ikke skal indberettes, venter på en anden person (US-135), og godkendelsen bliver forældet, hvis alarmen lukkes imens.
- **Begrundelse:** Alarmerne findes allerede i GitHub og overvågningen. At få dem foreslået sparer at skrive dem af, og CRA art. 14's frist regnes fra, da alarmen kom ind.
- **Fravalgt:**
  - At oprette hændelser automatisk. En alarm kan være falsk, og en hændelse har frister.
  - Et eget token til overvågningen. Upload-tokenet kan allerede kun sende til sit projekt og ikke læse noget.

### B164 – AI'en skriver teksterne i ledelsesrapporten (US-040)

- **Beslutning:**
  - **Ingen ny rapport.** Ledelsesrapporten (US-141) har allerede tallene, PDF'en og låsningen med SHA-256 (US-050). AI'en skriver kun et resumé og en kort tekst pr. regulering (`StatusText`), som står i afsnittet "Assessment" lige efter status.
  - **Tallene kommer aldrig fra AI'en.** Den får kun trackerens egne tal: dækning, bekræftet evidens, de første 10 mangler og antallet af åbne høje risici, alvorlige fund og hændelser. Tabellerne i rapporten laves som før.
  - **"AI-generated" står ved teksten** på Reports-fanen og i rapporten, indtil en person accepterer den eller skriver den om (AI Act art. 50). At udarbejde igen erstatter teksterne og markerer dem igen.
  - **En tekst bliver forældet,** når tallene ændrer sig. Hver tekst gemmer et fingeraftryk af de tal, den blev skrevet eller accepteret til. Er det et andet i dag, står der "Outdated: the figures changed".
  - **Låsning kræver færdige tekster.** En ledelsesrapport med tekster kan kun låses, når hver vist tekst er accepteret eller redigeret og ikke er forældet. En rapport uden tekster låses som før. Rapporten for én regulering viser kun den regulerings tekst.
  - **Rettigheder:** alle i organisationen kan læse teksterne. Kun roller med `DecideCompliance`, de samme som må låse, kan udarbejde, acceptere og redigere.
  - **Modellen er opgaven `StatusReport`'s** (US-117). Udarbejdning logges som `ReportDrafted` med modellen, accept og redigering som `ReportTextChanged`, og låsning som før som `ReportLocked`.
- **Begrundelse:** Ledelsen skal have et kort resumé, og det er hurtigere at rette et udkast end at skrive det selv. En ny rapporttype ville gentage tal og låsning, som allerede findes.
- **Fravalgt:**
  - En egen `Report` med status Draft/Final, som issuet foreslog. Låsningen (US-050) er allerede den endelige version.
  - At AI'en skriver tal eller tabeller.
  - At gemme teksternes historik. Den låste PDF har de tekster, der blev godkendt.

### B165 – Efter MVP'en prioriteres efter værdi, og Kontrol, Kravvurdering og Release bygges ikke

- **Status:** Vedtaget 2026-11-21. Beskriver, hvad der faktisk er sket siden MVP'en, og retter B05, B07, B08 og B33.
- **Beslutning:**
  - **Faserne:** fase 1 (MVP'en, B33) blev bygget først og var færdig 2026-11-05. Derefter er stories prioriteret efter, hvad der giver mest værdi i hverdagen og ved en revision, og ikke fase for fase. Dele af fase 2, 3 og 4 er derfor bygget side om side. [Roadmappen](roadmap.md) viser status pr. fase, og ISMS-stories US-208 til US-216 er samlet som fase 5.
  - **`Kontrol` bygges ikke (retter B05).** ISO 27001's kontroller er krav i Annex A. Statement of Applicability (US-053) viser, hvilke der gælder og hvordan, og kravplacering i organisationen (US-115) lader én politik opfylde et krav for alle projekter. Det var det, `Kontrol` skulle.
  - **`Kravvurdering` bygges ikke (retter B08).** Koblede stories, evidens og "Not applicable" med begrundelse viser, om et krav er opfyldt, som allerede besluttet for MVP'en (B33). Låste rapporter (US-050) giver historikken.
  - **`Release` bygges ikke (retter B07).** Livsfasen og supportperioden ligger på projektet (US-024, US-047), og en SBOM hører til et versionsnummer (B87).
  - **Modellerne følger koden.** [Domænemodellen](domaenemodel.md) og [klassediagrammet](klassediagram.md) viser kun det, der er bygget. Det planlagte står som «planlagt» eller under "Ikke bygget".
- **Begrundelse:** Faserne var en plan for rækkefølgen, før det var klart, hvad en lille virksomhed og en revisor har mest brug for. Kunderne har brug for at kunne bevise compliance over for en revisor og myndighederne, og det krævede dele fra flere faser på én gang, fx revisionspakken (fase 2) med SBOM og scanninger (fase 3) og NIS2 (fase 4). De tre begreber ville gentage data, som trackeren allerede har på en anden måde.
- **Fravalgt:**
  - At holde fast i faserne efter MVP'en. Det ville have udskudt NIS2 og revisionspakken.
  - At bygge `Kontrol` som en ekstra tabel ved siden af Annex A-kravene. To steder for det samme giver uenighed om, hvad der gælder.
  - At bygge `Release` kun for SBOM'ens skyld.

### B166 – ISMS'et får sit eget område, og det starter med scope og kontekst (US-208)

- **Beslutning:**
  - **"ISMS" i topmenuen:** ISMS'et hører til hele organisationen og er det, compliance-folk arbejder i hver dag. Derfor har det sit eget punkt i topmenuen (der er stadig højst 5) med sine sider som en række links, ligesom organisationens sider (#52). De næste ISMS-stories (US-209 til US-216) lægger deres sider her.
  - **Ét scope pr. organisation** (`IsmsScope`) med:
    - scopeteksten (4.3), lokationer og afdelinger
    - interne og eksterne forhold (4.1)
    - ejer og næste review-dato
  - **Projekterne:** hvert projekt er inde eller ude, og et projekt ude kræver en begrundelse. Et nyt projekt er "Not decided" og regnes som ude, til nogen sætter det ind. Organisationsområdet (US-142) er ikke et projekt i scopet.
  - **Versioner:** at godkende scopet laver en ny version (4.4). Det kræver en scopetekst og mindst ét projekt inde. En ændring efter godkendelsen viser "Changed since approval", indtil scopet godkendes igen.
  - **Interessenter** (4.2): navn, art og deres krav, hver med "Handled" eller ej. Kravene gemmes som JSON på interessenten. Samme navn kan ikke bruges to gange, og en fjernet interessent beholdes med et tidspunkt.
  - **PDF og låsning:** scopet laves som PDF med samme rapportmodel som de andre rapporter. En godkendt version kan låses med fingeraftryk (US-050) og ligger så på organisationsområdets Reports-fane.
  - **Rettigheder:** alle i organisationen kan læse scopet. Kun roller med `DecideCompliance` kan ændre og godkende det. Alt logges som `IsmsScopeChanged`, `IsmsScopeApproved` og `InterestedPartyChanged`.
- **Begrundelse:** ISO 27001 4.1–4.4 er grundlaget for resten af ISMS'et: mål, audits og ledelsens gennemgang skal vide, hvad der er omfattet. Projekterne findes allerede, så scopet peger på dem i stedet for at gentage dem.
- **Fravalgt:**
  - Flere scopes i én organisation.
  - At AI'en skriver konteksten. Det kan komme senere, som i US-040.
  - At gemme hver version af scopet for sig. Den låste PDF er den gemte version.

### B167 – Aktivfortegnelsen med organisationens egen klassifikation (US-214)

- **Beslutning:**
  - **Siden "Assets"** ligger i ISMS-området. Et aktiv har:
    - navn, der kun må bruges én gang i organisationen
    - art: Information, Software, Hardware, Service, People eller Location
    - ejer og klassifikation
    - om det rummer persondata
    - en valgfri beskrivelse
  - **Koblinger:** et aktiv kan høre til flere projekter. Det kan hostes eller leveres af leverandører (US-191) og rammes af risici (US-042). Koblingerne gemmes som lister af id'er på aktivet, fordi et aktiv altid læses helt.
  - **Klassifikationsskema:** ét pr. organisation. Det starter med Public < Internal < Confidential < Strictly confidential, og organisationen kan ændre niveauerne. Rækkefølgen afgør, hvad "højest" er.
  - **Et niveau i brug kan ikke fjernes.** Så står intet aktiv med et niveau, skemaet ikke kender. Brugeren giver først aktiverne et andet niveau.
  - **Ejer:** en ejer, der ikke længere er medlem, vises som "No owner" (rød). De aktiver står øverst, og man kan filtrere på dem.
  - **Risici:** risikosiden viser de aktiver, risikoen rammer, og den højeste klassifikation blandt dem.
  - **Leverandører:** leverandørsiden viser de aktiver, leverandøren hoster.
  - **Persondata:** filteret "Personal data" er listen til GDPR art. 30.
  - **Import:** fra CSV eller Excel med kolonnerne Name, Kind, Owner, Classification, Personal data og Description. Ejeren er et medlems navn eller e-mail. Én forkert række stopper hele importen, og fejlene nævner rækkerne.
  - **Eksport:** til Excel med sidens filtre.
  - **Rettigheder:** alle i organisationen kan læse fortegnelsen. Kun roller med `DecideCompliance` kan ændre den. Ændringer logges som `AssetChanged` og `ClassificationSchemeChanged` og nævner felterne, ikke værdierne.
- **Begrundelse:** ISO 27001 A.5.9–A.5.12 kræver en fortegnelse med ejere og en klassifikation, som organisationen selv bestemmer. Med koblingen til risici kan man se, hvilke risici der rammer de vigtigste aktiver.
- **Fravalgt:**
  - At finde aktiver automatisk i skyen eller på netværket.
  - En CMDB med versioner af hver server.
  - En forhåndsvisning af importen som ved risici. Aktivlisten er enkel, så alt eller intet er nok.

### B168 – Ét register for afvigelser og korrigerende handlinger (US-212)

- **Beslutning:**
  - **Siden "Nonconformities"** ligger i ISMS-området. En afvigelse har:
    - titel, kilde og alvor (Major, Minor eller Observation)
    - en valgfri beskrivelse
  - **Kilder:** Internal audit, External audit, Security event, Missed deadline, Complaint, Management review eller Other.
  - **Fra et fund:** en afvigelse kan oprettes fra et åbent auditfund (US-134) eller en åben sikkerhedshændelse. Så følger kilden og titlen med, og samme fund kan ikke have to åbne afvigelser.
  - **Forløbet følger ISO 27001 10.2:**
    1. Korrektionen, der blev gjort med det samme, og årsagen
    2. Den korrigerende handling med ejer og frist. En frist i fortiden afvises.
    3. "The action is done"
    4. Effektivitetstjekket
  - **Effektivitetstjekket:**
    - "It worked" lukker afvigelsen, men kun når årsagen er skrevet.
    - "It did not work" åbner den igen. Handlingen gemmes under "Actions that did not work", og der skal planlægges en ny.
  - **Close** lukker kun en afvigelse, der har årsag, handling og et tjek, der fandt, at handlingen virkede. Ellers siger siden, hvad der mangler.
  - **Genåbning:** en lukket afvigelse kan åbnes igen med en begrundelse.
  - **Forfalden:** en planlagt handling efter sin frist er "Overdue" og står øverst.
  - **Rettigheder:** alle læser registret. Kun roller med `DecideCompliance` ændrer det. Alt logges som `NonconformityChanged`.
- **Begrundelse:** ISO 27001 10.2 kræver, at man reagerer, finder årsagen, handler og tjekker, at det virkede. Ét register gør det synligt for auditor, uanset hvor afvigelsen kom fra. Fund og hændelser findes allerede i projekterne, så registret peger på dem i stedet for at gentage dem.
- **Fravalgt:**
  - 5 whys og fiskebensdiagrammer som formularer. Årsagen er fritekst indtil videre.
  - At AI'en foreslår årsager.
  - Afvigelser fra overskredne mål (US-209) og faste opgaver (US-215). De kommer med de stories.

### B169 – Sikkerhedsmål, som trackeren selv måler i baggrunden (US-209)

- **Beslutning:**
  - **Siden "Objectives"** ligger i ISMS-området. Et mål har:
    - tekst og ejer
    - et mål-tal og en retning ("højere er bedre" eller "lavere er bedre")
    - en frekvens (månedlig, kvartalsvis eller årlig) og en valgfri deadline
  - **Trackerens egne mål:** et mål kan måles af trackeren selv. Det er:
    - kravdækning og krav med evidens, i procent
    - åbne High- og Critical-risici
    - forfaldne risikohandlinger
    - åbne afvigelser (US-212)
    - åbne sikkerhedshændelser

    Ellers måles målet i hånden.
  - **Baggrundsjobbet** (brugerens valg): det kører hver time og tager en måling af hvert trackermål, der er forfaldent efter sin frekvens. Det kører uden en bruger, der er logget ind. Derfor kan `ComplianceQueries` nu også regne dækningen ud for en given organisation.
  - **Kun scopet:** trackeren tæller kun projekter inde i ISMS-scopet (US-208). Er der ingen, tages der ingen måling. Åbne afvigelser hører til hele organisationen og tælles altid.
  - **Farven** (R4):
    - grøn, når den seneste værdi når målet
    - gul inden for 10 % af målet
    - rød ellers
    - et mål på 0 med "lavere er bedre" har ingen gul margin
  - **Målinger ændres aldrig** (R6). En person kan rette trackerens måling med en ny måling, men skal skrive hvorfor.
  - **Forfalden:** et mål, der måles i hånden, er "Due", når frekvensen er gået siden sidste måling.
  - **Trenden** er en lille linje over de sidste 8 målinger.
  - **Log:** mål og målinger fra personer logges som `ObjectiveChanged`. Trackerens egne målinger logges ikke, men gemmes på målet med "the tracker" som afsender.
- **Begrundelse:** ISO 27001 6.2 og 9.1 kræver mål, der kan måles, og at de bliver målt. Når trackeren selv måler det, den allerede ved, sparer det arbejde og giver ærlige tal.
- **Fravalgt:**
  - At måle, når siden åbnes. Så kommer der ingen måling i en periode, hvor ingen åbner siden.
  - Andelen af læste politikker. Den kommer med US-213.
  - Sikkerhedshændelser efter deres frist. Indtil videre tælles de åbne.

### B170 – Et internt auditprogram med kontrolejere sat på siden (US-211)

- **Beslutning:**
  - **Siden "Internal audits"** ligger i ISMS-området. Programmet har en start og en længde på 1–5 år, ét pr. organisation.
  - **En planlagt audit** har:
    - en dato inden for programmet
    - de klausuler (4–10) og Annex A-kontroller, den dækker (mindst én)
    - projekterne
    - en auditor: et medlem eller navnet på en ekstern
  - **Dækningen** viser hver klausul 4–10 og hver kontrol som Done, Planned eller Missing i cyklussen. Kontrollerne er:
    - dem, der gælder i SoA'en (US-053), når organisationen har ISO/IEC 27001
    - de kontroller, der er planlagt

    Øverst står fx "Missing: 9, 10 and 53 controls".
  - **Kontrolejere sættes på siden** (brugerens valg). Trackeren havde ingen ejere på kontroller.
  - **Uafhængighed:** en auditor, der ejer en kontrol i planen, får en advarsel med navn og kontroller. Planen gemmes kun med en begrundelse, som vises ved auditten og står i loggen.
  - **Udført:** en planlagt audit bliver Done, når en registreret audit (US-134) kobles til den. Auditens åbne fund bliver afvigelser (US-212) med kilden Internal audit. Et fund, der allerede har en åben afvigelse, får ikke en til.
  - **Forfalden:** en planlagt audit efter sin dato uden en registreret audit er Overdue.
  - **Rettigheder:** alle læser programmet. Kun roller med `DecideCompliance` ændrer det. Alt logges som `AuditProgrammeChanged`.
- **Begrundelse:** ISO 27001 9.2 kræver et program, der over tid dækker hele ledelsessystemet, og auditorer, der er uafhængige af det, de auditerer. Når auditterne kobles til de registrerede, kommer fundene ind i den samme afvigelsesproces.
- **Fravalgt:**
  - At udlede ejeren af, hvem der har tilføjet evidensen.
  - En tidslinje-grafik. Planen vises som en liste sorteret efter dato.
  - At kun interne audits kan kobles. Alle registrerede audits kan vælges, og deres art står ved dem.

### B171 – Kompetencer og læste politikker pr. person (US-213)

- **Beslutning:**
  - **Siden "Competence and awareness"** ligger i ISMS-området.
  - **En kompetence** har:
    - navn
    - hvor tit den skal fornyes (1–60 måneder eller aldrig)
    - de roller, der skal have den
    - om personer uden en bruger skal have den
  - **Personer uden bruger:** de tilføjes med navn alene, fx rengøring.
  - **Uddannelse:** den registreres med person, kompetence, dato og en valgfri henvisning til beviset. Den gælder til fornyelsen, så en årlig uddannelse fra 2025-10-01 er udløbet 2026-10-01. Den nyeste uddannelse afgør status.
  - **Matrixen** viser hver person og kompetence som Valid, Expired, Missing eller Not required. De, der mangler mest, står øverst.
  - **Politikker** er organisationens dokumenter af arten Policy (US-136).
    - Hvert medlem ser "Policies to read" og bekræfter selv med ét klik. Ingen kan bekræfte for andre.
    - En ny version skal bekræftes igen.
    - Tabellen viser for hver politik, hvem der har læst den nuværende version, og hvem der ikke har.
  - **Den, der går:** et medlem eller en person, der fjernes, beholder sin historik, men tælles ikke længere.
  - **Mål:** andelen af læste politikker er et nyt trackermål for målene (US-209), "Policies read by the members", i procent over hele organisationen.
  - **Log:** ændringer logges som `CompetenceChanged`, og bekræftelser som `PolicyConfirmed`.
- **Begrundelse:** ISO 27001 7.2 og 7.3 kræver, at organisationen kan vise, at folk har de nødvendige kompetencer og kender politikkerne. Bekræftelsen skal være personens egen, ellers beviser den ikke noget.
- **Fravalgt:**
  - Påmindelser om manglende uddannelse og ulæste politikker (US-183). De kan komme senere.
  - At uploade et bevis-dokument for hver uddannelse. Indtil videre er det en tekst.
  - "Policies to read" på startsiden. Den kommer med US-216.

### B172 – Årshjulet: faste opgaver, hvis evidens bliver organisationsdokumenter (US-215)

- **Beslutning:**
  - **Siden "Yearly plan"** ligger i ISMS-området. Den har:
    - et årshjul over de næste 12 måneder med hver frist; forfaldne vises i denne måned
    - en liste over opgaverne
  - **En fast opgave** har:
    - titel og interval (månedlig, kvartalsvis, halvårlig eller årlig)
    - ejer og næste frist
    - de kontroller eller klausuler, den viser
    - hvilken evidens den kræver, hvis nogen
  - **Udført:** når en opgave er udført, bliver næste frist den gamle frist plus intervallet, ikke den dag, den blev udført. Sidste dag i en måned forbliver sidste dag (2026-12-31 → 2027-03-31). En opgave, der kræver evidens, kan ikke udføres uden en fil.
  - **Evidensen bliver et organisationsdokument** (US-136) af arten Other, fx "Access review 2027-01-05". Det kobles til opgavens kontroller i organisationens ISO/IEC 27001. Så får hvert projekt, der følger kontrollen, det som evidens på samme måde som politikkerne (US-057). Har organisationen ikke ISO/IEC 27001, gemmes filen uden koblinger.
  - **Afvigelse:** en forfalden opgave kan med ét klik blive en afvigelse (US-212) med kilden Missed deadline, kun én ad gangen.
  - **Startplanen** foreslås, og intet lægges ind, før personen accepterer forslaget med ejer og første frist.
    - Forslag ud fra kontroller i brug i SoA'en: A.5.18 adgangsgennemgang, A.8.13 restore-test, A.5.1 gennemgang af politikker, A.8.8 sårbarhedsscanninger, A.5.24 beredskabsøvelse, A.6.3 awareness og A.5.19 leverandørgennemgang.
    - Forslag, der altid kommer: gennemgang af risikovurderingen, intern audit og ledelsens gennemgang.
  - **Log:** ændringer logges som `RecurringTaskChanged`.
- **Begrundelse:** ISO 27001 8.1 kræver, at det planlagte bliver gjort. Når evidensen følger med opgaven, bliver kontrollerne dokumenteret, mens arbejdet gøres.
- **Fravalgt:**
  - Påmindelser om frister (US-183). De kan komme senere.
  - Et cirkulært hjul som grafik. Månederne vises som et gitter, der også virker på en telefon.

### B173 – Ledelsens gennemgang med et input-sæt, som trackeren samler (US-210)

- **Beslutning:**
  - **Siden "Management review"** ligger i ISMS-området. En gennemgang planlægges med dato og mindst én deltager. Deltagerne vælges blandt medlemmerne eller skrives ind, fx "CEO". Kun én gennemgang kan være planlagt ad gangen.
  - **Input-sættet** har altid alle input fra 9.3.2. Et tomt input siger "Nothing to report" i stedet for at mangle. Inputtene er:
    - a) åbne handlinger fra tidligere gennemgange
    - b) ændringer i konteksten (scope-version, ændret siden godkendelsen, projekter i scopet og interessenter)
    - c) interessenternes krav, der ikke er håndteret
    - d) målene med seneste værdi, afvigelserne (åbne, lukkede de sidste 12 måneder, forfaldne og dem, hvor en handling ikke virkede) og auditresultaterne (programmet og audits de sidste 12 måneder)
    - e) feedback fra interessenter, skrevet i hånden
    - f) åbne High- og Critical-risici i projekterne i scopet
    - g) muligheder for forbedring, skrevet i hånden
  - **Snapshot:** før mødet samles inputtene, hver gang siden åbnes. Når gennemgangen er holdt, gemmes de som de var, og senere ændringer ændrer ikke, hvad ledelsen så.
  - **Beslutninger** kan have en handling med ejer og frist. En åben handling er input a) i næste gennemgang.
  - **Låsning:** referatet låses. Derefter kan intet ændres, men handlinger kan stadig markeres udført. PDF'en låses samtidig med fingeraftryk på organisationsområdets Reports-fane (US-050).
  - **Forfalden:** siden siger "Management review due", når der ikke er holdt en gennemgang i 12 måneder.
  - **Log:** ændringer logges som `ManagementReviewChanged`.
- **Begrundelse:** ISO 27001 9.3 kræver bestemte input og, at beslutningerne dokumenteres. Når trackeren samler inputtene fra resten af ISMS'et, bliver forberedelsen kort. Snapshottet sikrer, at referatet viser det, ledelsen faktisk så.
- **Fravalgt:**
  - At intervallet på 12 måneder kan ændres. Det er fast indtil videre.
  - At en handling bliver en afvigelses korrigerende handling (US-212). Det kan komme senere.

### B174 – ISMS-forsiden: min uge, status pr. del og en guidet start (US-216)

- **Beslutning:**
  - **Forsiden ligger i ISMS-området** (brugerens valg). "ISMS" i topmenuen åbner den. Man lander stadig på projekterne efter login, så resten af flowet er uændret.
  - **Min uge** viser kun personens egne ting, skrevet som noget at gøre, fx "Review access rights". De forfaldne står først, så efter frist, op til 7 dage frem. Resten ligger under "Show more". Tingene kommer fra:
    - faste opgaver (US-215)
    - risikohandlinger (US-111)
    - handlinger fra ledelsens gennemgang (US-210)
    - korrigerende handlinger (US-212)
    - politikker at læse (US-213) og dokumenter at gennemgå (US-136)
    - mål at måle (US-209)
    - planlagte audits, hvor personen er auditor (US-211)
  - **En politik skal læses inden for 14 dage** efter, at versionen kom. Det er fristen, den har i "Min uge".
  - **Status pr. del:** Context (4), Leadership (5), Planning (6), Support (7), Operation (8), Performance evaluation (9), Improvement (10) og SoA'en.
    - Rød, når noget krævet mangler eller er forfaldent. Gul, når noget forfalder inden for 30 dage. Ellers grøn.
    - Rød vinder over gul i samme del.
    - Hver rød eller gul del har én sætning om, hvad der mangler, fx "No management review for 14 months", og et link til siden, hvor det rettes.
    - Status vises også som ord og ikon, så den kan læses uden farver.
  - **Guidet start:** rækkefølgen er scope → politikker → aktiver → risici → SoA → mål → årshjul.
    - Hvert trin siger hvorfor i én sætning.
    - Den vises, til alle trin er gjort, eller organisationen skjuler den.
    - Den kan vises igen fra siden. Det gemmes på organisationen (`HidesIsmsGuide`).
  - **Læserettigheder:** en rolle, der kun må læse, ser status, men ingen knapper.
  - **Antal forespørgsler:** risici fra alle projekter hentes i én forespørgsel, så siden har en fast grænse på 17 i QueryLimits.feature.
- **Begrundelse:** ISMS'et bruges hver dag af folk, der ikke kan ISO 27001 udenad. Siden binder US-208 til US-215 sammen og siger, hvad der skal gøres, og hvor det gøres.
- **Fravalgt:**
  - At gøre siden til landingssiden efter login (brugerens valg).
  - Udløbende certifikater og leverandørevidens i "Min uge". De har ingen ejer pr. person, så de ses på leverandør- og auditsiderne.

### B175 – Dobbelt bekræftelse med AI'ens argument, før et forslag laver et link (US-221)

- **Beslutning:**
  - **Hvilke forslag:** de AI-forslag, der laver et link og dermed ændrer dækningen (brugerens valg):
    - krav ↔ story
    - evidens ↔ krav
    - evidens ↔ story
    - risikotiltag

    Evidens fra testkørsler og pull requests (US-058) er ikke AI, men tags og links, og er ikke med. De andre forslag (tjeklister, "ikke relevant", "needs action", sikkerhedshændelser, dokumentlinks) beholder én bekræftelse.
  - **Første trin:** "Approve" viser argumentet på en side for sig, og intet linkes endnu. Siden virker uden JavaScript. Et risikotiltag har argumentet og fluebenet i selve formularen, fordi den også tager evidens og filer.
  - **Andet trin:** linket laves kun, når "I have read the argument and agree" er sat. Servicen afviser en godkendelse uden flueben, så det gælder også uden om siden. "Cancel" ændrer intet.
  - **Argumentet har fem faste dele:**
    1. hvad kravet beder om (kode og titel)
    2. citatet, der opfylder det
    3. hvad der måske mangler
    4. hvor sikker AI'en er
    5. modeller og hvornår
  - **Citatet:** kun forslag om evidens ↔ krav har et citat i dag (tjekket fra US-205). Uden citat står der "No quote found: the AI's judgement only", og AI'ens begrundelse vises (brugerens valg). US-222 kan give citater til stories senere.
  - **Sikkerheden:**
    - High fra score 0,75, Medium fra 0,55, ellers Low.
    - Uden citat er det højst Low, fordi det kun er AI'ens vurdering.
    - Uden tjek står der "Not checked" under "what may be missing".
  - **Gemmes og logges:** tidspunktet for bekræftelsen gemmes på forslaget (`ArgumentConfirmedAt`), og loggen siger "argument confirmed, confidence …". Er forslaget afgjort af en anden, siger siden hvem.
- **Begrundelse:** AI Act art. 14 kræver, at et menneske reelt vurderer AI'ens forslag. Når argumentet skal læses og bekræftes, bliver en godkendelse et bevidst valg, ikke et klik.
- **Fravalgt:**
  - Et dialogvindue oven på siden. En side for sig virker uden JavaScript og med den strenge CSP; et vindue kan komme senere.
  - At bede chat-modellen om del 1 og 3 i begrundelsen. Kravets titel og tjekkets "mangler" bruges i stedet.

### B176 – Valgte roller godkender at acceptere High- og Critical-risici (US-217)

- **Beslutning:**
  - **Godkenderroller:** organisationen vælger de roller, der godkender at acceptere en High- eller Critical-risiko. Standarden er Organisation admin, og eksisterende organisationer får den. Det er en udvidelse af B69 (US-135).
  - **Valg af roller:** kun roller med `DecideCompliance` (Organisation admin og Compliance manager) kan vælges, og mindst én skal vælges.
  - **Hvem ændrer:** kun organisationens admin, under Settings → "Who approves accepting High and Critical risks". Ændringen logges som `RiskApproverRolesChanged` med de gamle og nye roller.
  - **Med en godkenderrolle** accepterer man risikoen med det samme. Loggen siger fx "as Organisation admin (approver role, no approval needed; risk owner Anna)", og risikoejeren nævnes, som brugeren valgte.
  - **Uden en godkenderrolle**, men med `DecideCompliance`, skal man bede om godkendelse.
    - Risikosiden siger "Ask for approval" og nævner rollerne og hvor mange medlemmer, der har dem.
    - Har ingen medlem rollen, siger siden det, og anmodningen venter.
  - **Kun en godkenderrolle kan godkende.** Andre får "Only Organisation admin can approve this". Ingen kan godkende sin egen anmodning, og det tjekkes først.
    - En ventende anmodning følger reglerne, når den godkendes. Det gælder også, hvis rollerne er ændret imens.
  - **Medium og Low** accepteres som før. Den resterende risiko tæller, når den er sat (B69).
  - **Andre fire-øjne-handlinger er uændrede:** "ikke rapporteringspligtig", end of support og aktivering af frameworkversioner.
  - **Teknik:** `IApprovalExecutor` har fået `MayApprove` og `WhoMayApprove`. Standarden er handlingens tilladelse, og risici overskriver den.
- **Begrundelse:** ISO 27001 6.1.3 og A.5.3: accept af de største risici skal ligge hos dem, organisationen har udpeget. Hos små organisationer skal admin kunne gøre det alene. Med to-personers-princippet kunne admin ikke det før.
- **Fravalgt:**
  - At risikoejeren også skal sige ja (brugerens valg).
  - At en accept udløber og skal fornyes (brugerens valg).

### B177 – Fem punkter i topmenuen, hvert med én række undersider (US-225)

- **Beslutning:**
  - **Topmenuen:** Home · Projects · Compliance · ISMS · Organisation (brugerens valg efter en mockup).
    - **Home** er "My tasks" indtil videre, med tælleren for det forfaldne. Fra US-227 er det "Your week" og landingssiden (B179).
    - "Frameworks" og "My tasks" er ikke længere selvstændige punkter.
  - **Compliance** er de regler, hele organisationen følger: Frameworks (først), NIS2 scope, NIS2 gap analysis, Requirements, Evidence, Checklists og Reports. Link-siden kommer med US-220.
  - **Organisation** er administrationen: Profile, Suppliers, Documents, Users, Activity og Settings. Documents og Suppliers er flyttet til ISMS med US-226 (B178).
  - **Organisationsområdet** (US-142) er stadig et projekt i data, men vises ikke længere med et projekts faner og tre "Overview".
    - Dets sider er sider under Compliance med Compliances række: NIS2 scope er områdets oversigt, og Requirements, Evidence og Reports er dets faner.
    - Dets risici ligger under ISMS og vises med ISMS' række.
  - **Én række undersider:** hver side har højst én række. Den tegnes i layoutet ud fra det topmenupunkt, siden hører til (`TopBar.SectionOf`). Fra US-226 gælder det også ISMS-siderne (B178).
  - **Gamle adresser virker:** fx `/Organisation?tab=nis2-scope` sender stadig videre til NIS2-scopetjekket, nu under Compliance.
- **Begrundelse:** det var svært at finde rundt, når organisationens sider lå i tre lag med "Overview" tre gange. Nu har hver side ét sted og én række undersider.
- **Fravalgt:**
  - At ændre organisationsområdet i data. Det er kun visningen, der er ændret.
  - Områdets aktivitetslog som egen side under Compliance. Den ses i organisationens Activity og på sin gamle adresse.

### B178 – Hele ISMS samlet ét sted i standardens rækkefølge (US-226)

- **Beslutning:**
  - **ISMS' række** følger ISO/IEC 27001 og den guidede start: Overview, Scope, Documents, Risks, Assets, Suppliers, Objectives, Competence, Yearly plan, Internal audits, Management review og Nonconformities (brugerens valg: "ISO order").
  - **Documents og Suppliers** er flyttet fra Organisation til ISMS. Organisation er nu kun Profile, Users, Activity og Settings.
  - **Risks** er organisationsområdets risici (US-142) og vises med ISMS' række.
  - **Alle dokumenter ligger under ISMS → Documents** (brugerens valg). Siden har et filter øverst: Policies, Procedures, Risk tables, Other og All. Den åbner på Policies, fordi politikkerne er det første, en auditor spørger efter.
  - **Checklists** bliver under Compliance.
  - Layoutet tegner ISMS' række på alle ISMS-sider. Siderne tegner den ikke længere selv.
- **Begrundelse:** ISMS' dele lå spredt under både Organisation og ISMS. Nu ligger de ét sted i den rækkefølge, man bygger et ISMS op i.
- **Fravalgt:** Et separat dokumentbibliotek under Organisation. Det ville give to steder at lede efter den samme politik.

### B179 – Home er "Your week", og man lander der efter login (US-227)

- **Beslutning:**
  - **Man lander på Home efter login** og når man skifter organisation. En auditor med adgang til ét projekt lander stadig på projektet (US-182).
  - **Home har én del: "Your week"** (brugerens valg). Den samler tre ting i én liste, det forfaldne først:
    - medlemmets opgaver med en frist fra projekterne (det, der før var "My tasks")
    - "My week" fra ISMS-oversigten
    - de godkendelser, der venter på medlemmet. Fristen er, når anmodningen udløber.
  - Listen viser de næste 7 dage. Resten ligger under "Show more".
  - Medlemmets egne anmodninger står ikke på listen, fordi de venter på en anden.
  - **"Waiting for your decision"** (AI-forslagene) står stadig under listen. Godkendelserne står ikke også dér.
  - **Filtrene** (projekt og type) gælder projekternes opgaver. Når et filter er valgt, udelades ISMS-punkterne.
  - **Klokken** tæller det samme som listen: det, der er forfaldent eller forfalder inden for 7 dage.
  - **ISMS-status og den guidede start** bliver på ISMS-oversigten. De er ikke på Home.
  - **Et fast antal databaseforespørgsler:** "My tasks" og beslutningerne læste før hvert projekt for sig (244 forespørgsler med 16 projekter). Nu læses hver slags én gang for alle projekter (34 forespørgsler).
- **Begrundelse:** man skal kunne starte dagen fra én side uden at lede på flere.
- **Fravalgt:** ISMS-status og projekternes dækning på Home. Brugeren valgte kun "Your week", så siden er kort.

### B180 – Én Link-side med kravene, produktkravene og beviserne side om side (US-220)

- **Beslutning:**
  - **Hvor:** en "Link"-fane i hvert projekt, efter Compliance. Organisationen har den samme side som "Link" under Compliance i topbaren.
  - **Tre kolonner** (mockup'en, brugerens valg 2026-11-25):
    1. compliance-kravene i én ramme. Hver har et mærke: "Covered", "Partly", "Not covered" eller "Not applicable", og "AI", når AI'en har forslag.
    2. produktkravene: de linkede først, så AI'ens forslag med grunden på én linje, så resten
    3. beviserne: det samme
  - **Mærkerne:**
    - "Covered" kræver både et produktkrav og et bevis, der tæller.
    - "Partly" betyder, at kun det ene er der, eller at et link venter på gennemsyn.
    - Filteret "Not covered" bruger Compliance-fanens regel (intet produktkrav dækker kravet), så de to sider tæller ens.
  - **Siden åbner med filteret "Not covered".** "Has suggestions" og "All" kan vælges, og filteret står i adressen, så det kan deles.
  - **Link og Unlink** bruger Compliance-fanens handlinger. Derfor er rettighederne og loggen de samme. Efter en handling vises Link-siden igen med det samme krav valgt.
  - **AI-forslag** godkendes med den dobbelte bekræftelse (US-221). Bagefter kommer man tilbage til Link-siden.
  - **Et krav, der ikke gælder,** viser sin grund og kan ikke få links, før mærket fjernes på kravets side.
  - **Resten af en kolonne** viser højst 50. En søgning finder de øvrige.
  - **Upload af en ny fil** sker på kravets side, hvor upload-formularen er. Kravets side linker til Link-siden.
  - **Tastatur og telefon:** piletasterne flytter i en kolonne, og Enter linker. På en telefon står kolonnerne under hinanden som tre trin.
- **Begrundelse:** det var svært at følge med i, hvad der var linket. På én side kan man se, hvad der mangler for hvert krav.
- **Fravalgt:** træk og slip og at linke flere krav på én gang (uden for US-220).

### B181 – Adgangsloggen: hvem så hvad, og hvornår (US-219)

- **Beslutning:**
  - **Den er slået fra fra start.** Organisationens admin slår den til under Settings med en startdato mindst 6 uger fremme. Medlemmerne får det at vide først, fordi man i Danmark skal varsle kontrolforanstaltninger. Indtil startdatoen logges intet. Når den slås fra, stopper logningen med det samme.
  - **Det, der logges,** er en ny `PageVisit`: medlemmet, rollen på tidspunktet, projektet, hvad der blev åbnet og hvornår, og den tid, siden var synlig. Et besøg ændres aldrig af en person.
  - **Følsomme sider:**
    - et projekts faner og et enkelt bevis
    - en risiko
    - leverandørerne
    - en låst rapport eller en auditpakke, der hentes, og en auditrapport. Et hentet dokument er et besøg uden tid.
  - **Ikke besøg:** sikkerhedshændelser har ingen side for sig selv og logges som projektets fane. Godkendelser logges heller ikke for sig.
  - **Tid brugt:**
    - Siden sender et livstegn hvert 30. sekund og siger, om den er synlig. Det gør den også, mens fanen er skjult, så besøget ikke slutter. Kun den synlige tid tæller.
    - Besøget slutter, når siden forlades, eller 2 minutter efter sidste livstegn.
    - Et livstegn, der kommer senere, ændrer intet.
    - To faner på samme side tæller kun én gang i "Seen by".
  - **Hvem ser hvad:**
    - Organisation admin, Compliance manager og Auditor ser adgangsloggen. Alle, der kan læse et projekt, ser stadig dets ændringslog ("Changes").
    - Adgangsloggen ligger under /AccessLog og kan filtreres på projekt, medlem, type og periode.
    - Projektets Activity-fane og organisationens Activity-side har "Changes" og "Access".
  - **"On the page now"** vises i projektets hoved for de samme roller, når andre har sendt et livstegn inden for 2 minutter. Det vises kun, mens loggen er slået til, så siderne ikke laver ekstra forespørgsler, når den er slået fra.
  - **"Seen by"** vises på en risiko og et bevis.
  - **Opbevaring:** 12 måneder som standard, mindst 1 måned og højst 5 år. En gang i døgnet slettes ældre besøg, og antallet skrives i ændringsloggen som en handling udført af systemet.
  - **Medlemmerne får det at vide:**
    - en kort besked på Home, indtil medlemmet har trykket "I have read it". Det huskes med en cookie pr. organisation og startdato.
    - et afsnit på siden Privacy
    - medlemmets egne besøg under /AccessLog/Mine og i "download my data" (GDPR art. 15)
  - **Rapporter:**
    - Auditpakken får `05-access-log-…csv` for sin periode, når den, der laver pakken, må se adgangsloggen. Ellers står det i indholdsfortegnelsen, hvorfor filen mangler.
    - Aktivitetsloggens PDF-eksport får arket "Access log", og adgangsloggen kan hentes som CSV.
- **Begrundelse:** efter en hændelse eller i en audit skal det kunne vises, hvem der kendte informationen og hvornår (ISO/IEC 27001 A.8.15, A.5.28, NIS2 art. 23). At logge medarbejderes læsning er behandling af personoplysninger i ansættelsesforhold (GDPR art. 88). Derfor logges kun følsomme sider, loggen slettes efter sin tid, og medlemmerne varsles.
- **Fravalgt:**
  - at logge klik og scroll
  - advarsler, når nogen åbner noget
  - at logge læsning gennem API'et
  - at gemme "beskeden er læst" i databasen. En cookie er nok til en besked, og afsnittet på Privacy står der altid.

### B182 – Én return address for projektsiderne (US-228)

- **Beslutning:**
  - **Én module:** reglen for, hvor en formular på en projektside vender tilbage til, ligger i én module, `ReturnAddress` i Web. Det gælder både efter handlingen og ved en brudt regel.
  - **Formularens felter:** formularen bærer fire navngivne felter (`back`, `sub`, `req`, `show`), som ét partial skriver (`_ReturnAddress.cshtml`). Feltet `back` siger altid, hvilken side formularen kom fra: `compliance`, `requirement`, `link` eller `evidence`.
  - **Ingen URL fra formularen:** serveren bygger adressen ud fra kendte sider. Intet felt bruges som URL, så ingen kan sendes et andet sted hen (ingen open redirect). En ukendt side giver ingen adresse.
  - **Ved fejl:** fejlfilteret bruger den samme adresse, så en brudt regel lander samme sted som en handling, der lykkes. `[OnDomainError(Tab=…)]` er kun en reserve og er ikke ændret.
  - **Følger med:** bekræftelsen af et AI-forslag og undo-tilbuddene tager adressen med.
  - **Formularer uden `back`:** fx compliance-fanens AI-knapper og tests, der poster felterne selv, går tilbage efter den gamle regel. Hvis formularen sendte en kravkode, er det kravets side. Ellers er det rammens liste.
- **Begrundelse:** arkitekturgennemgangen af `ProjectsController` (27-11-2026) viste, at reglen fandtes fire steder, og at kopierne var gledet fra hinanden. En fejl på Link-siden landede derfor på kravets side.
- **Fravalgt:**
  - ét felt med en færdig URL, på grund af risikoen for open redirect
  - at fjerne de 114 `[OnDomainError]`-attributter
  - at tage andre controllere med nu

## Spørgsmål til klassediagrammet

Der er ingen åbne spørgsmål. De første spørgsmål fra oktober 2026 er besvaret sådan:

- **Skal `Mappingforslag` pege på en fælles overklasse, eller valideres xor-reglen i konstruktøren?** Ingen af delene. `Kontrol` blev ikke bygget (B165), så et forslag peger altid på et arbejdselement.
- **Hvilke attributter bliver enums?** Status, type, niveau og fase er enums og gemmes som tekst, så nye værdier ikke forskyder gamle data. Livsfasen er afgjort i B19 og B127.
- **Skal mange-til-mange-koblingerne have egne data?** Ja. `RequirementLink`, `EvidenceLink` og `RiskMeasure` gemmer kilden (manuelt eller godkendt AI-forslag), hvem og hvornår.
- **Kører AI-modellen lokalt eller hos en udbyder?** Lokalt med Ollama som standard. En OpenAI-kompatibel server kan vælges i indstillingerne (B29, B36).
