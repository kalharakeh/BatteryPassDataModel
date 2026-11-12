// "Please confirm" before a risky action (step 3e; on its own since redesign R6 so the battery and product data pages
// can use it without the admin pages' other behaviour): a button with data-confirm-action asks first, in the shared
// window (_ConfirmModal). Without JavaScript the button sends its form at once, as before.
(function () {
    const confirmModal = document.querySelector('.bp-confirm-modal[data-confirm-modal]');
    const confirmTitle = document.getElementById('bp-confirm-title');
    const confirmMessage = confirmModal?.querySelector('[data-confirm-message]');
    const confirmSubmit = confirmModal?.querySelector('[data-confirm-submit]');
    let pendingForm = null;
    let pendingButton = null;
    document.querySelectorAll('[data-confirm-action]').forEach((button) => {
        button.addEventListener('click', (event) => {
            const form = button.closest('form');
            if (!form || !confirmModal || !confirmTitle || !confirmMessage || !confirmSubmit) {
                return;
            }

            event.preventDefault();
            pendingForm = form;
            pendingButton = button;
            confirmTitle.textContent = button.dataset.confirmTitle || 'Confirm action';
            confirmMessage.textContent = button.dataset.confirmMessage || 'Continue with this action?';
            confirmSubmit.textContent = button.dataset.confirmSubmit || 'Confirm';
            confirmModal.hidden = false;
            confirmSubmit.focus();
        });
    });

    confirmModal?.querySelectorAll('[data-confirm-cancel]').forEach((button) => {
        button.addEventListener('click', () => {
            if (confirmSubmit?.disabled) {
                return; // already sent: the page reloads when it's done
            }

            confirmModal.hidden = true;
            pendingForm = null;
        });
    });

    confirmSubmit?.addEventListener('click', () => {
        if (!pendingForm) {
            return;
        }

        // A long action (the demo data) says it's working and can't be sent twice (redesign R1, owner's point 1).
        const busy = pendingButton?.dataset.confirmBusy;
        if (busy) {
            confirmMessage.textContent = busy;
            confirmSubmit.textContent = 'Working…';
            confirmSubmit.disabled = true;
            confirmModal.querySelectorAll('button[data-confirm-cancel]').forEach((button) => { button.disabled = true; });
            confirmModal.querySelector('[role="dialog"]')?.setAttribute('aria-busy', 'true');
        }

        pendingForm.submit();
    });
})();
