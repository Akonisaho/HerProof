// Live camera self-view + sound-level meter during a voice interview — a visible, honest "yes this
// is on" signal (like a video call's self-view), not a recording pipeline: nothing here is uploaded,
// stored, or sent anywhere. Degrades silently (CLAUDE.md "transparent, never punitive") if the
// candidate denies permission or has no camera - the interview continues exactly the same either way.
(function () {
    const panel = document.getElementById('camera-panel');
    const video = document.getElementById('candidate-camera');
    const recBadge = document.getElementById('camera-rec-badge');
    const soundBar = document.querySelector('.camera-sound-bar');
    const unavailable = document.getElementById('camera-unavailable');
    if (!panel || !video) {
        return;
    }

    let activeStream = null;

    function showUnavailable() {
        panel.querySelector('.camera-box')?.classList.add('d-none');
        unavailable?.classList.remove('d-none');
    }

    function setupSoundMeter(mediaStream) {
        if (!soundBar || mediaStream.getAudioTracks().length === 0) {
            return;
        }

        const AudioContextCtor = window.AudioContext || window.webkitAudioContext;
        if (!AudioContextCtor) {
            return;
        }

        const audioCtx = new AudioContextCtor();
        const source = audioCtx.createMediaStreamSource(mediaStream);
        const analyser = audioCtx.createAnalyser();
        analyser.fftSize = 256;
        source.connect(analyser);
        const data = new Uint8Array(analyser.frequencyBinCount);

        function tick() {
            if (!activeStream) {
                return;
            }
            analyser.getByteFrequencyData(data);
            const average = data.reduce((sum, value) => sum + value, 0) / data.length;
            const level = Math.min(100, Math.round((average / 60) * 100));
            soundBar.style.width = `${level}%`;
            soundBar.classList.toggle('active', level > 8);
            requestAnimationFrame(tick);
        }

        tick();
    }

    async function start() {
        try {
            activeStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
            video.srcObject = activeStream;
            recBadge?.classList.remove('d-none');
            setupSoundMeter(activeStream);
        } catch {
            showUnavailable();
        }
    }

    function stop() {
        activeStream?.getTracks().forEach((track) => track.stop());
        activeStream = null;
    }

    window.addEventListener('beforeunload', stop);
    start();
})();
