// Live camera self-view + sound-level meter, shown first as a dedicated device check before the
// interview starts, then continuing during the interview itself (Nolwazi's side, right column) — a
// visible, honest "yes this is on" signal, not a recording pipeline: nothing here is uploaded, stored,
// or sent anywhere. Degrades silently (CLAUDE.md "transparent, never punitive") if the candidate
// denies permission or has no camera - the interview continues exactly the same either way.
(function () {
    const deviceCheckStep = document.getElementById('device-check-step');
    const interviewStep = document.getElementById('interview-step');
    const startBtn = document.getElementById('start-interview-btn');
    const checkVideo = document.getElementById('candidate-camera');

    const checkRecBadge = document.getElementById('camera-rec-badge');
    const checkSoundBar = document.querySelector('.camera-sound-bar');
    const unavailable = document.getElementById('camera-unavailable');

    const interviewVideo = document.getElementById('candidate-camera-interview');
    const interviewRecBadge = document.getElementById('camera-rec-badge-interview');
    const interviewSoundBar = document.querySelector('.camera-sound-bar-interview');

    if (!checkVideo && !interviewVideo) {
        return;
    }

    // From question 2 onward there is no device-check gate (it only shows once, on question 1),
    // so the camera must attach straight into the interview panel instead of the check panel.
    if (!checkVideo) {
        (async () => {
            try {
                const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
                interviewVideo.srcObject = stream;
                interviewRecBadge?.classList.remove('d-none');
                if (stream.getAudioTracks().length > 0 && (window.AudioContext || window.webkitAudioContext)) {
                    const AudioContextCtor = window.AudioContext || window.webkitAudioContext;
                    const audioCtx = new AudioContextCtor();
                    const source = audioCtx.createMediaStreamSource(stream);
                    const analyserNode = audioCtx.createAnalyser();
                    analyserNode.fftSize = 256;
                    source.connect(analyserNode);
                    const data = new Uint8Array(analyserNode.frequencyBinCount);
                    const tick = () => {
                        analyserNode.getByteFrequencyData(data);
                        const average = data.reduce((sum, value) => sum + value, 0) / data.length;
                        const level = Math.min(100, Math.round((average / 60) * 100));
                        if (interviewSoundBar) {
                            interviewSoundBar.style.width = `${level}%`;
                            interviewSoundBar.classList.toggle('active', level > 8);
                        }
                        requestAnimationFrame(tick);
                    };
                    tick();
                }
                window.addEventListener('beforeunload', () => stream.getTracks().forEach((track) => track.stop()));
            } catch {
                interviewVideo.closest('.camera-box')?.classList.add('d-none');
            }
        })();
        return;
    }

    let activeStream = null;
    let analyser = null;
    let analyserData = null;

    function showUnavailable() {
        checkVideo.closest('.camera-box')?.classList.add('d-none');
        unavailable?.classList.remove('d-none');
    }

    function setupSoundMeter(mediaStream) {
        if (mediaStream.getAudioTracks().length === 0 || !(window.AudioContext || window.webkitAudioContext)) {
            return;
        }

        const AudioContextCtor = window.AudioContext || window.webkitAudioContext;
        const audioCtx = new AudioContextCtor();
        const source = audioCtx.createMediaStreamSource(mediaStream);
        analyser = audioCtx.createAnalyser();
        analyser.fftSize = 256;
        source.connect(analyser);
        analyserData = new Uint8Array(analyser.frequencyBinCount);

        function tick() {
            if (!activeStream || !analyser) {
                return;
            }
            analyser.getByteFrequencyData(analyserData);
            const average = analyserData.reduce((sum, value) => sum + value, 0) / analyserData.length;
            const level = Math.min(100, Math.round((average / 60) * 100));
            const bar = interviewStep && !interviewStep.classList.contains('d-none') ? interviewSoundBar : checkSoundBar;
            if (bar) {
                bar.style.width = `${level}%`;
                bar.classList.toggle('active', level > 8);
            }
            requestAnimationFrame(tick);
        }

        tick();
    }

    async function start() {
        try {
            activeStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
            checkVideo.srcObject = activeStream;
            checkRecBadge?.classList.remove('d-none');
            setupSoundMeter(activeStream);
        } catch {
            showUnavailable();
        }
    }

    function moveStreamToInterviewPanel() {
        if (!activeStream || !interviewVideo) {
            return;
        }
        interviewVideo.srcObject = activeStream;
        interviewRecBadge?.classList.remove('d-none');
    }

    function stop() {
        activeStream?.getTracks().forEach((track) => track.stop());
        activeStream = null;
    }

    if (startBtn) {
        startBtn.addEventListener('click', () => {
            deviceCheckStep?.classList.add('d-none');
            interviewStep?.classList.remove('d-none');
            moveStreamToInterviewPanel();
        });
    }

    window.addEventListener('beforeunload', stop);
    start();
})();
