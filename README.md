# Construction Daily Report Approval System

[![CI](https://github.com/yhlinbim/construction-daily-report-system/actions/workflows/ci.yml/badge.svg)](https://github.com/yhlinbim/construction-daily-report-system/actions/workflows/ci.yml)

## Live Demo

Swagger UI: https://cdrs-poc-hansl-hnh3gvavdsf7b3a5.australiaeast-01.azurewebsites.net/swagger

Running on Azure App Service (Australia East), deployed automatically via GitHub Actions.

## Background

Approval workflows in enterprise environments are often spread across
multiple layers — application code handles some logic, stored procedures
handle data access and transformations, platform configuration handles
routing. It works, but the business rules are scattered and hard to test.

I built this to explore what the same workflow looks like when the
business rules are consolidated in a domain entity with a clear state
machine, properly layered with Clean Architecture, and covered by
automated tests. The construction site daily report is the domain
context; the engineering practices are the point.

This is a side project applying modern .NET practices —
Clean Architecture, automated testing, and CI/CD — to a
business domain I know from real-world construction IT work.

## What it does

A simplified approval workflow for construction site daily reports,
modelling the core state transitions:

```
Draft → Submitted → UnderReview → Approved
                                ↘ Rejected
```

Real-world approval workflows are more complex — delegated signing,
escalation, rejection to specific steps, proxy approval. This project
focuses on the engineering fundamentals: how to model a state machine
in a domain entity, enforce business rules, and test them in isolation.

## How it's structured

```
Web (Controllers + API endpoints)
    ↓
Application (DailyReportService, interfaces)
    ↓
Infrastructure (EF Core, Repository)
    ↓
Domain (DailyReport — pure business logic, no dependencies)
```

The business rules live in the domain entity. The service layer
coordinates the steps. Controllers just handle HTTP.

## Technology Stack

| Category | Technology | Purpose |
|----------|-----------|---------|
| Framework | ASP.NET Core 8 | Web API + MVC |
| ORM | Entity Framework Core 8 | Database access |
| Database (local) | SQL Server LocalDB | Development |
| Database (cloud) | Azure SQL Database (Serverless) | Production |
| Testing | xUnit + Moq + FluentAssertions | Unit + Integration tests |
| Logging | Serilog (structured JSON) | Structured request logging |
| Monitoring | Azure Application Insights | Production telemetry and alerting |
| Authentication | JWT Bearer + RBAC | API security |
| API | REST + GraphQL (Hot Chocolate) | Flexible query interface |
| API Versioning | Asp.Versioning 8.1.0 | URL-based versioning (v1/v2) |
| Rate Limiting | Fixed Window (60 req/min) | API protection |
| Secret Management | Azure Key Vault + Managed Identity | Production secrets |
| Containerisation | Docker (multi-stage build) | Portable deployment |
| CI/CD | GitHub Actions → Azure App Service | Automated build, test, and deploy |
| Cloud | Azure App Service (Australia East) | Hosting |

## Engineering Practices

- **Domain-driven design**: Aggregate with state machine, domain exceptions enforced at the domain layer
- **Testing**: 73 unit + integration tests, AAA pattern, Moq for isolation; 80% coverage threshold enforced in CI
- **CI/CD**: Automated quality gate — failing tests block deployment; separate migration job before deploy
- **Structured logging**: Serilog with Correlation ID middleware for end-to-end request tracing
- **Security**: No secrets in source control; environment-based configuration; Azure Key Vault + Managed Identity for production secrets
- **API design**: REST for write operations with JWT/RBAC; GraphQL for flexible read queries
- **API versioning**: URL-based versioning — v1 deprecated, v2 introduces breaking change (status as string)
- **Rate limiting**: Fixed window (60 req/min) partitioned by user identity, falling back to IP
- **CORS**: Configuration-driven allowed origins per environment — no hardcoded values
- **Monitoring**: Azure Application Insights for request tracking, exception monitoring, and EF Core dependency telemetry
- **Containerisation**: Multi-stage Dockerfile — tests run during build; failing tests block image creation
- **Project management**: Git feature-branch workflow, PR reviews with written technical decision rationale on every pull request, Jira Scrum board across 6 sprints

Each pull request includes written rationale for the technical decisions made — see the PR history for the full record.

## Tests

73 tests across all four layers — domain, service, repository, and controller.
All passing in CI on every push. Coverage threshold of 80% enforced for Domain and Application layers.

## API Versions

| Version | Status | Notes |
|---------|--------|-------|
| v1 | Deprecated | `status` field returns integer |
| v2 | Active | `status` field returns string; `statusCode` added for backward reference |

## Prerequisites

- .NET 8 SDK
- SQL Server LocalDB (Windows) or Docker (macOS/Linux)
- EF Core CLI: `dotnet tool install --global dotnet-ef`

## Running locally

**Windows:**

```bash
git clone https://github.com/yhlinbim/construction-daily-report-system
cd construction-daily-report-system
dotnet restore
dotnet ef database update --project CDRS.Infrastructure --startup-project CDRS.Web
dotnet run --project CDRS.Web
```

**macOS/Linux:** Start SQL Server via Docker before running migrations:

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPassword123!" \
  -p 1433:1433 --name sqlserver -d mcr.microsoft.com/mssql/server:2022-latest
```

Then update `appsettings.Development.json` with the connection string, run migrations, and start the app:

```bash
dotnet restore
dotnet ef database update --project CDRS.Infrastructure --startup-project CDRS.Web
dotnet run --project CDRS.Web
```

Then open `/swagger`.
