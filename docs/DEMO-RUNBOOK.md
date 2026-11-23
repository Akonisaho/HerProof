# Demo runbook

The exact click path for docs/00-PITCH.md's five-minute demo, the fallback switches to use if
something goes wrong live, and the pre-flight checklist to run before you walk on stage. Written so
anyone on the team can run the demo, not just whoever built the feature.

## 0. Reset before every rehearsal and before the real thing

```
bash scripts/demo-reset.sh
```

This wipes the local Postgres volume, brings Postgres and Mailpit back up, applies every migration,
and runs the seeder once (it's Development-only and runs automatically on first startup). After it
finishes, start the app for real:

```
dotnet run --project src/HerProof.Web
```

Demo accounts (all have password `Demo#Passw0rd!`):

| Role | Email |
|---|---|
| Candidate (the household-budget story) | lerato@demo.herproof.test |
| Candidate (pre-analysed repo + interview, see below) | anko@demo.herproof.test |
| Candidate | keabetswe@demo.herproof.test |
| Candidate | naledi@demo.herproof.test |
| Recruiter | recruiter@demo.herproof.test |
| Assessor | assessor@demo.herproof.test |
| Bursary partner | bursarypartner@demo.herproof.test |

Anko's `anko-task-tracker` repository, its analysis, and her Thandi interview are **pre-seeded**
(`DbSeeder`, marked `IsDemoData`) — this is the instant fallback from step 3 below if GitHub or the
LLM is slow or unreachable at the venue. Every page showing seeded data says "Demo data" so nothing is
ever presented as a live result that isn't one.

## 1. Pre-flight checklist (run this before the room fills up)

- [ ] Venue wifi tested; phone hotspot charged and its password written down, as the fallback network.
- [ ] `docker ps` shows `docker-postgres-1` and `docker-mailpit-1` healthy.
- [ ] `dotnet run --project src/HerProof.Web` starts clean, `http://localhost:5280/` loads.
- [ ] Logged into all seven demo accounts once each, just to confirm no lockouts from rehearsal.
- [ ] `AI__ApiKey` set if the live-LLM beat (step 3) will be attempted for real; otherwise confirm
      `FakeAiService`'s output still reads sensibly for a cold, non-demo repository if someone submits
      one live (judges sometimes ask to see a real one).
- [ ] Browser microphone permission granted once already (even though voice is P11-partial/text-mode
      today) so no OS permission dialog interrupts the flow.
- [ ] A phone with a QR scanner ready for step 5.
- [ ] Electron app installer present on the demo laptop if attempting the live integrity-agent beat;
      otherwise the recorded-video fallback (docs/05 section 5) is queued up and ready to play.
- [ ] Laptop power connected, not on battery — a mid-demo sleep/throttle is avoidable.

## 2. The click path

**1. Lerato — Gap-to-Proof, never a zero (20s)**
Log in as `lerato@demo.herproof.test`. Go to **Opportunities → Women in Tech Bursary**. Point out the
gap report: competencies she hasn't demonstrated show **"Not yet assessed"**, never a flat failure —
say the line out loud: "we never turn missing evidence into a zero."

**2. Candidate picks a role, sees the gap (20s)**
Still as Lerato: **Opportunities → Junior C# Developer**. Show the three-state gap report
(Demonstrated / Not yet assessed / Not demonstrated) and the eligibility section
(Met/NotMet/Unknown per rule).

**3. Anko — repository + Thandi interview (60s)**
Log in as `anko@demo.herproof.test`. Go to **Repositories**. Her `anko-task-tracker` repo is already
analysed — point at the **Demo data** badge and say so plainly: this is cached, not computed live,
and the badge is how you'd always know. Click through to the interview at
**Interviews → (her interview) → report**: code on one side (`TaskService.cs`), her answer on the
other, Thandi's two questions each naming a real line in the file.
*Live fallback*: if network/LLM is solid, submit a fresh public repo instead and run a real interview
— same flow, no badge.

**4. Electron integrity moment (40s)**
Launch the Electron agent, consent screen, start a timed assessment, **switch tabs on purpose**. Show
the event appearing in the candidate's own panel, then switch to the assessor's integrity timeline and
show the same event labelled "flag for review" — say explicitly: no automatic penalty, ever.
*Fallback if Electron can't run on the demo machine*: play the pre-recorded video instead (docs/05
section 5) and narrate over it.

**5. Assessor verifies, Passport updates, QR scan (15s)**
Log in as `assessor@demo.herproof.test`. Go to **Assessor dashboard**, open the pending item, show the
**"AI suggestion (not a decision)"** badge clearly separated from the decision form, submit
**Verified**. Switch back to the candidate (or a second logged-in tab) and open **My Skills Passport**
— the new credential is there. Create a share link scoped to just that credential, show the QR code,
and have a judge scan it on their own phone: the public verify page loads with nothing but the
credential — no recordings, transcripts, private repo, contact details, integrity events or scores.

**Bonus, if time remains: bursary application with attached credential**
Back in Lerato's account, apply to **Women in Tech Bursary** with her verified credential attached (if
she has one from a prior rehearsal) — or show it on Anko's account once step 5 has verified a
competency for her. Log in as `bursarypartner@demo.herproof.test` and show the partner's applications
review page: the attached credential resolved to its competency name, evidence included, no file to
download, no email required.

## 3. Fallback switches (docs/05 section 5 — do not remove these)

| Risk | What to actually do |
|---|---|
| Venue wifi fails | Switch to the phone hotspot; everything in step 3 can run entirely on Anko's pre-seeded demo data with zero network calls |
| LLM slow or down | Use Anko's pre-seeded repo/interview (step 3) instead of submitting a fresh one; the "Demo data" badge is expected and fine to point at |
| GitHub rate-limited (real risk — hit during this build's own testing) | Same as above: pre-seeded data needs no GitHub call at all |
| Microphone problem | Text mode carries the full rubric already (interviews are text-first in this build) — nothing to switch, just don't try to speak |
| Electron won't launch on the demo machine | Play the pre-recorded integrity-agent video and narrate over it |
| A login locks out (5 failed attempts) | Use a different seeded account for that role, or wait 15 minutes — do not disable lockout for the demo |
| Rate limiter trips (e.g. too many page refreshes while rehearsing) | Wait out the 1-minute window, or restart the app (`dotnet run` again) to reset in-memory limiter state |

## 4. Never fake results silently

Every piece of cached/pre-seeded data in this demo is visibly marked **"Demo data"** in the UI
(`RepositoryAnalysis.IsDemoData`, `Interview.IsDemoData`). If a judge asks "is this real or cached?",
the honest answer is always on the screen already — point at it rather than answering from memory.
