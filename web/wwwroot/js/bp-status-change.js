// The "Change status" window (step 4b): what happened first, then the details; and the capacity fade suggested from a
// measured remaining capacity (1 - remaining / rated, J-11). Without this script both steps show and the form still works.
(() => {
    document.querySelectorAll('[data-status-dialog]').forEach((dialog) => {
        dialog.classList.add('is-stepped');
        const chosen = () => dialog.querySelector('input[name="what"]:checked');
        const show = (step) => {
            dialog.classList.toggle('is-step-2', step === 2);
            const choice = chosen();
            dialog.querySelector('[data-status-chosen]').textContent = choice?.dataset.statusChoice ?? '';
            const repair = dialog.querySelector('[data-status-repair]');
            const repaired = choice?.value === 'Repaired';
            repair.hidden = !repaired;
            repair.querySelectorAll('input').forEach((input) => { input.required = repaired; });
        };
        dialog.querySelector('[data-status-next]')?.addEventListener('click', () => show(2));
        dialog.querySelector('[data-status-back]')?.addEventListener('click', () => show(1));
        dialog.addEventListener('close', () => show(1));
        show(1);
    });

    document.querySelectorAll('[data-fade-suggest]').forEach((suggest) => {
        const rated = Number(suggest.dataset.rated);
        const remaining = suggest.querySelector('[data-fade-remaining]');
        const note = suggest.querySelector('[data-fade-note]');
        const fade = suggest.closest('form')?.querySelector('input[name="v.capacityFade"]');
        suggest.querySelector('[data-fade-button]')?.addEventListener('click', () => {
            const measured = Number(String(remaining.value).replace(',', '.'));
            if (!fade || !(rated > 0) || !(measured >= 0) || remaining.value.trim() === '') {
                note.textContent = 'Type the measured remaining capacity in Ah first.';
                return;
            }

            const percent = Math.max(0, Math.round((1 - measured / rated) * 100)); // the passport keeps whole percent
            fade.value = String(percent);
            fade.dispatchEvent(new Event('input', { bubbles: true }));
            note.textContent = `1 − ${measured} / ${rated} Ah rated = ${percent} %. The measured capacity isn't stored.`;
        });
    });
})();
