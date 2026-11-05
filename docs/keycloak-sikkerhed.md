# Keycloak: beskyttelse af log-ind

US-089. Realm-filen `keycloak/compliance-tracker-realm.json` sætter disse indstillinger. En **ny** Keycloak får dem, når realmen importeres første gang (`--import-realm`). En Keycloak, der allerede kører, ændres **ikke** af trackeren.

| Indstilling | Værdi | Krav |
|---|---|---|
| Beskyttelse mod brute force | Til. Efter 5 forkerte passwords låses kontoen midlertidigt: først 1 minut, derefter længere og længere, højst 15 minutter. Ingen permanent lås | ISO 27001 A.8.5, ASVS V2.2 |
| Password-politik | Mindst 12 tegn, ikke brugernavnet, ikke e-mailen og ikke et af de 3 seneste passwords | ISO 27001 A.5.17, ASVS V2.1 |
| Engangskode (MFA) | **Alle brugere** skal sætte en authenticator-app op (TOTP: 6 cifre hvert 30. sekund) ved første log-ind. Derefter beder Keycloak om koden ved hvert log-ind | ISO 27001 A.8.5, ASVS V2.8 |

MFA gælder alle, ikke kun organisationens Admins. Keycloak kender ikke trackerens roller, og alle brugere kan se fortrolige compliance-data (B39).

## En Keycloak, der allerede kører

Realmen importeres kun, når den ikke findes. Indstillingerne sættes derfor i Keycloaks admin-konsol, realm **compliance-tracker**:

1. **Realm settings → Security defenses → Brute force detection:**
   - Brute force mode: *Lockout temporarily*
   - Max login failures: 5
   - Wait increment: 1 minut
   - Max wait: 15 minutter
2. **Authentication → Policies → Password policy:**
   - Minimum length 12
   - Not username
   - Not email
   - Password history 3
3. **Authentication → Required actions:** sæt *Set as default action* til for **Configure OTP**. Så skal alle nye brugere sætte engangskoden op.
4. **Eksisterende brugere:** åbn brugeren under **Users**, og vælg *Configure OTP* under **Required user actions**. Brugeren sætter koden op ved næste log-ind.

Alternativet er at slette realmen og importere den igen. Det sletter alle brugere i Keycloak, så det kun er en mulighed i et udviklingsmiljø.

## Afprøvning

- `KeycloakHardening.feature` starter en rigtig Keycloak med realm-filen og tjekker:
  - brute force-beskyttelsen og password-politikken
  - at en ny bruger skal sætte engangskoden op
  - at første log-ind sætter den op, og at næste log-ind beder om koden
  - at 5 forkerte passwords låser kontoen
- CI-jobbet `docker` tjekker de samme indstillinger i Docker-stakkens Keycloak efter importen.
