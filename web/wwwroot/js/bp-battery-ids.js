// The "Battery IDs" page (step 6d): more ranges in one release, and the void windows. Without JavaScript one range and
// the list still release, and the IDs can be voided from their own rows' forms in a later step.
(function () {
    const ranges = document.querySelector('[data-ranges]');
    const add = ranges?.querySelector('[data-add-range]');
    const sync = () => {
        const rows = ranges.querySelectorAll('[data-range-row]');
        rows.forEach((row) => { row.querySelector('[data-remove-range]').hidden = rows.length === 1; });
    };
    if (ranges && add) {
        add.addEventListener('click', () => {
            const rows = ranges.querySelectorAll('[data-range-row]');
            const copy = rows[rows.length - 1].cloneNode(true);
            copy.querySelectorAll('input').forEach((input) => { input.value = ''; });
            add.before(copy);
            copy.querySelector('input')?.focus();
            sync();
        });
        ranges.addEventListener('click', (event) => {
            const remove = event.target.closest('[data-remove-range]');
            if (remove && ranges.querySelectorAll('[data-range-row]').length > 1) {
                remove.closest('[data-range-row]').remove();
                sync();
            }
        });
        sync();
    }

    const voidDialog = document.getElementById('void-dialog');
    document.querySelectorAll('[data-void-id]').forEach((button) => {
        button.addEventListener('click', () => {
            voidDialog.querySelector('[data-void-serial-field]').value = button.dataset.serial;
            voidDialog.querySelector('[data-void-serial-name]').textContent = button.dataset.serial;
            voidDialog.showModal();
        });
    });

    const unusedDialog = document.getElementById('void-unused-dialog');
    document.querySelectorAll('[data-void-unused]').forEach((button) => {
        button.addEventListener('click', () => {
            unusedDialog.querySelector('[data-void-release-field]').value = button.dataset.releaseId;
            unusedDialog.querySelector('[data-void-release-name]').textContent = button.dataset.releaseReference;
            unusedDialog.querySelector('[data-void-release-count]').textContent = button.dataset.releaseAvailable;
            unusedDialog.showModal();
        });
    });
})();

// The create page's picker (6d): once a batch is chosen, only the IDs its model may use are offered, its model's first.
(function () {
    const batch = document.querySelector('[data-batch-select]');
    const picker = document.querySelector('[data-id-picker]');
    if (!batch || !picker) {
        return;
    }

    const note = document.querySelector('[data-id-picker-note]');
    const groups = Array.from(picker.querySelectorAll('optgroup'));
    const words = (count) => `${count} ${count === 1 ? 'ID' : 'IDs'}`;
    const filter = () => {
        const model = batch.value ? (batch.selectedOptions[0]?.dataset.model ?? null) : null;
        let offered = 0;
        groups.forEach((group) => {
            const usable = model === null || group.dataset.model === '' || group.dataset.model === model;
            group.hidden = !usable;
            group.disabled = !usable;
            offered += usable ? group.children.length : 0;
        });
        if (picker.selectedOptions[0]?.parentElement?.disabled) {
            picker.value = '';
        }

        if (note) {
            note.textContent = model === null
                ? `${words(offered)} available. Choose the batch first to see only those its model may use.`
                : offered === 0
                    ? 'No released ID may be used for this batch’s model. Release some on the Battery IDs page.'
                    : `${words(offered)} available for this batch’s model, its own first.`;
        }
    };
    batch.addEventListener('change', filter);
    filter();
})();
