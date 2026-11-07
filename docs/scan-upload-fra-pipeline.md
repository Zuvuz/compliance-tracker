# Scanninger fra byggepipelinen

US-122. En byggepipeline kan sende sine scanningsresultater (SARIF 2.1.0) direkte til trackeren med et upload-token for projektet. Scanningen gemmer commit og version, så man kan se, hvad der blev bygget. Det virker fra alle CI-systemer og med alle scannere, der skriver SARIF (Trivy, Grype, Semgrep, CodeQL, ZAP …).

## 1. Opret et token

1. Åbn projektet, fanen **Scans**, og fold **Upload tokens for build pipelines** ud.
2. Giv tokenet et navn, fx "GitHub Actions", og tryk **Create token**. Det kræver rollen Admin eller Compliance manager (`EditProjects`).
3. Kopiér tokenet med det samme. Det vises kun én gang, og trackeren gemmer kun dets SHA-256.
4. Læg tokenet som en hemmelighed i CI-systemet, fx GitHub-hemmeligheden `TRACKER_TOKEN`. Det må aldrig ligge i repositoriet.

Et token kan kun uploade til sit eget projekt og kan ikke læse noget. Man kan højst uploade 30 gange i timen med det samme token. Tryk **Revoke** for at spærre det med det samme.

## 2. Upload med curl

Projektets adresse står på fanen. `commit` og `version` er valgfrie.

```bash
curl --fail-with-body \
  -H "Authorization: Bearer $TRACKER_TOKEN" \
  -F "file=@trivy.sarif" \
  -F "commit=$(git rev-parse HEAD)" \
  -F "version=2.3.1" \
  https://tracker.example/api/projects/<projekt-id>/scans
```

Mod en lokal tracker er adressen `https://localhost:<port>/api/projects/<projekt-id>/scans`. Tilføj `-k`, hvis udviklingscertifikatet ikke er betroet.

## 3. GitHub Actions

```yaml
- name: Scan with Trivy
  uses: aquasecurity/trivy-action@<fast-version>
  with:
    scan-type: fs
    format: sarif
    output: trivy.sarif

- name: Send the scan to the compliance tracker
  env:
    TRACKER_TOKEN: ${{ secrets.TRACKER_TOKEN }}
    TRACKER_URL: ${{ vars.TRACKER_URL }}   # fx https://tracker.example/api/projects/<projekt-id>/scans
  run: |
    curl --fail-with-body \
      -H "Authorization: Bearer $TRACKER_TOKEN" \
      -F "file=@trivy.sarif" \
      -F "commit=$GITHUB_SHA" \
      -F "version=${GITHUB_REF_NAME}" \
      "$TRACKER_URL"
```

GitHub's egne runners kan kun nå en tracker, der er tilgængelig fra internettet. Det hører til driften, ikke trackeren.

## Svar

| Status | Betydning |
| --- | --- |
| 201 | Scanningen er importeret, fx `{"summary": "Trivy: 1 new, 2 fixed, 4 open", "scanId": "…", "commit": "3f2a9c1", "version": "2.3.1"}` |
| 400 | Noget mangler eller er forkert, fx en commit, der ikke er en Git-SHA på 7–40 tegn |
| 401 | Tokenet mangler, er forkert, er spærret eller hører til et andet projekt |
| 403 | Kaldet kom over http uden for udvikling. Brug https. |
| 413 | Filen er større end 50 MB |
| 415 | Filen er ikke SARIF 2.1.0 |
| 429 | Mere end 30 uploads i timen med samme token. Prøv igen om en time (`Retry-After`). |

API'et bruger ingen cookies og intet antiforgery-token. Det tager kun imod bearer-tokenet. Oprettelse og spærring af tokens logges med navnet, aldrig selve tokenet. Hver upload logges som en scanningsimport med tokenets navn.
