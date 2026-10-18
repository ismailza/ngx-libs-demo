---
name: library-feedback
description: Investigate and record a bug or rough edge in @ismailza/ngx-auth-client or @ismailza/ngx-api-client, or try an unreleased local build of either library in the demo. Use when behaviour looks like a library problem, when asked to log library feedback, or to test a local library change.
---

# Library feedback

The demo exists to surface integration problems in the two libraries. When something breaks,
decide whether it's the demo or the library before working around it.

## Investigate

1. Read the library's shipped code in `client/node_modules/@ismailza/<lib>/` (the `.d.ts` and
   `fesm2022/*.mjs` files) — don't guess from memory. Sibling source checkouts may exist at
   `../ngx-auth-client` and `../ngx-api-client`.
2. Reproduce with a client Vitest spec (see `client/src/app/core/api/http.spec.ts`) or a server
   integration test when it's about the contract.

## Record

Add an entry to `docs/library-feedback.md` under the right `## <lib> <version>` heading
(version from `client/package-lock.json`), numbered after the last entry, in the existing format:

- `### N. <one-line symptom>`
- What happens and why, with the library file/function and a short code snippet
- How it shows up in the demo (a failing test name, if any)
- `**Workaround here:**` — what the demo does instead, if anything
- `**Fix:**` — the change the library should make

Keep workarounds small and point to the entry from a one-line code comment.

## Try an unreleased library build

Never commit the result. Build, pack, install the tarball:

```bash
(cd ../ngx-auth-client && npx ng build ngx-auth-client && cd dist/ngx-auth-client && npm pack --pack-destination /tmp)
cd client && npm install /tmp/ismailza-ngx-auth-client-*.tgz
```

Same for `ngx-api-client`. Afterwards restore with
`git checkout client/package.json client/package-lock.json && npm ci`.
