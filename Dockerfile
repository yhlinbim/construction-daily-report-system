# =============================================
# Stage 1: Build
# SDK image contains the full .NET build toolchain
# =============================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
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
# Significantly smaller and has a reduced attack surface.
# =============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy only the published output from the build stage
COPY --from=build /app/publish .

# Expose port 8080 (ASP.NET Core 8 default non-root port)
EXPOSE 8080

ENTRYPOINT ["dotnet", "CDRS.Web.dll"]
