// Shows a "thinking" state the moment an answer is submitted, for the whole server round-trip while
// Thandi evaluates it (a real AI call, genuinely not instant) — otherwise the page just looks frozen.
// Works for both voice and text mode: evaluation happens server-side regardless of how the answer
// was captured, and a classic form POST still renders this page's current DOM until the next page
// arrives, so the visual update is visible for the whole wait, not just a flash.
(function () {
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
})();
