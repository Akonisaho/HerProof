# Partner Study Guide

## Explain HerProof in 2 minutes
HerProof is a verification pipeline. An employer or bursary posts an opportunity with required competencies and levels.
A candidate picks it and gets a gap report. The report separates what she has demonstrated, what she has only claimed,
and what she has not yet been assessed on. She can add non-traditional experience (caregiving, community work) and the
system suggests competency mappings she confirms.
For software roles she connects a GitHub repository. We pin the commit, analyse the code, and run build and tests in a
sandbox. Then Thandi, the AI interviewer, speaks to her in the browser, asks questions grounded in her own files,
listens, and asks follow-ups based on her answers. In parallel she completes a timed practical in the Electron app,
which records a short transparent list of integrity events. An assessor reviews AI recommendations, test results,
the transcript and the event timeline, and makes the decision. Approved competencies land on her Skills Passport,
which she can share by a revocable link or QR code. Bursary providers and recruiters can verify it without seeing private data.

## The five rules to repeat
1. Evidence before verification. 2. AI recommends, humans decide. 3. Missing evidence is not zero.
4. We never score accent, face or emotion. 5. Candidate code never runs on our web server.

## Self-quiz (answers are in the docs)
1. Which component is the only one allowed to create a Verified record? (01, 03)
2. What are the three states in the gap report and why? (01, 05)
3. Why does the web app not run candidate code, and where does it run? (01, 03)
4. What does the Electron agent record, and what must it never do? (07)
5. What happens after two failed follow-up probes? (06)
6. How is prompt injection from repo files mitigated? (03, 06)
7. Where are GitHub tokens stored and who can read them? (03)
8. What can a public passport link show and not show? (03, 02)
9. Which CI jobs block a merge? (04)
10. What is the fallback if the microphone fails during the demo? (00, 05)
11. Why a modular monolith, and what is the job queue? (01)
12. Name three differentiators versus HackerEarth-style tools. (00)
13. How does the system handle POPIA data subject requests? (03)
14. What does "Developing" mean versus "Not demonstrated"? (01 state diagram)
15. What is the demo's signature moment in the Electron agent? (00)
