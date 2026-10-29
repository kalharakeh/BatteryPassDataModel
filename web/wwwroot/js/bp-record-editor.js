// The team's product data pages: the editors' side menu, "Show only the values still needed", removing a row,
// "Discard unsaved edits?", the warning before leaving with unsaved edits, and the republish question.
// Without JavaScript every section stays visible and the forms still work.
(function () {
    // A chosen file shows its name; its Upload button waits for one.
    document.querySelectorAll('[data-upload-file]').forEach((file) => {
        const row = file.closest('[data-upload-row], .bp-attr-row');
        const chosen = row?.querySelector('[data-upload-chosen]');
        const name = row?.querySelector('[data-upload-name]');
        const button = row?.querySelector('[data-upload-button]');
        const update = () => {
            const picked = file.files && file.files.length > 0 ? file.files[0].name : null;
            if (button) {
                button.disabled = !picked;
            }

            if (chosen) {
                chosen.textContent = picked ? 'Ready to upload: ' + picked : 'No file chosen';
            } else if (name && picked) {
                name.value = 'Ready to upload: ' + picked;
            }
        };
        file.addEventListener('change', update);
        update();
    });

    // On a narrow screen the selected model's card can sit past the edge of the scrolling model row: bring it into view
    // within the row only, so the page itself doesn't jump.
    const selectedChip = document.querySelector('.bp-product-version-chip-rail .bp-product-version-chip.is-active');
    const rail = selectedChip?.closest('.bp-product-version-chip-rail');
    if (selectedChip && rail && rail.scrollWidth > rail.clientWidth) {
        rail.scrollLeft += selectedChip.getBoundingClientRect().left - rail.getBoundingClientRect().left - 16;
    }

    // A button with data-open-dialog opens that native <dialog>: focus, Escape and the dimmed page come with it, and
    // its Cancel button (formmethod="dialog") closes it without sending anything.
    document.querySelectorAll('[data-open-dialog]').forEach((button) => {
        const dialog = document.getElementById(button.dataset.openDialog);
        button.addEventListener('click', () => dialog?.showModal());
    });

    const form = document.querySelector('[data-record-editor]');
    if (!form) {
        return;
    }

    const navButtons = Array.from(form.querySelectorAll('.bp-edit-nav-button[data-section-target]'));
    const sections = Array.from(form.querySelectorAll('.bp-edit-section[id]'));

    function showSection(id) {
        sections.forEach((section) => { section.hidden = section.id !== id; });
        navButtons.forEach((button) => {
            const active = button.dataset.sectionTarget === id;
            button.classList.toggle('is-active', active);
            button.setAttribute('aria-pressed', active ? 'true' : 'false');
        });
    }

    // Opening a section from further down the page starts at its heading, not in its middle.
    navButtons.forEach((button) => button.addEventListener('click', () => {
        showSection(button.dataset.sectionTarget);
        const section = document.getElementById(button.dataset.sectionTarget);
        if (section && section.getBoundingClientRect().top < 0) {
            section.scrollIntoView({ block: 'start' });
        }
    }));

    // A list's empty row waits behind "+ Add a row", unless something was typed into it.
    form.querySelectorAll('[data-add-row]').forEach((add) => {
        const newRow = add.parentElement.querySelector('.bp-attr-repeat.is-new-row');
        const typed = newRow && Array.from(newRow.querySelectorAll('input, select, textarea')).some((input) => input.value.trim() !== '');
        if (!newRow || typed) {
            return;
        }

        newRow.classList.add('is-collapsed');
        add.hidden = false;
        add.addEventListener('click', () => {
            newRow.classList.remove('is-collapsed');
            add.hidden = true;
            newRow.querySelector('input, select, textarea')?.focus();
        });
    });

    // With a side menu, one section shows at a time: sections with a refused value are marked and the first of them
    // opens. A small record has no menu and shows all its sections.
    const refused = sections.filter((section) => section.querySelector('.is-invalid'));
    refused.forEach((section) => navButtons.find((button) => button.dataset.sectionTarget === section.id)?.classList.add('has-missing-fields'));
    if (navButtons.length > 0 && sections.length > 0) {
        showSection((refused[0] || sections[0]).id);
    }

    // After a save with refused values, bring the first one into view, ready to correct.
    const firstRefused = form.querySelector('.is-invalid');
    if (firstRefused) {
        firstRefused.scrollIntoView({ block: 'center' });
        (firstRefused.querySelector('.is-invalid-part input, .is-invalid-part select, .is-invalid-part textarea')
            || firstRefused.querySelector('input, select, textarea'))?.focus({ preventScroll: true });
    }

    // "Show only missing values": hides filled rows, sections with nothing missing and their side-menu entries, and
    // opens the first section that still needs something. It stays on after a save, for this page.
    const missingOnly = document.querySelector('[data-missing-only]');
    const missingOnlyKey = 'bp-missing-only:' + location.pathname + location.search;
    function applyMissingOnly(keepSection) {
        form.classList.toggle('bp-only-missing', missingOnly.checked);
        navButtons.forEach((button) => {
            button.hidden = missingOnly.checked && !button.classList.contains('has-missing-values') && !button.classList.contains('has-missing-fields');
        });
        const open = navButtons.find((button) => button.classList.contains('is-active'));
        if (missingOnly.checked && navButtons.length > 0 && !(keepSection && open && !open.hidden)) {
            const first = navButtons.find((button) => !button.hidden);
            if (first) {
                showSection(first.dataset.sectionTarget);
            }
        }
    }

    if (missingOnly) {
        try {
            missingOnly.checked = sessionStorage.getItem(missingOnlyKey) === 'on';
        } catch {
            // no storage: the switch starts off
        }

        if (missingOnly.checked) {
            applyMissingOnly(true);
        }

        missingOnly.addEventListener('change', () => {
            try {
                sessionStorage.setItem(missingOnlyKey, missingOnly.checked ? 'on' : 'off');
            } catch {
                // no storage: the switch is forgotten on the next page
            }

            applyMissingOnly(false);
        });
    }

    // "Remove row": empties the row so the save leaves it out, and hides it.
    form.querySelectorAll('[data-remove-row]').forEach((button) => {
        button.addEventListener('click', () => {
            const row = button.closest('.bp-attr-repeat');
            row.querySelectorAll('input, select, textarea').forEach((input) => { input.value = ''; });
            row.classList.add('is-removed');
        });
    });

    function formState() {
        const entries = [];
        for (const [key, value] of new FormData(form).entries()) {
            if (key !== '__RequestVerificationToken' && key !== 'confirmed') {
                entries.push(key + '=' + String(value));
            }
        }

        return entries.sort().join('&');
    }

    // A page shown again with refused values or the republish question holds typed values that aren't saved yet. Its
    // address is the save address, which can't be opened again: show the page's own address instead.
    const unsavedOnArrival = form.querySelector('.is-invalid, [data-confirm-modal]') !== null;
    const pristine = unsavedOnArrival ? '' : formState();
    let leaving = false;
    if (unsavedOnArrival && form.dataset.cancelHref) {
        history.replaceState(null, '', form.dataset.cancelHref + '#editor');
    }

    // The save bar says whether there are unsaved changes and where; the side menu marks those sections.
    const saveBar = form.querySelector('.bp-product-editor-savebar');
    const saveStatus = form.querySelector('[data-save-status]');
    const cancelEdits = form.querySelector('[data-cancel-edits]');
    const fieldsOf = (section) => Array.from(section.querySelectorAll('input, select, textarea')).filter((input) => input.name && input.type !== 'file');
    const initial = new Map(sections.flatMap((section) => fieldsOf(section).map((input) => [input, input.value])));
    function showUnsaved() {
        const dirty = unsavedOnArrival || formState() !== pristine;
        const edited = sections.filter((section) => fieldsOf(section).some((input) => initial.get(input) !== input.value));
        navButtons.forEach((button) => button.classList.toggle('has-edits', edited.some((section) => section.id === button.dataset.sectionTarget)));
        saveBar?.classList.toggle('has-unsaved', dirty);
        if (cancelEdits) {
            cancelEdits.disabled = !dirty;
        }

        if (saveStatus && !(saveStatus.classList.contains('is-done') && !dirty)) {
            const where = edited.map((section) => section.dataset.sectionTitle).filter(Boolean);
            saveStatus.classList.remove('is-done');
            saveStatus.textContent = !dirty
                ? 'No unsaved changes.'
                : where.length > 0 && navButtons.length > 0
                    ? 'Unsaved changes in ' + where.join(', ') + '. Save to keep them.'
                    : 'Unsaved changes. Save to keep them.';
        }
    }

    // After a save the page reloads: come back to the section and the place on the page where Save was pressed, and
    // say how it went in the save bar, which is where the eye is.
    const returnKey = 'bp-editor-return';
    form.addEventListener('submit', () => {
        try {
            const open = sections.find((section) => !section.hidden);
            sessionStorage.setItem(returnKey, JSON.stringify({ page: location.pathname + location.search, section: open?.id, y: window.scrollY, at: Date.now() }));
        } catch {
            // no storage: the page opens at the editor as before
        }
    });
    let back = null;
    try {
        back = JSON.parse(sessionStorage.getItem(returnKey) || 'null');
        sessionStorage.removeItem(returnKey);
    } catch {
        back = null;
    }

    if (back && !unsavedOnArrival && back.page === location.pathname + location.search && Date.now() - back.at < 15000) {
        if (back.section && navButtons.length > 0 && document.getElementById(back.section)) {
            showSection(back.section);
        }

        window.addEventListener('load', () => requestAnimationFrame(() => window.scrollTo({ top: back.y, behavior: 'instant' })));
        const done = document.querySelector('#editor .bp-editor-status.is-done span');
        if (done && saveStatus) {
            saveStatus.textContent = done.textContent;
            saveStatus.classList.add('is-done');
        }
    }

    form.addEventListener('input', showUnsaved);
    form.addEventListener('change', showUnsaved);
    form.addEventListener('click', (event) => {
        if (event.target.closest('[data-remove-row]')) {
            showUnsaved();
        }
    });
    showUnsaved();

    // Saving and "Discard changes" leave on purpose; anything else with unsaved edits asks first.
    form.addEventListener('submit', () => { leaving = true; });
    window.addEventListener('beforeunload', (event) => {
        if (!leaving && formState() !== pristine) {
            event.preventDefault();
            event.returnValue = '';
        }
    });

    // A dialog takes the keyboard focus while it is open, keeps Tab inside it, and gives the focus back when it closes.
    let returnFocus = null;
    function open(modal) {
        returnFocus = document.activeElement;
        modal.hidden = false;
        document.body.classList.add('bp-modal-open');
        modal.querySelector('.bp-modal-panel button')?.focus();
    }

    function close(modal) {
        modal.hidden = true;
        document.body.classList.remove('bp-modal-open');
        returnFocus?.focus?.();
        returnFocus = null;
    }

    document.addEventListener('keydown', (event) => {
        const modal = document.querySelector('.bp-modal:not([hidden])');
        if (event.key !== 'Tab' || !modal) {
            return;
        }

        const buttons = Array.from(modal.querySelectorAll('.bp-modal-panel button'));
        const first = buttons[0];
        const last = buttons[buttons.length - 1];
        if (!modal.contains(document.activeElement)) {
            event.preventDefault();
            first?.focus();
        } else if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first?.focus();
        }
    });

    // Uploading reloads the page: with unsaved edits, it asks first instead of losing them.
    const uploadModal = document.querySelector('[data-upload-guard-modal]');
    let uploadAnyway = null;
    document.querySelectorAll('[data-upload-form]').forEach((upload) => upload.addEventListener('submit', (event) => {
        if (uploadAnyway === upload || formState() === pristine || !uploadModal) {
            uploadAnyway = null; // a one-time pass: the next upload asks again
            leaving = true;
            return;
        }

        event.preventDefault();
        uploadModal.dataset.uploadForm = upload.id;
        open(uploadModal);
    }));
    uploadModal?.querySelector('[data-upload-stay]')?.addEventListener('click', () => close(uploadModal));
    uploadModal?.querySelector('[data-upload-dismiss]')?.addEventListener('click', () => close(uploadModal));
    uploadModal?.querySelector('[data-upload-discard]')?.addEventListener('click', () => {
        const upload = document.getElementById(uploadModal.dataset.uploadForm);
        close(uploadModal);
        // Only a form that can be sent passes the guard (e.g. "Publish new versions" must be ticked first).
        if (upload && upload.reportValidity()) {
            uploadAnyway = upload;
            upload.requestSubmit();
        }
    });

    const cancelButton = form.querySelector('[data-cancel-edits]');
    const cancelModal = document.querySelector('[data-cancel-modal]');
    const leave = () => {
        leaving = true;
        window.location.href = cancelButton.dataset.cancelHref;
    };
    cancelButton?.addEventListener('click', () => (formState() === pristine || !cancelModal ? leave() : open(cancelModal)));
    cancelModal?.querySelector('[data-cancel-stay]')?.addEventListener('click', () => close(cancelModal));
    cancelModal?.querySelector('[data-cancel-dismiss]')?.addEventListener('click', () => close(cancelModal));
    cancelModal?.querySelector('[data-cancel-discard]')?.addEventListener('click', leave);

    const confirmModal = form.querySelector('[data-confirm-modal]');
    if (confirmModal) {
        open(confirmModal);
        confirmModal.querySelector('[data-confirm-stay]')?.addEventListener('click', () => close(confirmModal));
    }

    document.addEventListener('keydown', (event) => {
        if (event.key !== 'Escape') {
            return;
        }

        [cancelModal, confirmModal, uploadModal].filter((modal) => modal && !modal.hidden).forEach(close);
    });
})();
