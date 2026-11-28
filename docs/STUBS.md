# Stubs register

Kept current throughout the build, not just at the end (CLAUDE.md working agreement). Every stub in code
is tagged `// STUB(HP-xxx)`; this file is the index. Remove a row only when the stub is replaced with the
real implementation.

| ID | Where | What's stubbed | Replaced in |
|---|---|---|---|
| HP-001 | src/HerProof.Web/Program.cs | Home page is the default MVC template page, not the real landing page | P04+ |
| HP-002a | src/HerProof.Web/Program.cs | `/readyz` does not check the database connection | P02 follow-up |
| HP-002b | src/HerProof.Infrastructure | No DB-level FK constraints or UPDATE/DELETE privilege restrictions for most tables; append-only is enforced only by `AppendOnlyInterceptor` in the app layer, and referential integrity for most relations is enforced in the Application layer, not by the database | P02 follow-up / P17 hardening |
| HP-004 | src/HerProof.Infrastructure/Notifications/SmtpEmailSender.cs | Plain SMTP to Mailpit, no auth/TLS, failures swallowed with a log warning | Before any real deployment |
| HP-006 | MFA for Assessor/Administrator | Not implemented (docs/03 section 3 explicitly allows deferring this pre-launch) | Before any real deployment |
| HP-012 | electron-app/src/main.ts, renderer/index.html | Electron app is a bare shell: no consent screen, timer, Monaco editor, autosave or sync | P12 (Phase 2/3) |
| HP-013 | electron-app/src/main.ts | Integrity events are validated but not persisted to a local encrypted queue or synced to the Web API | P12/P13 (Phase 3) |
| HP-DIST | electron-app/package.json (`npm run dist`) | `electron-builder` packaging not configured; `dist` script is a placeholder | P12 (Phase 3), before rehearsal |
| HP-008 | Opportunities UI | Plain server-rendered forms, no dropdown validation beyond HTML attributes, no edit/delete of existing requirements or rules | Polish later if time allows |
| HP-009 | GapToProofEngine | Generates a fresh GapReport on every page view rather than caching/reusing one; fine at hackathon scale, would need throttling for real traffic | Revisit if needed |
| HP-011 | GitHub integration | Private repo access via GitHub App/OAuth not built; public repo URL submission only | When real GitHub App credentials exist |
| HP-015 | HerProof.AI | No real LLM key configured in this environment; `FakeAiService` is the active provider. Real `AnthropicAiService` is fully implemented and wired, untested against the live API (no key available here) | Once a real `AI__ApiKey` is set |
| HP-016 | RepositoryAnalysisBackgroundService | Single polling instance, 5s fixed interval, no exponential backoff or dead-letter handling for repeatedly-failing analyses | P17 hardening if needed |
| HP-017 | RepositoryAnalysisBackgroundService | ExecutionReport's testsPassed/testsFailed are a crude 1/0 stand-in — the sandbox entrypoint reports only a pass/fail boolean, not real per-test counts | Parse the .trx inside the container if real counts matter later |
| HP-018 | docker/Dockerfile.sandbox | `dotnet restore` inside the sandbox needs network for any real NuGet dependency, but the container runs with `--network none`; for most real candidate repos this means `restore_ok: false` honestly, not a fabricated pass | An offline NuGet cache baked into the image, or an allow-listed restore proxy step (docs/03 section 5) |
| HP-020 | HerProof.AssessmentWorker | No retry/backoff on GitHubRateLimitException — a rate-limited analysis is marked Failed outright rather than requeued for later (docs/01 section 15 says "queue retry with backoff") | P17 hardening if needed |
| HP-023 | AzureSpeechServices | Written against Azure's documented REST contracts, not exercised against a live key in this environment — no Speech__Key available here | Review against a real key before a rehearsal |
| HP-024 | Voice layer (P11) | No browser-side audio capture, consent screen, or playback UI — needs a real browser and microphone to build and verify meaningfully, neither available in this environment. Text mode (P10) carries the full rubric with no voice at all, so the differentiator isn't blocked, just not demoed with actual speech yet | Before rehearsal, with a real browser |
| HP-025 | electron-app/src/main/index.ts | Session context (sessionId/assessmentId/candidateId/accessToken) comes from environment variables, not a real OAuth2 PKCE sign-in (ADR-08) | P13, once Assessment/AssessmentSession has an API |
| HP-028 | AssessmentSessionsController | Submission is a single plain-text file, not the zipped workspace docs/07 section 9 describes | When a multi-file workspace lands |
| HP-029 | AssessmentEvaluationBackgroundService | No retry/dead-letter path if the worker crashes mid-evaluation after claiming — the submission stays claimed forever | P17 hardening if needed |
| HP-030 | VerificationRecord / AssessorDashboardController.Decide | `VerificationRecord.AssessmentId` is non-nullable, but evidence packs routinely combine repository + interview evidence with no single formal Assessment behind them, so `Decide` currently passes `Guid.Empty` as a placeholder rather than a real Assessment id | Make AssessmentId nullable, or introduce an evidence-pack-level id, whichever P15/P17 needs |
| HP-031 | PassportVerificationController.Show | Resolves a share by loading every passport's shares and hashing the given token in memory, rather than querying `passport_shares` by token hash directly | Query `db.PassportShares` directly if candidate count grows past hackathon scale |
| HP-034 | docs/03-SECURITY-PRIVACY.md section 12 / docs/SECURITY-EXCEPTIONS.md | No CI-wired secrets scanner, no WCAG 2.2 AA audit tooling — all tracked with reasoning in docs/SECURITY-EXCEPTIONS.md rather than silently skipped. (Electron GUI re-verification and the CI pipeline itself are no longer exceptions — see round 5) | Before any real deployment / before the hackathon demo rehearsal |
| HP-035 | docker/docker-compose.yml (minio service) | `IBlobStore` is named in CLAUDE.md's stack description but was never actually implemented anywhere in the codebase; `scripts/demo-reset.sh` deliberately skips starting minio since nothing uses it yet, to avoid an unnecessary image pull | If/when a real blob store is needed |
| HP-036 | HerProof.AI/FakeAiService.cs | **Found live, real limitation**: the fake repository-analysis heuristic only ever detects README/markdown files — it has no code-structure detection at all, so it can never produce a finding with a real line reference. Since Thandi's question generation requires a snippet with a real line reference (`SnippetReferenceVerifier`), this means **no real public repository can ever produce a live interview without a real Anthropic API key** — not a voice-specific gap, it blocks the whole repo-to-interview pipeline for any actual code. Confirmed live against octocat/Hello-World and octocat/Spoon-Knife, both real repos, both produced 0 usable questions. The pre-seeded demo interview (P18) bypasses this entirely by inserting InterviewQuestion/Turn/Evaluation rows directly | Set a real `AI__ApiKey`, or extend FakeAiService with a code-aware heuristic if a demo must run live without one |
| HP-037 | Organisation.VerificationStatus / AdminController | The intended onboarding story for a new employer or bursary partner is: they reach out via the public "Partner with us" form (real, built) → an Administrator receives the inquiry (real, built, `/admin/partner-inquiries`) → the team manually sends them the contact details and documents they need to submit → a real due-diligence/anti-scam and security check runs → only then is the organisation actually onboarded (created + first member added, both real, built). `Organisation.VerificationStatus` (`Unverified`/`Verified`) already exists on the domain model specifically for that last step, but nothing anywhere actually sets it, displays it, or gates anything on it yet — it's a real field with no real action behind it | Add an Admin action to mark an organisation Verified once due diligence is actually done, and surface the status somewhere an Assessor/Candidate/other Recruiter could see it, if that ever becomes a real trust signal worth showing |

