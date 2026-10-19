# Compliance-tracker – Domænemodel

Senest opdateret: 2026-10-19

## Formål

Domænemodellen beskriver de begreber, compliance-trackeren skal holde styr på, og hvordan de hænger sammen. Den er teknologineutral: ingen datatyper, metoder eller navigationsretning. Det kommer i klassediagrammet.

Trackeren svarer på spørgsmålet *"overholder vi reglerne, og kan vi bevise det?"*. Den svarer ikke på *"hvad skal vi bygge?"*; det er udviklernes ansvar og bor i deres eget værktøj.

Se også [beslutningsloggen](beslutningslog.md) og [udvidelser i fase 2–4](roadmap.md).

## Diagram

Begreber markeret med «fase 2» er med i modellen, så strukturen er klar til dem, men bygges ikke i første fase.

```mermaid
classDiagram
  direction TB
  class Organisation {
    navn
    cvr
    sektor
    størrelse
  }
  class Bruger {
    navn
    rolle
  }
  class Projekt {
    navn
    beskrivelse
    tilsigtetFormål
    scope
    livscyklusfase
    risikoklasse
  }
  class Release {
    versionsnummer
    udgivelsesdato
    status
  }
  class Framework {
    navn
    niveau
  }
  class Frameworkversion {
    betegnelse
    kilde
    kildereference
    status
    importeretDato
  }
  class Krav {
    kravId
    kategori
    titel
    beskrivelse
  }
  class Kontrol {
    navn
    beskrivelse
  }
  class Kravvurdering {
    status
    begrundelse
    dato
  }
  class Evidens {
    type
    beskrivelse
    reference
    gyldigFra
    gyldigTil
  }
  class Arbejdselement {
    type
    titel
    acceptkriterier
    kildesystem
    eksternReference
  }
  class Mappingforslag {
    score
    status
    aiBegrundelse
    afvisningsårsag
    brugerkommentar
    beslutningsdato
  }
  class Logpost {
    handling
    objekttype
    objektId
    detaljer
    tidspunkt
  }
  class Audit {
    <<fase 2>>
    dato
    auditor
    type
    konklusion
  }
  class Fund {
    <<fase 2>>
    alvorlighed
    beskrivelse
    frist
    status
  }
  class Rapportsnapshot {
    <<fase 2>>
    type
    genereretDato
    hashværdi
  }
  Organisation "1" -- "*" Projekt : har
  Organisation "1" -- "*" Bruger : har
  Organisation "*" -- "*" Frameworkversion : vælger
  Organisation "0..1" -- "*" Frameworkversion : importerer
  Projekt "*" -- "*" Frameworkversion : vælger
  Projekt "1" -- "*" Release : udgiver
  Projekt "1" -- "*" Arbejdselement : har
  Framework "1" -- "*" Frameworkversion : findes i
  Frameworkversion "1" -- "*" Krav : indeholder
  Organisation "1" -- "*" Kontrol : ejer
  Krav "*" -- "*" Kontrol : opfyldes af
  Krav "*" -- "*" Arbejdselement : implementeres i
  Arbejdselement "*" -- "0..1" Release : leveres i
  Release "0..1" -- "*" Kravvurdering : vurderes i
  Organisation "0..1" -- "*" Kravvurdering : vurderes i
  Krav "1" -- "*" Kravvurdering
  Kontrol "1" -- "*" Evidens : dokumenteres af
  Release "0..1" -- "*" Evidens : gælder for
  Bruger "1" -- "*" Evidens : indsender
  Bruger "1" -- "*" Kontrol : ansvarlig for
  Krav "1" -- "*" Mappingforslag
  Arbejdselement "0..1" -- "*" Mappingforslag
  Kontrol "0..1" -- "*" Mappingforslag
  Bruger "0..1" -- "*" Mappingforslag : besluttet af
  Organisation "1" -- "*" Logpost : har
  Bruger "1" -- "*" Logpost : udført af
  Organisation "1" -- "*" Audit : gennemgår
  Audit "*" -- "*" Frameworkversion : dækker
  Audit "1" -- "*" Fund : resulterer i
  Fund "*" -- "0..1" Krav : vedrører
  Fund "*" -- "0..1" Kontrol : vedrører
  Bruger "1" -- "*" Fund : ansvarlig for
  Organisation "1" -- "*" Rapportsnapshot : har
  Release "0..1" -- "*" Rapportsnapshot : dokumenterer
  Audit "0..1" -- "*" Rapportsnapshot : bruger
  Bruger "1" -- "*" Rapportsnapshot : genereret af
  note for Mappingforslag "{xor} arbejdselement eller kontrol"
  note for Kravvurdering "{xor} release eller organisation"
  note for Fund "{mindst én} krav eller kontrol"
  note for Frameworkversion "indbygget: ingen ejer / importeret: præcis én organisation"
```

## Begreber

