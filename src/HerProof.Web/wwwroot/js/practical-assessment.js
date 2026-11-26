// Browser-based practical assessment workspace (replaces the Electron-only path for the common
// case): real Monaco editor per question, a server-authoritative countdown, and the same fixed,
// published integrity-event list docs/07 describes — never screen capture, keystrokes, clipboard
// contents, or camera/microphone recording. Plain script, no bundler, matching this app's existing
// wwwroot/js convention (see interview-voice.js).
(function () {
  const root = document.getElementById("practical-assessment");
  if (!root) {
    return;
  }

  const sessionId = root.dataset.sessionId;
  const startedAtUtc = root.dataset.startedAtUtc;
  const durationMinutes = Number(root.dataset.durationMinutes);
  const questions = JSON.parse(root.dataset.questions || "[]");

  const csrfToken = document.querySelector('#submit-form input[name="__RequestVerificationToken"]')?.value ?? "";

  let editor = null;
  let models = [];
  let activeQuestionIndex = 0;
  let timerHandle = null;
  let hasSubmitted = false;
  let lastDeviceCounts = { cameras: 0, microphones: 0 };

  function postEvent(eventType) {
    fetch(`/assessment-sessions/${sessionId}/events`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrfToken,
      },
      body: JSON.stringify({ eventType }),
    }).catch(() => {
      // Offline is routine, not an error (docs/01 section 15) — the event is simply not recorded
      // server-side this time; nothing here blocks the candidate from continuing to work.
    });
    appendLogEntry(eventType);
  }

  const EVENT_LABELS = {
    focus_lost: "Tab lost focus",
    focus_regained: "Tab regained focus",
    network_lost: "Network lost",
    network_restored: "Network restored",
    camera_connected: "Camera connected",
    camera_disconnected: "Camera disconnected",
    microphone_connected: "Microphone connected",
    microphone_disconnected: "Microphone disconnected",
  };

  const FLAGGED_EVENT_TYPES = new Set(["focus_lost", "network_lost"]);

  function appendLogEntry(eventType) {
    const list = document.getElementById("event-list");
    if (!list) {
      return;
    }

    const item = document.createElement("li");
    item.textContent = `${new Date().toLocaleTimeString()} — ${EVENT_LABELS[eventType] ?? eventType}`;
    if (FLAGGED_EVENT_TYPES.has(eventType)) {
      item.classList.add("flag");
    }

    list.prepend(item);
  }

  function wireFocusAndNetworkEvents() {
    document.addEventListener("visibilitychange", () => {
      postEvent(document.hidden ? "focus_lost" : "focus_regained");
    });
    window.addEventListener("offline", () => postEvent("network_lost"));
    window.addEventListener("online", () => postEvent("network_restored"));
  }

  /**
   * Device *presence*, never device *content* (CLAUDE.md rule 4/5): enumerateDevices() lists
   * device kinds without ever calling getUserMedia, so no stream, no frame, no recording is ever
   * captured — only "a camera/microphone is plugged in or not".
   */
  function setDeviceStatus(elementId, label, count) {
    const el = document.getElementById(elementId);
    if (!el) {
      return;
    }

    const detected = count > 0;
    el.textContent = `${label}: ${detected ? `detected (${count})` : "not detected"}`;
    el.classList.toggle("detected", detected);
  }

  async function checkDevices(isInitialCheck) {
    if (!navigator.mediaDevices?.enumerateDevices) {
      setDeviceStatus("camera-status", "Camera", 0);
      setDeviceStatus("microphone-status", "Microphone", 0);
      return;
    }

    try {
      const devices = await navigator.mediaDevices.enumerateDevices();
      const cameras = devices.filter((d) => d.kind === "videoinput").length;
      const microphones = devices.filter((d) => d.kind === "audioinput").length;
      setDeviceStatus("camera-status", "Camera", cameras);
      setDeviceStatus("microphone-status", "Microphone", microphones);

      if (!isInitialCheck) {
        if (cameras > lastDeviceCounts.cameras) {
          postEvent("camera_connected");
        } else if (cameras < lastDeviceCounts.cameras) {
          postEvent("camera_disconnected");
        }

        if (microphones > lastDeviceCounts.microphones) {
          postEvent("microphone_connected");
        } else if (microphones < lastDeviceCounts.microphones) {
          postEvent("microphone_disconnected");
        }
      }

      lastDeviceCounts = { cameras, microphones };
    } catch {
      // A nice-to-have integrity signal, never a requirement for the assessment to proceed.
    }
  }

  function wireDeviceEvents() {
    void checkDevices(true);
    navigator.mediaDevices?.addEventListener?.("devicechange", () => void checkDevices(false));
  }

  function loadMonaco() {
    return new Promise((resolve) => {
      const loaderScript = document.createElement("script");
      loaderScript.src = "/lib/monaco/vs/loader.js";
      loaderScript.onload = () => {
        self.MonacoEnvironment = { getWorkerUrl: () => "/lib/monaco/vs/editor/editor.worker.js" };
        require.config({ paths: { vs: "/lib/monaco/vs" } });
        require(["vs/editor/editor.main"], () => resolve());
      };
      document.body.appendChild(loaderScript);
    });
  }

  function renderQuestionTabs() {
    const tabsEl = document.getElementById("question-tabs");
    if (!tabsEl || questions.length === 0) {
      return;
    }

    questions.forEach((q, index) => {
      const tab = document.createElement("button");
      tab.type = "button";
      tab.className = "btn btn-sm btn-outline-secondary question-tab";
      tab.textContent = `${index + 1}. ${q.title}`;
      tab.addEventListener("click", () => switchQuestion(index));
      tabsEl.appendChild(tab);
    });

    updateActiveTabStyling();
  }

  function updateActiveTabStyling() {
    const tabsEl = document.getElementById("question-tabs");
    if (!tabsEl) {
      return;
    }

    [...tabsEl.children].forEach((tab, index) => {
      tab.classList.toggle("btn-brand-tab-active", index === activeQuestionIndex);
    });
  }

  function switchQuestion(index) {
    activeQuestionIndex = index;
    editor.setModel(models[index]);
    document.getElementById("question-prompt").textContent = questions[index].prompt;
    updateActiveTabStyling();
  }

  async function createEditor() {
    await loadMonaco();

    if (questions.length > 0) {
      models = questions.map((q) => monaco.editor.createModel(q.starterCode, "csharp"));
      editor = monaco.editor.create(document.getElementById("editor-container"), {
        automaticLayout: true,
        minimap: { enabled: false },
      });
      renderQuestionTabs();
      switchQuestion(0);
    } else {
      editor = monaco.editor.create(document.getElementById("editor-container"), {
        value: "// Write your solution here\n",
        language: "csharp",
        automaticLayout: true,
        minimap: { enabled: false },
      });
      document.getElementById("question-prompt").textContent = "Write your solution below.";
    }
  }

  /**
   * Real server-authoritative timer (same rule as the Electron agent's — see
   * AssessmentSessionInfoController / AssessmentSessionsController.Submit): the deadline comes
   * from the server's own StartedAtUtc and the assessment's DurationMinutes, not a client-side
   * stopwatch. This countdown is a UI convenience only; the server independently decides
   * Submitted vs TimedOut when the code actually lands.
   */
  function startTimer() {
    const timerEl = document.getElementById("timer");
    if (!startedAtUtc) {
      timerEl.textContent = "--:--";
      return;
    }

    const deadline = new Date(startedAtUtc).getTime() + durationMinutes * 60_000;

    const tick = () => {
      const remainingMs = deadline - Date.now();
      if (remainingMs <= 0) {
        timerEl.textContent = "00:00";
        timerEl.classList.add("low-time");
        if (timerHandle) {
          clearInterval(timerHandle);
        }

        if (!hasSubmitted) {
          hasSubmitted = true;
          submitForm();
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

  function submitForm() {
    const files =
      questions.length > 0
        ? questions.map((q, index) => ({ path: q.fileName, content: models[index].getValue() }))
        : [{ path: "Solution.cs", content: editor.getValue() }];

    document.getElementById("files-json-input").value = JSON.stringify(files);

    const submitButton = document.getElementById("submit-button");
    submitButton.disabled = true;
    submitButton.textContent = "Submitting…";

    if (timerHandle) {
      clearInterval(timerHandle);
    }

    document.getElementById("submit-form").submit();
  }

  async function main() {
    wireFocusAndNetworkEvents();
    wireDeviceEvents();
    await createEditor();
    startTimer();

    document.getElementById("submit-form").addEventListener("submit", () => {
      hasSubmitted = true;
      const files =
        questions.length > 0
          ? questions.map((q, index) => ({ path: q.fileName, content: models[index].getValue() }))
          : [{ path: "Solution.cs", content: editor.getValue() }];
      document.getElementById("files-json-input").value = JSON.stringify(files);
    });
  }

  void main();
})();
