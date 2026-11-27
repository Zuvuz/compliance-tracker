// US-219 R2: the open page's signs of life for the access log. Every 30 seconds, saying whether the page is visible, one
// at once when it is hidden or shown again, and a goodbye when it is left. Time while the page is hidden does not count,
// but the signs go on, so a tab that is hidden for a while keeps its visit. A browser that closes without a goodbye simply
// stops; the visit ends 2 minutes after its last sign.
(function () {
    'use strict';

    const form = document.getElementById('page-visit');
    if (!form) {
        return;
    }

    const every = Number(form.dataset.every) || 30000;
    let left = false;

    function send(visible, ended) {
        if (left) {
            return;
        }

        const data = new FormData(form);
        data.set('visible', visible ? 'true' : 'false');
        data.set('ended', ended ? 'true' : 'false');
        if (ended) {
            left = true;
            if (navigator.sendBeacon && navigator.sendBeacon(form.action, data)) {
                return;
            }
        }

        fetch(form.action, { method: 'POST', body: data, credentials: 'same-origin', keepalive: ended }).catch(() => { });
    }

    setInterval(() => send(document.visibilityState === 'visible', false), every);

    document.addEventListener('visibilitychange', () => send(document.visibilityState === 'visible', false));
    window.addEventListener('pagehide', () => send(false, true));
})();
