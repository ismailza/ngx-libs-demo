---
name: verify
description: Run the same checks as CI (server build + integration tests, client lint, format check, tests, build) and report what fails. Use before saying a change is done, before committing, or when asked to "check", "verify" or "run CI locally".
---

# Verify (local CI)

Mirror `.github/workflows/ci.yml`. Run only the side(s) touched by the change unless asked for
everything; if both `server/` and `client/` changed, run both (in parallel when possible).

## Server

Requires Docker running (Testcontainers starts Postgres 18). If `docker info` fails, say so and
run only the build.

```bash
cd server
dotnet build NgxLibsDemo.slnx -c Release
dotnet test NgxLibsDemo.slnx -c Release --no-build
```

Warnings are errors. Fix analyzer warnings properly; only suppress with a short reason in
`server/.editorconfig` when the rule doesn't fit the project (see existing entries).

## Client

```bash
cd client
[ -d node_modules ] || npm ci --ignore-scripts
npm run lint
npm run format:check   # on failure: npm run format, then re-check
npm run test:ci
npm run build
```

## Report

Start with pass/fail per step. For failures, show the first relevant error lines and the fix
(or apply it if the failure is caused by the current change). Don't claim success for a step
that didn't run.