| Begreb | Betydning | Fase |
|---|---|---|
| Organisation | Virksomheden, der bruger trackeren. Sektor og størrelse afgør bl.a., om NIS2 er relevant. | 1 |
| Bruger | En person i organisationen med en rolle. Kan være ansvarlig for kontroller og indsende evidens. | 1 |
| Projekt | Et produkt, der skal overholde regler. Tilsigtet formål og scope afgør, hvilke krav der gælder, og er kontekst for AI'en. | 1 |
| Release | En konkret version af produktet. CRA tænker i versioner, så evidens og vurderinger kan knyttes hertil. | 1 |
| Framework | Regelsættets navn, fx CRA, ISO 27001 eller AI Act. Niveau angiver, om det gælder organisationen eller et projekt. Indeholder ingen kravtekst. | 1 |
| Frameworkversion | En konkret udgave af et framework, fx "ISO/IEC 27001:2022" eller "Forordning (EU) 2024/2847". Det er versionen, der indeholder kravene, og som vælges. Kilden er enten *indbygget* (offentlig EU-lovgivning, der følger med trackeren) eller *importeret* (organisationens eget licenserede katalog). | 1 |
| Krav | Ét krav i en frameworkversion, fx et Annex I-krav i CRA. Kategori afspejler frameworkets egen struktur. | 1 |
| Kontrol | Organisationens eget tiltag, der opfylder et eller flere krav. Én kontrol kan dække krav fra flere frameworks. | 1 |
| Kravvurdering | Status for ét krav, fx opfyldt, delvist, ikke opfyldt eller ikke relevant, med begrundelse. | 1 |
| Evidens | Bevis for, at en kontrol virker: dokument, scanningsresultat, link til PR osv. Har gyldighedsperiode. | 1 |
| Arbejdselement | En henvisning til en user story, use case eller opgave i et eksternt system. Trackeren ejer den ikke. | 1 |
| Mappingforslag | AI'ens forslag til en kobling mellem et krav og et arbejdselement eller en kontrol. Skal godkendes af et menneske. | 1 |
| Logpost | En registrering af, hvem der gjorde hvad og hvornår, fx at et katalog blev importeret eller et framework valgt. Ændres og slettes aldrig. | 1 |
| Audit | En intern eller ekstern gennemgang. | 2 |
| Fund | En afvigelse fundet ved en audit, med alvorlighed, frist og ansvarlig. | 2 |
| Rapportsnapshot | En låst, afleveret rapport. Hashværdien beviser, at den ikke er ændret. | 2 |

## Regler

Disse regler kan ikke udtrykkes med multipliciteter alene. De vises som noter i diagrammet og bliver til validering og unit tests i koden.

1. **Mappingforslag:** peger på præcis ét af følgende: et arbejdselement eller en kontrol.
2. **Kravvurdering:** hører til præcis ét af følgende: en release (projektframeworks som CRA) eller organisationen (organisationsframeworks som ISO 27001).
3. **Fund:** vedrører mindst ét krav eller én kontrol.
4. **Framework** er fælles referencedata (kun navn og niveau). **Indbyggede frameworkversioner** er også fælles. **Importerede frameworkversioner** ejes af præcis én organisation og er usynlige for alle andre.
5. **Evidens** knyttes altid til en kontrol og kan desuden knyttes til en release, når den kun gælder en bestemt version (fx en SBOM).
6. **En frameworkversion er låst, når den er aktiv.** Den starter som *kladde* og kan rettes; når den aktiveres, kan dens krav ikke længere ændres. Rettelser kræver en ny version. Derfor peger en mapping eller vurdering altid på præcis den tekst, den blev lavet imod.
7. **Kun aktive versioner kan vælges**, og et projekt kan kun vælge versioner med niveau *projekt*, mens organisationen vælger versioner med niveau *organisation*.
8. **Højst én version pr. framework** pr. projekt og pr. organisation ad gangen.
9. **AI-mapping i fase 1** laves kun mod krav fra indbyggede frameworkversioner (CRA og AI Act). Importerede kataloger mappes manuelt, indtil licensforholdene for AI-behandling er afklaret.
10. **Logposter** oprettes for alle ændringer af organisation, scope og kataloger og kan aldrig ændres eller slettes.

## Ændret 2026-10-19

- **Framework delt i Framework og Frameworkversion.** ISO 27001:2013 og 2022 har forskellig Annex A, så "ISO 27001" alene er ikke præcist nok. Organisationer og projekter vælger nu en version, og krav hører til versionen.
- **Kravkataloger importeres af organisationen.** ISO-standarder er ophavsretligt beskyttede, så trackeren leverer ikke selv kravteksten. Kun offentlig EU-lovgivning (CRA, AI Act fra EUR-Lex) følger med som indbygget katalog (regel 4).
- **AI'en mapper mod den gemte kravtekst**, ikke mod sin egen viden om reglerne. Låste versioner (regel 6) sikrer, at teksten ikke ændrer sig under en mapping.
- **Logpost tilføjet.** Flere stories kræver sporbarhed over, hvem der ændrede scope og kataloger (ISO 27001 A.8.15).
