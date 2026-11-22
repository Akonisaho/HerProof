# Build Plan and Claude Code Prompts

## 1. How to build (so nothing ends up half-working)
1. **Foundation first** (solution, database, auth, CI). Nothing else starts until CI is green on an empty-but-real solution.
2. **Thin end-to-end slice next**: one seeded candidate through opportunity, gap, repo, interview, assessment, review, passport. Each step crude but real or clearly marked STUB.
3. **Deepen in parallel** by workstream once the slice runs.
4. **Fallbacks and demo mode last but before rehearsal**, never skipped.
5. Work **one prompt at a time**. Review the diff, run the tests, merge, then the next prompt. Never ask Claude Code to build the platform in one go.

## 2. Workstreams (adapt to team size)
| Stream | Owns | Prompts |
|---|---|---|
| A. Core and data | Solution, DB, auth, opportunities, bursaries, gap engine, Beyond Employment | P01-P06 |
| B. Repo and AI | GitHub, analysis, sandbox, Thandi engine, voice | P07-P11 |
| C. Electron and assessment | Agent, assessment engine, worker | P12-P13 |
| D. Verification, passport, polish | Assessor, passport, matching, security, demo | P14-P18 |
| Everyone | Pitch rehearsal, demo seed data | docs/00 |

If the team is two people: A+D and B+C. Agree the API contract (docs/API.md, OpenAPI) in the first hour so streams do not block each other.

## 3. Suggested sequence over the build window
- **Block 1 (foundation):** P00, P01, P02, P03. CI green. Seed data in.
- **Block 2 (slice):** P04, P05, P07 (public repo only), P08 (LLM only), P10 (text mode), P12 (skeleton), P14 (basic), P15 (basic). Slice runs end to end.
- **Block 3 (depth):** P06, P09, P11, P12 complete, P13, P16.
- **Block 4 (hardening and demo):** P17, P18, rehearsals, fix list.
Freeze features at least a few hours before presenting. Rehearse the demo at full speed three times.

## 4. Prompts (copy, paste, adapt names)

Each prompt assumes CLAUDE.md and docs/ are in the repo. Start every Claude Code session with: "Read CLAUDE.md and the docs referenced below before doing anything."

### P00 — Orientation
```
Read CLAUDE.md and docs/01-ARCHITECTURE.md, 02-DATABASE.md, 03-SECURITY-PRIVACY.md.
Inspect the repository. Summarise: current state, what is missing versus the architecture, risks.
Propose a sequenced plan for P01 to P18 as small PR-sized tasks. Do not write code yet.
```

### P01 — Solution skeleton and CI
```
Create the .NET solution structure from docs/01-ARCHITECTURE.md: src/HerProof.Domain, .Application, .Infrastructure,
.Web (ASP.NET Core MVC + API), .AI, .AssessmentWorker, and tests/ projects for unit, application, integration, AI.
Add global.json pinning the current LTS SDK, Directory.Build.props (nullable enabled, warnings as errors, analyzers),
.editorconfig, packages.lock.json via RestorePackagesWithLockFile, Serilog, health endpoints (/healthz, /readyz),
Swagger, security headers middleware, rate limiting, and an empty home page.
Add electron-app/ skeleton with package.json scripts (dev, build, lint, typecheck, test, dist), .nvmrc, ESLint, tsc.
Ensure the existing .github workflows pass locally where possible. Add docs/STUBS.md.
Verify: dotnet build, dotnet test, dotnet format --verify-no-changes all succeed. Report exactly what runs.
```

### P02 — Database
```
Implement the PostgreSQL schema with EF Core exactly as docs/02-DATABASE.md (all four ERDs).
Use snake_case naming, UUID keys, enum-as-string with check constraints, indexes on every FK, unique constraints noted in the doc.
Make verification_records, assessment_reviews, consent_records, audit_log, integrity_events, interview_turns append-only
(DB privileges or triggers, plus no update/delete methods on repositories). Encrypt GitHub tokens with Data Protection.
Seed: competency categories, levels 0-4 with observable criteria, software and non-traditional competencies,
learning resources, and the demo dataset described in docs/02 (use fake data only).
Add migrations, a startup seeder guarded by environment, and tests proving append-only behaviour.
Build, apply migrations to docker compose Postgres, run tests, report.
```

### P03 — Identity and authorisation
```
Implement authentication with ASP.NET Core Identity: register, email confirmation, login, logout, forgot/reset password, lockout.
Roles: Candidate, Recruiter, Assessor, Partner, Administrator. Policy-based authorisation plus resource ownership checks
via an IResourceAuthorizer. Cookie settings per docs/03. Antiforgery on all forms.
Write an authorisation test matrix: every endpoint x (anonymous, wrong role, right role wrong owner, right role right owner).
Add audit logging for sign-in, role changes and permission failures. No secrets in logs.
```

