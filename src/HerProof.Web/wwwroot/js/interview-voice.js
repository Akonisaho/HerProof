// docs/05 P11 voice layer. Loaded only on the Take view when the interview is in Voice mode.
// Served from wwwroot so it works under a strict script-src 'self' CSP (no inline scripts allowed).
//
// Speech-to-text runs entirely in the browser (the free, built-in Web Speech API: Chrome/Edge only,
// no server round trip, no API key, no audio ever leaves the device) rather than recording and
// uploading audio to a paid cloud STT provider. ISpeechToText/the /transcribe endpoint still exist
// server-side for a future real provider key, but the default candidate-facing flow no longer uses
// them, since there is no free hosted STT option as good as what the browser already gives away.
(function () {
    const panel = document.getElementById('voice-panel');
    if (!panel) {
        return;
    }

    const interviewId = panel.dataset.interviewId;
    const turnId = panel.dataset.turnId;
    const promptText = document.getElementById('prompt-text').textContent;

    // --- A simple visual cue that Thandi is "speaking" while her audio plays ---
    const avatar = document.getElementById('thandi-avatar');
    const statusEl = document.getElementById('thandi-status');

    function setSpeaking(isSpeaking) {
        if (!avatar) {
            return;
        }
        avatar.classList.toggle('speaking', isSpeaking);
        if (statusEl) {
            statusEl.textContent = isSpeaking ? 'Speaking…' : 'Your interviewer';
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

    // --- Live speech-to-text while recording, free and entirely in the browser ---
    const recordBtn = document.getElementById('record-btn');
    const stopBtn = document.getElementById('stop-btn');
    const indicator = document.getElementById('recording-indicator');
    const unsupportedMsg = document.getElementById('voice-unsupported');
    const errorEl = document.getElementById('voice-error');
    const textarea = document.getElementById('answer-textarea');

    const SpeechRecognitionCtor = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognitionCtor) {
        recordBtn.classList.add('d-none');
        unsupportedMsg.classList.remove('d-none');
        return;
    }

    let recognition = null;
    let baseText = '';

    function showError(message) {
        errorEl.textContent = message;
        errorEl.classList.remove('d-none');
    }

    recordBtn.addEventListener('click', () => {
        errorEl.classList.add('d-none');
        baseText = textarea.value ? `${textarea.value} ` : '';

        recognition = new SpeechRecognitionCtor();
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
            } else if (event.error !== 'no-speech' && event.error !== 'aborted') {
                showError('Something went wrong with voice recognition — please type your answer instead.');
            }
        };

        recognition.onend = () => {
            indicator.classList.add('d-none');
            recordBtn.classList.remove('d-none');
            stopBtn.classList.add('d-none');
        };

        recognition.start();
        indicator.classList.remove('d-none');
        recordBtn.classList.add('d-none');
        stopBtn.classList.remove('d-none');
    });

    stopBtn.addEventListener('click', () => {
        if (recognition) {
            recognition.stop();
        }
    });
})();
