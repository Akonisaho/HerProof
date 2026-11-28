// Shows a "thinking" state the moment an answer is submitted, for the whole server round-trip while
// Nolwazi evaluates it (a real AI call, genuinely not instant) — otherwise the page just looks frozen.
// Works for both voice and text mode: evaluation happens server-side regardless of how the answer
// was captured.
//
// initInterviewThinking() is callable more than once: interview-ajax.js re-invokes it after each
// in-page question swap (voice mode only), since that replaces the <form> node this file attaches to.
function initInterviewThinking() {
    const form = document.getElementById('answer-form');
    const avatar = document.getElementById('thandi-avatar');
    const statusEl = document.getElementById('thandi-status');
    const dots = document.getElementById('thandi-thinking-dots');
    if (!form || !avatar) {
        return;
    }

    form.addEventListener('submit', () => {
        avatar.classList.remove('speaking');
        avatar.classList.add('thinking');
        if (statusEl) {
            statusEl.textContent = 'Reviewing your answer…';
        }
        if (dots) {
            dots.classList.remove('d-none');
        }
        form.querySelectorAll('button[type="submit"]').forEach((btn) => {
            btn.disabled = true;
        });
    });
}

window.initInterviewThinking = initInterviewThinking;
initInterviewThinking();
