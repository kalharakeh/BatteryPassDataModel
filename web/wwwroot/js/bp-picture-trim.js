// A battery picture fits its visible shape (owner, 2 Nov 2026): a picture's transparent margins are cut off, so a
// tall battery like the 7M gets a narrow box. It serves the battery pages' header and the public passport's hero.
// A picture without transparent margins, a JPEG, or no JavaScript shows the
// picture as it is. The picture is hidden only while it is being cut, and data-trimmed marks it done.
(function () {
    const image = document.querySelector('.bp-bh-picture img, .bp-hero-picture img');
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
