function initCareerGuide(ids) {
  const messages = document.getElementById(ids.messages);
  const form = document.getElementById(ids.form);
  const input = document.getElementById(ids.input);
  const button = form?.querySelector("button");
  const csrfToken = document.getElementById(ids.csrf)?.value ?? "";

  if (!messages || !form || !input || !button) {
    console.warn("Career guide: one or more required elements were not found.", ids);
    return null;
  }

  // Never let this form submit natively (which would reload the page and look like the whole
  // thing just closed) - belt and suspenders alongside the click/keydown handlers below. (Not an
  // inline onsubmit="" attribute: this app's CSP is script-src 'self' with no unsafe-inline, so an
  // inline handler would be silently blocked by the browser and never actually run.)
  form.addEventListener("submit", (event) => event.preventDefault());

  let conversationSoFar = "";

  function appendMessage(text, who) {
    const bubble = document.createElement("div");
    bubble.className = `career-guide-bubble career-guide-bubble-${who}`;
    bubble.textContent = text;
    messages.appendChild(bubble);
    messages.scrollTop = messages.scrollHeight;
  }

  function appendRoadmapLink(goal) {
    const link = document.createElement("a");
    link.className = "btn-brand-outline btn-sm career-guide-roadmap-link";
    link.href = `/learning/roadmap?goal=${encodeURIComponent(goal)}`;
    link.textContent = `View the ${goal} roadmap`;
    messages.appendChild(link);
    messages.scrollTop = messages.scrollHeight;
  }

  function greet() {
    if (messages.childElementCount === 0) {
      appendMessage("Hi! Tell me what you're hoping to become, or what you enjoy, and I'll help point you to a real roadmap.", "guide");
    }
  }

  async function send() {
    const text = input.value.trim();
    if (!text) {
      return;
    }

    appendMessage(text, "candidate");
    input.value = "";
    input.disabled = true;
    button.disabled = true;

    try {
      const response = await fetch("/career-guide/chat", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrfToken,
        },
        body: JSON.stringify({ conversationSoFar, message: text }),
      });

      if (!response.ok) {
        appendMessage("Sorry, I couldn't reply just now - try again in a moment.", "guide");
        return;
      }

      const data = await response.json();
      appendMessage(data.reply, "guide");
      conversationSoFar += `\nCandidate: ${text}\nGuide: ${data.reply}`;

      if (data.suggestedGoal) {
        appendRoadmapLink(data.suggestedGoal);
      }
    } catch {
      appendMessage("Sorry, I couldn't reply just now - try again in a moment.", "guide");
    } finally {
      input.disabled = false;
      button.disabled = false;
      input.focus();
    }
  }

  button.type = "button";
  button.addEventListener("click", send);
  input.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      send();
    }
  });

  return { greet };
}

(() => {
  const toggle = document.getElementById("career-guide-toggle");
  const panel = document.getElementById("career-guide-panel");
  if (!toggle || !panel) {
    return;
  }

  const guide = initCareerGuide({
    messages: "career-guide-messages",
    form: "career-guide-form",
    input: "career-guide-input",
    csrf: "career-guide-csrf",
  });

  let opened = false;
  toggle.addEventListener("click", () => {
    opened = !opened;
    panel.hidden = !opened;
    if (opened) {
      guide?.greet();
    }
  });
})();

// The Roadmap page's "Or just ask" card opens a full Bootstrap modal version of the same chat
// (career-guide-inline-*) instead of the small corner panel above. This wiring used to live in an
// inline <script> block on that page - this app's CSP (script-src 'self', no unsafe-inline, no
// nonce) silently blocked it from ever running at all, so the modal's Send button looked wired up
// in the markup but had no JavaScript behind it whatsoever. Living here, in this externally-loaded
// file, it actually executes.
(() => {
  const modalEl = document.getElementById("career-guide-modal");
  if (!modalEl) {
    return;
  }

  // Not gated on `typeof bootstrap` here: this script loads before bootstrap.bundle.min.js in
  // document order (see _Layout.cshtml), so that global isn't defined yet at this point - but
  // "shown.bs.modal" is a plain DOM event Bootstrap dispatches later, once it's actually loaded
  // and the modal is actually shown, so addEventListener here doesn't need it to exist yet.

  const inlineGuide = initCareerGuide({
    messages: "career-guide-inline-messages",
    form: "career-guide-inline-form",
    input: "career-guide-inline-input",
    csrf: "career-guide-csrf",
  });

  modalEl.addEventListener("shown.bs.modal", () => {
    inlineGuide?.greet();
    document.getElementById("career-guide-inline-input")?.focus();
  });
})();
