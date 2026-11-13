# Last- og stresstest (US-138)

k6-scripts, der tester trackeren med mange brugere på én gang. De kører mod hele systemet i docker compose med testdata og testbrugere, aldrig mod produktion.

## I GitHub (gratis, kun når du starter den)

Actions → **Load test** → **Run workflow**. Du kan vælge antal brugere (50) og varighed (5m). Workflowen:

1. starter systemet med `docker compose --profile full`
2. laver testbrugerne `load-user-1 … n` i test-realmen med et tilfældigt kodeord, som kun findes i den kørsel
3. fylder databasen med `tests/load/seed`: 5 projekter, CRA, AI Act og ISO/IEC 27001, 1.000 stories, 300 evidens, 200 risici og 5.000 logposter
4. kører `smoke.js`, `load.js` og `stress.js` og til sidst `smoke.js` igen efter et minut, som tjekker, at systemet kommer sig (R5)
5. gemmer resultaterne (`load-results`) i 90 dage

## Scripts

| Script | Hvad |
|--------|------|
| `smoke.js` | 2 brugere i 30 sekunder |
| `load.js` | `LOAD_USERS` brugere i `LOAD_DURATION`. Fejler, hvis p95 er over 1 s for sider eller 5 s for rapporter, eller hvis mere end 1 % fejler. |
| `stress.js` | flere og flere brugere, op til fire gange `LOAD_USERS`. Resumeet viser, hvor grænserne blev passeret. |

## Lokalt

Kræver k6 eller Docker. Du starter systemet, laver brugerne og fylder databasen som i workflowen. Derefter:

```
docker run --rm --network host -v "$PWD/tests/load:/scripts:ro" -e LOAD_PASSWORD=... grafana/k6:0.54.0 run /scripts/smoke.js
```