### P04 — Opportunities, bursaries, eligibility
```
Implement organisations, opportunities (all types in the spec, including Bursary), required competencies with level and mandatory flag,
and eligibility rules (province, gender declared, education level, household income band, field of study, age range) with
hard-filter vs visible-rule support. Evaluate each rule to Met, NotMet or Unknown (Unknown when the candidate has not provided data).
Recruiter and Partner dashboards, candidate opportunity list and detail, application flow.
Use the Gender rule only as a provider-configured eligibility criterion that the candidate sees explained. Never used in AI scoring.
Tests for rule evaluation and for Unknown handling.
```

### P05 — Gap-to-Proof engine
```
Implement the Gap-to-Proof engine in the Domain/Application layers as pure, heavily tested logic.
Input: opportunity requirements, candidate competencies, evidence and verification records.
Output per competency: required level, demonstrated level (nullable), evidence_state in {Demonstrated, ClaimedNotAssessed,
NotDemonstrated, NoEvidence}, gap size (null when not assessed), plain-language explanation (which competency, why it matters,
required level, what is demonstrated, recommended activity), and a linked learning resource or assessment.
Critical rule: an unassessed competency is NEVER treated as level 0. Write tests that fail if this rule is broken.
Store reports (gap_reports, gap_report_items). Candidate UI shows the report with three clearly distinct states.
```

### P06 — Beyond Employment
```
Implement Beyond Employment: candidate adds an experience record (category, responsibilities, duration, context).
A mapping service proposes potential competencies (start with a curated keyword/rule mapper, optional LLM behind IAIService with
schema validation). Proposals are status Suggested. The candidate confirms or rejects each, attaches evidence, and may request an assessment.
Mappings are never awards. The same assessment standard applies regardless of where a competency was acquired.
Tests: suggestion, confirmation, rejection, and that confirmed mapping creates Claimed status only.
```

### P07 — GitHub integration
```
Implement GitHub connection with a GitHub App or OAuth App (minimum scopes, read-only). Store tokens encrypted, never expose to the browser.
Support public repo URL submission and authorised private repo selection. Resolve branch to the exact commit SHA and store it.
Fetch tree, manifests, README, test files and selected source files at that SHA with size and count caps, skipping binaries and vendored directories.
Handle rate limits with backoff and caching. Add revoke. Integration tests with a fake GitHub server.
```

### P08 — Repository analysis agent
```
Implement the Repository Analysis pipeline per docs/01 section 6, as a background job claimed from a Postgres-backed queue (FOR UPDATE SKIP LOCKED).
Steps: ingest, Roslyn static analysis for C# (classes, interfaces, DI registrations, controllers, EF usage, tests), secret redaction,
LLM analysis through IAIService returning JSON findings (schema in HerProof.AI/Models) with file and line references.
Treat all repo text as untrusted data in the prompt. Validate schema, verify every file/line reference exists at the pinned commit, and discard findings that fail.
Record model name, prompt version, input hash. Map findings to competencies as recommendations only. Never set Verified.
Add a prompt-injection test repository fixture. Report progress to the UI.
```

### P09 — Sandbox runner
```
Implement the sandbox execution integration per docs/03 section 5 and docker/Dockerfile.sandbox.
The worker requests a job from a runner that creates one disposable container: no network, read-only root fs, tmpfs workdir, dropped capabilities,
no-new-privileges, pids/memory/cpu/time limits, non-root user. The web host must never run candidate code and never mount the Docker socket.
If isolation is not available in the environment, the feature flag Execution__Enabled=false returns "not executed" and the report says so.
Store execution_reports. Tests including a malicious fixture (fork bomb, huge output, network call attempt) that must be contained.
```

### P10 — Thandi interview engine (text first)
```
Implement the interview engine per docs/06 as a text-first service: question generation from findings with verified snippet references,
the probe ladder state machine (max 3 probes), per-turn evaluation with the JSON schema, verdicts Understood/Partial/NotDemonstrated,
contradictions with evidence, and the final interview report mapped to competencies. Persist interview, questions, turns, evaluations.
Include forbidden-content filter, schema validation, injection fixtures, and Demo__Enabled cached interviews.
Language and persona come from config. No scoring of accent, fluency, appearance or emotion, ever.
```

### P11 — Voice layer (browser)
```
Add the voice layer to the interview per docs/06 section 9: consent screen, ISpeechToText and ITextToSpeech interfaces with an Azure adapter (en-ZA)
and fakes, server-side TTS with caching, browser SpeechSynthesis fallback, MediaRecorder capture with silence detection, "I'm still thinking",
"Repeat", and "Switch to typing" controls, transcript review and correction before scoring, recording indicator, phrase hints from repo identifiers.
Store audio only with consent, encrypted, with retention metadata. Accessibility: keyboard operable, captions of what Thandi says.
Test with fakes. Document manual test steps for real microphone and accents.
```

