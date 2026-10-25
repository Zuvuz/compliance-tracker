# Compliance Tracker

A tracker that helps an organisation keep track of the standards and regulations (ISO/IEC 27001, CRA, AI Act and its own policies) that it and its products must meet.

- Architecture: Clean Architecture in .NET 10 (`Domain`, `Application`, `Infrastructure`, `Web`)
- Web: ASP.NET Core MVC
- Database: PostgreSQL with EF Core
- Sign-in: Keycloak via OpenID Connect
- Tests: Reqnroll (BDD) with PostgreSQL and Keycloak in Testcontainers

Project documentation (scope, domain model, class diagram, decisions) is in [`docs/`](docs/README.md).

## Getting started

You need [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Docker Desktop](https://www.docker.com/products/docker-desktop/).

### 1. Passwords for the local environment

Copy `.env.example` to `.env` and choose your own passwords. `.env` is never committed.

```
POSTGRES_PASSWORD=choose-a-password
KEYCLOAK_ADMIN_PASSWORD=choose-another-password
```

### 2. Start PostgreSQL and Keycloak

```bash
docker compose up -d
```

| Service | Address | Notes |
|---|---|---|
| PostgreSQL | `localhost:5433` | Database `compliance_tracker`, user `postgres` |
| Keycloak | `http://localhost:8180` | Admin user `admin`, password from `.env` |

The first start creates the realm `compliance-tracker` from [`keycloak/compliance-tracker-realm.json`](keycloak/compliance-tracker-realm.json), with the client `compliance-tracker-web` and self-registration enabled. Data survives `docker compose down` / `up`.

### 3. Connection string and client secret

Both are read from environment variables, never from files in the repo.

1. In Keycloak (`http://localhost:8180`), open the realm **compliance-tracker** → **Clients** → **compliance-tracker-web** → **Credentials**, and copy the **Client secret**.
2. Set the two variables (Windows; open a new terminal and restart Visual Studio afterwards):

```bash
setx ConnectionStrings__ComplianceTracker "Host=localhost;Port=5433;Database=compliance_tracker;Username=postgres;Password=<POSTGRES_PASSWORD>"
```

```bash
setx Authentication__Keycloak__ClientSecret "<client secret>"
```

### 4. Create the database tables

```bash
dotnet tool install --global dotnet-ef
```

```bash
dotnet ef database update --project src/ComplianceTracker.Infrastructure
```

### 5. Run the app

```bash
dotnet run --project src/ComplianceTracker.Web --launch-profile https
```

Open `https://localhost:7068`, click **Sign in**, and choose **Register** to create a user in Keycloak. After the first sign-in you create your organisation.

## Running the tests

```bash
dotnet test
```

Docker Desktop must be running: the integration tests start their own PostgreSQL and Keycloak containers with Testcontainers, so they do not use your local environment. The Keycloak scenario takes about a minute the first time, because the image is downloaded.

## Stopping and resetting

```bash
docker compose down
```

To delete all local data as well (database, Keycloak users and settings):

```bash
docker compose down --volumes
```
