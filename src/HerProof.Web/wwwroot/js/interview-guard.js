// An interview is one sitting, not a draft. If the candidate closes the tab, navigates away, or
// hits back mid-interview instead of submitting an answer, the interview is abandoned server-side
// (see InterviewController.Abandon) so there is no way to come back and "resume" it later.
//
// initInterviewGuard() is callable more than once: interview-ajax.js re-invokes it after each
// in-page question swap, since that replaces the <form> node this file watches (the old node's
// "submitting" flag would otherwise go stale, making a real mid-question abandon silently unreported).
// The pagehide listener itself is registered only once - it isn't tied to the form node, so it stays
// correct across swaps on its own.
let interviewGuardSubmitting = false;
let interviewGuardPagehideWired = false;

function initInterviewGuard() {
    const stepEl = document.getElementById('interview-step');
    const form = document.getElementById('answer-form');
    if (!stepEl || !form) {
        return;
    }

    const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
    if (!tokenInput) {
        return;
    }

    interviewGuardSubmitting = false;
    form.addEventListener('submit', () => {
        interviewGuardSubmitting = true;
    });

    if (interviewGuardPagehideWired) {
        return;
    }
    interviewGuardPagehideWired = true;

    window.addEventListener('pagehide', () => {
        if (interviewGuardSubmitting) {
            return;
        }
        const interviewId = document.getElementById('interview-step')?.dataset.interviewId;
        const currentTokenInput = document.getElementById('answer-form')?.querySelector('input[name="__RequestVerificationToken"]');
        if (!interviewId || !currentTokenInput) {
            return;
        }
        const body = new Blob(
            [`__RequestVerificationToken=${encodeURIComponent(currentTokenInput.value)}`],
            { type: 'application/x-www-form-urlencoded' }
        );
        navigator.sendBeacon(`/interviews/${interviewId}/abandon`, body);
    });
}

window.initInterviewGuard = initInterviewGuard;
initInterviewGuard();
