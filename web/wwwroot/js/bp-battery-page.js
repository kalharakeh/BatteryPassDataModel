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
})();

// The header's picture fits its visible shape (owner, 2 Nov 2026): a picture's transparent margins are cut off, so a
// tall battery like the 7M gets a narrow box. A picture without transparent margins, a JPEG, or no JavaScript shows the
// picture as it is. The picture is hidden only while it is being cut, and data-trimmed marks it done.
(function () {
    const image = document.querySelector('.bp-bh-picture img');
    if (!image) {
        return;
    }

    function done() {
        image.classList.remove('is-trimming');
        image.dataset.trimmed = 'true';
    }

    // The smallest box around the pixels that aren't transparent, or null when there is nothing to cut.
    function visibleBox(width, height, pixels) {
        let left = width;
        let right = -1;
        let top = height;
        let bottom = -1;
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                if (pixels[(y * width + x) * 4 + 3] > 8) {
                    left = Math.min(left, x);
                    right = Math.max(right, x);
                    top = Math.min(top, y);
                    bottom = Math.max(bottom, y);
                }
            }
        }
        if (right < 0 || (left === 0 && top === 0 && right === width - 1 && bottom === height - 1)) {
            return null;
        }
        return { left, top, width: right - left + 1, height: bottom - top + 1 };
    }

    function trim() {
        let cut = null;
        try {
            const width = image.naturalWidth;
            const height = image.naturalHeight;
            if (width && height) {
                const canvas = document.createElement('canvas');
                canvas.width = width;
                canvas.height = height;
                const context = canvas.getContext('2d', { willReadFrequently: true });
                context.drawImage(image, 0, 0);
                const box = visibleBox(width, height, context.getImageData(0, 0, width, height).data);
                if (box) {
                    const out = document.createElement('canvas');
                    out.width = box.width;
                    out.height = box.height;
                    out.getContext('2d').drawImage(canvas, box.left, box.top, box.width, box.height, 0, 0, box.width, box.height);
                    cut = out.toDataURL('image/png');
                }
            }
        } catch {
            cut = null; // a picture the page may not read shows as it is
        }
        if (!cut) {
            done();
            return;
        }
        image.addEventListener('load', done, { once: true });
        image.addEventListener('error', done, { once: true });
        image.src = cut;
    }

    image.classList.add('is-trimming');
    if (image.complete) {
        trim();
    } else {
        image.addEventListener('load', trim, { once: true });
        image.addEventListener('error', done, { once: true });
    }
})();
