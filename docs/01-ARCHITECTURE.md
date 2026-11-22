# HerProof — System Architecture

Version 1.0 · Status: Design baseline for the SheCode hackathon MVP · Diagrams are Mermaid (render on GitHub and VS Code).

## 1. Context and goals

HerProof turns opportunity requirements into independently verified candidate competencies.

**Architecture drivers**
1. Trustworthy evidence: every verified competency traces to assessment evidence and a named assessor.
2. Safety: candidate code is hostile until proven otherwise. AI input and output are untrusted.
3. Privacy: POPIA-aligned handling of recordings, transcripts and personal data.
4. Resilience: South African network and power conditions. Demo must survive failures.
5. Speed of delivery: one deployable backend, replaceable providers, thin vertical slices.

**Style:** modular monolith (Clean Architecture layers) + one desktop client + one isolated sandbox runner.
Microservices are explicitly deferred: operational cost exceeds benefit at MVP scale.

## 2. System context (C4 level 1)

```mermaid
flowchart LR
  cand["Candidate (browser)"]
  elec["Candidate (Electron Assessment Agent)"]
  rec["Recruiter or Bursary Partner"]
  ass["Assessor"]
  adm["Administrator"]
  pub["Anyone with a passport link"]
  hp(("HerProof Platform"))
  gh["GitHub API"]
  speech["Speech services: STT and TTS"]
  llm["LLM provider"]
  mail["Email service"]
  cand --> hp
  elec --> hp
  rec --> hp
  ass --> hp
  adm --> hp
  pub --> hp
  hp --> gh
  hp --> speech
  hp --> llm
  hp --> mail
```

## 3. Containers (C4 level 2)

```mermaid
flowchart TB
  subgraph clients["Clients"]
    br["Browser: Razor MVC views and JS, voice interview, dashboards"]
    el["Electron app: TypeScript, practical assessments and integrity events"]
  end
  subgraph platform["HerProof platform (modular monolith)"]
    web["HerProof.Web: MVC and Web API, auth, rate limits"]
    app["Application layer: use cases, validators, DTOs"]
    dom["Domain: entities, rules, state machines"]
    infra["Infrastructure: EF Core, GitHub, storage, provider adapters"]
    ai["HerProof.AI: agents, prompts, schemas, validators"]
    wk["AssessmentWorker: hosted service for async jobs"]
  end
  subgraph data["Data stores"]
    pg[("PostgreSQL: data and job queue")]
    obj[("Object storage: recordings, evidence, PDFs")]
  end
  sbx["Sandbox runner: disposable containers, no network, resource limits"]
  ext["External: GitHub, LLM, Speech, Email"]
  br --> web
  el --> web
  web --> app
  app --> dom
  app --> infra
  app --> ai
  infra --> pg
  infra --> obj
  wk --> app
  wk --> sbx
  infra --> ext
  ai --> ext
```

Dependency rule: **Web -> Application -> Domain** and **Infrastructure -> Application (interfaces) -> Domain**.
Domain depends on nothing. AI project depends on Application interfaces only.

## 4. Modules inside the monolith

```mermaid
flowchart LR
  subgraph identity["Identity and Access"]
    id1["Users, roles, policies"]
  end
  subgraph people["People"]
    pr["Candidate profiles"]
    org["Organisations and members"]
  end
  subgraph opps["Opportunities"]
    op["Opportunities and bursaries"]
    el2["Eligibility rules"]
    ap["Applications"]
  end
  subgraph comp["Competency"]
    cf["Framework and levels"]
    cc["Candidate competencies and evidence"]
    gap["Gap-to-Proof engine"]
    exp["Beyond Employment"]
  end
  subgraph evid["Evidence generation"]
    repo["Repository ingestion and analysis"]
    iv["Interview (Thandi)"]
    asm["Assessments and sessions"]
  end
  subgraph ver["Verification"]
    vr["Review workflow and decisions"]
    pp["Skills Passport"]
  end
  subgraph support["Supporting"]
    ln["Learning resources"]
    mt["Matching"]
    au["Audit log"]
    cs["Consent and retention"]
    nt["Notifications"]
  end
  gap --> cf
  gap --> cc
  exp --> cc
  repo --> cc
  iv --> repo
  asm --> cc
  vr --> cc
  vr --> iv
  vr --> asm
  vr --> repo
  pp --> vr
  mt --> op
  mt --> pp
  ap --> pp
  ln --> gap
```

Modules talk through Application interfaces and domain events, never through each other's tables.
Cross-cutting: Audit and Consent are called by every evidence and verification use case.

## 5. End-to-end journey

