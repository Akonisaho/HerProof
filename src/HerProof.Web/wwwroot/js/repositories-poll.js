// Replaces "refresh the page manually" with a real spinner that polls until analysis finishes,
// then reloads once so the full result (findings, summary, Nolwazi button) renders normally.
(function () {
    const pendingCells = Array.from(document.querySelectorAll('.repo-status-cell')).filter((cell) =>
        cell.querySelector('.repo-status-spinner'));

    if (pendingCells.length === 0) {
        return;
    }

    const pollIntervalMs = 3000;

    async function checkOne(cell) {
        const repositoryId = cell.dataset.repositoryId;
        try {
            const response = await fetch(`/repositories/${repositoryId}/status`);
            if (!response.ok) {
                return;
            }
            const data = await response.json();
            if (data.status !== 'Pending' && data.status !== 'InProgress') {
                window.location.reload();
            }
        } catch {
            // Transient network hiccup - just try again on the next tick.
        }
    }

    const timer = setInterval(() => {
        pendingCells.forEach(checkOne);
    }, pollIntervalMs);

    window.addEventListener('beforeunload', () => clearInterval(timer));
})();
