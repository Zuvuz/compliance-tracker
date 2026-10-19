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

**Spørgsmål:** Hvilken AI-model bruges (ekstern API eller lokal model)? Det påvirker både dataflow, leverandørstyring og AI Act-rolle.

---

## Ændringslog

| Version | Dato | Ændring | Af |
| --- | --- | --- | --- |
| 0.1 | 2026-10-14 | Første udkast | Rune |
| 0.2 | 2026-10-19 | Første udkast | Rune |
