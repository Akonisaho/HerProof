# Hackathon Build Schedule — 7 Days, Long Hours

Derived from docs/00-PITCH.md and docs/05-BUILD-PLAN-AND-CLAUDE-CODE-PROMPTS.md. This is the single
source of truth for "what are we doing today" — update it daily, don't let it drift from reality.

## Ground rules
- One prompt at a time with Claude Code. Review diff -> run tests -> merge -> next prompt. Never batch prompts.
- Nothing starts on Phase 2+ until CI is green on Phase 1.
- Keep docs/STUBS.md updated continuously — not at the end.
- Freeze features at least half a day before presenting. That time is rehearsal only, no feature work.
- If team = 2 people: Person 1 = Stream A+D, Person 2 = Stream B+C. Agree docs/API.md / OpenAPI contract
  in the first hour so streams don't block each other.
- If team = 3-4: split A / B / C / D roughly one owner each, "Everyone" tasks (pitch, seed data, rehearsal)
  are shared from Day 1, not bolted on at the end.

## Differentiator coverage plan (the thing that actually wins this)
Every item in docs/00-PITCH.md section 2 is something judges are told to look for. The goal is to make
**all twelve real**, not just two — but not all twelve at equal effort at equal time. They're tiered by
build risk and by how visible they are in the live demo. Higher tier = build and rehearse first;
lower tier can slip to "good enough" or demo-mode without wrecking the pitch, but only slips if time
actually runs out, not by default.

