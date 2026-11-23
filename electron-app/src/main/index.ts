import { app, BrowserWindow, ipcMain, session } from "electron";
import path from "node:path";
import { randomUUID } from "node:crypto";
import { EventQueue } from "./eventQueue";
import { ElectronSecretStore } from "./secretStore.electron";
import { SyncClient } from "./syncClient";
import { recordEventRequestSchema, type IntegrityEvent } from "../shared/events";

/** STUB(HP-025): full OAuth2 authorization-code-with-PKCE sign-in (ADR-08, docs/07 section 3) isn't
 * wired yet — that needs the Assessment/AssessmentSession API surface P13 builds. Session context is
 * read from environment variables for now so the rest of the agent (queue, sync, hardening, UI) is
 * real and testable ahead of that. Never a placeholder for the hardening itself, only for sign-in. */
const sessionContext = {
  sessionId: process.env.HERPROOF_SESSION_ID ?? randomUUID(),
  assessmentId: process.env.HERPROOF_ASSESSMENT_ID ?? randomUUID(),
  candidateId: process.env.HERPROOF_CANDIDATE_ID ?? randomUUID(),
  apiBaseUrl: process.env.HERPROOF_API_BASE_URL ?? "http://localhost:5299",
  accessToken: process.env.HERPROOF_ACCESS_TOKEN ?? null,
};

const queue = new EventQueue(
  path.join(app.getPath("userData"), "event-queue.json"),
  new ElectronSecretStore(),
);

const syncClient = new SyncClient(sessionContext.apiBaseUrl, () => sessionContext.accessToken);

let mainWindow: BrowserWindow | null = null;

function recordEvent(eventType: IntegrityEvent["eventType"], metadata: Record<string, unknown> = {}): IntegrityEvent {
  const event = queue.append({
    sessionId: sessionContext.sessionId,
    assessmentId: sessionContext.assessmentId,
    candidateId: sessionContext.candidateId,
    eventType,
    occurredAt: new Date().toISOString(),
    metadata,
  });

  // Principle #2 (docs/07 section 2): the candidate sees exactly what the assessor sees. Every
  // recorded event is pushed to the renderer's live panel the instant it's queued, not after sync.
  mainWindow?.webContents.send("herproof:event-recorded", event);
  return event;
}

async function syncPendingEvents(): Promise<void> {
  const pending = queue.pendingSync();
  if (pending.length === 0) {
    return;
  }

  const result = await syncClient.syncBatch(sessionContext.sessionId, pending);
  if (result.acceptedClientSeqs.length > 0) {
    queue.markSynced(result.acceptedClientSeqs);
  }
}

function createMainWindow(): BrowserWindow {
  const window = new BrowserWindow({
    width: 1200,
    height: 800,
    webPreferences: {
      preload: path.join(__dirname, "..", "preload", "index.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      webSecurity: true,
      webviewTag: false,
    },
  });

  window.webContents.setWindowOpenHandler(() => ({ action: "deny" }));
  window.webContents.on("will-navigate", (event, url) => {
    if (!url.startsWith("file://")) {
      event.preventDefault();
    }
  });

  // docs/07 section 5: the agent can only know its OWN window lost focus — never what the candidate
  // switched to. That's the entire signal, by design, not a limitation to work around.
  window.on("blur", () => recordEvent("focus_lost"));
  window.on("focus", () => recordEvent("focus_regained"));
  window.on("close", () => recordEvent("window_closed"));

  void window.loadFile(path.join(__dirname, "..", "renderer", "index.html"));
  return window;
}

ipcMain.handle("herproof:start-session", () => {
  if (queue.hasUnfinishedSession()) {
    // Relaunch after a crash or power loss (docs/07 section 6): record it, don't blame the candidate.
    recordEvent("session_interrupted");
  }

  return recordEvent("assessment_started");
});

ipcMain.handle("herproof:record-event", (_event, payload: unknown) => {
  const parsed = recordEventRequestSchema.safeParse(payload);
  if (!parsed.success) {
    return { ok: false };
  }

  recordEvent(parsed.data.eventType, parsed.data.metadata ?? {});
  return { ok: true };
});

ipcMain.handle("herproof:submit", async (_event, code: unknown) => {
  if (typeof code === "string") {
    await syncClient.submitCode(sessionContext.sessionId, code);
  }

  return recordEvent("assessment_submitted");
});

ipcMain.handle("herproof:get-events", () => queue.allOrdered());

ipcMain.handle("herproof:get-session-info", () => syncClient.fetchSessionInfo(sessionContext.sessionId));

app.whenReady().then(() => {
  session.defaultSession.webRequest.onHeadersReceived((details, callback) => {
    callback({
      responseHeaders: {
        ...details.responseHeaders,
        // A header-delivered CSP and the page's own <meta> CSP are enforced together (the stricter
        // directive wins per-directive), so this must explicitly allow what the meta tag allows —
        // style-src 'unsafe-inline' for the page's own styles and Monaco's injected theme/icon CSS,
        // worker-src 'self' for Monaco's same-origin editor.worker.js (never a blob: URL, so this
        // stays narrow; see renderer/app.js's MonacoEnvironment.getWorkerUrl).
        "Content-Security-Policy": [
          "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; worker-src 'self'; font-src 'self' data:",
        ],
      },
    });
  });

  mainWindow = createMainWindow();

  // docs/07 section 4: autosave every 15s, sync every 30s.
  setInterval(() => recordEvent("snapshot_saved"), 15_000);
  setInterval(() => void syncPendingEvents(), 30_000);

  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      mainWindow = createMainWindow();
    }
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});
