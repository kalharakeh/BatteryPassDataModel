// The top bar's account menu (finding L04): a disclosure. Enter or Space on the button opens and closes it (the
// browser's own <details>); Escape closes it and gives the focus back to the button; a click outside it or the focus
// leaving it closes it too. Without this script the menu still opens and closes.
(function () {
    'use strict';

    function setUp(menu) {
        var button = menu.querySelector('summary');
        if (!button) {
            return;
        }

        function mark() {
            button.setAttribute('aria-expanded', menu.open ? 'true' : 'false');
        }

        function close(giveFocusBack) {
            if (!menu.open) {
                return;
            }

            menu.open = false;
            mark();
            if (giveFocusBack) {
                button.focus();
            }
        }

        mark();
        menu.addEventListener('toggle', mark);
        menu.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && menu.open) {
                event.preventDefault();
                close(true);
            }
        });
        menu.addEventListener('focusout', function (event) {
            if (event.relatedTarget && !menu.contains(event.relatedTarget)) {
                close(false);
            }
        });
        document.addEventListener('click', function (event) {
            if (!menu.contains(event.target)) {
                close(false);
            }
        });
    }

    document.querySelectorAll('[data-account-menu]').forEach(setUp);
})();
