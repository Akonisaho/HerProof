// Keeps the voice interview on one page across questions instead of a full navigation per question -
// a full page load would tear down and reconnect the candidate's camera/mic stream every single
// question (interview-camera.js has no way to hand a live MediaStream across a navigation), which
// looked like "the camera opens again" each time she answered. Submitting the answer form here is
// intercepted, posted via fetch, and only the left column (prompt/form/question counter) is swapped
// in from the server's response - the camera panel in the right column is never touched, so her
// stream keeps running untouched between questions. Text-mode interviews (no camera) and the end of
// the interview (redirected to the Report page) still navigate normally.
(function () {
    function wireAnswerForm() {
        const form = document.getElementById('answer-form');
        const voicePanel = document.getElementById('voice-panel');
        if (!form || !voicePanel) {
            return; // text mode: no camera to preserve, let the normal full-page submit happen.
        }

        form.addEventListener('submit', async (event) => {
            event.preventDefault();
            const buttons = form.querySelectorAll('button[type="submit"]');
            buttons.forEach((b) => { b.disabled = true; });

            try {
                const action = event.submitter?.getAttribute('formaction') || form.action;
                const response = await fetch(action, { method: 'POST', body: new FormData(form) });

                if (!response.ok) {
                    buttons.forEach((b) => { b.disabled = false; });
                    return;
                }

                const html = await response.text();
                const parsed = new DOMParser().parseFromString(html, 'text/html');
                const newLeftCol = parsed.querySelector('.col-lg-7');

                if (!parsed.getElementById('voice-panel') || !newLeftCol) {
                    // Interview finished (Report page) or switched to text mode - a real navigation
                    // is correct here; there's no camera stream left worth preserving either way.
                    window.location.href = response.url;
                    return;
                }

                const currentLeftCol = document.querySelector('.col-lg-7');
                currentLeftCol.innerHTML = newLeftCol.innerHTML;

                wireAnswerForm(); // the <form> node was just replaced - rewire the fresh one.
                window.initInterviewVoice?.();
                window.initInterviewGuard?.();
                window.initInterviewThinking?.();
            } catch {
                buttons.forEach((b) => { b.disabled = false; });
            }
        });
    }

    wireAnswerForm();
})();
