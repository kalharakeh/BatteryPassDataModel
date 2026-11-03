// The admin and cluster admin pages (step 3e): the windows for adding things, "Please confirm" before a risky action,
// showing a typed password, searching a list, and opening a row named in the address. Without JavaScript the rows
// still open and close, and every form in them still works.
(function () {
    // A button with data-open-dialog opens that native <dialog>: focus, Escape and the dimmed page come with it. A
    // dialog marked data-open-on-load="true" opens at once, e.g. after a refused "Add cluster", with the typed values
    // kept. (Razor writes data- attributes even when false, so the value says it.)
    document.querySelectorAll('[data-open-dialog]').forEach((button) => {
        const dialog = document.getElementById(button.dataset.openDialog);
        button.addEventListener('click', () => dialog?.showModal());
    });
    document.querySelectorAll('dialog[data-open-on-load="true"]').forEach((dialog) => dialog.showModal());

    // The eye button beside a password shows or hides what was typed.
    document.querySelectorAll('[data-password-reveal]').forEach((button) => {
        button.addEventListener('click', () => {
            const input = button.closest('.bp-password-field')?.querySelector('input');
            if (!input) {
                return;
            }

            const reveal = input.type === 'password';
            input.type = reveal ? 'text' : 'password';
            button.setAttribute('aria-label', reveal ? 'Hide password' : 'Show password');
            button.setAttribute('title', reveal ? 'Hide password' : 'Show password');
        });
    });

    // A list's search box hides the rows that don't match; "No ... match" shows when none are left.
    document.querySelectorAll('[data-list-search]').forEach((input) => {
        const list = input.closest('[data-list]');
        const rows = Array.from(list?.querySelectorAll('[data-search-text]') ?? []);
        const none = list?.querySelector('[data-list-no-results]');
        input.addEventListener('input', () => {
            const query = input.value.trim().toLowerCase();
            let shown = 0;
            rows.forEach((row) => {
                const match = query.length === 0 || (row.dataset.searchText || '').toLowerCase().includes(query);
                row.hidden = !match;
                shown += match ? 1 : 0;
            });
            if (none) {
                none.hidden = shown > 0;
            }
        });
    });

    // After a save the page comes back with ?openUser=...: that user's row opens again.
    const openUser = new URLSearchParams(window.location.search).get('openUser');
    if (openUser) {
        const row = Array.from(document.querySelectorAll('details[data-open-user]'))
            .find((candidate) => (candidate.dataset.openUser || '').toLowerCase() === openUser.toLowerCase());
        if (row) {
            row.open = true;
            row.scrollIntoView({ block: 'center' });
        }
    }

    // "Please confirm" before a risky action is in bp-confirm.js (redesign R6).
})();
