---
id: DOK-02
title: Produktets tilsigtede formål
version: 0.2
status: udkast
owner: Rune
references: CRA art. 3 og 13, AI Act art. 3(12)
created: 2026-10-14
next_review: 2027-01-15
---

# DOK-02: Produktets tilsigtede formål

Compliance-trackeren hjælper udviklingsteams med at holde styr på krav, risici og evidens gennem hele et softwareprojekts livscyklus. Både CRA og AI Act vurderer produktet ud fra dette formål, så formuleringerne her har juridisk vægt.

## 1. Formål i én sætning

Et selvhostet webværktøj, hvor en organisation vælger relevante rammeværker, og hvert projekt kobler krav til risici, ansvarlige og evidens, så compliance-status altid er synlig og kan dokumenteres over for en auditor.

## 2. Brugere og roller

| Rolle | Hvad gør de i systemet? |
| --- | --- |
| Administrator | Opretter organisation, brugere og vælger rammeværker |
| Compliance-ansvarlig | Vedligeholder krav og mappings, godkender AI-forslag |
| Udvikler / projektdeltager | Tilknytter evidens og opdaterer status på egne krav |
| Auditor (læseadgang) | Ser status, historik og eksporterer rapporter |

## 3. Data i systemet

- Rammeværker og krav (fx ISO 27001 Annex A, ASVS, CRA bilag I)
- Projekter, risici og mappings mellem krav
- Evidens: links, dokumenter, testresultater
- Brugerkonti, roller og revisionslog
- Persondata: navn, e-mail og handlinger på brugere (begrænset, men GDPR gælder)

## 4. AI-funktionen – hvad den gør og ikke gør

*Gør:*

- Foreslår mappings mellem krav på tværs af rammeværker
- Peger på huller, fx krav uden evidens

*Gør ikke:*

- Træffer ingen beslutninger selv. Alle forslag skal godkendes af et menneske
- Vurderer ikke personer eller medarbejdere
- Erklærer ikke en organisation "compliant"

## 5. Leveringsform og forudsigelig fejlbrug

Produktet sælges kommercielt som et Docker-image, kunden selv driver. Det valg er bevidst: ren SaaS falder som udgangspunkt uden for CRA, mens software kunden installerer selv, er et "produkt med digitale elementer".

**Forudsigelig fejlbrug** 

- Brugeren stoler blindt på AI-forslag uden at tjekke dem
- Systemet eksponeres direkte på internettet uden reverse proxy eller TLS
- Følsomme dokumenter uploades som evidens uden adgangsstyring

**Valg:** Lokal model. Ingen data sendes til eksterne AI-udbydere.

**Model i fase 1:** En lokal embedding-model (fx multilingual-e5 eller bge-m3) kørt via ONNX Runtime i .NET, eventuelt suppleret med en lokal reranker. Der bruges ikke en generativ sprogmodel i første fase; `aiBegrundelse` bygges ud fra score og de tekstdele, der matchede.

**Dataflow:** Krav, kontroller og arbejdselementer (inkl. acceptkriterier) behandles udelukkende på organisationens egen infrastruktur. Fortrolige user stories forlader aldrig systemet.

**Leverandørstyring:** Der er ingen databehandler for AI-delen og dermed ingen databehandleraftale eller overførsel uden for EU. Til gengæld behandles modellen som en tredjepartskomponent i forsyningskæden: version og kilde fastlåses, checksum verificeres, vægte hentes i `safetensors`-format, og modellen registreres i SBOM'en.

**AI Act-rolle:** Ved at integrere en open source-model i eget system er projektet *udbyder* af AI-systemet, mens organisationer, der bruger trackeren, er *idriftsættere*. Mapping af compliance-krav falder ikke under højrisiko-kategorierne i bilag III og vurderes som begrænset risiko. Forslag markeres tydeligt som AI-genererede, og et menneske godkender altid (se B13).

**Fravalgt:** Ekstern API (fx Claude eller GPT). Bedre sprogforståelse, men kræver databehandleraftale, sender fortrolige data ud af huset og gør trackeren afhængig af en ekstern leverandør.


---

## Ændringslog

| Version | Dato | Ændring | Af |
| --- | --- | --- | --- |
| 0.1 | 2026-10-14 | Første udkast | Rune |
| 0.2 | 2026-10-19 | Første udkast | Rune |