```mermaid
sequenceDiagram
  autonumber
  actor R as Recruiter
  actor C as Candidate
  actor A as Assessor
  participant H as HerProof
  R->>H: Create opportunity with required competencies and levels
  C->>H: Select opportunity
  H-->>C: Gap report: demonstrated, claimed, not assessed, not demonstrated
  C->>H: Add Beyond Employment experience
  H-->>C: Suggested competency mappings for confirmation
  C->>H: Submit GitHub repository and branch
  H->>H: Pin commit, analyse, build and test in sandbox
  C->>H: Voice interview with Thandi
  C->>H: Practical assessment in Electron agent
  H->>A: Evidence pack: AI findings, test results, transcript, integrity timeline
  A->>H: Approve, develop or reject per competency
  H-->>C: Skills Passport updated
  C->>H: Share passport or apply for bursary
  R->>H: Verify credential without seeing private data
```

## 6. Repository analysis pipeline

```mermaid
sequenceDiagram
  autonumber
  participant C as Candidate
  participant W as Web API
  participant Q as Job queue (Postgres)
  participant K as Worker
  participant G as GitHub API
  participant S as Sandbox runner
  participant L as LLM agent
  participant D as PostgreSQL
  C->>W: Submit repository URL and branch
  W->>G: Resolve branch to commit SHA
  W->>D: Save Repository with pinned SHA
  W->>Q: Enqueue analysis job
  W-->>C: 202 Accepted with analysis id
  K->>Q: Claim job
  K->>G: Fetch tree, manifests, README, selected files at SHA
  K->>K: Roslyn static analysis of C# projects
  K->>S: Restore, build, test with no network and limits
  S-->>K: Build and test report
  K->>L: Evidence bundle wrapped as untrusted data
  L-->>K: JSON findings with file and line references
  K->>K: Validate JSON schema and verify each reference exists
  K->>D: Save RepositoryFindings, mark analysis Completed
  C->>W: Poll or receive notification
```

A finding whose file or line reference cannot be verified against the pinned commit is discarded.

## 7. Interview turn loop (Thandi)

```mermaid
sequenceDiagram
  autonumber
  participant B as Browser
  participant W as Web API
  participant T as Speech services
  participant I as Interview agent
  participant D as PostgreSQL
  W->>B: Question text, snippet reference, audio URL
  B->>T: Request speech for question
  T-->>B: Audio stream
  B->>B: Play audio, show question and code snippet
  B->>B: Record answer, detect end of speech
  B->>W: Upload audio and consent flag
  W->>T: Transcribe with repo vocabulary hints
  T-->>W: Transcript
  W-->>B: Show transcript for correction
  B->>W: Confirm or edit transcript
  W->>I: Question, code context, confirmed transcript, probe depth
  I-->>W: JSON: verdict so far, next action, next question
  W->>D: Save InterviewTurn and evidence
  W-->>B: Next turn or move on or finish
```

Probe ladder and rubric: docs/06. Maximum three probes per question. Transcript is corrected by the candidate before scoring.

## 8. Electron assessment session

```mermaid
sequenceDiagram
  autonumber
  participant E as Electron agent
  participant W as Web API
  participant D as PostgreSQL
  participant K as Worker
  participant S as Sandbox runner
  actor A as Assessor
  E->>W: Sign in (system browser, PKCE)
  W-->>E: Short-lived session token
  E->>W: Fetch assessment and rules
  E->>E: Consent screen lists exactly what is recorded
  E->>W: Record consent and start session
  loop During assessment
    E->>E: Autosave locally every 15 seconds
    E->>W: Sync work snapshot and integrity events (batched)
  end
  E->>W: Submit final work
  W->>D: Save submission
  W->>K: Enqueue evaluation
  K->>S: Build and run tests
  S-->>K: Results
  K->>D: Save results
  A->>W: Review submission with integrity timeline
```

Offline: events and snapshots queue locally and sync when connectivity returns. Gaps are recorded as "connection lost", never as misconduct.

## 9. Competency status state machine

```mermaid
stateDiagram-v2
  [*] --> Claimed
  Claimed --> EvidenceSubmitted : candidate adds evidence
  EvidenceSubmitted --> UnderAssessment : assessment started
  UnderAssessment --> AssessmentCompleted : evidence complete
  AssessmentCompleted --> Verified : ASSESSOR approves
  AssessmentCompleted --> Developing : ASSESSOR recommends development
  AssessmentCompleted --> NotDemonstrated : ASSESSOR decides not demonstrated
  Developing --> UnderAssessment : reassessment
  NotDemonstrated --> UnderAssessment : reassessment
  Verified --> Expired : validity period ends
  Verified --> UnderAssessment : reassessment
  Expired --> UnderAssessment : reassessment
```

Domain enforcement: `Verified`, `Developing` and `NotDemonstrated` can only be set through `VerificationDecision`
created by a user in role Assessor. Unit-test that every other caller is rejected.

## 10. Assessment session states