### P12 — Electron agent
```
Build electron-app per docs/07 in TypeScript with the hardening in section 3 (contextIsolation, no nodeIntegration, sandbox, typed preload, IPC allow-list with zod,
CSP, blocked navigation). Implement: PKCE sign-in via system browser, assessment fetch, consent screen listing the exact events, device check,
timer (server-authoritative), Monaco editor, autosave, local encrypted event queue with client_seq, batched sync with dedupe, resume after crash/offline,
live event panel visible to the candidate, submit and auto-submit. Events: only the list in docs/07 section 5. No screen capture, no keylogging, no clipboard reads.
Never auto-fail. Tests per docs/07 section 11. Provide a scripted "demo switch-tab" test.
```

### P13 — Assessment engine and worker
```
Implement assessments: creation with instructions, duration, competency mapping, rubric criteria with configurable weights, test cases and pass requirements.
Session lifecycle per docs/01 section 10, submission upload with size and type validation, evaluation job in the worker that builds and tests in the sandbox,
automated scores per rubric criterion, and persisted results. Integrity events ingestion endpoint with dedupe and gap detection.
Auto-evaluation produces a recommendation. It never creates a verification record.
```

### P14 — Verification and assessor dashboard
```
Implement the verification engine and assessor UI. Evidence pack per competency: declarations, repo findings, execution report, interview evaluations and
transcripts (if consented), assessment scores, integrity timeline. AI recommendations are visually distinct from human decisions.
Assessor can approve, recommend development, or reject, with feedback and rubric scores. Only role Assessor can create verification_records (domain-enforced and tested).
Append-only history with supersedes_id. Audit log entry for every decision. Candidate sees decisions and feedback.
Tests: every non-assessor path to Verified is rejected.
```

### P15 — Skills Passport
```
Implement the Skills Passport: passport per candidate, credentials from verification records with identifiers, validity periods and expiry, candidate dashboard
(verified vs unverified clearly labelled), share links (random tokens stored hashed, revocable, optional expiry, field-level visibility), QR code,
public verification page with an allow-list of fields only, and PDF export. Public page must never expose recordings, transcripts, private repos, contact details,
integrity events or scores. Tests proving the allow-list and revocation.
```

### P16 — Learning, matching, bursary applications
```
Implement curated learning recommendations linked to exact gaps and levels, progress tracking, and reassessment requests.
Implement opportunity matching with explanations (matching competencies, missing competencies, eligibility Met/NotMet/Unknown per rule, deadline).
No opaque ranking and no automatic rejection. Bursary application attaches selected passport credentials. Partner dashboard to review applications with evidence.
```

### P17 — Hardening
```
Run the security checklist in docs/03 section 12 and fix or document every item. Add: CSP and security headers, rate limits on auth/upload/analysis/interview,
upload validation, IDOR tests for every resource, log redaction tests, retention job (recordings, transcripts, repo snapshots) with audit log,
data subject request endpoints (export, delete), accessibility pass (WCAG 2.2 AA on candidate flows), error pages, and a threat-model delta.
Confirm CI security gates pass. Update docs/SECURITY-EXCEPTIONS.md if anything is deferred.
```

### P18 — Demo mode and runbook
```
Create demo mode and the runbook: seeded accounts for Lerato, Keabetswe, Anko, Naledi, plus recruiter, assessor and bursary partner;
pre-analysed repositories and pre-generated interviews; banner "Demo data" whenever cached results are used; one-command reset (docker compose + seeder);
a docs/DEMO-RUNBOOK.md with the exact click path from docs/00-PITCH.md, fallback switches, and a pre-flight checklist (network, microphone, permissions,
camera, API keys, hotspot, installer on the demo laptop). Never fake results silently.
```

## 5. Demo fallbacks (do not remove)
| Risk | Fallback |
|---|---|
| Venue wifi fails | Phone hotspot, cached analysis and interview data in Demo mode |
| LLM slow or down | Cached analysis and interview, banner "Demo data" |
| Microphone problem | Typed answers, then recorded video of the voice interview |
| Speech provider issue | Browser speech voice and typed answers |
| Electron installer issue | Pre-installed build on the demo laptop plus screen recording |
| Sandbox unavailable | Execution evidence "not executed", static analysis only, say so |
| Live question from judges about stubs | Show docs/STUBS.md, be exact about what is live |

## 6. Honesty rule for the presentation
Keep docs/STUBS.md current. In the pitch, label anything cached or stubbed. It strengthens credibility and matches the product's own principle: evidence before verification.
