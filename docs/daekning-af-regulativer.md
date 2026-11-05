# Dækning af de indbyggede regulativer

US-068. For hvert indbygget regulativ står her hver artikel og hvert bilag, og om trackeren har kravene i dem. Listen siger, hvad trackeren indeholder, ikke hvordan et regulativ skal fortolkes. Den er ikke juridisk rådgivning.

**Status:**

- **I trackeren:** kravene er indbygget, med de koder der står i sidste kolonne.
- **Mangler:** en pligt for producenten eller udbyderen, som trackeren ikke har endnu. Kolonnen linker til det issue, der tilføjer den.
- **Udeladt:** pligten gælder ikke producenten eller udbyderen, der bruger trackeren (fx en myndighed eller et bemyndiget organ), eller den indeholder ingen pligt. Grunden står i kolonnen.

Testen `RegulationCoverage.feature` fejler, hvis en kode under "I trackeren" ikke findes blandt de indbyggede krav, hvis et indbygget krav ikke står på listen, eller hvis en "Mangler" ikke linker til et issue (R5).

Listen gennemgås igen, når et regulativ får en ny version (US-035).

## CRA – Regulation (EU) 2024/2847

- **Tjekket mod:** den officielle engelske tekst på EUR-Lex, OJ L, 2024/2847, 20.11.2024 (<https://eur-lex.europa.eu/eli/reg/2024/2847/oj>).
- **Tjekket:** 05-11-2026.
- **Gælder fra:** 11-12-2027. Artikel 14 gælder fra 11-09-2026, og kapitel IV (artikel 35–51) fra 11-06-2026 (artikel 71).

| Del | Indhold (med egne ord) | Status | Koder eller issue |
|---|---|---|---|
| Art. 1–5 | Formål, anvendelsesområde, definitioner, fri bevægelighed og offentlige indkøb | Udeladt | Ingen pligt i sig selv. Anvendelsesområdet bruges, når regulativet slås til for organisationen |
| Art. 6 | Produkter skal opfylde de væsentlige krav i bilag I | I trackeren | Se bilag I nedenfor |
| Art. 7–8 | Vigtige og kritiske produkter (bilag III og IV) | Mangler | [#58](https://github.com/Zuvuz/compliance-tracker/issues/58) (US-026, klassificering) |
| Art. 9–11 | Høring, kompetencer og generel produktsikkerhed | Udeladt | Pligter for Kommissionen og medlemsstaterne |
| Art. 12 | Højrisiko-AI-systemer, der også er produkter med digitale elementer | Udeladt | Samspillet med AI Act håndteres under AI Act nedenfor |
| Art. 13(1) | Produktet er designet, udviklet og produceret efter bilag I, del I | I trackeren | Se bilag I, del I |
| Art. 13(2)–(4) | En dokumenteret risikovurdering, der holdes opdateret, og som følger med den tekniske dokumentation | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) (US-069) |
| Art. 13(5) | Due diligence, når komponenter fra tredjepart bygges ind | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Art. 13(6)–(7) | Fejl i en komponent meldes til dem, der vedligeholder den, og sikkerheden dokumenteres løbende | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Art. 13(8) | Sårbarheder håndteres efter bilag I, del II, i hele supportperioden | I trackeren | Se bilag I, del II |
| Art. 13(8) | Supportperioden fastsættes ud fra, hvor længe produktet forventes brugt | Mangler | [#57](https://github.com/Zuvuz/compliance-tracker/issues/57) (US-025, supportperiode) |
| Art. 13(9) | Hver sikkerhedsopdatering forbliver tilgængelig i mindst 10 år eller supportperioden | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Art. 13(10)–(11) | Valgfrit: kun nyeste version opdateres, og offentlige arkiver med gamle versioner | Udeladt | Muligheder ("may"), ikke pligter |
| Art. 13(12) | Teknisk dokumentation og overensstemmelsesvurdering, før produktet sælges | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) (se også art. 31–32) |
| Art. 13(13) | Teknisk dokumentation og EU-overensstemmelseserklæring opbevares i mindst 10 år eller supportperioden, hvis den er længere | Mangler | [#134](https://github.com/Zuvuz/compliance-tracker/issues/134) (US-074) |
| Art. 13(14) | Produkter i serieproduktion forbliver i overensstemmelse | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Art. 13(15)–(17) | Typenummer, producentens kontaktoplysninger og ét kontaktpunkt til sårbarheder | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Art. 13(18)–(20) | Information og vejledning efter bilag II, supportperiodens slutdato og en kopi af overensstemmelseserklæringen | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Art. 13(21) | Korrigerende handling, hvis produktet ikke er i overensstemmelse | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Art. 13(22) | Oplysninger til markedsovervågningen, når den beder om dem | Udeladt | Et svar på en anmodning, ikke noget, der kan opfyldes på forhånd |
| Art. 13(23) | Besked til myndigheder og brugere, hvis producenten lukker | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Art. 13(24)–(25) | Kommissionens gennemførelsesretsakter | Udeladt | Pligter for Kommissionen |
| Art. 14 | Indberetning af aktivt udnyttede sårbarheder og alvorlige hændelser (24 timer, 72 timer, 14 dage og en måned) | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) (krav) og [#133](https://github.com/Zuvuz/compliance-tracker/issues/133) (US-073, frister) |
| Art. 15 | Frivillig indberetning | Udeladt | Frivilligt |
| Art. 16–17 | ENISA's indberetningsplatform og regler for den | Udeladt | Pligter for ENISA og myndighederne |
| Art. 18 | Bemyndiget repræsentant | Udeladt | Valgfrit for producenten |
| Art. 19–22 | Importører, distributører, og hvornår de får producentens pligter | Udeladt | Trackeren følger producentens pligter (uden for US-069) |
| Art. 23 | Oplysninger om, hvem der har leveret produktet, når myndighederne beder om det | Udeladt | Et svar på en anmodning |
| Art. 24–25 | Open source-forvaltere og sikkerhedsattestering af open source | Udeladt | Gælder open source-forvaltere, ikke producenter |
| Art. 26 | Vejledning fra Kommissionen | Udeladt | Pligt for Kommissionen |
| Art. 27 | Formodning om overensstemmelse (standarder og certificering) | Udeladt | Ingen pligt. Bruges i bilag VII, punkt 5 |
| Art. 28 | EU-overensstemmelseserklæring | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Art. 29–30 | CE-mærkning og reglerne for den | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Art. 31 | Teknisk dokumentation | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) (krav) og [#105](https://github.com/Zuvuz/compliance-tracker/issues/105) (US-056, rapport) |
| Art. 32 | Procedurer for overensstemmelsesvurdering | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Art. 33–34 | Støtte til SMV'er og aftaler om gensidig anerkendelse | Udeladt | Pligter for medlemsstaterne og Kommissionen |
| Art. 35–51 | Bemyndigende myndigheder og bemyndigede organer | Udeladt | Pligter for myndigheder og bemyndigede organer |
| Art. 52–60 | Markedsovervågning og håndhævelse | Udeladt | Pligter for markedsovervågningen |
| Art. 61–62 | Delegerede retsakter og udvalgsprocedure | Udeladt | Kommissionen |
| Art. 63–65 | Fortrolighed, sanktioner og gruppesøgsmål | Udeladt | Konsekvenser og myndigheder, ingen pligt for producenten |
| Art. 66–71 | Ændringer af andre regler, overgangsregler, evaluering og ikrafttræden | Udeladt | Datoerne bruges som versionens startdato (US-035) |
| Bilag I, del I | Væsentlige cybersikkerhedskrav | I trackeren | I.1, I.2(a), I.2(b), I.2(c), I.2(d), I.2(e), I.2(f), I.2(g), I.2(h), I.2(i), I.2(j), I.2(k), I.2(l), I.2(m) |
| Bilag I, del II | Håndtering af sårbarheder | I trackeren | II.1, II.2, II.3, II.4, II.5, II.6, II.7, II.8 |
| Bilag II | Information og vejledning til brugeren (9 punkter) | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Bilag III–IV | Vigtige og kritiske produkter | Mangler | [#58](https://github.com/Zuvuz/compliance-tracker/issues/58) (US-026, klassificering) |
| Bilag V–VI | Indholdet af EU-overensstemmelseserklæringen og den forenklede udgave | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Bilag VII | Indholdet af den tekniske dokumentation (8 punkter) | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |
| Bilag VIII | Procedurerne for overensstemmelsesvurdering | Mangler | [#129](https://github.com/Zuvuz/compliance-tracker/issues/129) |

**Fundet under gennemgangen:** CRA's pligter om dokumentation og overensstemmelse står ikke kun i artikel 13, men også i artikel 28 (overensstemmelseserklæring), 29–30 (CE-mærkning), 31 (teknisk dokumentation) og 32 (overensstemmelsesvurdering). US-069 (#129) skal derfor også dække dem.

## AI Act – Regulation (EU) 2024/1689

- **Tjekket mod:** den officielle engelske tekst på EUR-Lex, OJ L, 2024/1689, 12.7.2024 (<https://eur-lex.europa.eu/eli/reg/2024/1689/oj>).
- **Tjekket:** 05-11-2026.
- **Gælder fra:** 02-08-2026, med disse undtagelser (artikel 113):
  - Kapitel I og II gælder fra 02-02-2025.
  - Kapitel III, afdeling 4, kapitel V, VII og XII og artikel 78 gælder fra 02-08-2025, undtagen artikel 101.
  - Artikel 6(1) og de pligter, der hører til, gælder fra 02-08-2027.

AI Act er indbygget, men har **ingen krav endnu**. De kommer med US-062 ([#117](https://github.com/Zuvuz/compliance-tracker/issues/117)).

| Del | Indhold (med egne ord) | Status | Koder eller issue |
|---|---|---|---|
| Art. 1–3 | Formål, anvendelsesområde og definitioner | Udeladt | Ingen pligt i sig selv |
| Art. 4 | AI-færdigheder hos dem, der udvikler og bruger AI | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) |
| Art. 5 | Forbudt AI-praksis | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) |
| Art. 6–7 | Klassificering som højrisiko | Mangler | [#58](https://github.com/Zuvuz/compliance-tracker/issues/58) (US-026) og [#116](https://github.com/Zuvuz/compliance-tracker/issues/116) (US-061) |
| Art. 8–15 | Krav til højrisiko-AI-systemer: risikostyring, data, teknisk dokumentation, logning, gennemsigtighed, menneskeligt tilsyn, nøjagtighed, robusthed og cybersikkerhed | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) |
| Art. 16–21 | Udbyderens pligter: kvalitetsstyring, opbevaring af dokumentation, logs, korrigerende handlinger og samarbejde med myndigheder | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) |
| Art. 22–25 | Bemyndigede repræsentanter, importører, distributører og værdikæden | Udeladt | Trackeren følger udbyderens og idriftsætterens pligter |
| Art. 26–27 | Idriftsætterens pligter og konsekvensanalyse for grundlæggende rettigheder | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) (og [#145](https://github.com/Zuvuz/compliance-tracker/issues/145), US-085, register over AI-systemer) |
| Art. 28–39 | Bemyndigende myndigheder og bemyndigede organer | Udeladt | Pligter for myndigheder og bemyndigede organer |
| Art. 40–42 | Standarder, fælles specifikationer og formodning om overensstemmelse | Udeladt | Ingen pligt |
| Art. 43 | Overensstemmelsesvurdering | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) |
| Art. 44–46 | Certifikater, bemyndigede organers oplysningspligt og undtagelser | Udeladt | Pligter for bemyndigede organer og myndigheder |
| Art. 47–49 | EU-overensstemmelseserklæring, CE-mærkning og registrering | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) |
| Art. 50 | Gennemsigtighed for visse AI-systemer (fx chatbots og genereret indhold) | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) |
| Art. 51–56 | AI-modeller til almene formål | Udeladt | Gælder udbydere af store modeller. Uden for trackerens målgruppe indtil videre |
| Art. 57–63 | Reguleringsmæssige sandkasser og test under virkelige forhold | Udeladt | Frivillige ordninger |
| Art. 64–71 | Forvaltning, myndigheder og EU-databasen | Udeladt | Pligter for EU og myndighederne |
| Art. 72 | Overvågning efter markedsføring | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) |
| Art. 73 | Indberetning af alvorlige hændelser | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) |
| Art. 74–94 | Markedsovervågning, klageadgang og tilsyn med modeller til almene formål | Udeladt | Pligter for myndighederne |
| Art. 95–113 | Adfærdskodekser, vejledning, sanktioner og afsluttende bestemmelser | Udeladt | Frivilligt, Kommissionen og konsekvenser |
| Bilag I–II | Lister over EU-lovgivning og strafbare handlinger | Udeladt | Opslagslister, ingen pligt |
| Bilag III | Højrisiko-områder | Mangler | [#58](https://github.com/Zuvuz/compliance-tracker/issues/58) (US-026) |
| Bilag IV | Indholdet af den tekniske dokumentation | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) |
| Bilag V–VII | Overensstemmelseserklæring og procedurer for overensstemmelsesvurdering | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) |
| Bilag VIII | Oplysninger ved registrering af højrisiko-systemer | Mangler | [#117](https://github.com/Zuvuz/compliance-tracker/issues/117) |
| Bilag IX–XIII | Test under virkelige forhold, store IT-systemer og AI-modeller til almene formål | Udeladt | Se art. 51–63 |
