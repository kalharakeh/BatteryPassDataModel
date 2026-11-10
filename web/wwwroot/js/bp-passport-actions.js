// What people can do to a passport (fix wave 1): the withdraw button's reason check, and the second questions inside the
// windows. Each part is only an addition: without this script the forms still send, and the server asks for the rest.
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
})();
