# Security exceptions

Anything in docs/03-SECURITY-PRIVACY.md section 12's checklist that isn't fully done, and why, with
the honest tradeoff. This is a living document — update it whenever something here is actually fixed
rather than deleting the row silently. See docs/STUBS.md for the full stub registry; this file is the
security-specific subset of it, read on its own for a security review.

| Item | Why it's an exception | Risk if exploited | Mitigation in place today |
|---|---|---|---|
| Repo-wide automated secrets scan (e.g. gitleaks) in CI | Not wired up — hackathon time budget | A secret committed to the repo wouldn't be caught automatically | `SecretRedactor` strips secret-shaped strings from anything an LLM or sandbox sees (P07/P08); `.gitignore`/`appsettings` conventions keep real secrets out of source; manual review before every commit |
| Electron hardening (contextIsolation, sandbox, IPC allow-list) not re-verified with a real running Electron window in this environment | `ELECTRON_RUN_AS_NODE=1` is pre-set in this sandbox, so a real Electron GUI cannot be launched here (HP-024/025/026) | Low — the hardening flags are literal, static configuration in `src/main/index.ts`, not runtime-conditional behaviour, so a code read plus the 23 passing unit tests is strong evidence, just not a live GUI proof | Re-verify with a real browser/Electron build before the hackathon demo rehearsal |
| File upload validation (type/size/content-sniffing/malware scan) | The interview voice layer added the app's first real `IFormFile` endpoint (`POST /interviews/{id}/turns/{id}/transcribe`). Content-type allow-list and a 10 MB cap are enforced; no content-sniffing or malware scan, and no file is ever written to disk (it only exists for the length of the request, before being sent to speech-to-text and discarded) | Low — the file is never persisted, never served back, and never passed to anything except the speech-to-text provider, so there is no path traversal, stored-XSS or execution risk even from a malformed upload | Content-sniffing/malware scanning still not done; revisit if audio is ever persisted, or when HP-028 (zipped multi-file workspace submission) lands |
| DB-level FK constraints / UPDATE-DELETE privilege restrictions (HP-002b) | Referential integrity and append-only are enforced in the application layer (`AppendOnlyInterceptor`), not by Postgres privileges | A bug that bypasses the application layer entirely (e.g. a raw psql session with the app's own credentials) could violate append-only | `AppendOnlyInterceptor` covers every path through EF Core, which is the only path the application itself ever takes; a DB user with fewer privileges is the real fix before any production deployment |
| Accessibility (WCAG 2.2 AA) pass on candidate flows | No real browser or screen-reader tooling available in this environment to run an automated or manual audit | Some candidate flows may not meet AA (unverified either way) | Semantic HTML, `aria-label`s on icon-only controls, and labelled form inputs were used throughout by convention, but this has not been checked against the standard — do a real pass with a browser before the hackathon demo |
| CI security gates | `.github/workflows/ci.yml` (backend/electron/docker-lint/ci-gate) existed from the initial scaffold but had never actually been run end-to-end before this round | A broken CI step (e.g. a vulnerable transitive package) could have gone unnoticed until the first real push | Every step run locally against the CI's own commands and a matching ephemeral Postgres container: restore, format check, Release build `-warnaserror`, vulnerable-packages gate, test with coverage, `dotnet ef migrations has-pending-model-changes`, and the full electron job (lint/typecheck/test/audit/build) — all genuinely green. One real bug was found and fixed this way: old `Microsoft.NET.Test.Sdk 17.8.0` pulled in `System.Net.Http`/`System.Text.RegularExpressions 4.3.0`, both High-severity; bumped test SDK/xunit package versions across all 4 test projects, confirmed clean. `hadolint` on `docker/Dockerfile.web` passes (two info-level findings, below the workflow's warning threshold). Not yet exercised by GitHub Actions itself, since nothing is pushed until the planned push date |

## Security headers changed for the voice layer (not exceptions — deliberate, narrow changes)
- `Permissions-Policy` microphone directive changed from `microphone=()` (disabled for everyone,
  including this origin — would have silently broken voice capture) to `microphone=(self)`. Camera and
  geolocation remain denied; HerProof never uses either.
- `Content-Security-Policy` gained `media-src 'self' blob:` for the `<audio>` element's blob: URL
  (TTS playback). `script-src 'self'` was kept as-is on purpose — the voice layer's JS lives in
  `wwwroot/js/interview-voice.js`, not an inline `<script>` block, specifically so this policy didn't
  need to be weakened.

## What is NOT an exception (confirmed done, see docs/03 section 12 for detail)
Authorisation matrix + IDOR tests, CSRF/CORS, security headers, named rate-limit policies on
auth/upload/analysis/interview, the passport public-page allow-list, prompt-injection defence, and
sandbox containment of malicious repos are all real and tested as of P17 — they are listed as checked
in docs/03-SECURITY-PRIVACY.md section 12, not repeated here.