## What's real post-P18, round 7 (demo-readiness pass: errors, polish, seed data, auto-publish)
- **Friendly GitHub errors** (closes HP-019): any non-404/non-rate-limit GitHub API error (a 422 for
  an invalid branch, a transient 5xx, etc.) previously fell through to
  `HttpResponseMessage.EnsureSuccessStatusCode()`, surfacing as an unhandled 500. A new
  `GitHubApiException` carries the real status code; `RepositoriesController` and
  `InterviewController` both catch it and show a plain-language message instead, with the
  candidate's already-saved interview answer never lost on the way.
- **Opportunities auto-publish on creation**: a recruiter/partner's new post used to sit in Draft
  until a separate "Publish" click — easy to forget mid-demo and look like nothing happened. It now
  publishes immediately; "Close" is still there to take it down later. Verified live against the
  real database: created via the real form flow, appeared on the public `/opportunities` listing
  with zero extra clicks.
- **Real learning resources seeded** (closes HP-032): the gap-to-proof engine's "here's how to close
  this gap" recommendation had nothing to show for any competency before this. Seven real, free
  resources (Microsoft Learn, GitHub's own docs, freeCodeCamp) now cover every competency used by
  the demo opportunities.
- **Assessor can decide on a never-claimed competency** (closes HP-033): the `CandidateCompetency`
  row is now created if missing, rather than the decision silently having nothing to update. Covered
  by a new integration test (seeds a candidate with zero prior evidence, submits a decision, confirms
  the row now exists and carries it).
- **Candidate-facing interview history** (closes HP-022): `/interviews` lists every interview a
  candidate has taken (in progress or completed), linking to the live take-it page or the finished
  report — previously the only way back to an interview was mid-flow, right after starting it.
- **Recruiter/partner opportunity pages, Assessor dashboard, and the secondary account pages (Forgot/
  Reset Password) restyled** to match the rest of the brand system — previously plain Bootstrap next
  to an otherwise-redesigned site.

## What's real post-P18, round 6 (Electron: Monaco editor + server-authoritative timer)
- **Server-authoritative timer** (`AssessmentSessionInfoController`, closes half of HP-026): the
  Electron agent's countdown previously counted up from zero locally with no real deadline at all.
  Now `GET /api/assessment-sessions/{id}/info` (same bearer-token auth as the existing integrity-
  events endpoint) returns the session's real `StartedAtUtc` and the assessment's `DurationMinutes`;
  the renderer computes the deadline from those, not from when the app happened to be opened. The
  server, not the client, has the final word: `POST /api/assessment-sessions/{id}/code` independently
  checks the deadline against its own clock and calls the pre-existing (and previously unused)
  `AssessmentSession.Submit` or `.TimeOut` domain method accordingly — a manipulated client clock can't
  turn a late submission into an on-time one. 4 new integration tests, including one that genuinely
  found a bug before it shipped: the new `AssessmentSubmission` wasn't explicitly `db.Add()`-ed, which
  surfaced as an EF `DbUpdateConcurrencyException` on the very first real test run.
- **Real Monaco editor** (closes the other half of HP-026): the renderer's plain `<textarea>` is
  replaced with a real `monaco-editor` instance (C# syntax highlighting via its built-in Monarch
  tokenizer — no language server needed for this). Loaded the non-bundler way (`vs/loader.js` +
  `vs/editor.main`, copied into `dist/renderer/vs` by a new `copy-monaco` build step), with its worker
  pointed at a same-origin `vs/editor/editor.worker.js` file rather than a `blob:` URL, so it works
  under the agent's existing strict CSP with only `style-src 'unsafe-inline'` and `worker-src 'self'`
  added — no `unsafe-eval`, no CSP relaxation beyond what Monaco's own worker and theme CSS need.
- **Real bug found by actually launching the app**: with the real timer/editor wiring in place, this
  was the first time the Electron GUI was driven far enough to exercise the preload script for real —
  and it crashed immediately (`Error: module not found: ../shared/events`). Electron's sandboxed
  preload context (`sandbox: true`, a real hardening control, not weakened to fix this) can't use
  relative `require()` of local files at all; it needs a single self-contained bundle. This bug
  predates this round entirely — it would have broken `window.herproof` (and therefore the whole
  agent) from the very first real run, regardless of Monaco or the timer. Fixed with a new
  `bundle-preload` esbuild step (`esbuild --bundle --platform=node --format=cjs`), chained into both
  `dev` and `build`. Confirmed fixed by actually launching the built app: preload now loads with no
  error. Not yet confirmed by clicking through consent → editor → timer by eye, since this environment
  has no mouse/keyboard/screenshot access to the live window — only process-alive and console-output
  verification were possible here.

## What's real post-P18, round 5 (admin UI, real CI verification, real Electron launch, partner intake)
- **Admin UI** (`AdminController`, `/admin/users`, `/admin/users/{id}/roles`, `/admin/organisations`):
  closes HP-005. An Administrator can search users, grant/revoke Recruiter/Assessor/Partner/
  Administrator roles (audit-logged via `IAuditLogger`), create organisations, and add/remove
  members — adding a Recruiter/Partner org member also grants the matching Identity role, since
  org membership alone doesn't satisfy `[Authorize(Roles=...)]`. A seeded `admin@demo.herproof.test`
  account now exists so this is reachable out of the box. Covered by IDOR tests (anonymous/wrong role).
- **Real, first-ever end-to-end CI verification**: `.github/workflows/ci.yml` existed from the initial
  scaffold but had never been run. Every step of the `backend` and `electron` jobs was run locally
  against the CI's own commands (including a matching ephemeral Postgres container for the
  connection-string-dependent steps) and is genuinely green: restore, format check, Release build
  `-warnaserror`, vulnerable-packages gate, test-with-coverage, `dotnet ef migrations
  has-pending-model-changes`, and electron's lint/typecheck/test/audit/build. Found and fixed one real
  bug this surfaced: `Microsoft.NET.Test.Sdk 17.8.0` pulled in two High-severity transitive packages
  (`System.Net.Http`/`System.Text.RegularExpressions` 4.3.0) via all 4 test projects — bumped
  `Microsoft.NET.Test.Sdk`/`xunit`/`xunit.runner.visualstudio`/`coverlet.collector`, confirmed clean.
  The version bump's newer `xunit.analyzers` also caught one real xUnit2031 violation
  (`Assert.Single(x.Where(...))`), fixed to the predicate overload. `docker-lint` (hadolint on
  `docker/Dockerfile.web`) passes (two info-level findings, below the workflow's warning threshold).
  See docs/SECURITY-EXCEPTIONS.md for the full "CI security gates" row update.
- **Real Electron GUI launch, for the first time in this environment**: previously blocked by three
  stacked bugs, now all fixed. (1) `electron`'s npm postinstall was silently no-op'ing — `extract-zip`
  hangs/fails silently in this environment; worked around with PowerShell's `Expand-Archive` plus
  manually-written `path.txt`/`version` files. (2) `ELECTRON_RUN_AS_NODE=1` is pre-set in this sandbox,
  forcing Electron into plain-Node mode with no GUI runtime; worked around with `env -u
  ELECTRON_RUN_AS_NODE` on direct binary invocation (going through `npm run dev` did not reliably
  propagate the unset). (3) the renderer's HTML/JS was never actually copied into `dist/` by any build
  step; fixed permanently with a new `copy-renderer` npm script (`fs.cpSync`, no new dependency)
  chained into both `dev` and `build`. A real window process launched and stayed alive with no fatal
  errors (only harmless Chromium GPU-cache permission warnings).
- **Real "Partner with us" intake** (`PartnerController`, `PartnershipInquiry`, new migration):
  replaces a `mailto:hello@herproof.app` link that left no record of who asked. Public, anonymous,
  rate-limited (5/min/IP) form at `/partner` creates a `PartnershipInquiry` row, emails a configurable
  `Partner__NotificationEmail` (optional — the inquiry is still saved if unset) via the same
  `IEmailSender` the rest of the app uses, and sends the submitter a confirmation email.
  `/admin/partner-inquiries` lists every inquiry with Mark contacted/Close actions. 4 domain tests,
  3 new integration tests (admin access rejects anonymous/wrong role, public form is reachable
  anonymously).

## What's real post-P18, round 4 (contributor verification + Thandi avatar + brand overhaul)
- **GitHub contributor verification** (`GitHubContributorVerifier`, 11 tests; `Repository.ContributorStatus`,
  new migration): when a candidate submits a repository, her declared GitHub profile URL (My Profile)
  is checked against the repo's real contributor list via GitHub's API. Flags, never blocks
  (CLAUDE.md rule 5): a mismatch or an uncheckable case (no profile set, GitHub error/rate limit)
  never stops the submission — it's shown as a badge (Confirmed / Not confirmed / Not checked) on the
  candidate's own repositories page and in the assessor's evidence pack, as evidence for a human to
  weigh. Verified live with a real GitHub account (octocat) against two real repos: correctly
  Confirmed on one she really contributed to, correctly NotConfirmed on one she didn't.
- **Thandi avatar**: a simple static avatar (initial "T", pink gradient) with a pulsing ring while her
  audio is actually playing — server TTS or the browser's speechSynthesis fallback, both wired to the
  same visual cue. Not a video avatar; an honest, lightweight "she's speaking now" indicator.
- **Full brand/visual redesign**: pink + dark "code-editor" palette, a software-developer-focused hero
  (real C# snippet, a multi-skill "Skills Passport" mockup card with a genuinely real, scannable QR
  code generated by the same QRCoder library the real Passport feature uses — not a fake pattern), a
  real Privacy page (previously a stock empty stub), a branded public verify page, and a proper footer.
  Found and fixed two real, pre-existing CSP bugs along the way: `style-src 'self'` was silently
  breaking several `style="display:none"` toggles across the app (voice recording buttons, a passport
  revoke button) since before this round — converted to real CSS classes (`d-none`) everywhere.

## What's real post-P18, round 3 (free-tier-only pivot)
The user asked to use only resources with a genuinely free tier, no card required. Two changes:
- **AI**: added `GroqAiService` (Infrastructure's own HTTP-based pattern, same as AnthropicAiService) over
  Groq's free, OpenAI-compatible chat completions API (console.groq.com, no card). Wired into both
  HerProof.Web and HerProof.AssessmentWorker with the same precedence everywhere: `Groq:ApiKey` tried
  first (free), then `AI:ApiKey` (Anthropic, paid), then `FakeAiService`. Still waiting on the user's
  actual Groq key to go live — code is ready, untested against the real API in this environment.
- **Voice STT**: replaced the MediaRecorder-records-then-uploads-to-a-server-endpoint design from round 2
  with the browser's own free, built-in Web Speech API (`SpeechRecognition`) — live, real-time
  transcription with zero setup, zero cost, and the audio never leaves the device at all (stronger
  privacy than the round-2 design, which at least uploaded audio transiently). Chrome/Edge only; other
  browsers get an honest "not supported, please type" message rather than a silent dead end. The
  server-side `ISpeechToText`/`/transcribe` endpoint and `FakeSpeechToText`/`AzureSpeechToText` all
  still exist and still work (a real Azure key would still plug in), but the candidate-facing flow no
  longer calls them by default, since there's no free hosted STT as good as what shipping in the
  browser already gives away. TTS is unaffected (browser `speechSynthesis` was always the free path).

## What's real post-P18, round 2 (Brevo email + real voice layer)
- **Brevo transactional email** (`BrevoEmailSender`, Infrastructure/Notifications): real HTTP calls to
  Brevo's `v3/smtp/email` API, wired in alongside the existing `SmtpEmailSender`/Mailpit path with the
  same conditional-real-vs-fake pattern used everywhere else (`Brevo:ApiKey` configured → Brevo; not
  configured → Mailpit). The API key and sender identity live in `dotnet user-secrets`, never in a
  committed file.
- **Full P11 voice layer, built for real** (consent screen, browser mic capture, server transcription,
  TTS playback with browser fallback, transcript review before scoring, switch-to-typing):
  - `PhraseHintExtractor` (Application, 4 tests): pulls identifier-shaped tokens from a question's own
    snippet path and finding evidence, passed as STT phrase hints — never invented, never re-fetched.
  - `ConsentRecord` (which existed since P02 but was never once used anywhere) is now written for real
    on every consent decision — granted or declined — via `InterviewController.Consent`.
  - `Interview.Mode` (Voice/Text) is now actually set from that consent decision; previously the field
    existed on the domain entity but `Start` always hardcoded `InterviewMode.Text`.
  - New endpoints: `POST /interviews/{id}/turns/{id}/transcribe` (validated audio upload — the app's
    first real `IFormFile` endpoint, with content-type allow-list and a 10 MB cap — calls
    `ISpeechToText`, returns a transcript to review/correct, never writes the audio anywhere);
    `GET /interviews/{id}/turns/{id}/prompt-audio` (cached TTS, 204 when there's no audio so the
    browser falls back to its own `speechSynthesis`); `POST /interviews/{id}/switch-to-text`.
  - `wwwroot/js/interview-voice.js`: MediaRecorder capture with a recording indicator, "Play question"
    with the SpeechSynthesis fallback, upload-and-fill-the-textarea-for-review flow.
  - **Two real security-header bugs found and fixed along the way, not yet caught by any test**: the
    strict `script-src 'self'` CSP from P17 would have silently blocked this feature's own inline
    script (moved to the external file above instead of weakening the policy); and P17's
    `Permissions-Policy: microphone=()` was unconditionally *disabling* the microphone for every
    origin including this one — changed to `microphone=(self)` (camera/geolocation stay denied).
  - Audio is never written to disk or any store — by design, not by omission: it exists only for the
    length of the transcribe request, which is also the honest reason there's nothing to encrypt-at-
    rest here (docs/05 P11 asked for "store audio only with consent, encrypted"; the actual choice
    made was to not store it at all, which is strictly more private and needs no key management).
  - Verified live end-to-end except the literal browser microphone/MediaRecorder call (no real browser
    available in this environment): consent GET/POST (ConsentRecord row confirmed in Postgres),
    `Interview.Mode` set correctly, `/transcribe` accepting a real multipart upload and returning
    `{"transcript": ""}` via `FakeSpeechToText` (correct, honest behaviour — no key configured),
    `/prompt-audio` returning 204 via `FakeTextToSpeech`, `/switch-to-text` flipping the mode. See
    HP-036 for a real, pre-existing limitation this testing surfaced in the repo-analysis fallback.

## What's real post-P18 (found and fixed during a live user walkthrough)
Registering a genuinely new candidate account end to end (not a seeded demo one) surfaced a real,
previously undocumented gap: nobody could actually edit their own profile. `CandidateProfileController`
had only ever been a read-only reference implementation of the P03 ownership check, with the view
literally saying "STUB: full profile editing comes with P04+" — a promise P04 never kept, even though
its own eligibility rules (Province, GenderDeclared, HouseholdIncomeBand, EducationLevel, FieldOfStudy,
DateOfBirth) depend on exactly this data. Also found: the layout had no navigation at all beyond
Home/Privacy — no login/logout, no links to Opportunities/Repositories/Passport.
- Built a real `Edit` GET/POST pair on `CandidateProfileController`, plus a `/my-profile` redirect so
  the nav doesn't need to know its own candidate-profile id.
- Added a real nav bar: Login/Register/Logout, and role-conditional links (Candidate: Opportunities,
  Repositories, My Passport, My Profile; Assessor: Assessor dashboard).
- Verified live, start to finish, with a brand-new account (not a seed): registered, confirmed email via
  Mailpit, logged in, edited the profile (province, education, gender, income band, field of study, date
  of birth, career interests, GitHub/LinkedIn), confirmed every field persisted in Postgres, confirmed
  the Women in Tech Bursary's two eligibility rules now evaluate `Met` against that real data (previously
  unreachable — "Unknown" was all anyone could ever get, having no way to declare anything), applied,
  and confirmed the application row.
- Along the way, fixed an unrelated test-tooling bug (not a product bug): a token-extraction script
  grabbed two identical antiforgery hidden inputs off one page (the new logout form plus the edit form)
  and concatenated them into a corrupted token — a reminder that any future scripted walkthrough must
  take the *first* matching hidden input, not all of them.

## What's real as of P18 (final build-plan phase)
- `scripts/demo-reset.sh`: a real, tested one-command reset — wipes the Postgres volume, brings
  Postgres and Mailpit back up (minio skipped, see HP-035), applies every migration, and runs the
  seeder (Development-only, via one short-lived app startup). Run twice live during this phase; both
  times it rebuilt a clean, fully-seeded database from nothing.
- Anko's `anko-task-tracker` repository now has a pre-analysed `RepositoryAnalysis` (3 findings across
  C#/OOP/Testing, a real `ExecutionReport`) and a pre-generated two-question Thandi interview (both
  `Understood` verdicts, code-beside-answer), both marked `IsDemoData` — this is the live fallback for
  docs/05 section 5's "LLM slow or down" / "venue wifi fails" risks, verified end to end: Anko's own
  repositories page, her interview report, and the assessor's evidence pack for her all show a visible
  "Demo data" badge or banner, confirmed live against real Postgres rows, not just by reading the code.
