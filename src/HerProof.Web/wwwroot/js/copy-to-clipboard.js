// Generic "copy this input's value" button, wired up by data-copy-target="<input id>".
(function () {
    document.querySelectorAll('[data-copy-target]').forEach((button) => {
        const input = document.getElementById(button.dataset.copyTarget);
        if (!input) {
            return;
        }

        button.addEventListener('click', async () => {
            const originalText = button.textContent;
            try {
                await navigator.clipboard.writeText(input.value);
            } catch {
                input.select();
                document.execCommand('copy');
            }

            button.textContent = 'Copied!';
            setTimeout(() => {
                button.textContent = originalText;
            }, 1500);
        });
    });
})();
