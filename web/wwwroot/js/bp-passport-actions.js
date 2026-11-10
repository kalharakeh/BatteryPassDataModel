// What people can do to a passport (fix wave 1). Each part only adds to the page: without this script the forms still
// send, and the server asks for what is missing.
(() => {
    // A button that opens the shared "Please confirm" window (bp-confirm.js) must not do so while its reason is empty.
    document.addEventListener('click', (event) => {
        const button = event.target instanceof Element ? event.target.closest('[data-needs-reason]') : null;
        const form = button?.closest('form');
        if (button && form && !form.reportValidity()) {
            event.preventDefault();
            event.stopPropagation();
        }
    }, true);

    // A button with data-passport-dialog opens that native <dialog> (the passport page has no other script for it).
    document.querySelectorAll('[data-passport-dialog]').forEach((button) => {
        button.addEventListener('click', () => {
            const dialog = document.getElementById(button.dataset.passportDialog);
            if (dialog && typeof dialog.showModal === 'function' && !dialog.open) {
                dialog.showModal();
            }
        });
    });

    // The "Change status" window: the team's reason for undoing a waste, and a second question before a cluster admin's
    // "Waste" (R10). The first is only shown for "Undo the waste"; the second names the battery and says who can undo it.
    document.querySelectorAll('[data-status-dialog]').forEach((dialog) => {
        const form = dialog.querySelector('form');
        if (!form) {
            return;
        }

        const chosen = () => form.querySelector('input[name="what"]:checked')?.value;
        const undo = dialog.querySelector('[data-undo-reason]');
        const syncUndo = () => {
            if (!undo) {
                return;
            }

            const on = chosen() === 'WasteUndone';
            undo.hidden = !on;
            undo.querySelectorAll('input').forEach((input) => { input.required = on; });
        };
        form.querySelectorAll('input[name="what"]').forEach((radio) => radio.addEventListener('change', syncUndo));
        syncUndo();

        const waste = dialog.querySelector('[data-waste-step]');
        if (!waste || !('wasteConfirm' in dialog.dataset)) {
            return;
        }

        let sure = false;
        const ask = (asking) => {
            dialog.classList.toggle('bp-waste-asking', asking);
            waste.hidden = !asking;
        };
        form.addEventListener('submit', (event) => {
            if (event.submitter?.formMethod === 'dialog' || sure || chosen() !== 'Waste') {
                return;
            }

            event.preventDefault();
            ask(true);
        });
        waste.querySelector('[data-waste-yes]')?.addEventListener('click', () => {
            sure = true;
            form.requestSubmit();
        });
        waste.querySelector('[data-waste-back]')?.addEventListener('click', () => ask(false));
        dialog.addEventListener('close', () => { sure = false; ask(false); syncUndo(); });
    });

    // A window with a second question (data-two-step): the reason first, then a confirm step that says what the answer
    // does. Without this script both parts show and the form sends as before.
    document.querySelectorAll('dialog[data-two-step]').forEach((dialog) => {
        const first = dialog.querySelector('[data-step="1"]');
        const second = dialog.querySelector('[data-step="2"]');
        const form = dialog.querySelector('form');
        const next = dialog.querySelector('[data-step-next]');
        const submit = dialog.querySelector('[data-step-submit]');
        if (!first || !second || !form || !next || !submit) {
            return;
        }

        next.hidden = false;
        submit.hidden = true;
        const show = (step) => {
            first.hidden = step !== 1;
            second.hidden = step !== 2;
        };
        next.addEventListener('click', () => {
            if (!form.reportValidity()) {
                return;
            }

            const choice = form.querySelector('input[name="what"]:checked')?.value ?? 'invalid';
            second.querySelectorAll('[data-step-for]').forEach((text) => { text.hidden = text.dataset.stepFor !== choice; });
            const reason = second.querySelector('[data-step-reason]');
            if (reason) {
                reason.textContent = form.querySelector('input[name="reason"]')?.value ?? '';
            }

            show(2);
        });
        dialog.querySelector('[data-step-back]')?.addEventListener('click', () => show(1));
        dialog.addEventListener('close', () => show(1));
        show(1);
    });
})();