- `docs/DEMO-RUNBOOK.md`: the exact click path from docs/00-PITCH.md's five-minute script mapped onto
  this build's real routes and seeded accounts, a pre-flight checklist, and the fallback-switch table
  carried over from docs/05 section 5 so it's never lost track of mid-demo.
- "Never fake results silently" (CLAUDE.md, repeated in docs/05 P18): every seeded/cached result is
  visibly labelled, not merely internally flagged — checked for all three places it can appear
  (candidate repositories list, candidate interview report, assessor evidence pack).
- This closes out docs/05-BUILD-PLAN-AND-CLAUDE-CODE-PROMPTS.md's full P01-P18 phase list. Everything
  genuinely deferred along the way is in the HP-XXX table above and docs/SECURITY-EXCEPTIONS.md — not
  silently dropped.

## What's real as of P17
- Error pages: `Views/Home/Error.cshtml` (was missing — the `Error()` action would have thrown
  `InvalidOperationException: view not found` on its very first real use) and a friendly
  `Views/Home/StatusCode.cshtml` for 404/403/429, wired via `UseStatusCodePagesWithReExecute`, scoped
  to skip `/api` so Electron's JSON clients still get plain status codes.
- Named rate-limit policies on top of the existing global per-IP limiter (P03): `auth` (10/min),
  `upload` (5/min, repository submission), `analysis` (5/min, practical assessment submission),
  `interview` (20/min, Thandi). Verified live: the 11th `/account/login` request inside a minute
  returns 429 with the friendly status page, not a raw error.
