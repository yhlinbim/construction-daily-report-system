# =============================================
# Stage 1: Build
# SDK image contains the full .NET build toolchain.
# Pinned to a patch tag + digest for reproducible builds.
# =============================================
FROM mcr.microsoft.com/dotnet/sdk:8.0.424@sha256:bb32ba3ba3ea36e38572d9d8db76fa15f7cbf722f3f886e06bca6d528bd4fba8 AS build
WORKDIR /src

# Copy solution and project files first (layer caching optimisation).
# If only code changes, NuGet restore is skipped on subsequent builds.
COPY ConstructionDailyReport.sln .
COPY CDRS.Domain/CDRS.Domain.csproj CDRS.Domain/
COPY CDRS.Application/CDRS.Application.csproj CDRS.Application/
COPY CDRS.Infrastructure/CDRS.Infrastructure.csproj CDRS.Infrastructure/
COPY CDRS.Web/CDRS.Web.csproj CDRS.Web/
COPY CDRS.Tests/CDRS.Tests.csproj CDRS.Tests/

RUN dotnet restore ConstructionDailyReport.sln

# Copy all remaining source files
COPY . .

# Run the test suite - the build fails if any test fails
RUN dotnet test CDRS.Tests/CDRS.Tests.csproj \
    --configuration Release \
    --no-restore \
    --verbosity minimal

# Publish release build to /app/publish
RUN dotnet publish CDRS.Web/CDRS.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# =============================================
# Stage 2: Runtime
# ASP.NET runtime image - no SDK, no build tools.
# Pinned to a patch tag + digest; runs as the image's non-root user.
# =============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0.30@sha256:787c228ea85457bec43c8b084e6ac360b26ea43b5c2fcbe861f721f2e8670dd3 AS runtime
WORKDIR /app

# curl is used only by the container HEALTHCHECK below
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy only the published output from the build stage, owned by the runtime user
COPY --chown=app:app --from=build /app/publish .

# Port 8080 (ASP.NET Core 8 default non-root port)
EXPOSE 8080

# Drop root - APP_UID (1654) and the 'app' user are defined by the base image
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "CDRS.Web.dll"]
