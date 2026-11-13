# Ydelse

Trackeren holder sin ydelse under kontrol på tre måder:

- **US-139:** hver vigtig side har en grænse for, hvor mange databasekald den må lave, og antallet må ikke vokse med data. Det tjekkes i hver testkørsel. Et kald over 500 ms logges.
- **US-097:** grænser for tunge handlinger, så én bruger ikke kan gøre trackeren langsom for andre.
- **US-138:** en last- og stresstest med k6 (se `tests/load/README.md`). Den kører kun, når nogen starter den.

## Mål (US-138 R3)

| Hvad | Grænse |
|------|--------|
| Sider (p95) | under 1 sekund |
| Rapporter, Excel og PDF (p95) | under 5 sekunder |
| Fejl | under 1 % |
| 50 brugere på én gang i 5 minutter | holder grænserne |

Resultaterne af hver kørsel gemmes som `load-results` i GitHub i 90 dage, så kørsler kan sammenlignes over tid.
