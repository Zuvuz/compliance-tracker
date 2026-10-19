# Compliance-tracker – Udvidelser fase 2–4

Senest opdateret: 2026-10-18

En ide-liste over, hvad der kan komme efter første fase. Kun kernen i fase 2 (audit, fund og rapportsnapshot) er besluttet og allerede med i [domænemodellen](domaenemodel.md). Resten er forslag, der kan omprioriteres, når fase 1 er bygget.

| Fase | Tema | Status |
|---|---|---|
| 1 | Kernen: krav, kontroller, evidens, releases og AI-mapping | Modelleret |
| 2 | Revision og dokumentation | Kerne besluttet |
| 3 | Sårbarheder og risiko | Forslag |
| 4 | Bredere dækning og klogere AI | Forslag |

## Fase 2 – Revision og dokumentation

**Mål:** Kunne gennemføre og dokumentere en audit, og bevise hvem der ændrede hvad.

- **Audit og fund** *(besluttet)*: registrering af interne og eksterne audits, og de afvigelser de finder, med ansvarlig og frist. Svarer til ISO 27001's krav om intern audit og håndtering af afvigelser.
- **Rapportsnapshot** *(besluttet)*: låste rapporter til auditor eller som release-dokumentation, sikret med hashværdi.
- **Notifikationer:** besked til den ansvarlige, når evidens nærmer sig udløb, et mappingforslag venter, eller et fund overskrider sin frist. Ingen nye begreber; det er adfærd på eksisterende data.
- **Ændringshistorik:** en log over ændringer i vurderinger, kontroller og evidens. En auditor spørger typisk "hvornår blev det her markeret som opfyldt, og af hvem?". Hører nok mere til arkitekturen end til domænet.

## Fase 3 – Sårbarheder og risiko

**Mål:** Dække CRA's krav om sårbarhedshåndtering og forbinde kontrollerne med de risici, de skal mindske.

- **Sårbarhed og komponent:** en softwarestykliste (SBOM) per release og de kendte sårbarheder i komponenterne, med status (åben, vurderet, rettet, ikke relevant).
- **Scanner-integration:** Syft (SBOM), Trivy eller Grype (sårbarheder) og Semgrep (kodeanalyse) leverer resultater som automatisk evidens. Bygger på samme adapter-tankegang som arbejdselementerne.
- **Rapporteringspligt:** støtte til CRA's krav om at varsle myndighederne hurtigt om aktivt udnyttede sårbarheder, fx med frister og påmindelser.
- **Risiko og trussel:** registrering af risici fra trusselsmodellen (fx STRIDE) med sandsynlighed og konsekvens, koblet til de kontroller, der mindsker dem. Gør det muligt at begrunde, *hvorfor* en kontrol findes.
- **Flere adaptere:** Azure DevOps og Jira ved siden af GitHub.

## Fase 4 – Bredere dækning og klogere AI

**Mål:** Flere frameworks og bedre AI-støtte, bygget på data fra de tidligere faser.

- **Flere frameworks:** fx NIS2, GDPR (privacy by design og konsekvensanalyse), ISO/IEC 42001 og OWASP ASVS. Kræver ingen nye begreber, kun nyt indhold.
- **Versionering af frameworks:** når en standard opdateres, skal eksisterende kontroller og mappinger kunne flyttes til den nye version, uden at historikken går tabt.
- **Bedre forslag:** godkendte og afviste mappingforslag bruges til at forbedre AI'en, fx ved at finjustere en reranker. Kræver samtykke, hvis kunders data indgår.
- **Gap-analyse:** AI'en peger på krav, som ingen kontrol eller story endnu dækker, holdt op mod projektets tilsigtede formål. Holder sig inden for beslutningen om, at AI ikke opretter acceptkriterier.
- **Intern kilde:** små teams uden eget værktøj kan oprette arbejdselementer direkte i trackeren (`kildesystem = intern`).
- **Adgangsstyring:** single sign-on og roller per projekt frem for kun per organisation.

## Rækkefølge og afhængigheder

- Fase 2 kan bygges direkte oven på fase 1; modellen er allerede klar.
- Sårbarheder i fase 3 forudsætter releases fra fase 1, fordi en SBOM altid hører til en bestemt version.
- Bedre AI i fase 4 kræver, at der er samlet nok godkendte og afviste forslag i fase 1–3.
- Nye frameworks kan tilføjes når som helst, men versionering bør være på plads, før det første framework skal opdateres.
