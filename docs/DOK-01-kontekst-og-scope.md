---
id: DOK-01
title: Virksomhedens kontekst og scope
version: 0.2
status: udkast
owner: Rune
references: ISO 27001 kap. 4.1–4.3
created: 2026-10-14
next_review: 2027-01-15
---

# DOK-01: Virksomhedens kontekst og scope
> **Bemærk:** North Compliance Tracker er en fiktiv virksomhed. Projektet er et
> soloprojekt, hvor den fiktive organisation bruges til at øve ISMS-arbejde
> i en realistisk kontekst.

Den fiktive virksomhed North Compliance Tracker udvikler og sælger compliance-trackeren. ISMS'et dækker udvikling, drift og support af produktet.

## 1. Virksomheden (4.1 – intern og ekstern kontekst)

- Branche: B2B-software til små og mellemstore udviklingsvirksomheder.
- Størrelse: 12 ansatte, heraf 7 udviklere, 1 sikkerhedsansvarlig, 2 salg/support og 2 ledelse.
- Lokation: kontor i Aalborg, delvist hjemmearbejde. Hosting hos EU-baseret cloududbyder.
- Interne forhold: lille team, få dedikerede sikkerhedsressourcer, høj brug af open source-komponenter.
- Eksterne forhold: skærpet EU-regulering (CRA, AI Act, NIS2), kunder der selv skal dokumentere compliance, stigende trusler mod software-leverandørkæden.

### Forretningsmål

| # | Forretningsmål | Sikkerheden understøtter det med |
| --- | --- | --- |
| F1 | North Compliance Tracker skal vinde kunder ved selv at være troværdig | ISO 27001-certificering på sigt, dokumenteret sikker udvikling |
| F2 | CRA-kompatibelt produkt før december 2027 | Secure-by-design, SBOM, sårbarhedshåndtering, teknisk dokumentation |
| F3 | Beskytte kundernes compliance-data | Adgangskontrol, kryptering, logning, incident response |

## 2. Interessenter og deres krav (4.2)

| Interessent | Hvad forventer de? | Krav der følger |
| --- | --- | --- |
| Kunder (udviklingsvirksomheder) | At deres compliance-data er fortrolige og korrekte | Adgangskontrol, kryptering, backup |
| Kundernes auditorer | Sporbar evidens og historik | Revisionslog, eksport |
| Myndigheder (fx Digitaliseringsstyrelsen, Datatilsynet) | Overholdelse af CRA, AI Act, GDPR | Sårbarhedshåndtering, dokumentation, databehandleraftaler |
| Ansatte | Klare roller og værktøjer | Politikker, awareness-træning |
| Ejere/ledelse | Lav risiko, salgbart produkt | Risikostyring, ISO-certificering på sigt |
| Leverandører (cloud, AI-API) | Aftaler og ansvarsfordeling | Leverandørstyring (Annex A 5.19–5.23) |


### tilsynsførende
- Digitaliseringsstyrelsen er den myndighed som føre tilsyn for AI ACT
- Styrelsen for Samfundssikkerhed er den myndighed som føre tilsyn for CRA

## 3. Scope for ISMS'et (4.3)

*Omfattet:*

- Udvikling, test og release af compliance-trackeren
- Drift af demo- og supportmiljøer
- Kildekode, CI/CD-pipeline, kundedata i supportsager
- Medarbejdere og laptops brugt til ovenstående

*Ikke omfattet:*

- Kundernes egne installationer (de driver dem selv)
- Bogføring og HR-systemer (håndteres af ekstern leverandør)

### Grænseflader og afhængigheder

| Grænseflade | Ekstern part | Hvad krydser grænsen | Ansvarsfordeling |
| --- | --- | --- | --- |
| Kildekode og CI/CD | GitHub | Kildekode, build-artefakter, hemmeligheder i CI | GitHub: platformens sikkerhed. North Compliance Tracker: adgange, branch protection, secrets |
| Hosting af demo- og supportmiljøer | EU-baseret cloududbyder | Demodata, logs, kundedata i supportsager | Udbyder: fysisk sikkerhed og infrastruktur. North Compliance Tracker: konfiguration, adgang, data |
| AI-funktionen | AI-API-udbyder (hvis ekstern model) | Kravtekster der sendes til mapping-forslag | Udbyder: modellen og deres drift. North Compliance Tracker: hvilke data der sendes, og leverandøraftale |
| Distribution af produktet | Container registry | Docker-image og opdateringer | North Compliance Tracker: signering og integritet af image, sårbarhedshåndtering |
| Kundernes installationer | Kunder | Docker-image ud, logs og fejlrapporter ind via support | Kunden: drift af egen installation. North Compliance Tracker: produktets sikkerhed og opdateringer (CRA) |
| Tredjepartskomponenter | Open source via NuGet | Biblioteker der indgår i produktet | North Compliance Tracker: valg, opdatering og SBOM |

---

## Ændringslog

| Version | Dato | Ændring | Af |
| --- | --- | --- | --- |
| 0.1 | 2026-10-14 | Første udkast | Rune |
| 0.2 | 2026-10-14 | Der er nu taget stilling til de ting som der ikke var taget stilling til ved udkastet | Rune |
