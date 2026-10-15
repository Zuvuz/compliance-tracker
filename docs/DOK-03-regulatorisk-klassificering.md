---
id: DOK-03
title: Regulatorisk klassificering
version: 0.1
status: udkast
owner: Rune
references: AI Act art. 5, 6, 50 og bilag III; CRA art. 7 og bilag III–IV
created: 2026-10-14
next_review: 2027-01-15
---

# DOK-03: Regulatorisk klassificering

*Konklusion: AI-funktionen er minimal risiko under AI Act, og produktet er i CRA's standardkategori med selvvurdering.* Begrundelsen nedenfor er det vigtige: en auditor vil se, at du har stillet de rigtige spørgsmål i den rigtige rækkefølge.

| Regulering | Gælder? | Klasse | Konsekvens |
| --- | --- | --- | --- |
| EU AI Act | Ja | Minimal risiko (evt. transparens) | AI-kompetencer, tydelig mærkning af AI-forslag |
| CRA | Ja | Standardkategori | Bilag I-krav, selvvurdering (modul A), CE-mærkning |
| GDPR | Ja, begrænset | – | Persondata om brugere, databehandleraftaler ved support |
| ISO 27001, SSDF, ASVS | Frivillige | – | Bruges som metode til at opfylde ovenstående |

## 1. EU AI Act – trin for trin

1. **Er det et AI-system (art. 3(1))?** *Ja, hvis forslagene genereres af en ML- eller sprogmodel. Er det rene regler (if/else), er det ikke et AI-system.*
2. **Forbudt praksis (art. 5)?** *Nej. Ingen manipulation, social scoring eller biometri.*
3. **Højrisiko (art. 6, bilag I og III)?** *Nej. Bilag III dækker fx biometri, kritisk infrastruktur, uddannelse, beskæftigelse og adgang til væsentlige ydelser. Et compliance-værktøj falder uden for alle områder, og det er ikke en sikkerhedskomponent i et reguleret produkt.*
4. **Transparens (art. 50)?** *Sandsynligvis ikke påkrævet, da systemet ikke er en chatbot. Hvis det genererer fri tekst, kan art. 50(2) blive relevant. Vi mærker AI-forslag tydeligt uanset.*
5. **Rolle:** *NordKontrol er udbyder af AI-systemet, fordi det sælges under eget navn. Bruges en ekstern sprogmodel, er vi samtidig "downstream" i forhold til modeludbyderen.*

*Pligter der gælder alligevel:* AI-kompetencer hos personalet (art. 4, gældende siden februar 2025) og frivillig brug af adfærdskodekser (art. 95). Menneskelig godkendelse af forslag indbygges som god praksis, selvom art. 14 kun kræver det for højrisiko.

## 2. Cyber Resilience Act – trin for trin

1. **Produkt med digitale elementer, gjort tilgængeligt i kommerciel aktivitet (art. 2 og 3)?** *Ja. Software solgt som Docker-image.*
2. **Undtaget?** *Nej. Ikke medicinsk udstyr, køretøj eller ren SaaS, og ikke ikke-kommerciel open source.*
3. **Vigtigt eller kritisk produkt (art. 7, bilag III og IV)?** *Nej. Listerne dækker fx identitetsstyring, password managers, VPN, firewalls og SIEM. Pas på: hvis trackeren senere begynder at indsamle og analysere sikkerhedslogs, kan den nærme sig SIEM-kategorien.*
4. **Overensstemmelsesvurdering:** *Modul A, intern kontrol. Vi udarbejder selv teknisk dokumentation, EU-overensstemmelseserklæring og sætter CE-mærke.*

*Pligter der følger (grøftegravning til senere dokumenter):*

- Cybersikkerheds-risikovurdering af produktet (art. 13) → DOK-05
- Sikkerhedskravene i bilag I, del I → kravkataloget
- Sårbarhedshåndtering og SBOM (bilag I, del II)
- Supportperiode, som udgangspunkt mindst 5 år
- Brugerinformation (bilag II) og teknisk dokumentation (bilag VII)

*Tidsfrister:* Indberetning af aktivt udnyttede sårbarheder gælder fra 11. september 2026. Resten af CRA gælder fra 11. december 2027.

**Spørgsmål:** Tjek om AI Act-tidsfristerne er rykket af EU's "Digital Omnibus"-forslag. Det påvirker ikke din klassificering, men nok datoerne.

---

## Ændringslog

| Version | Dato | Ændring | Af |
| --- | --- | --- | --- |
| 0.1 | 2026-10-14 | Første udkast | Rune |
