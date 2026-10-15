# Dokumentation
Dette er et projekt som jeg laver til gavn for mit portfolio. Det er en fiktiv orginasation som laver et compliance tracker system. Her anvendes AI til at hjælpe med at generere kontekst, scope og lignende dokumenter. Det er ej heller alt regulativer som er 100% sandt da det ikke er alle dokumenter som er tilgængelig uden betaling. 

De tre første dokumenter lægger fundamentet: hvem virksomheden er, hvad produktet skal, og hvilke regler der gælder. Alt senere (risici, krav, politikker) bygger på dem, så de skrives først og opdateres løbende.

Tekst i *kursiv* er udkast, du kan rette til. Linjer der starter med **Spørgsmål:** er huller, du selv skal udfylde.

| Dok-ID | Titel | Primær reference | Version |
| --- | --- | --- | --- |
| [DOK-01](DOK-01-kontekst-og-scope.md) | Virksomhedens kontekst og scope | ISO 27001 kap. 4.1–4.3 | 0.1 |
| [DOK-02](DOK-02-tilsigtet-formaal.md) | Produktets tilsigtede formål | CRA art. 3 og 13, AI Act art. 3(12) | 0.1 |
| [DOK-03](DOK-03-regulatorisk-klassificering.md) | Regulatorisk klassificering | AI Act art. 5, 6, 50 og bilag III; CRA art. 7 og bilag III–IV | 0.1 |

**Dokumentstyring (ISO 27001 kap. 7.5):** Hvert dokument får ID, version, dato og ændringslog. Dokumenterne ligger som Markdown i `docs/`, så Git-historikken fungerer som ændringslog. Hvert dokument har front matter øverst og en ændringslog nederst.

## Næste dokumenter

Når de tre første er rettet til, følger disse i rækkefølge. Hver bygger på det forrige.

- [ ] DOK-04: Aktiver og dataflowdiagram (bruger → webapp → database → AI-service)
- [ ] DOK-05: Trusselsmodel (STRIDE) og risikoregister, inkl. CRA art. 13-vurdering
- [ ] DOK-06: Kravkatalog med sporbarhed (krav-ID → kilde → risiko → evidens)
- [ ] DOK-07: Statement of Applicability for ISO 27001 Annex A
- [ ] DOK-08: Informationssikkerhedspolitik (kort, skrives sidst)
- [ ] DOK-09: Politik for sårbarhedshåndtering og offentliggørelse (CRA, ISO/IEC 29147)

**Tip:** Mens du skriver, så noter hvilke felter og relationer du selv mangler. Det er præcis dem, compliance-trackeren skal kunne håndtere.