| Tier | # | Differentiator | Prompt(s) | Why this tier |
|---|---|---|---|---|
| 1 — signature, build first | 4 | Repo-grounded voice interviewer (Thandi) | P10, P11 | The single most impressive live moment; hardest to fake convincingly |
| 1 | 6 | Two independent proofs (repo interview + Electron practical) | P10/P11 + P12/P13 | Needs both halves real to land |
| 1 | 7 | Transparent integrity agent (switch-tab moment) | P12 | Named as "the signature moment" in docs/00 section 5 |
| 1 | 1 | Evidence before verification (AI recommends / human decides) | P14 | Core trust claim; also the easiest to make genuinely real (one `[Authorize(Roles="Assessor")]` check + UI split) |
| 2 — build second, still real | 2 | Gap-to-Proof Engine (unassessed != zero) | P05 | Pure domain logic, cheap to build right, disproportionately persuasive |
| 2 | 5 | Honest results (Understood/Partial/Not demonstrated) | P10/P14 | Falls out of Tier 1 work if evaluation schema is done properly |
| 2 | 9 | Portable Skills Passport (QR, public verify) | P15 | Visually strong demo beat (judges scan a phone), moderate effort |
| 2 | 3 | Beyond Employment | P06 | Narrative-critical (Lerato's story) but logic can be a simple rule mapper and still be genuinely real |
| 3 — real if time allows, else honest demo-mode | 8 | Bursary-ready eligibility | P04, P16 | Important for the ask, but Met/NotMet/Unknown table is simpler than it sounds — push for real |
| 3 | 11 | SA conditions (autosave/resume, en-ZA) | P11, P12 | Resume-after-disconnect is part of Tier 1 Electron work anyway; en-ZA voice is a config value if the provider supports it |
| 3 | 10 | Fair by design (no biometric scoring, text-only alt) | P10/P11 | This is a *constraint*, not a feature to build — enforce it by never adding the forbidden signals, costs ~zero extra time, so there's no excuse for this not being real |
| 3 | 12 | Engineered like a real product (sandbox, CI, SBOM) | P01, P09, P17 | Judges see one slide + a green pipeline; sandbox execution itself is the one allowed to fall back to "not executed" per the fallback matrix |

**Rule:** #10 and most of #3 cost little extra engineering time — they're design constraints and config,
not features — so there's no real reason to let them slip. The actual time pressure is concentrated in
Tier 1 (Thandi + Electron + verification split) and in sandbox execution (#12) and Beyond Employment's
LLM path (#3) if you try to make those fancy instead of simple.

## Workstreams (reference)
| Stream | Owns | Prompts |
|---|---|---|
| A. Core & data | Solution, DB, auth, opportunities, bursaries, gap engine, Beyond Employment | P01-P06 |
| B. Repo & AI | GitHub integration, analysis, sandbox, Thandi engine, voice | P07-P11 |
| C. Electron & assessment | Agent, assessment engine, worker | P12-P13 |
| D. Verification, passport, polish | Assessor, passport, matching, security, demo | P14-P18 |

---

## Phase 0 — Kickoff (Day 1, first hour)
- Confirm team roles / stream ownership.
- Agree API contract shape (docs/API.md, OpenAPI) so streams don't block each other.
- Confirm env vars, Docker, access to any AI/speech provider keys (.env.example).
- Run `P00 — Orientation` with Claude Code: read CLAUDE.md + docs/01, 02, 03. Get the sequenced plan confirmed, no code yet.

**Exit check:** everyone agrees on the contract and who owns what.

## Phase 1 — Foundation (Day 1, rest of day)
Nothing else starts until this is green.
- **P01** Solution skeleton + CI (.NET solution, electron-app skeleton, health endpoints, Swagger, security headers)
- **P02** Database (EF Core schema, append-only tables, seed data)
- **P03** Identity & authorisation (roles, policies, ownership checks, auth test matrix)

**Exit check:** `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` all pass. Seed data loads. CI green.

## Phase 2 — Thin End-to-End Slice (Day 2)
Get one seeded candidate through the *entire* journey, crude but real (or explicitly marked STUB).
Build the Tier 1 pieces (Thandi, Electron, verification split) deliberately crude-but-real here, not stubbed —
depth comes in Phase 3, but the real mechanism exists today.
- **P04** Opportunities, bursaries, eligibility (Met/NotMet/Unknown) — real, it's simple (#8)
- **P05** Gap-to-Proof engine (the "unassessed != zero" rule — non-negotiable, test it explicitly) (#2)
- **P07** GitHub integration — public repo only for now
- **P08** Repository analysis agent — LLM path only, defer sandbox execution
- **P10** Thandi interview engine — text mode only, real question generation off real repo findings, no voice yet (#4 foundation)
- **P12** Electron agent — skeleton: consent screen listing real events, basic shell, timer (#7 foundation)
- **P14** Verification & assessor dashboard — real role check (only Assessor can set Verified), basic approve/reject UI (#1, #5 foundation)
- **P15** Skills Passport — basic version (credential + share link, no QR/PDF polish yet) (#9 foundation)
- Enforce from day one, zero extra effort: never log/score accent, face, emotion, fluency anywhere (#10)

**Exit check:** Opportunity -> Gap -> Repo -> Interview (text) -> Assessment (skeleton) -> Assessor approve -> Passport shows a credential. Walk it end to end, live, even if every step is ugly.

## Phase 3 — Depth (Days 3-4)
Now go back and fill in what Phase 2 deferred, in parallel across streams, **tier order**: finish all of
Tier 1 to demo-quality before spending further time on Tier 2/3.
- **Tier 1 first:**
  - **P11** Voice layer (STT/TTS en-ZA, consent, transcript correction, typing fallback) — completes #4, #11
  - **P12 (complete)** Full Electron hardening: contextIsolation, IPC allow-list, autosave, **resume after crash/offline**, live event panel, **switch-tab event end to end on both candidate and assessor screens** — completes #6, #7, #11
  - **P13** Assessment engine & worker (rubric scoring, integrity events ingestion feeding the assessor timeline) — completes #6
  - **P14 (complete)** Full evidence pack view (repo findings + interview + assessment + integrity timeline side by side, AI recommendation visually distinct from human decision) — completes #1, #5
- **Tier 2 next, if Tier 1 is demo-ready:**
  - **P06** Beyond Employment (caregiving/household mapping, Suggested->Claimed flow) — simple rule mapper is enough, keep it real (#3)
  - **P15 (complete)** QR code + public verify page with field allow-list — completes #9
- **Tier 3, time-boxed, real if possible, explicit demo-mode fallback if not:**
  - **P09** Sandbox runner (disposable container, hardened, malicious-fixture tests) — if it slips, flip `Execution__Enabled=false` honestly (#12)
  - **P16** Learning recommendations, opportunity matching with explanations, bursary applications (#8)

**Exit check:** re-score against the differentiator table — every Tier 1 and Tier 2 row has something real on screen, not just described. Re-walk the end-to-end slice — did depth work break anything upstream?

## Phase 4 — Hardening & Demo Mode (Day 5)
- **P17** Security hardening checklist (docs/03 section 12): CSP, rate limits, IDOR tests, log redaction, retention job, data subject requests, accessibility pass, threat-model delta.
- **P18** Demo mode & runbook: seeded accounts (Lerato, Keabetswe, Anko, Naledi + recruiter/assessor/partner), pre-analysed repos, pre-generated interviews, "Demo data" banner, one-command reset, docs/DEMO-RUNBOOK.md.

**Exit check:** CI security gates pass. Demo mode reset works in one command. docs/STUBS.md is accurate and current.

## Phase 5 — Freeze & Rehearsal (Day 6)
- **Hard feature freeze** by midday. Remaining time is bugfix-only, triaged by a shared fix list — no new features, no refactors.
- Build the 12-slide deck per docs/00-PITCH.md section 4.
- Rehearse the 5-minute script (section 3) at full speed, three times, with the three speaker roles assigned.
- Rehearse the demo safety net: switch-tab moment, pull-network-cable resume, mic failure -> typed fallback -> recorded video fallback.
- Verify every fact in docs/00-PITCH.md section 8 (unemployment stat, women-in-tech stat, competitor claims, bursary details, name meaning).
- Record the 60-second backup video of the full voice interview now, not on the day.

**Exit check:** three full dry runs completed at demo speed. Backup video exists and plays. Fallback switches tested, not just described.

## Phase 6 — Buffer & Pitch Day (Day 7)
- Morning: one more full rehearsal, fix anything that broke overnight only if trivial and low-risk.
- Pre-flight checklist before going on: network, hotspot charged, microphone, permissions, camera, API keys valid, installer present on the demo laptop, seeded accounts load.
- One person owns the clicker, one owns the laptop — never both, per docs/00 section 5.
- Present.

---

## Daily checkpoint ritual (every day, end of day)
1. Does CI pass right now, on main?
2. Is docs/STUBS.md accurate for what's actually live vs stubbed?
3. Did today's work break the end-to-end slice? Re-run it.
4. What's the single biggest risk for tomorrow, and who owns unblocking it?

## Fallback matrix (keep visible, don't let it rot — docs/05 section 5)
| Risk | Fallback |
|---|---|
| Venue wifi fails | Phone hotspot, cached analysis/interview data in Demo mode |
| LLM slow or down | Cached analysis and interview, "Demo data" banner |
| Microphone problem | Typed answers, then recorded video of the voice interview |
| Speech provider issue | Browser speech voice + typed answers |
| Electron installer issue | Pre-installed build on demo laptop + screen recording |
| Sandbox unavailable | Execution evidence "not executed", static analysis only, say so |
| Judge asks about stubs | Show docs/STUBS.md, be exact about what is live |
