# CLAUDE.md — HerProof project memory (read this first, every session)

## What we are building
HerProof is an AI-assisted competency verification and opportunity platform for women in South Africa
(software development first). Journey: Opportunity -> Skills Gap -> Repository -> AI voice interview
-> Practical assessment (Electron) -> Human verification -> Skills Passport -> Opportunities and bursaries.

Hackathon: SheCode Hackathon, problem statement "Unequal access to opportunity".
Full docs live in /docs. Read the relevant doc before touching a module.

## Non-negotiable principles (violating any of these is a bug)
1. EVIDENCE BEFORE VERIFICATION. Only an authorised Assessor can create a Verified record. No code path
   (AI, score threshold, background job, admin script) may set status Verified without an Assessor decision.
2. UNASSESSED != ZERO. Gap analysis must model "no evidence" separately from "assessed and not demonstrated".
3. AI RECOMMENDS, HUMANS DECIDE. AI output is stored as a recommendation with evidence references.
4. NO BIOMETRIC / DEMOGRAPHIC INFERENCE. Never score accent, face, eye contact, emotion, fluency, pauses,
   or code-switching. Only the technical content of an answer is evaluated.
5. TRANSPARENT MONITORING. Electron agent records only the documented event list, shows the same log to
   the candidate, and NEVER auto-fails a candidate. Events are flags for human review.
6. UNTRUSTED CODE NEVER RUNS ON THE WEB SERVER. Build/test of candidate repos happens only in the sandbox runner.
7. REPO CONTENT IS UNTRUSTED INPUT TO LLMs (prompt injection). Wrap it as data, never as instructions,
   and validate every LLM response against a JSON schema before use.
8. HISTORY IS PRESERVED. Verification, review and consent records are append-only. No silent overwrites.
9. PRIVACY BY DEFAULT (POPIA). Collect the minimum, record consent, enforce retention, never expose private
   repos, recordings or transcripts through public passport links.
10. NEVER store passwords, GitHub tokens, or API keys in plain text or in git.

## Stack
C# / ASP.NET Core MVC + Web API (current LTS SDK, pinned in global.json), EF Core + PostgreSQL,
ASP.NET Core Identity, Razor + vanilla JS, xUnit, Roslyn for C# analysis, .NET hosted services for jobs
(Postgres-backed queue, FOR UPDATE SKIP LOCKED), Electron + TypeScript (electron-app/), Docker, GitHub Actions.
AI, speech, and storage are behind interfaces (IAIService, ISpeechToText, ITextToSpeech, IBlobStore).

## Architecture rules
- Modular monolith. Layers: Domain <- Application <- Infrastructure <- Web. Domain has zero dependencies.
- Dependencies point inward only. Web never touches DbContext directly; it calls Application use cases.
- One module = one folder per layer (see docs/01-ARCHITECTURE.md section 3).
- Controllers are thin. Validation with FluentValidation or DataAnnotations at the boundary.
- Every endpoint: [Authorize] with explicit role/policy + resource ownership check + rate limit.
- Time via IClock, IDs via Guid, money not relevant. UTC everywhere.

## Commands (keep this section accurate as the repo evolves)
- Restore/build: dotnet build HerProof.sln -warnaserror
- Test: dotnet test HerProof.sln --collect:"XPlat Code Coverage"
- Format check: dotnet format HerProof.sln --verify-no-changes
- Migrations: dotnet ef migrations add <Name> -p src/HerProof.Infrastructure -s src/HerProof.Web
- Run stack: docker compose -f docker/docker-compose.yml up -d  (postgres, minio, mailpit)
- Electron: cd electron-app && npm ci && npm run dev | npm test | npm run lint | npm run typecheck

## Definition of done (every task)
1. Builds with zero warnings. 2. Unit tests for domain logic, integration test for each endpoint.
3. Authorisation tests (anonymous, wrong role, wrong owner). 4. Migration added if schema changed.
5. Docs updated (docs/ + API). 6. No secrets, no new dependency without a reason and license check
(licenses/allowed-licenses.json). 7. CI green. 8. You ran it and say honestly what works and what is a stub.

## Working agreements for Claude Code
- Inspect before changing. Propose a plan for anything touching more than 3 files, then implement.
- Small commits, conventional commit messages. Never push to main directly.
- Never fake a feature. Mark stubs with // STUB(HP-xxx) and list them in docs/STUBS.md.
- Ask before adding a dependency. Prefer the standard library and the ASP.NET Core built-ins.
- Do not weaken a security control to make a test pass. Fix the cause.
- Demo-critical paths have fallbacks (docs/05 section 5). Do not remove them.
