# Construction Daily Report Approval System
[![.github/workflows/ci.yml](https://github.com/yhlinbim/construction-daily-report-system/actions/workflows/ci.yml/badge.svg?branch=feature%2FCDRA-15-unit-tests)](https://github.com/yhlinbim/construction-daily-report-system/actions/workflows/ci.yml)

## Why this project exists

In construction enterprise environments, site daily reporting often relies on
legacy BPM platforms where business rules are embedded in stored procedures
and platform configurations. This makes the logic untestable, hard to audit,
and tightly coupled to the platform vendor.

This project reimplements the same business domain using modern .NET 8
engineering practices — demonstrating how the same workflow can be built
with clean architecture, testable service layers, and automated CI/CD.

## Business Domain

Construction site workers submit daily progress reports each day.
Site supervisors review and approve or reject submissions.
Project managers have oversight across all site reports.

## Architecture

[To be updated as the project progresses]

## Technology Stack

- ASP.NET Core 8 MVC
- Entity Framework Core 8 (SQL Server / LocalDB)
- xUnit + Moq (unit testing)
- Swagger / OpenAPI
- GitHub Actions (CI/CD)
- Azure App Service (deployment)

## Status

🚧 Under active development — started June 2026
