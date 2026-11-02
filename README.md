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

Nothing to do: when the app runs locally (Development), it creates the tables and runs new migrations
by itself at start-up (`Database:MigrateOnStartup` in `appsettings.Development.json`).

To run them by hand instead, e.g. outside Development:

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

## Roles and the system admin

The person who creates an organisation is its **organisation admin**. They invite colleagues from the organisation's **Users** tab with a role (Organisation admin, Compliance manager, Developer or Auditor). The table "What may each role do?" on that tab shows the permissions.

The **system admin** is the owner of the tracker. They see all organisations and their members under **System**, and can change roles, but cannot see the organisations' data. It is a Keycloak realm role, so the app can never give it:

1. Open Keycloak's admin console (http://localhost:8180, user `admin`).
2. Choose the realm `compliance-tracker`, then **Users**, then your user, then **Role mapping**, then **Assign role**, and choose `system-admin`.
3. Sign out of the tracker and in again.

A new Keycloak gets the role and the "realm roles" mapper from `keycloak/compliance-tracker-realm.json`. A Keycloak created before that has neither. Either add them by hand (**Realm roles**: create `system-admin`. **Clients**: open `compliance-tracker-web`, then **Client scopes**, then the dedicated scope, then **Add mapper**, **By configuration**, **User Realm Role**, with the token claim name `roles` and "Add to ID token" on), or delete the realm so it is imported again.

## The local AI (optional)

AI suggestions (US-020) need a local model server. Without one, the app works, and "Suggest mappings" says the AI is not available.

1. Install [Ollama](https://ollama.com/download).
2. Download the two default models:

```bash
ollama pull nomic-embed-text
```

```bash
ollama pull qwen2.5:3b
```

The models are set in the `Ai` section of `appsettings.json` and can be changed without code changes. For example, to use a bigger chat model, or another server with an OpenAI-compatible API (vLLM, LM Studio, llama.cpp):

```bash
setx Ai__ChatModel "qwen2.5:7b"
```

```bash
setx Ai__Provider "OpenAICompatible"
```

```bash
setx Ai__BaseUrl "http://my-gpu-server:8000"
```

Every suggestion stores which models and prompt version made it, so results from different models can be compared.

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
