# US-017: the tracker as container images, built in stages.
#   build   – restores and publishes the web app with the .NET SDK
#   bundle  – makes an EF Core migration bundle (one file that brings the database up to date)
#   migrate – a small image that only runs the bundle (the "migrate" service in docker-compose.yml)
#   web     – the web app on Microsoft's ASP.NET image (the default target, last in the file)
# Both run images use the non-root user "app" that Microsoft's images include (R6). No secrets are baked in:
# they come from environment variables when the container starts.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The project files first, so "restore" is cached until a dependency changes
COPY src/ComplianceTracker.Domain/ComplianceTracker.Domain.csproj src/ComplianceTracker.Domain/
COPY src/ComplianceTracker.Application/ComplianceTracker.Application.csproj src/ComplianceTracker.Application/
COPY src/ComplianceTracker.Infrastructure/ComplianceTracker.Infrastructure.csproj src/ComplianceTracker.Infrastructure/
COPY src/ComplianceTracker.Web/ComplianceTracker.Web.csproj src/ComplianceTracker.Web/
RUN dotnet restore src/ComplianceTracker.Web/ComplianceTracker.Web.csproj

COPY src/ src/
RUN dotnet publish src/ComplianceTracker.Web/ComplianceTracker.Web.csproj --configuration Release --no-restore --output /app

FROM build AS bundle
# The same version as Microsoft.EntityFrameworkCore.Design in the Infrastructure project
RUN dotnet tool install --global dotnet-ef --version 10.0.13
ENV PATH="${PATH}:/root/.dotnet/tools"
# The bundle reads the connection string from ConnectionStrings__ComplianceTracker (DesignTimeDbContextFactory)
# when it runs. Making it only needs a value, not a database, so a placeholder without a password is used here.
RUN ConnectionStrings__ComplianceTracker="Host=build-only" dotnet ef migrations bundle \
    --project src/ComplianceTracker.Infrastructure \
    --startup-project src/ComplianceTracker.Infrastructure \
    --configuration Release \
    --self-contained --target-runtime linux-x64 \
    --output /bundle/efbundle --force

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrate
WORKDIR /app
COPY --from=bundle /bundle/efbundle ./efbundle
USER $APP_UID
# R2: exits with an error when a migration fails, so the web app is not started
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS web
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ComplianceTracker.Web.dll"]
