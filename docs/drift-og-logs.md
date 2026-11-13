# Drift og logs (US-098)

Trackeren skriver sine logs som JSON til stdout. Det gælder uden for Development, og ellers når `Logging__Format=json` er sat. Docker samler dem, så de kan læses med `docker compose logs web` eller sendes videre til et logsystem, fx Seq, Loki eller Elastic. Det er et frit valg; trackeren kræver ikke et bestemt.

## Hvad en loglinje indeholder

- tid i UTC, niveau, kategori og besked
- `Reference`: requestens reference, den samme som fejlsiden viser ("If it happens again, give the operator this reference …")
- `UserId`: brugerens id fra Keycloak ("sub")

Logs indeholder aldrig navne, e-mails, tokens, adgangskoder, connection strings, filindhold eller teksten i user stories.

## Det, man kan alarmere på

| Hændelse | Niveau | EventId | Besked |
|----------|--------|---------|--------|
| Adgang nægtet (403, eller 404 på en side) | Warning | 4031 | `Security event: access refused (403) for GET /Activity` |
| Grænse for requests ramt (US-097) | Warning | 4291 | `Security event: request limit hit for POST …` |
| Login fejlede | Warning | 4011 | `Security event: sign-in failed (…)` |
| Mange fejl | Error | 5001 | `Error rate high: 11 errors in the last 5 minutes` |
| Langsomt databasekald (US-139) | Warning | – | `Slow database query: 812 ms on /Projects/…` |

"Error rate high" skrives én gang, når der er mere end 10 fejl (5xx) inden for 5 minutter. Den skrives først igen efter et roligt vindue.

## Indstillinger

- `Logging:LogLevel:*`: niveauerne, som i alle .NET-apps
- `Logging:Format`: `json` eller `simple`
- `Database:SlowQueryMilliseconds`: hvornår et databasekald er langsomt (500)
