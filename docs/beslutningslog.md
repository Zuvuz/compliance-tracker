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

## Åbne spørgsmål til klassediagrammet

- Skal `Mappingforslag` pege på en fælles overklasse, eller valideres xor-reglen i konstruktøren?
- Hvilke attributter bliver enums (status, type, niveau)? Livscyklusfasen er afgjort i B19.
- Skal mange-til-mange-koblingerne (fx Krav–Kontrol) have egne data, som en begrundelse for koblingen?
- Kører AI-modellen lokalt eller hos en udbyder? En lokal model beskytter fortrolige stories bedre.
