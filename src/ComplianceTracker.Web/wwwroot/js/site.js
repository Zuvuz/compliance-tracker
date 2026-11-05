// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// US-087: progress bars get their width from data-percent. The Content-Security-Policy forbids inline styles
// (style="width: 40%"), but a script from the tracker itself may set the width.
document.querySelectorAll("[data-percent]").forEach(bar => {
    const percent = Number(bar.dataset.percent);
    bar.style.width = `${Number.isFinite(percent) ? Math.min(Math.max(percent, 0), 100) : 0}%`;
});

// #52: evidence cards and requirement rows can be folded and opened again. The choice is remembered in this browser only
// (nothing in the database). A part that needs attention (data-attention="true") is never folded by a remembered choice.
// Without JavaScript the buttons stay hidden and everything is open.
(() => {
    const read = key => { try { return localStorage.getItem(key); } catch { return null; } };
    const write = (key, folded) => { try { folded ? localStorage.setItem(key, "1") : localStorage.removeItem(key); } catch { /* private window */ } };

    document.querySelectorAll("[data-fold-key]").forEach(part => {
        const name = part.dataset.foldKey;
        const key = `ct-fold:${name}`;
        const toggle = [...document.querySelectorAll("[data-fold-target]")].find(t => t.dataset.foldTarget === name);
        const summary = [...document.querySelectorAll("[data-fold-summary]")].find(s => s.dataset.foldSummary === name);
        const attention = part.closest("[data-attention='true']") !== null;

        const show = folded => {
            part.hidden = folded;
            if (summary) summary.hidden = !folded;
            if (toggle) {
                toggle.textContent = folded ? "Open" : "Fold";
                toggle.setAttribute("aria-expanded", String(!folded));
            }
        };

        show(!attention && read(key) === "1");
        if (toggle) {
            toggle.hidden = false;
            toggle.addEventListener("click", () => {
                const folded = !part.hidden;
                show(folded);
                write(key, folded);
            });
        }
    });
})();

