---
id: DOK-01
title: Virksomhedens kontekst og scope
version: 0.1
status: udkast
owner: Rune
references: ISO 27001 kap. 4.1–4.3
created: 2026-10-14
next_review: 2027-01-15
---

# DOK-01: Virksomhedens kontekst og scope

*Den fiktive virksomhed NordKontrol ApS i Aalborg udvikler og sælger compliance-trackeren. ISMS'et dækker udvikling, drift og support af produktet.* Navnet er et forslag, så skift det endelig.

## 1. Virksomheden (4.1 – intern og ekstern kontekst)

- *Branche: B2B-software til små og mellemstore udviklingsvirksomheder.*
- *Størrelse: 12 ansatte, heraf 7 udviklere, 1 sikkerhedsansvarlig, 2 salg/support og 2 ledelse.*
- *Lokation: kontor i Aalborg, delvist hjemmearbejde. Hosting hos EU-baseret cloududbyder.*
- *Interne forhold: lille team, få dedikerede sikkerhedsressourcer, høj brug af open source-komponenter.*
- *Eksterne forhold: skærpet EU-regulering (CRA, AI Act, NIS2), kunder der selv skal dokumentere compliance, stigende trusler mod software-leverandørkæden.*

**Spørgsmål:** Hvad er virksomhedens 2–3 vigtigste forretningsmål, som sikkerheden skal understøtte?

## 2. Interessenter og deres krav (4.2)

| Interessent | Hvad forventer de? | Krav der følger |
| --- | --- | --- |
| Kunder (udviklingsvirksomheder) | At deres compliance-data er fortrolige og korrekte | Adgangskontrol, kryptering, backup |
| Kundernes auditorer | Sporbar evidens og historik | Revisionslog, eksport |
| Myndigheder (fx Digitaliseringsstyrelsen, Datatilsynet) | Overholdelse af CRA, AI Act, GDPR | Sårbarhedshåndtering, dokumentation, databehandleraftaler |
| Ansatte | Klare roller og værktøjer | Politikker, awareness-træning |
| Ejere/ledelse | Lav risiko, salgbart produkt | Risikostyring, ISO-certificering på sigt |
| Leverandører (cloud, AI-API) | Aftaler og ansvarsfordeling | Leverandørstyring (Annex A 5.19–5.23) |

**Spørgsmål:** Hvilken myndighed bliver dansk markedsovervågning for CRA og AI Act? Slå det op, når du når dertil.

## 3. Scope for ISMS'et (4.3)

*Omfattet:*

- *Udvikling, test og release af compliance-trackeren*
- *Drift af demo- og supportmiljøer*
- *Kildekode, CI/CD-pipeline, kundedata i supportsager*
- *Medarbejdere og laptops brugt til ovenstående*

*Ikke omfattet:*

- *Kundernes egne installationer (de driver dem selv)*
- *Bogføring og HR-systemer (håndteres af ekstern leverandør)*

**Spørgsmål:** Hvilke grænseflader krydser scope-grænsen, fx AI-API'et og cloududbyderen? De skal nævnes eksplicit.

---

## Ændringslog

| Version | Dato | Ændring | Af |
| --- | --- | --- | --- |
| 0.1 | 2026-10-14 | Første udkast | Rune |