```mermaid
stateDiagram-v2
  [*] --> Invited
  Invited --> ConsentGiven : candidate consents
  ConsentGiven --> InProgress : start
  InProgress --> Interrupted : connection or device lost
  Interrupted --> InProgress : resume
  InProgress --> Submitted : submit
  InProgress --> TimedOut : time limit reached, work auto-submitted
  Submitted --> Evaluated : tests and rubric run
  TimedOut --> Evaluated
  Evaluated --> Reviewed : assessor decision
  Reviewed --> [*]
```

## 11. Deployment view

```mermaid
flowchart TB
  user["Users"] --> cdn["TLS reverse proxy or platform ingress"]
  cdn --> web["Web container (non-root)"]
  web --> pg[("Managed PostgreSQL")]
  web --> obj[("Private object storage")]
  wk["Worker container"] --> pg
  wk --> obj
  wk -->|"job requests only"| sbx
  subgraph isolated["Isolated sandbox host: no secrets, no route to internal network"]
    sbx["Runner: disposable container per job, CPU and memory limits, timeout, no network"]
  end
  web --> ext["GitHub, LLM, Speech, Email over HTTPS"]
  wk --> ext
  elec["Electron installer (signed)"] --> cdn
```

Hackathon option: single host with Docker for web, worker and sandbox, managed Postgres, S3-compatible storage.
Keep the sandbox on a separate host or at minimum a separate Docker network with no egress and rootless runtime.

## 12. Trust boundaries and data flow

```mermaid
flowchart LR
  subgraph untrusted["Untrusted zone"]
    u1["Candidate browser and Electron"]
    u2["Candidate repository content"]
    u3["Candidate audio and answers"]
    u4["LLM output"]
  end
  subgraph trusted["Trusted zone"]
    t1["Web API: authn, authz, validation"]
    t2["Application and Domain"]
    t3[("PostgreSQL and object storage")]
  end
  subgraph contained["Contained zone"]
    c1["Sandbox runner"]
  end
  u1 -->|"validated requests"| t1
  u2 -->|"fetched by worker, treated as data"| t2
  u3 -->|"stored with consent"| t3
  u4 -->|"schema validated, references verified"| t2
  t1 --> t2 --> t3
  t2 -->|"job spec only"| c1
  c1 -->|"result report only"| t2
```

## 13. Technology decisions (ADR summary)

| ID | Decision | Reason | Alternative deferred |
|---|---|---|---|
| ADR-01 | Modular monolith | One deployable, fast to build, clear seams | Microservices |
| ADR-02 | Postgres as queue (SKIP LOCKED) | No extra infrastructure | RabbitMQ, Azure Service Bus |
| ADR-03 | Provider interfaces for AI, speech, storage | Swap vendors, test with fakes | Direct SDK use |
| ADR-04 | Voice in browser, assessment in Electron | Interview needs no install, assessment needs window focus control | Everything in Electron |
| ADR-05 | Server-side STT and TTS | Consistent quality, vocabulary hints, key never in browser | Browser Web Speech API (fallback only) |
| ADR-06 | Pin commit SHA for every analysis | Reproducibility and fairness | Analyse default branch |
| ADR-07 | Append-only VerificationRecords | Auditability | Mutable status column only |
| ADR-08 | Cookie auth for web, token auth for Electron | Right tool per client | One scheme for both |
| ADR-09 | Roslyn for C# static facts plus LLM for judgement | Deterministic evidence anchors the LLM | LLM only |
| ADR-10 | Sandbox on separate network, no egress | Untrusted code containment | Run on web host |

## 14. Non-functional requirements

| Area | Target (MVP) |
|---|---|
| Availability for demo | Works with provider failure via fallbacks |
| Interview latency | Candidate stops talking to Thandi starts speaking: under 3 s typical |
| Repo analysis | Small repo under 3 minutes, async with progress |
| Security | OWASP ASVS L1 minimum, no critical or high findings open at release |
| Accessibility | WCAG 2.2 AA on candidate flows, keyboard operable, captions for audio |
| Privacy | Consent per purpose, retention schedule enforced by job |
| Observability | Structured logs, correlation id per request and job, audit log for decisions |
| Portability | Everything runs from docker compose plus .env |

## 15. Failure modes and fallbacks

| Failure | Behaviour |
|---|---|
| LLM down or invalid JSON | Retry once, then mark analysis "needs retry", show cached demo result in demo mode only |
| Speech STT down | Switch to typed answers, same rubric |
| TTS down | Browser speech synthesis, then on-screen text |
| Microphone denied | Text-only interview offered immediately |
| GitHub rate limit | Use cached tree for pinned SHA, queue retry with backoff |
| Sandbox unavailable | Skip execution evidence, label "not executed", never fabricate test results |
| Electron loses network | Local autosave and event queue, resume and sync, record interruption |
| Time limit reached | Auto-submit latest snapshot |
