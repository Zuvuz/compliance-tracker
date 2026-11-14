// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// US-087: progress bars get their width from data-percent. The Content-Security-Policy forbids inline styles
// (style="width: 40%"), but a script from the tracker itself may set the width.
document.querySelectorAll("[data-percent]").forEach(bar => {
    const percent = Number(bar.dataset.percent);
    bar.style.width = `${Number.isFinite(percent) ? Math.min(Math.max(percent, 0), 100) : 0}%`;
});

// #52: evidence cards can be folded and opened again (requirement rows no longer fold since US-153). The choice is remembered in this browser only
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


// US-143 R4: a requirement with requirements under it can be folded; the rows under it (at any depth) are hidden.
// Only in this view: nothing is remembered.
(() => {
    const rows = [...document.querySelectorAll("tr.requirement-row[data-id]")];
    const children = id => rows.filter(r => r.dataset.parent === id);
    const setHidden = (id, hidden) => children(id).forEach(row => {
        row.hidden = hidden;
        const toggle = row.querySelector("[data-tree-toggle]");
        // A row that is shown again shows its own children only if it was open
        setHidden(row.dataset.id, hidden || (toggle !== null && toggle.getAttribute("aria-expanded") === "false"));
    });

    document.querySelectorAll("[data-tree-toggle]").forEach(toggle => {
        toggle.addEventListener("click", () => {
            const open = toggle.getAttribute("aria-expanded") !== "true";
            toggle.setAttribute("aria-expanded", String(open));
            toggle.textContent = open ? "▾" : "▸";
            setHidden(toggle.dataset.treeToggle, !open);
        });
    });
})();


// US-135 R1: a form with data-confirm asks first, in a dialog that says what will happen and whether it can be undone.
// Nothing happens on one click. Without JavaScript the form is sent as before (the server still checks everything).
(() => {
    const dialog = document.getElementById("confirm-dialog");
    if (!dialog || !window.bootstrap) return;
    const modal = new bootstrap.Modal(dialog);
    let pending = null;

    document.addEventListener("submit", event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.dataset.confirm || form.dataset.confirmed === "true") return;
        event.preventDefault();
        pending = { form, submitter: event.submitter };
        dialog.querySelector("#confirm-dialog-title").textContent = form.dataset.confirmTitle || "Are you sure?";
        dialog.querySelector("#confirm-dialog-text").textContent = form.dataset.confirm;
        dialog.querySelector("#confirm-dialog-final").hidden = form.dataset.confirmFinal !== "true";
        dialog.querySelector("#confirm-dialog-ok").textContent = form.dataset.confirmButton || "Confirm";
        modal.show();
    });

    dialog.querySelector("#confirm-dialog-ok").addEventListener("click", () => {
        if (!pending) return;
        const { form, submitter } = pending;
        pending = null;
        form.dataset.confirmed = "true";
        modal.hide();
        form.requestSubmit(submitter && submitter.form === form ? submitter : undefined);
    });

    dialog.addEventListener("hidden.bs.modal", () => { pending = null; });
})();
