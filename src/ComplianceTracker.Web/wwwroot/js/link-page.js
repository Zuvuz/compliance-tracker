// US-220 R7: on the Link page the arrow keys move between the items of a column, and Enter links (or chooses) the item
// that has focus. Without JavaScript every item is still a link or a button that works with Tab and Enter.
(function () {
    'use strict';

    const page = document.getElementById('link-page');
    if (!page) {
        return;
    }

    // The focusable thing of each item: the requirement itself in the left column, the first button elsewhere
    function targets(column) {
        return Array.from(column.querySelectorAll('a.link-requirement-choice, li.link-item'))
            .map(item => item.matches('a') ? item : item.querySelector('button[type="submit"], a'))
            .filter(Boolean);
    }

    page.querySelectorAll('.link-column').forEach(column => {
        column.addEventListener('keydown', event => {
            if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') {
                return;
            }

            if (event.target.matches('input, select, textarea')) {
                return; // typing in a search or choosing a reason keeps the keys
            }

            const items = targets(column);
            if (items.length === 0) {
                return;
            }

            const at = items.indexOf(document.activeElement);
            const next = event.key === 'ArrowDown'
                ? (at < 0 ? 0 : Math.min(at + 1, items.length - 1))
                : (at < 0 ? 0 : Math.max(at - 1, 0));
            items[next].focus();
            event.preventDefault();
        });
    });

    // The chosen requirement is in view when the page opens
    const chosen = page.querySelector('a.link-requirement-choice[aria-current="true"]');
    if (chosen && typeof chosen.scrollIntoView === 'function') {
        chosen.scrollIntoView({ block: 'nearest' });
    }
})();