- IDOR tests (`IdorTests.cs`, 5 tests) covering the assessor dashboard, the evidence pack, and the
  partner/recruiter applications review added in P14-P16, on top of the existing P03
  `AuthorizationMatrixTests` matrix — all passing, with a fixed flaky-test bug along the way: two
  `IClassFixture<HerProofWebApplicationFactory>` test classes running in parallel on different xUnit
  threads triggered a real `WebApplicationFactory` host-resolution race; moved both to one shared
  xUnit collection (`WebAppFactoryCollection`) so they build one factory and never race.
- Data subject self-service (`DataSubjectController`, CLAUDE.md rule 9/POPIA): `/privacy/export`
  returns the signed-in user's own data as JSON (verified live against Lerato's real account);
  `/privacy/delete` anonymises identifying fields (email, name, candidate profile PII) and
  deactivates+locks the account rather than deleting rows outright, since verification, review and
  audit records are append-only by design and must survive — the right of erasure is honoured by no
  longer identifying the person, not by destroying the record that something was verified. Built and
  unit/integration-tested; the live end-to-end delete flow itself was deliberately **not** exercised
  against the running demo environment, because doing so would have irreversibly anonymised the
  Lerato demo persona used throughout this whole build and every later demo rehearsal — verified by
  code review instead.
