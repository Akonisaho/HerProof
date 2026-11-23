// Plain renderer script (docs/07 section 10: "minimal framework"). Runs fully sandboxed — no Node,
// no require — the only bridge to the main process is window.herproof, exposed by the preload script.

const EVENT_LABELS = {
  assessment_started: "Assessment started",
  assessment_paused: "Assessment paused",
  assessment_resumed: "Assessment resumed",
  assessment_submitted: "Assessment submitted",
  focus_lost: "Focus lost",
  focus_regained: "Focus regained",
  window_closed: "Window closed",
  camera_connected: "Camera connected",
  camera_disconnected: "Camera disconnected",
  microphone_connected: "Microphone connected",
  microphone_disconnected: "Microphone disconnected",
  network_lost: "Network lost",
  network_restored: "Network restored",
  session_interrupted: "Session interrupted (crash or power loss)",
  snapshot_saved: "Autosave",
};

// Events that are worth visually flagging in the panel — informational only, never a verdict
// (docs/07 section 7: "thresholds only choose what to highlight, never change a score or status").
const FLAGGED_EVENT_TYPES = new Set(["focus_lost", "window_closed", "network_lost", "session_interrupted"]);

function renderConsentList() {
  const list = document.getElementById("consent-event-list");
  for (const [type, label] of Object.entries(EVENT_LABELS)) {
    const item = document.createElement("li");
    item.textContent = label;
    list.appendChild(item);
  }
}

function appendEvent(event) {
  const list = document.getElementById("event-list");
  const item = document.createElement("li");
  const time = new Date(event.occurredAt).toLocaleTimeString();
  item.textContent = `${time} — ${EVENT_LABELS[event.eventType] ?? event.eventType}`;
  if (FLAGGED_EVENT_TYPES.has(event.eventType)) {
    item.classList.add("flag");
  }

  list.prepend(item);
}

let timerHandle = null;
let hasAutoSubmitted = false;
let editor = null;

// Monaco needs its own worker loaded from a same-origin file, not a blob: URL, so the editor
// works under this app's strict CSP (default-src 'self', no worker-src relaxation needed).
self.MonacoEnvironment = {
  getWorkerUrl: () => "./vs/editor/editor.worker.js",
};

function loadMonaco() {
  return new Promise((resolve) => {
    const loaderScript = document.createElement("script");
    loaderScript.src = "vs/loader.js";
    loaderScript.onload = () => {
      require.config({ paths: { vs: "vs" } });
      require(["vs/editor/editor.main"], () => resolve());
    };
    document.body.appendChild(loaderScript);
  });
}

async function createEditor() {
  await loadMonaco();
  editor = monaco.editor.create(document.getElementById("editor-container"), {
    value: "// Write your solution here\n",
    language: "csharp",
    automaticLayout: true,
    minimap: { enabled: false },
  });
}

/**
 * Real server-authoritative timer (HP-026, closed): the deadline comes from the server's own
 * StartedAtUtc + the assessment's DurationMinutes (docs/01 section 10), not a client-side stopwatch
 * counting up from zero. The countdown here is a UI convenience only — the server independently
 * decides Submitted vs TimedOut when the code actually lands (AssessmentSessionInfoController).
 */
async function startTimer() {
  const timerEl = document.getElementById("timer");
  const info = await window.herproof.getSessionInfo();

  if (!info || !info.startedAtUtc) {
    // No real session info available (offline, no token) — show an honest "unknown" rather than a
    // fabricated countdown, but never block the candidate from working.
    timerEl.textContent = "--:--";
    return;
  }

  const deadline = new Date(info.startedAtUtc).getTime() + info.durationMinutes * 60_000;

  const tick = () => {
    const remainingMs = deadline - Date.now();
    if (remainingMs <= 0) {
      timerEl.textContent = "00:00";
      timerEl.classList.add("low-time");
      if (timerHandle) {
        clearInterval(timerHandle);
      }

      if (!hasAutoSubmitted) {
        hasAutoSubmitted = true;
        void doSubmit();
      }

      return;
    }

    const totalSeconds = Math.ceil(remainingMs / 1000);
    const minutes = String(Math.floor(totalSeconds / 60)).padStart(2, "0");
    const seconds = String(totalSeconds % 60).padStart(2, "0");
    timerEl.textContent = `${minutes}:${seconds}`;
    timerEl.classList.toggle("low-time", totalSeconds <= 60);
  };

  tick();
  timerHandle = setInterval(tick, 1000);
}

async function doSubmit() {
  const code = editor ? editor.getValue() : "";
  await window.herproof.submit(code);
  if (timerHandle) {
    clearInterval(timerHandle);
  }

  const submitButton = document.getElementById("submit-button");
  submitButton.disabled = true;
  submitButton.textContent = "Submitted";
}

function wireNetworkEvents() {
  window.addEventListener("offline", () => window.herproof.recordEvent({ eventType: "network_lost" }));
  window.addEventListener("online", () => window.herproof.recordEvent({ eventType: "network_restored" }));
}

async function main() {
  renderConsentList();
  window.herproof.onEvent(appendEvent);
  wireNetworkEvents();

  document.getElementById("consent-button").addEventListener("click", async () => {
    await window.herproof.startSession();
    document.getElementById("consent-screen").classList.add("hidden");
    document.getElementById("assessment-screen").classList.remove("hidden");
    await createEditor();
    await startTimer();
  });

  document.getElementById("submit-button").addEventListener("click", () => {
    hasAutoSubmitted = true;
    void doSubmit();
  });
}

void main();
