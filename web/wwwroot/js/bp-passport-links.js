// The detail page's links into its tabs (redesign R5, the owner's items 4 and 8): a link to #value-<n> opens the tab the
// entry sits in and any folded part around it, scrolls to it and marks it; a link to #tab-<name> (e.g. #tab-live) opens
// that tab. Without JavaScript the link still works when its tab is the open one.
(function () {
    function land(hash) {
        if (!/^#(value-\d+|tab-[\w-]+)$/.test(hash)) { return; }
        const entry = document.querySelector(hash);
        if (!entry) { return; }
        const pane = entry.closest('.tab-pane');
        const whole = entry === pane;
        if (pane && !pane.classList.contains('active')) {
            const button = document.querySelector(`[data-bs-target="#${pane.id}"]`);
            if (button && window.bootstrap) {
                window.bootstrap.Tab.getOrCreateInstance(button).show();
            } else if (button) {
                button.click();
            }
        }
        for (let folded = entry.closest('details:not([open])'); folded; folded = folded.parentElement?.closest('details:not([open])')) {
            folded.open = true;
        }
        if (whole) {
            window.setTimeout(() => document.querySelector('#passport-tabs').scrollIntoView({ behavior: 'smooth', block: 'start' }), 160);
            return;
        }
        document.querySelectorAll('.bp-field-grid > .is-landed').forEach((other) => other.classList.remove('is-landed'));
        entry.classList.add('is-landed');
        // After the tab's fade, so the entry has its place.
        window.setTimeout(() => entry.scrollIntoView({ behavior: 'smooth', block: 'center' }), 160);
    }

    document.addEventListener('click', (event) => {
        const link = event.target.closest('a[data-key-figure-link], a[href^="#tab-"]');
        if (!link) { return; }
        event.preventDefault();
        history.replaceState(null, '', link.hash);
        land(link.hash);
    });
    window.addEventListener('hashchange', () => land(location.hash));
    land(location.hash);
})();
