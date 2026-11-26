// The battery pages' side menu (redesign R4, option B): one section at a time. The server opens the right one, also
// after a save; this switches sections without a reload, keeps the address so a reload stays put, and follows a
// #link into a hidden section (e.g. "Fill in" or a form's return to #live). Without JavaScript the menu's links work.
(function () {
    const nav = document.querySelector('[data-battery-nav]');
    if (!nav) {
        return;
    }

    const sections = Array.from(document.querySelectorAll('[data-section]'));
    const links = Array.from(nav.querySelectorAll('[data-section-link]'));
    const picker = document.querySelector('[data-section-picker]');

    function show(id) {
        if (!sections.some((section) => section.dataset.section === id)) {
            return false;
        }

        sections.forEach((section) => { section.hidden = section.dataset.section !== id; });
        links.forEach((link) => {
            const open = link.dataset.sectionLink === id;
            link.classList.toggle('is-active', open);
            if (open) {
                link.setAttribute('aria-current', 'page');
            } else {
                link.removeAttribute('aria-current');
            }
        });
        if (picker) {
            picker.value = id;
        }

        return true;
    }

    // The phone's section picker switches like the menu (owner's improvement 7).
    picker?.addEventListener('change', () => {
        if (show(picker.value)) {
            const url = new URL(window.location.href);
            url.searchParams.set('section', picker.value);
            url.hash = '';
            history.replaceState(null, '', url);
        }
    });

    // On a phone the less used facts start folded behind "More details" (owner's improvement 9).
    if (window.matchMedia('(max-width: 767.98px)').matches) {
        document.querySelectorAll('details[data-fold-on-phone]').forEach((details) => { details.open = false; });
    }

    links.forEach((link) => {
        link.addEventListener('click', (event) => {
            if (event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) {
                return; // a new tab or window: let the browser open the link
            }

            event.preventDefault();
            if (show(link.dataset.sectionLink)) {
                history.replaceState(null, '', link.getAttribute('href'));
                const panel = document.querySelector('.bp-bpanel');
                if (panel && panel.getBoundingClientRect().top < 0) {
                    panel.scrollIntoView({ block: 'start' }); // on a phone the menu sits above the section
                }
            }
        });
    });

    function follow() {
        const id = decodeURIComponent(location.hash.slice(1));
        const target = id ? document.getElementById(id) : null;
        const section = target?.closest('[data-section]');
        if (section && section.hidden && show(section.dataset.section)) {
            target.scrollIntoView({ block: 'start' });
        }
    }

    window.addEventListener('hashchange', follow);
    follow();

    // "Measured at" (H1 round two): the typed time is the browser's, so the form sends its offset from UTC for that very
    // time (summer or winter); without this script the server reads the time as UTC, as the hint says.
    const offsetText = (minutes) => 'UTC' + (minutes < 0 ? '-' : '+')
        + String(Math.floor(Math.abs(minutes) / 60)).padStart(2, '0') + ':' + String(Math.abs(minutes) % 60).padStart(2, '0');
    document.querySelectorAll('input[name="measuredAtOffset"]').forEach((hidden) => {
        const form = hidden.form;
        const typed = form ? form.querySelector('input[name="measuredAt"]') : null;
        const hint = form ? form.querySelector('[data-measured-zone]') : null;
        if (!form || !typed) {
            return;
        }

        const fill = () => {
            const when = typed.value ? new Date(typed.value) : new Date();
            const minutes = Number.isNaN(when.getTime()) ? -new Date().getTimezoneOffset() : -when.getTimezoneOffset();
            hidden.value = String(minutes);
            if (hint) {
                hint.textContent = 'Read in your time zone (' + offsetText(minutes) + ').';
            }
        };
        fill();
        typed.addEventListener('change', fill);
        form.addEventListener('submit', fill);
    });
})();
