# HerProof Build Kit

Everything needed to take HerProof from spec to a professional, demo-ready, securely-built product.

| File | What it is | Who uses it |
|---|---|---|
| CLAUDE.md | Project memory for Claude Code. Put in repo root. | Claude Code |
| docs/00-PITCH.md | Pitch script, demo script, differentiators, judge Q&A | Presenters |
| docs/01-ARCHITECTURE.md | Full architecture with diagrams (C4, sequences, states, deployment) | Everyone |
| docs/02-DATABASE.md | ERD and data rules | Backend |
| docs/03-SECURITY-PRIVACY.md | Threat model, sandbox, POPIA mapping, responsible AI | Everyone |
| docs/04-CICD-DEVSECOPS.md | Pipeline design, scans, release, branch protection | DevOps |
| docs/05-BUILD-PLAN-AND-CLAUDE-CODE-PROMPTS.md | Order of work, workstreams, copy-paste prompts, demo fallbacks | Builders |
| docs/06-AI-INTERVIEWER-SPEC.md | The voice interviewer: persona, loop, rubric, JSON schemas | AI workstream |
| docs/07-ELECTRON-AGENT-SPEC.md | Assessment agent: security, events, offline, consent | Electron workstream |
| docs/08-PARTNER-STUDY-GUIDE.md | 2-minute system explanation and self-quiz | Teammates |
| .github/ | CI, security scans, CodeQL, release, Dependabot, PR template | GitHub |
| licenses/allowed-licenses.json | License allow-list used by scans | CI |
| docker/ | Compose stack, Dockerfiles | Local dev |

## How to start
1. Create the repo. Copy this kit into it. Also add your original master spec as docs/MASTER-SPEC.md.
2. Open VS Code, start Claude Code, and run prompt P00 from docs/05.
3. Work prompt by prompt. Merge only when CI is green.
4. In GitHub: apply the settings in docs/04 section 7 (branch protection, secret scanning, push protection).

## Verify-before-use notes
- Workflow action versions and CLI flags were written from experience, not run against your repo.
  First CI run will tell you what to adjust. Dependabot will keep action versions current.
- Any statistic or competitor claim in the pitch is marked [VERIFY]. Check each before presenting.
- Bursary details change often. Confirm every seeded bursary on the provider's own page.
- Get legal review of consent text and retention periods before real candidates use the system.
