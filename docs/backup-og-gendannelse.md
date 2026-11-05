---
title: Backup og gendannelse af databasen
version: 0.1
status: udkast
owner: Rune
references: ISO 27001 A.8.13, 7.5.3; US-014
created: 2026-11-03
next_review: 2027-02-01
---
# Backup og gendannelse af databasen

Denne procedure beskriver, hvordan trackerens database tages backup af, hvor backupperne ligger, og hvordan en backup gendannes. Den gælder Docker-installationen (`docker compose --profile full`, US-017).

## 1. Hvad tages der backup af?

Hele databasen `compliance_tracker`, med `pg_dump` i PostgreSQLs eget format (custom format). Det betyder:

- organisationer, brugere, roller og invitationer
- frameworks og deres PDF'er
- projekter, krav, koblinger og evidens med filer
- risici, AI-forslag og beslutninger
- aktivitetsloggen
- nøglerne, der beskytter GitHub-tokens

Keycloaks brugere ligger i Keycloaks egen volume og er ikke med. Det er de lokale certifikater i `./certs` heller ikke.

## 2. Hvornår og hvor?

| | |
|---|---|
| Hvornår | Hver nat kl. 02:00 (dansk tid). Når servicen starter, og dagens backup mangler, laves den med det samme. |
| Hvor | `./backups` på værtsmaskinen, uden for database-containeren. Mappen kommer aldrig i git. |
| Filnavn | `compliance_tracker_<åååå-mm-dd>.dump` |
| Hvor længe | 30 dage. Ældre backupper slettes, når den nye er lavet. |
| Hvem laver den | Servicen `backup` i `docker-compose.yml` (`docker/backup-scheduler.sh` → `docker/backup-db.sh`). |

En backup skrives først som `.partial` og kontrolleres med `pg_restore --list`, før den får sit rigtige navn. En backup, der fejlede, ser derfor aldrig færdig ud.

Servicen er "healthy", når der findes en backup, der er under 26 timer gammel:

```bash
docker compose --profile full ps backup
```

Backup-servicen forbinder til hele stakkens database, `postgres-full`, over TLS og godkender dens certifikat (US-084).

Udviklingsdatabasen kan få den samme backup for sig med servicen `backup-dev`. Backupperne havner i `./backups/dev`:

```bash
docker compose --profile backup up -d
```

## 3. Gendannelse

Gendannelse erstatter **alle** data i databasen med backuppen. Det sker i én transaktion: fejler noget, ændres intet. En fil, der ikke er en backup, afvises, før databasen røres.

1. Find backuppen i `./backups`.
2. Kør:

   ```bash
   ./scripts/restore.sh backups/compliance_tracker_2026-10-25.dump
   ```

3. Skriv `yes`, når scriptet spørger.

Scriptet stopper webappen, gendanner, og starter den igen. På Windows køres det fra Git Bash.

Scriptet gendanner i hele stakkens database (`postgres-full`). En backup af udviklingsdatabasen gendannes med `--dev`:

```bash
./scripts/restore.sh backups/dev/compliance_tracker_2026-10-25.dump --dev
```

## 4. Test af gendannelsen

Gendannelsen testes automatisk i hver CI-kørsel (`BackupAndRestore.feature`) med de samme scripts, som backup-servicen bruger:

- Der laves en backup af en database med en organisation, en framework-PDF og logposter. Den gendannes i en tom database, og testen tjekker, at organisationen, brugerne, PDF'en (byte for byte) og alle logposter er der.
- En backup fra 30 dage siden slettes, og en fra 29 dage siden beholdes.
- En fil, der ikke er en backup, afvises, og den tomme database forbliver tom.

**Spørgsmål:** Skal der også være en manuel gendannelsestest, fx hvert kvartal, hvor en rigtig backup gendannes i en separat database?

## 5. Ikke med endnu

- Kryptering af backupperne. De indeholder persondata og licenserede PDF'er, så de skal behandles som fortrolige.
- En kopi et andet sted, fx en anden maskine eller cloud.
- Point-in-time recovery.

## Ændringslog

| Version | Dato | Ændring |
| --- | --- | --- |
| 0.1 | 2026-11-03 | Første udgave (US-014) |
