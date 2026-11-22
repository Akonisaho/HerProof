# Stubs register

Kept current throughout the build, not just at the end (CLAUDE.md working agreement). Every stub in code
is tagged `// STUB(HP-xxx)`; this file is the index. Remove a row only when the stub is replaced with the
real implementation.

| ID | Where | What's stubbed | Replaced in |
|---|---|---|---|
| HP-001 | src/HerProof.Web/Program.cs | Home page is the default MVC template page, not the real landing page | P04+ |
| HP-012 | electron-app/src/main.ts, renderer/index.html | Electron app is a bare shell: no consent screen, timer, Monaco editor, autosave or sync | P12 (Phase 2/3) |
| HP-013 | electron-app/src/main.ts | Integrity events are validated but not persisted to a local encrypted queue or synced to the Web API | P12/P13 (Phase 3) |
| HP-DIST | electron-app/package.json (`npm run dist`) | `electron-builder` packaging not configured; `dist` script is a placeholder | P12 (Phase 3), before rehearsal |
