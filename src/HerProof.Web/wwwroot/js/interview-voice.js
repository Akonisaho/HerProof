// docs/05 P11 voice layer. Loaded only on the Take view when the interview is in Voice mode.
// Served from wwwroot so it works under a strict script-src 'self' CSP (no inline scripts allowed).
//
// Speech-to-text runs entirely in the browser (the free, built-in Web Speech API: Chrome/Edge only,
// no server round trip, no API key, no audio ever leaves the device) rather than recording and
// uploading audio to a paid cloud STT provider. ISpeechToText/the /transcribe endpoint still exist
// server-side for a future real provider key, but the default candidate-facing flow no longer uses
// them, since there is no free hosted STT option as good as what the browser already gives away.
//
// initInterviewVoice() is callable more than once: interview-ajax.js re-invokes it after swapping in
// each new question's markup (left column only, in place - the camera video element is never touched,
// so her camera/mic stream never has to reconnect between questions), replacing the previous
// question's prompt/form/recognition wiring with fresh ones for the new turn.
let activeRecognition = null;

function initInterviewVoice() {
    const panel = document.getElementById('voice-panel');
    if (!panel) {
        return;
    }

    // Tear down whatever the previous question's init() started - these elements were just replaced
    // (innerHTML swap), so any listeners attached to the old nodes are already gone with them; only
    // the free-running SpeechRecognition instance needs an explicit stop.
    if (activeRecognition) {
        try {
            activeRecognition.onend = null;
            activeRecognition.stop();
        } catch {
            // Already stopped - nothing to do.
        }
        activeRecognition = null;
    }

    const interviewId = panel.dataset.interviewId;
    const turnId = panel.dataset.turnId;
    const promptText = document.getElementById('prompt-text').textContent;

    // --- A simple visual cue that Nolwazi is "speaking" while her audio plays ---
    const avatar = document.getElementById('thandi-avatar');
    const statusEl = document.getElementById('thandi-status');

    let pausedForNolwazi = false;

    function setSpeaking(isSpeaking) {
        if (avatar) {
            avatar.classList.toggle('speaking', isSpeaking);
        }
        if (statusEl) {
            statusEl.textContent = isSpeaking ? 'Speaking…' : 'Your interviewer';
        }

        if (isSpeaking && !pausedForNolwazi) {
            pausedForNolwazi = true;
            activeRecognition?.stop();
        } else if (!isSpeaking && pausedForNolwazi) {
            pausedForNolwazi = false;
            startRecognition();
        }
    }

    // --- Play the question aloud: server-side TTS first, browser SpeechSynthesis as fallback ---
    const playBtn = document.getElementById('play-question-btn');
    const audioEl = document.getElementById('prompt-audio');
    audioEl.addEventListener('play', () => setSpeaking(true));
    audioEl.addEventListener('ended', () => setSpeaking(false));
    audioEl.addEventListener('pause', () => setSpeaking(false));

    playBtn.addEventListener('click', async () => {
        try {
            const response = await fetch(`/interviews/${interviewId}/turns/${turnId}/prompt-audio`);
            if (response.status === 204 || !response.ok) {
                throw new Error('no server audio');
            }
            const blob = await response.blob();
            audioEl.src = URL.createObjectURL(blob);
            await audioEl.play();
        } catch {
            if ('speechSynthesis' in window) {
                const utterance = new SpeechSynthesisUtterance(promptText);
                utterance.onstart = () => setSpeaking(true);
                utterance.onend = () => setSpeaking(false);
                window.speechSynthesis.speak(utterance);
            }
        }
    });

    // --- Live speech-to-text, free and entirely in the browser. The candidate's mic is already
    // recording continuously for the whole session (see interview-camera.js) - there is no separate
    // manual "record my answer" step. Recognition starts the moment the question loads and keeps
    // listening until the candidate submits, restarting itself if the browser's engine times out.
    const indicator = document.getElementById('recording-indicator');
    const unsupportedMsg = document.getElementById('voice-unsupported');
    const errorEl = document.getElementById('voice-error');
    const textarea = document.getElementById('answer-textarea');

    const SpeechRecognitionCtor = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognitionCtor) {
        indicator.classList.add('d-none');
        unsupportedMsg.classList.remove('d-none');
        // The server renders the textarea readonly whenever the session's Mode is Voice, regardless
        // of whether this specific browser actually supports speech recognition - without this, a
        // candidate on an unsupported browser sees "please type your answer below" next to a
        // textarea she physically cannot type into.
        textarea.removeAttribute('readonly');
        textarea.focus();
        return;
    }

    let baseText = textarea.value ? `${textarea.value} ` : '';
    let stoppedIntentionally = false;

    function showError(message) {
        errorEl.textContent = message;
        errorEl.classList.remove('d-none');
    }

    function startRecognition() {
        const recognition = new SpeechRecognitionCtor();
        activeRecognition = recognition;
        recognition.continuous = true;
        recognition.interimResults = true;
        recognition.lang = 'en-US';

        recognition.onresult = (event) => {
            let finalText = '';
            let interimText = '';
            for (let i = event.resultIndex; i < event.results.length; i++) {
                const transcript = event.results[i][0].transcript;
                if (event.results[i].isFinal) {
                    finalText += transcript;
                } else {
                    interimText += transcript;
                }
            }
            if (finalText) {
                baseText += `${finalText} `;
            }
            textarea.value = baseText + interimText;
        };

        recognition.onerror = (event) => {
            if (event.error === 'not-allowed' || event.error === 'service-not-allowed') {
                showError('Microphone access was denied — please type your answer instead.');
                stoppedIntentionally = true;
            } else if (event.error !== 'no-speech' && event.error !== 'aborted') {
                showError('Something went wrong with voice recognition — please type your answer instead.');
            }
        };

        // Chrome's recognition engine stops itself after a period of silence even with
        // continuous = true; restart it so listening genuinely never pauses mid-session.
        recognition.onend = () => {
            if (recognition !== activeRecognition) {
                return; // superseded by a newer question's recognition instance - let it go.
            }
            if (!stoppedIntentionally && !pausedForNolwazi) {
                try {
                    startRecognition();
                } catch {
                    // Ignore: a navigation already in flight will tear this down anyway.
                }
            }
        };

        try {
            recognition.start();
        } catch {
            // Already running, or the page is mid-navigation - nothing to do.
        }
    }

    startRecognition();

    const answerForm = document.getElementById('answer-form');
    answerForm?.addEventListener('submit', () => {
        stoppedIntentionally = true;
        activeRecognition?.stop();
    });
}

window.initInterviewVoice = initInterviewVoice;
initInterviewVoice();
