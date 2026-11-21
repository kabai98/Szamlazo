# Repository Guidelines

## Project Structure

Számlázó is a Hungarian expense tracker built with ASP.NET Core 10, Angular 22, and SQLite. Angular source, templates, and styles live in `web/src/`.

## Clean Architecture

Apply these boundaries to every backend change. Dependencies point inward toward Application and Domain.

- **API** (`api/Controllers/`). Keep controllers thin. Bind HTTP requests, enforce authentication, pass the current account and cancellation token to a use case, and return the appropriate HTTP response. Wire dependencies in `api/Program.cs`.
- **Application** (`backend/Application/`). Put use-case orchestration and cross-entity validation in services, request and response shapes in DTOs, and persistence or import interfaces in `Contracts/`. Keep ASP.NET Core and EF Core out of this layer.
- **Domain** (`backend/Domain/`). Keep entities, enums, and business invariants here. Reuse the existing money conversions and category definitions. Keep Domain independent of the other projects.
- **Infrastructure** (`backend/Infrastructure/`). Implement contracts with EF Core, SQLite, and document readers. Keep queries account-scoped and persist amounts using the existing integer minor-unit representation.

## Build and Development

- `dotnet build Szamlazo.slnx` builds the backend solution.
- `npm.cmd --prefix web ci` installs dependencies from the lockfile. `npm.cmd --prefix web run build` compiles the frontend.
- `docker compose up --build -d` starts the app at `http://localhost:8087` with OCR and PDF tools.
- For live frontend development, run `dotnet run --project api --no-launch-profile --urls http://localhost:5094` and `npm.cmd --prefix web start` in separate terminals. The Angular proxy expects port 5094.

## EF Core Migrations

For schema changes, use the EF command in `README.md`. Add a descriptively named migration under `backend/Infrastructure/Persistence/Migrations/` and commit it with the updated snapshot. Review the generated operations for data loss. Verify startup against both an empty SQLite database and an existing one whose data must survive. Keep startup migration handling in `DatabaseSetup`.

## Coding Style

Use four spaces in C# and two in TypeScript, HTML, and CSS. Use file-scoped C# namespaces, PascalCase public members, and `Async` for asynchronous service methods. Prefer small, single-purpose methods and descriptive names. Reuse existing logic before adding abstractions. Keep user-facing copy in Hungarian.

## Testing

The repository has no automated test suite or coverage threshold. Run both builds, then smoke-test the affected workflow in Docker. For import changes, use synthetic receipts and verify preview, correction, save, and reload. Check that another account cannot access saved expenses.

## Commits and Pull Requests

Start feature branches from `develop`. Use descriptive English names such as `feature/real-store-receipt-import`.

Follow the initial commit’s short English subject style. In PRs, describe the change and verification, link related issues, and include screenshots for UI changes. Keep SQLite data, session keys, and real receipts out of Git.