- Transcript retention job (`TranscriptRetentionBackgroundService`, new in HerProof.AssessmentWorker):
  redacts `interview_turns.transcript_confirmed`/`transcript_raw` past a 180-day window
  (`RetentionPolicy`, 3 tests), via raw SQL rather than EF SaveChanges — the one deliberate, logged,
  narrow exception to `AppendOnlyInterceptor`, since `InterviewTurn` is `IAppendOnly` by design and
  this is the sanctioned way to honour retention without weakening that guard for anyone else.
- Assessment submission now has an explicit size limit (200,000 characters) — previously unbounded.
- docs/03-SECURITY-PRIVACY.md section 12's checklist was gone through item by item and marked honestly
  (checked, or explained); docs/SECURITY-EXCEPTIONS.md is the new living record of what's still
  deferred and why, plus a threat-model delta section added directly to docs/03.
- Known gap, not silently papered over: see HP-034 for what remains genuinely undone (CI-wired secrets
  scan, a real-browser Electron re-check, and an actual WCAG audit) and why.

## What's real as of P16
- Opportunity matching with explanations was already real as of P04/P05 (`OpportunityEligibilitySummary`
  for Met/NotMet/Unknown per rule, `GapToProofEngine` for matching vs missing competencies with the
  never-zero rule) — P16 adds the deadline to the candidate-facing page and closes the remaining gaps:
  learning progress, reassessment requests, credential-attached bursary applications, and a partner/
  recruiter review dashboard. No opaque ranking or score anywhere in this path, and no code path can
  auto-reject an application — `Apply` only ever blocks on a hard-filter eligibility rule the candidate
  can see the reason for.
- Learning progress (`LearningProgress`, new table): a candidate marks a recommended resource
  Not started/In progress/Completed from the opportunity page itself; purely descriptive, never read
  by any verification or eligibility logic (CLAUDE.md rule 3 boundary respected).
- Reassessment requests (`LearningController.RequestReassessment`) reuse the existing
  evidence-before-verification pipeline rather than adding a side door: requesting one just calls
  `CandidateCompetency.AddEvidence(...)`, which is the same method that already re-queues a competency
  to `EvidenceSubmitted` and puts it back on the real assessor dashboard — verified live (see below).
- Bursary applications attach the candidate's own verified passport credentials
  (`AppliedCredentialSelector`, 3 tests): the candidate picks from her own credentials in a checkbox
  list, and the server filters the posted identifiers down to ones she actually owns before storing
  them — a forged form field naming someone else's credential can never attach.
- Partner/recruiter review dashboard (`OrganisationOpportunitiesController.Applications`): lists every
  application for one of the organisation's own opportunities, each with the candidate's name, status,
  and the attached credentials resolved back to competency names; a "mark reviewed" action; the same
  `IResourceAuthorizer.CanAccessOrganisationAsync` ownership check used everywhere else in this
  controller.
- Verified live end-to-end: Lerato applied to the "Women in Tech Bursary" with her verified Financial
  management credential attached, confirmed via Postgres (`applications.shared_credentials_json`); the
  Demo Bursary Trust partner's applications page showed exactly that credential resolved to its
  competency name, and marking it reviewed persisted; candidate and anonymous access to that same URL
  correctly redirected to Access Denied / login respectively. Separately: marked a learning resource
  in-progress and confirmed the `learning_progresses` row; requested reassessment on a Developing
  competency and confirmed both the new `ReassessmentRequest` evidence row and that the competency
  reappeared on the real assessor dashboard queue.
- Known gaps, not silently papered over: see HP-032 (no learning resources seeded yet — a P18 demo-data
  job, not a code gap) and HP-033 (an assessor could in principle decide on a never-claimed competency
  by visiting the URL directly; not reachable from the real queue).

