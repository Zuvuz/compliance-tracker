# Alarmer fra GitHub og overvågning

US-160. Alvorlige alarmer bliver foreslåede sikkerhedshændelser på fanen **Security events**. Intet bliver en hændelse af sig selv: en person registrerer eller afviser hvert forslag.

## GitHub

Når projektet er koblet til et repository, henter **Fetch from GitHub** på fanen **Scans** alarmerne fra code scanning, Dependabot og secret scanning (US-121).

- En kritisk eller høj alarm foreslås som en sårbarhed. En Dependabot-alarm nævner pakken, fx "Vulnerability in left-pad (CVE-2026-1234)".
- En lækket hemmelighed foreslås som en hændelse, fx "Leaked secret: GitHub Personal Access Token". Selve hemmeligheden hentes aldrig.
- Tokenet skal kunne læse sikkerhedsalarmer (`security_events`, og for secret scanning `secret_scanning_alerts: read`). Ellers står der en advarsel, og de andre kilder hentes stadig.

## Overvågning

Overvågningen sender alarmer med projektets upload-token (se [scan-upload-fra-pipeline.md](scan-upload-fra-pipeline.md)). Adressen står på fanen **Scans** under **Upload tokens for build pipelines**.

```bash
curl --fail-with-body \
  -H "Authorization: Bearer $TRACKER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"alerts":[{"id":"abc","name":"HighErrorRate","status":"firing","severity":"critical","summary":"Errors above 5%"}]}' \
  https://tracker.example.com/api/projects/<id>/alerts
```

Prometheus Alertmanager kan sende direkte:

```yaml
receivers:
  - name: compliance-tracker
    webhook_configs:
      - url: https://tracker.example.com/api/projects/<id>/alerts
        http_config:
          authorization:
            credentials_file: /etc/alertmanager/tracker-token
```

- En `critical`- eller `high`-alarm med `firing` foreslås én gang, så længe den er åben. Andre alvorligheder foreslås ikke.
- `resolved` lukker forslaget med "Resolved at the source", hvis ingen har taget stilling til det.
- Svaret er 200 med antal forslag og lukninger, 401 ved et forkert eller spærret token og 400 ved en alarm over 256 KB eller i en ukendt form. En afvist alarm logges som `SecurityAlertRefused` uden indholdet.
- Adressen kræver HTTPS uden for udvikling.
