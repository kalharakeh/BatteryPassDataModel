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
            button.closest('details')?.removeAttribute('open');
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

// The create page's picker (6d): once a batch is chosen, only the IDs its model may use are offered, its model's first,
// and of those only the serial numbers that fit the model's serial number format (decision 81). Test IDs are free of it.
(function () {
    const batch = document.querySelector('[data-batch-select]');
    const picker = document.querySelector('[data-id-picker]');
    if (!batch || !picker) {
        return;
    }

    const note = document.querySelector('[data-id-picker-note]');
    const groups = Array.from(picker.querySelectorAll('optgroup'));
    const words = (count) => `${count} ${count === 1 ? 'ID' : 'IDs'}`;
    // As the app checks it: a digit for each #, every other character as it is, letters whatever their case.
    const fits = (format, serial) => {
        if (!format) {
            return true;
        }

        if (serial.length !== format.length) {
            return false;
        }

        for (let index = 0; index < format.length; index += 1) {
            const wanted = format[index];
            const given = serial[index];
            if (wanted === '#' ? !(given >= '0' && given <= '9') : wanted.toUpperCase() !== given.toUpperCase()) {
                return false;
            }
        }

        return true;
    };
    const filter = () => {
        const chosen = batch.value ? batch.selectedOptions[0] : null;
        const model = chosen ? (chosen.dataset.model ?? null) : null;
        const format = chosen ? (chosen.dataset.format ?? '') : '';
        let offered = 0;
        let left = 0;
        groups.forEach((group) => {
            const usable = model === null || group.dataset.model === '' || group.dataset.model === model;
            group.hidden = !usable;
            group.disabled = !usable;
            Array.from(group.children).forEach((option) => {
                const fitting = !usable || group.dataset.testIds !== undefined || fits(format, option.value);
                option.hidden = !fitting;
                option.disabled = !fitting;
                offered += usable && fitting ? 1 : 0;
                left += usable && !fitting ? 1 : 0;
            });
        });
        const picked = picker.selectedOptions[0];
        if (picked && (picked.disabled || picked.parentElement?.disabled)) {
            picker.value = '';
        }

        if (note) {
            const leftOut = left === 0 ? '' : ` ${words(left)} left out: ${left === 1 ? 'its serial number doesn’t' : 'their serial numbers don’t'} fit ${format}.`;
            note.textContent = model === null
                ? `${words(offered)} available. Choose the batch first to see only those its model may use.`
                : offered === 0
                    ? (left === 0
                        ? 'No released ID may be used for this batch’s model. Release some on the Battery IDs page.'
                        : `No released ID fits its model’s serial number format, ${format}. Release some on the Battery IDs page.`)
                    : `${words(offered)} available for this batch’s model, its own first.${leftOut}`;
        }
    };
    batch.addEventListener('change', filter);
    filter();
})();
