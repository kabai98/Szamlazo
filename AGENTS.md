# Repository Guidelines

## Project Structure

Számlázó is a Hungarian expense tracker built with ASP.NET Core 10, Angular 22, and SQLite. The web frontend lives in `web/src/`. The Ionic/Capacitor Android frontend lives in `mobile/src/`, with native configuration in `mobile/android/`. Keep mobile API/session, photo acquisition, and validation code in `mobile/src/app/core/`; keep screens in their feature folders. Follow `docs/mobile-roadmap.md` for later settings and file-retention work.

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
- For live frontend development, start Docker and run `npm.cmd --prefix web start`. The Angular proxy targets the Docker API at port 8087.
- `npm.cmd --prefix mobile ci`, `npm.cmd --prefix mobile run build`, and `npm.cmd --prefix mobile test` install, build, and test the mobile frontend. `npm.cmd --prefix mobile start` serves it at `http://localhost:8100` with an API proxy.
- `npm.cmd --prefix mobile run android:sync` builds native assets and synchronizes Capacitor; `npm.cmd --prefix mobile run android:open` opens Android Studio. See `mobile/README.md` for API URLs, JDK/SDK setup, APK builds, and USB testing. Never commit generated native environment files, SDK paths, or signing keys.

## EF Core Migrations

For schema changes, use the EF command in `README.md`. Add a descriptively named migration under `backend/Infrastructure/Persistence/Migrations/` and commit it with the updated snapshot. Review the generated operations for data loss. Verify startup against both an empty SQLite database and an existing one whose data must survive. Keep startup migration handling in `DatabaseSetup`.

## Coding Style

Use four spaces in C# and two in TypeScript, HTML, and CSS. Use file-scoped C# namespaces, PascalCase public members, and `Async` for asynchronous service methods. Prefer small, single-purpose methods and descriptive names. Reuse existing logic before adding abstractions. Keep user-facing copy in Hungarian.

## Receipt Image Processing

For receipt-image work, use the `receipt-image-processing` skill when available. Apply these rules even when the skill is unavailable:

- Keep originals intact and private samples/debug output in ignored directories. Preserve the original as an OCR candidate when cropping is uncertain.
- Detect paper boundaries, correct orientation and perspective, and compare a bounded set of preprocessing variants. Preserve names, prices, dates, and minus signs; inspect the resulting crop.
- Keep PDF text extraction and spreadsheet readers separate from image OCR. Keep external processes and image processing in Infrastructure.
- Preserve unknown fields and spatial OCR evidence. Never invent prices to reconcile a receipt. Exclude subtotals and savings summaries from purchase items; retain explicit discounts.
- Use VAT markers such as `B00`/`BOO` and `C00`/`COO` as item-location evidence before or after product text. Combine them with other receipt cues; a marker alone does not establish a purchase, and discount lines may also carry one.
- Test synthetic receipts and measure real samples by field and item-price pairing. Verify upload, preview, correction, save, reload, and account isolation. Report remaining uncertainty.

## Testing

Run `dotnet test tests/Szamlazo.Tests/Szamlazo.Tests.csproj` for xUnit validation and SQLite migration tests. There is no coverage threshold. Run both builds, then smoke-test the affected workflow in Docker. For import changes, use synthetic receipts and verify preview, correction, save, and reload. Check that another account cannot access saved expenses.

For image preprocessing changes, run `python -m unittest discover -s tests/ocr` in an environment with OpenCV and NumPy.

## Commits and Pull Requests

Before starting each new task, fetch the latest remote branches, switch to `develop`, and update it with `git pull --ff-only origin develop`. Create a new branch whose English name describes the task, such as `feature/real-store-receipt-import` or `fix/receipt-price-matching`. Do not start new work on `main`, `develop`, or a completed feature branch.

After completing the requested work, required checks, and authorized commit/push, automatically switch back to `main` and run `git pull --ff-only origin main`. This checkout does not merge the feature branch; merging remains a separate action. Only switch branches with a clean working tree. If local changes or diverging history prevent checkout or fast-forwarding, preserve them and report the blocker; never discard changes, reset history, or force-push to complete this workflow.

Follow the initial commit’s short English subject style. In PRs, describe the change and verification, link related issues, and include screenshots for UI changes. Keep SQLite data, session keys, and real receipts out of Git.
