// Live camera self-view + sound-level meter for the browser-based practical assessment, same honest
// "yes this is on" signal as interview-camera.js: nothing here is recorded, uploaded, or stored, and
// it never blocks the assessment if denied or unavailable (CLAUDE.md "transparent, never punitive").
// Acquired once when the InProgress page loads and kept running for the whole session - there's only
// one page load for the whole assessment (unlike the interview's per-question reloads), so no gating
// step is needed here.
(function () {
    const video = document.getElementById('candidate-camera-assessment');
    if (!video) {
        return;
    }

    const recBadge = document.getElementById('camera-rec-badge-assessment');
    const soundBar = document.querySelector('.camera-sound-bar-assessment');
    const unavailable = document.getElementById('camera-unavailable-assessment');

    let activeStream = null;

    function showUnavailable() {
        video.closest('.camera-box')?.classList.add('d-none');
        unavailable?.classList.remove('d-none');
    }

    function setupSoundMeter(mediaStream) {
        if (mediaStream.getAudioTracks().length === 0 || !(window.AudioContext || window.webkitAudioContext)) {
            return;
        }

        const AudioContextCtor = window.AudioContext || window.webkitAudioContext;
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
            if (soundBar) {
                soundBar.style.width = `${level}%`;
                soundBar.classList.toggle('active', level > 8);
            }
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
