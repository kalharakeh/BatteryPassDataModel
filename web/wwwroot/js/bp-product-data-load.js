// "Load product data from Excel": the check runs without leaving the page, so the chosen workbook and files stay
// chosen for "Load". Without this script both buttons post the form the usual way and the page says to choose the
// files again.
(function () {
    'use strict';

    const form = document.querySelector('[data-load-form]');
    const result = document.querySelector('[data-load-result]');
    if (!form || !result) {
        return;
    }

    const checkButton = form.querySelector('[data-load-check-button]');
    const loadButton = form.querySelector('[data-load-button]');
    const stamp = form.querySelector('[data-load-stamp]');
    const busy = form.querySelector('[data-load-busy]');
    const workbook = form.querySelector('input[name="workbook"]');

    function say(text) {
        busy.textContent = text;
        busy.hidden = text.length === 0;
    }

    function problem(text) {
        const card = document.createElement('section');
        card.className = 'bp-card bp-console-form-card mt-4';
        const line = document.createElement('p');
        line.className = 'alert alert-danger bp-status-line is-problem mb-0';
        line.setAttribute('role', 'alert');
        line.setAttribute('data-load-error', '');
        line.textContent = text;
        card.appendChild(line);
        result.replaceChildren(card);
    }

    // "Load" is for the check the page shows: a check without problems, of the files that are chosen now.
    function ready() {
        const check = result.querySelector('[data-load-check]');
        const can = !!check && check.dataset.canLoad === 'true' && workbook.files.length > 0;
        stamp.value = can ? check.dataset.stamp : '';
        loadButton.disabled = !can;
    }

    // The page's own figures, said before anything is sent (the app says the same if it is sent all the same).
    function tooMuch() {
        const files = form.querySelector('input[name="files"]');
        const chosen = [...files.files];
        const maxFiles = Number(files.dataset.maxFiles);
        const maxBytes = Number(files.dataset.maxBytes);
        const maxLoad = Number(files.dataset.maxLoadBytes);
        const megabytes = (bytes) => Math.round(bytes / 1048576);
        const big = chosen.find((file) => maxBytes > 0 && file.size > maxBytes);
        const total = chosen.reduce((sum, file) => sum + file.size, workbook.files[0]?.size ?? 0);
        const text = chosen.length > maxFiles ? `${chosen.length} files are chosen; one load takes up to ${maxFiles}. Send fewer files this time: a document that isn't sent is listed as still to come, and a later load adds it.`
            : big ? `${big.name} is larger than ${megabytes(maxBytes)} MB, the most a single file may be.`
            : total > maxLoad ? `The workbook and its files are more than ${megabytes(maxLoad)} MB together, and one load takes up to ${megabytes(maxLoad)} MB. Send fewer files this time: a document that isn't sent is listed as still to come, and a later load adds it.`
            : null;
        if (text) {
            problem(text);
        }

        return text !== null;
    }

    function forget() {
        result.replaceChildren();
        say('');
        ready();
    }

    form.querySelectorAll('[data-load-file]').forEach((input) => input.addEventListener('change', forget));

    // A check that came with the page (a load that was refused) is of files the browser no longer holds.
    if (result.querySelector('[data-load-check]')) {
        const again = form.querySelector('[data-load-again]');
        if (again) {
            again.hidden = false;
        }
    }

    ready();

    checkButton.addEventListener('click', (event) => {
        event.preventDefault();
        if (workbook.files.length === 0) {
            problem('Choose the filled workbook first.');
            return;
        }

        if (tooMuch()) {
            return;
        }

        checkButton.disabled = true;
        loadButton.disabled = true;
        say('Checking the workbook and its files…');
        fetch(checkButton.formAction, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'fetch' },
            credentials: 'same-origin',
        })
            .then((response) => {
                if (response.status === 413) {
                    return response.json().then((body) => { throw new Error(body.error || 'The workbook and its files are too large together for one load.'); }, () => { throw new Error('The workbook and its files are too large together for one load.'); });
                }

                if (!response.ok || response.redirected) {
                    throw new Error('The check could not be run. Reload the page, sign in again if it asks, and try again.');
                }

                return response.text();
            })
            .then((html) => {
                const holder = document.createElement('div');
                holder.innerHTML = html;
                if (!holder.querySelector('[data-load-check]')) {
                    throw new Error('The check could not be run. Reload the page, sign in again if it asks, and try again.');
                }

                result.replaceChildren(...holder.childNodes);
                say('');
                result.scrollIntoView({ block: 'start', behavior: 'smooth' });
            })
            .catch((error) => {
                say('');
                problem(error instanceof TypeError ? 'The check could not be run: the app did not answer. Try again.' : error.message);
            })
            .finally(() => {
                checkButton.disabled = false;
                ready();
            });
    });

    form.addEventListener('submit', (event) => {
        if (event.submitter === checkButton) {
            return;
        }

        if (loadButton.disabled || tooMuch()) {
            event.preventDefault();
            return;
        }

        // Disabled after the browser has gathered the form, so that a second click can't send it twice.
        window.setTimeout(() => {
            checkButton.disabled = true;
            loadButton.disabled = true;
        }, 0);
        say('Loading: saving the records, their values and their documents. Keep this page open…');
    });
})();