## What's real as of P15
- Skills Passport issuance is wired directly into the only place a Verified decision is ever made
  (`AssessorDashboardController.Decide`): a passport is created on a candidate's first Verified
  decision, and every later Verified decision adds one more `PassportCredential` — idempotent per
  `VerificationRecord.Id`, 4 tests covering create/reuse/reject-non-Verified/idempotent-issue, plus a
  default 24-month validity window (`PassportIssuanceService.DefaultValidity`, docs/05 "validity
  periods and expiry").
- Share links (`PassportShareService`, 6 tests): a candidate picks specific verified credentials to
  expose (never "share everything"), gets a random 256-bit token back exactly once, and only its
  SHA-256 hash is ever persisted (docs/02's own documented design) — revocable, with an optional
  expiry window, both checked on resolution.
- The public verify page (`PassportVerificationController`, anonymous, `/verify/{token}`) is built
  from nothing but the share's `VisibleFields` allow-list of credential identifiers: the controller
  never queries recordings, transcripts, repositories, contact details, integrity events or scores at
  all, so there is no field to accidentally leak (CLAUDE.md rule 9) — not merely "hidden from the view".
- QR codes (QRCoder, MIT, pure-C# PNG renderer — no System.Drawing dependency, so it runs the same on
  Linux as on Windows) and a PDF export of the candidate's own full passport (PDFsharp, MIT) are both
  real, not placeholders.
- Verified live end-to-end: an assessor's Verified decision on Lerato's "Financial management"
  (docs/00-PITCH.md's own demo scenario) produced a real `skills_passports` row and a real
  `passport_credentials` row in Postgres; Lerato's own `/passport` dashboard listed it; she created a
  share link scoped to that one credential; the resulting `/verify/{token}` page — hit anonymously,
  no cookies — showed exactly the competency, credential identifier and dates, nothing else; a bad
  token correctly 404s; the QR endpoint returned a real PNG; `/passport/export.pdf` returned a real,
  non-empty PDF.
- Known rough edge, not a functional bug: see HP-031 (share lookup is O(passports) today, fine at
  hackathon scale).

## What's real as of P14
- Evidence-before-verification (CLAUDE.md rule 1) is enforced at the only construction site in the whole
  codebase: `VerificationDecisionService.Create` throws unless the caller is actually in role Assessor,
  independent of whatever `[Authorize]` the controller itself declares — defence in depth.
- `AssessorDashboardController` assembles a single read-only evidence pack per candidate+competency from
  across the whole pipeline: repository findings, sandbox execution reports, Thandi interview turns +
  evaluations (code shown beside the candidate's answer and the verdict), practical assessment reviews,
  the full integrity-event timeline, and the append-only decision history — plus a clearly-labelled
  "AI suggestion (not a decision)" derived by the pure `AiRecommendationDeriver` (5 tests), never stored
  as if it were a verdict.
- `AiRecommendationDeriver`'s rule, confirmed by test and matching docs/00 differentiator #6 (two
  independent proofs): repository evidence alone never suggests Verified, only Developing — Verified
  requires the interview's understanding check too.
- Verified live end-to-end against real Postgres data for the exact docs/00-PITCH.md scenario
  (Lerato / "Financial management"): candidate-role GET to both the evidence-pack URL and the dashboard
  index redirect to Access Denied; anonymous access redirects to login; a real assessor logs in, opens
  the evidence pack, and submits `Decision=Verified, FinalLevel=2` with feedback — confirmed by direct
  SQL query that `verification_records` gained a new append-only row (decision, final_level, real
  assessor_id, evidence_summary), `candidate_competencies` was updated to `status=Verified,
  verified_level=2`, and `audit_log` recorded a `VerificationDecision` entry.
- Known gap, not silently papered over: see HP-030 — `VerificationRecord.AssessmentId` is currently
  `Guid.Empty` for evidence packs with no single formal Assessment behind them.

## What's real as of P11 (partial — see HP-023/HP-024)
- `ISpeechToText`/`ITextToSpeech` interfaces (ADR-05: server-side only, key never reaches the browser),
  with a real Azure Cognitive Services REST implementation and a Fake fallback used automatically when
  `Speech__Key`/`Speech__Region` aren't configured — same honest-fallback pattern as `FakeAiService`.
- TTS is cached by a hash of the text (docs/06 section 9 point 1), proven by 2 passing tests.
- Browser audio capture, the consent screen, and playback are not built (HP-024) — this genuinely needs
  a real browser and microphone to build and verify, neither of which exist in this environment. Text
  mode (P10) already carries the complete rubric with no voice at all, so no differentiator is blocked
  on this — it just isn't demonstrated with actual speech yet.

## What's real as of P10
- Thandi, text-first (`ThandiInterviewEngine` + `InterviewController`): generates 4-6 questions from
  real repository evidence, each citing a verified file/line snippet (`SnippetReferenceVerifier`,
  shared with P08's finding verification — one rule, one parser, used everywhere a snippet reference
  needs checking); runs the probe ladder (max 3 probes, enforced both by the domain's `InterviewQuestion.AddTurn`
  and defensively again in the engine); evaluates each answer to Understood/Partial/NotDemonstrated
  with covered/missing points and contradictions; produces a final report per competency.
- `ForbiddenContentFilter` is the last line of defence against accent/fluency/appearance/emotion
  language reaching a candidate or being stored as evidence — applied to every free-text field an LLM
  produces, not just the prompt's instruction to avoid it. 12 passing tests plus engine-level tests
  prove an utterance containing forbidden content is replaced with a neutral move-on, never shown as-is.
- Prompt-injection fixture (docs/06 section 11): a finding whose description tries to inject
  instructions does not produce a question citing a fabricated, unverifiable snippet — same discipline
  as P08, now proven for the interview path too.
- `FakeAiService` extended to produce valid, verifiable responses for both Thandi prompt shapes (question
  generation and turn evaluation), not just repository analysis — the documented no-key fallback works
  for every differentiator, not just one.
- Found and fixed a real gap live: unlike RepositoriesController, InterviewController didn't catch
  GitHub rate-limit/not-found exceptions, so a transient GitHub problem surfaced as an unhandled 500
  instead of a friendly message. Caught by actually exhausting the GitHub unauthenticated rate limit
  during testing (60 req/hour, used up fetching ~200 files from a real public repo) — fixed in both the
  interview-start and answer-submission paths, with the candidate's in-progress answer preserved rather
  than lost when the fix applies.
- 123 tests passing across the solution.

## What's real as of P09
- `ISandboxRunner` / `DockerSandboxRunner`: shells out to the exact `docker run` invocation documented
  in docker/Dockerfile.sandbox's header comment (network none, read-only root fs, tmpfs workdir,
  all capabilities dropped, no-new-privileges, pids/memory/cpu limits, non-root user, named container
  so a timeout can `docker kill` the actual container, not just the CLI client).
  `Execution__Enabled` defaults to false; when off, returns an honest "not executed" result — this is
  the documented hackathon fallback (docs/03 section 5), not a placeholder pretending to be real.
- Output capture is capped while streaming (`CappedBuffer`, keeps the tail, ~8KB), not just truncated
  after the fact — a flooding malicious program cannot exhaust the worker host's own memory while
  waiting for the container to finish or time out.
- `SandboxWorkspace` materialises fetched content to a throwaway temp directory for the read-only mount,
  with a path-traversal guard (a malicious `../../x` tree entry can't write outside the directory),
  always cleaned up afterward.
- Wired into `RepositoryAnalysisBackgroundService`: the previously hardcoded "not executed" stub is now
  a real call through `ISandboxRunner`.
- Malicious-fixture tests (fork bomb, huge output, network call attempt) exist as required by docs/05
  P09, gated behind `HERPROOF_SANDBOX_TESTS=1` since they need a real Docker daemon and the built
  `herproof-sandbox` image — not something CI or every dev machine can assume. They probe the container
  boundary directly (shell entrypoint override) rather than through `dotnet restore`, because restore
  needs network the sandbox deliberately doesn't have (see HP-018) — routing a malicious payload through
  a pipeline step that's expected to fail offline anyway would prove nothing.
  **Verified for real against the built image, not just written**: a classic exponential fork bomb
  (`:(){ :|:& };:`) is contained in ~0.5s by `--pids-limit`, a 50MB stdout flood doesn't hang the
  container, and a network call attempt is blocked by `--network none` — all three passing live.

## What's real as of P13
- Assessment authoring (`AssessmentsController`): title/description/type/duration, competency mapping
  with weights, rubric criteria with weights/descriptors, draft -> published, ownership-scoped to the
  creator.
- Full candidate session lifecycle (`AssessmentSessionsController`, docs/01 section 10 state machine):
  browse published assessments, start a session, consent (lists the real event catalogue), begin,
  submit — each a real state transition, not a stub.
- Integrity-events ingestion endpoint for the Electron agent (`IntegrityEventsController`, closing
  P12's HP-026): token-authenticated (`HmacSessionEventTokenService`, STUB(HP-025) stand-in for OAuth2
  PKCE), dedupe and gap detection delegated to `IntegrityEventIngestion` (10 passing pure-logic tests).
- Worker-side evaluation (`AssessmentEvaluationBackgroundService`): claims submissions via
  `FOR UPDATE SKIP LOCKED` (ADR-02, same pattern as repository analysis), runs the real sandbox,
  scores via `AssessmentScoring` (6 passing tests) — never fabricates a score when not executed, never
  creates a VerificationRecord.
- Verified live, end-to-end, including three real bugs found and fixed during testing, not just
  written and assumed correct:
  1. The global antiforgery filter was blocking the Electron agent's bearer-token API calls (fixed
     with `[IgnoreAntiforgeryToken]` — correctly scoped, since antiforgery is for cookie-authenticated
     browser forms, not token-bearing API clients).
  2. A JSON property-casing mismatch between submission serialization and worker deserialization threw
     a `NullReferenceException` — fixed by sharing one `SubmittedFile` record between both sides instead
     of two independently-typed DTOs.
  3. Confirmed the dedupe path works against a real resent batch (same `clientSeq` twice, inserted once)
     and that an invalid token is correctly rejected with 401.
  The full flow — session start, consent, begin, submit, worker claim, honest "not executed" sandbox
  result, score, candidate sees the result — ran live against Postgres.
- 137 passing tests across the whole .NET solution.
- STUB(HP-028): submission is a single plain-text file, not the zipped workspace docs/07 section 9
  describes — matches the Electron agent's plain-textarea stand-in for Monaco (HP-026).
- STUB(HP-029): no retry/dead-letter path if the worker crashes mid-evaluation after claiming a
  submission — it stays claimed forever. Acceptable at hackathon traffic levels, not for production.

## What's real as of P12 (partial — see HP-025/HP-026)
- Hardened Electron shell restructured per docs/07 section 10 (src/main, src/preload, src/renderer,
  src/shared): contextIsolation, sandbox, no nodeIntegration, blocked navigation/window-open, strict
  CSP, no webviewTag.
- `EventQueue`: local, encrypted-at-rest (via Electron's `safeStorage`, OS keychain) event queue with
  monotonically increasing `client_seq`, crash-safe resume (a relaunch continues the sequence and
  records `session_interrupted`), and gap detection — 9 passing tests covering exactly what docs/07
  section 11 asks for (ordering, dedupe, gap detection, resume).
- The complete, fixed 15-event catalogue (docs/07 section 5) enforced at the schema level — nothing
  outside it can ever be queued, proven by a test that a channel like `screen_capture` is rejected.
- `SyncClient`: batched upload, never throws on a network failure (offline is routine, not an error) —
  5 passing tests.
- Consent screen listing the exact event list, a live event panel the candidate sees in real time
  (updated the instant *any* event is recorded, before sync — docs/07 principle #2), a client-side
  timer, and a plain-textarea editor stand-in for Monaco.
- 23 passing electron-app tests total, plus lint/typecheck/`npm audit` all clean.
- STUB(HP-025): full OAuth2 PKCE sign-in (ADR-08) isn't wired — session context comes from environment
  variables for now, authenticating to the server via a session-scoped HMAC token instead (built in
  P13, see below). The queue, sync, hardening and UI above are real and tested, not placeholders.
- STUB(HP-026): Monaco editor and a server-authoritative timer are still deferred — P12's job was the
  agent's own hardened, tested machinery, which is done; the server-side ingestion endpoint this stub
  used to also cover was built in P13 (below) and is real, verified live.

## What's real as of P08
- Full repository analysis pipeline, async end-to-end: Web enqueues a `Pending` RepositoryAnalysis ->
  `HerProof.AssessmentWorker`'s `RepositoryAnalysisBackgroundService` claims it with a raw
  `FOR UPDATE SKIP LOCKED` query (ADR-02) -> fetches content -> redacts secrets -> runs Roslyn static
  analysis (deterministic, anchors the LLM per ADR-09) -> calls the LLM -> verifies every file/line
  reference against the pinned-commit content, discarding anything unverifiable -> persists findings,
  an ExecutionReport honestly marked "not executed" (sandbox is P09), and the analysis record with
  model name, prompt version and input hash. HP-014 is resolved, not just stubbed differently.
- Prompt-injection defence is mechanical, not a promise: `RepositoryAnalysisPromptBuilder` neutralises
  any attempt by repo content to forge the untrusted-data delimiters, and `FindingVerification` means
  even a compromised/obedient model's fabricated claims never become a finding unless they point at
  real, fetched content — proven by a dedicated fixture test that simulates a model obeying an
  injected instruction and shows the forged finding still gets discarded.
- `IAIService` has two implementations: `FakeAiService` (the real, documented fallback per docs/01
  section 15, active whenever `AI__ApiKey` is unset — not a placeholder) and `AnthropicAiService`
  (real HTTP client against the Messages API, used automatically once a key is configured).
- 99 passing tests total across the solution, including the required fake-GitHub-server tests (P07)
  and this phase's prompt-injection fixture, secret redaction, finding verification, and Roslyn
  analyzer tests. Verified live end-to-end: web app enqueues, a separately-running worker process
  claims and completes the job, and the candidate's repository list shows the live "Completed" status.

## What's real as of P07
- Public GitHub repository submission: URL parsing (`GitHubRepositoryUrlParser`), branch→commit SHA
  resolution pinned at submission (ADR-06), tree fetch, and content selection that skips vendored
  directories (node_modules, bin, obj, etc.) and binary-like extensions, with file-count and
  total-byte caps (`RepositoryContentSelector`). 60 passing tests overall, 8 of them against a fake
  GitHub HTTP server per docs/05 P07's explicit requirement — no real network needed to verify this.
- Rate-limit (403 + `X-RateLimit-Remaining: 0`) and not-found responses are distinguished and handled;
  SHA/tree/file-content are cached (short TTL for SHA, long TTL for pinned-commit content, since a
  commit's content can never change).
- Verified live against the real GitHub API (octocat/Hello-World): resolved master's SHA, fetched the
  tree, and correctly fetched only the one eligible file, skipping nothing malformed.
- STUB(HP-011): private repo access via GitHub App/OAuth is not built — there's no real client
  id/secret to test against yet. Public-repo submission is real, not a placeholder.
- STUB(HP-014): content fetch runs synchronously on the request thread rather than as a queued
  background job; P08 should move this to the Postgres-backed job queue described in docs/01 section 6.

## What's real as of P06
- Beyond Employment: candidates add experience records (category, title, description, dates); a curated
  keyword mapper (`ExperienceCompetencyMapper`) proposes non-traditional competencies as `Suggested`,
  never software ones. 8 passing tests, including the exact Lerato household-budgeting scenario from
  docs/00-PITCH.md section 3.
- Confirming a suggestion only ever claims a competency (`BeyondEmploymentConfirmation.EnsureClaimed`),
  never awards or verifies one, and never downgrades an already-assessed competency — verified live:
  confirming "Financial management" produced `status = Claimed`, `verified_level` still NULL.
- Rejecting sets `Rejected`; both actions are ownership-checked so a candidate can only act on her own
  experience records' mappings.
- Not a stub: an LLM-backed mapper behind IAIService is a documented *optional* upgrade (docs/05 P06),
  not a requirement. The keyword mapper is the real, shipped version.

## What's real as of P05
- Gap-to-Proof engine (`HerProof.Application.Competencies.GapToProofEngine`): pure, 27-test-covered logic
  turning an opportunity's requirements + a candidate's competencies into a `GapReport` with one of four
  evidence states per competency — `Demonstrated`, `ClaimedNotAssessed`, `NotDemonstrated`, `NoEvidence`.
- The non-negotiable rule (CLAUDE.md rule 2) is enforced at two layers, not one: `GapReportItem`'s own
  constructor throws if a demonstrated level is set without state `Demonstrated` (domain layer), and the
  engine never passes a level for any other state (application layer). Tests assert the literal word
  "zero" never appears in a generated explanation, and verified live: a real candidate's gap report row
  in Postgres has `demonstrated_level` genuinely NULL, not `0`.
- Candidate opportunity page now shows the real three-state report (plus NoEvidence as a fourth,
  distinguishable from NotDemonstrated) instead of a raw requirements list; each report is persisted.

## What's real as of P04
- Organisations, opportunities (Job/Internship/Learnership/Bursary), required competencies with level
  and mandatory flag, and eligibility rules (province, gender declared, education level, household
  income band, field of study, age range), each with hard-filter vs informational, evaluating to
  Met/NotMet/Unknown — Unknown (missing candidate data) never blocks an application, only a hard
  filter that's actually NotMet does. 20 passing unit tests cover every operator and the Unknown rule.
- Recruiter/Partner dashboard to post, manage and publish opportunities, with ownership enforced via
  `IResourceAuthorizer.CanAccessOrganisationAsync` (verified live: wrong organisation -> Access Denied).
- Candidate-facing browse/detail/apply flow, verified end-to-end live against Postgres (recruiter posts
  a role with a hard-filter rule, candidate with missing data sees "Unknown" and can still apply,
  applying twice is a no-op, a hard-filter NotMet blocks Apply server-side even if the UI were bypassed).
- Fixed a systemic EF Core gotcha: entities whose Id is pre-assigned in the constructor get tracked as
  `Modified` instead of `Added` when reached only through collection-navigation fixup on an already-loaded
  parent. Every "add a child to an existing aggregate" controller action must call `db.Add(child)`
  explicitly alongside the domain method — watch for this same bug in every later phase that adds
  children to a previously-persisted aggregate (P05 GapReport, P06 ExperienceRecord, P08 analysis
  findings, P10/P11 interview turns, P13 integrity events, P14 reviews, P15 passport credentials/shares).

## What's real as of P03
- Full register / login / logout / forgot-password / reset-password / email confirmation flow, hand-written
  (not scaffolded) over ASP.NET Core Identity, emailing through Mailpit in dev.
- Lockout after 5 failed attempts (15 min), confirmed-email-required before sign-in, cookie
  HttpOnly+Secure+SameSite=Lax (docs/03 section 3).
- Antiforgery validated globally on every unsafe-method request (`AutoValidateAntiforgeryTokenAttribute`),
  not per-action, so a new form can't forget it.
- One authorization policy per role (`RequireCandidate` etc.) plus `IResourceAuthorizer` for ownership
  checks (a candidate may read only her own profile) — demonstrated end-to-end on
  `CandidateProfileController` and proven by `AuthorizationMatrixTests`' four-case matrix (anonymous,
  wrong role, right role wrong owner, right role right owner).
- Every sign-in (success/failure/lockout), logout, password reset, registration, and every 401/403
  authorization failure is written to the append-only audit log.

## What's real as of P02
- Full EF Core schema for all four ERDs in docs/02-DATABASE.md: 38 domain tables + ASP.NET Identity tables,
  snake_case, UUID keys, enums stored as strings with CHECK constraints, every FK-like column indexed.
- Append-only enforced in code for `verification_records`, `assessment_reviews`, `consent_records`,
  `audit_log`, `integrity_events`, `interview_turns` — proven by `AppendOnlyInterceptorTests`.
- The never-zero rule is structural: `CandidateCompetency.VerifiedLevel` and `GapReportItem.DemonstratedLevel`
  are nullable `int?`, never `0`, until an assessor decision sets them.
- Seed data: 16 competencies (10 software + 6 non-traditional) × 5 levels, 7 demo users (4 candidates,
  recruiter, assessor, bursary partner), 2 organisations, 5 opportunities (3 jobs/internship + 2 bursaries).
