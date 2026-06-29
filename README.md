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

## Tech

- ASP.NET Core 8 MVC + Web API
- Entity Framework Core 8
- Azure SQL Database (Serverless) in production, LocalDB locally
- xUnit + Moq + FluentAssertions
- Serilog with Correlation ID middleware
- GitHub Actions CI/CD → Azure App Service

## Tests

41 tests across all four layers — domain, service, repository, and controller.
All passing in CI on every push.

## Running locally

```bash
git clone https://github.com/yhlinbim/construction-daily-report-system
cd construction-daily-report-system
dotnet restore
dotnet ef database update --project CDRS.Infrastructure --startup-project CDRS.Web
dotnet run --project CDRS.Web
```

Then open `/swagger`.
