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

// US-170: a form with data-busy (an AI action or an upload) shows that it works and cannot be sent twice. Only its own
// submit buttons are disabled; the rest of the page stays usable. When the page is shown again (back button, bfcache),
// the buttons work again. Without JavaScript the forms work as before; the server's rate limit still stops repeats.
(() => {
    const restore = form => form.querySelectorAll("button[type=submit][data-busy-text]").forEach(button => {
        button.disabled = false;
        button.textContent = button.dataset.busyText;
        delete button.dataset.busyText;
        form.removeAttribute("aria-busy");
    });

    document.addEventListener("submit", event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.dataset.busy || event.defaultPrevented) return;
        if (form.getAttribute("aria-busy") === "true") {
            event.preventDefault(); // R1: the second press does nothing
            return;
        }

        form.setAttribute("aria-busy", "true");
        // Disabled after the browser has taken the button's value, so the form is still sent as clicked
        setTimeout(() => form.querySelectorAll("button[type=submit]").forEach(button => {
            button.dataset.busyText = button.textContent;
            button.textContent = form.dataset.busy;
            button.disabled = true;
        }), 0);
    });

    // R3: back to a page that was left while it worked
    window.addEventListener("pageshow", () => document.querySelectorAll("form[aria-busy=true]").forEach(restore));
})();

// US-171 R2: a success message fades after 6 seconds; a warning or an error stays. Any success alert on a page counts,
// also the ones a page shows with its own id (R4). data-keep keeps one on purpose.
(() => {
    const reduce = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    document.querySelectorAll(".alert-success:not([data-keep])").forEach(message => {
        const delay = Number(message.dataset.fade) || 6000;
        setTimeout(() => {
            if (reduce) {
                message.hidden = true;
                return;
            }

            message.classList.add("message-fading");
            message.addEventListener("transitionend", () => { message.hidden = true; }, { once: true });
        }, delay);
    });
})();

// US-173 R1, R3: the "?" after a term opens its explanation in a popover; without JavaScript it is a link to the glossary
(() => {
    if (!window.bootstrap || !window.bootstrap.Popover) {
        return;
    }

    document.querySelectorAll(".term-help[data-explanation]").forEach(help => {
        help.removeAttribute("title");
        const popover = new window.bootstrap.Popover(help, {
            content: help.dataset.explanation,
            title: help.dataset.term,
            trigger: "focus",
            placement: "top"
        });
        help.setAttribute("role", "button");
        help.addEventListener("click", event => {
            event.preventDefault();
            help.focus();
            popover.show();
        });
    });
})();

// US-175: the member's choices are remembered in this browser only (nothing on the server), as #52 does for folding.
// R1: opening a project from elsewhere goes to the member's last tab of it. R2: a page's filters are remembered; an address
// with filters of its own always wins. R3: "Reset view" forgets them. R4: a remembered value that is no longer valid is
// ignored (a tab the project no longer has; filters the page no longer knows are ignored by the server).
(() => {
    const read = key => { try { return localStorage.getItem(key); } catch { return null; } };
    const write = (key, value) => { try { value ? localStorage.setItem(key, value) : localStorage.removeItem(key); } catch { /* private window */ } };
    const once = key => { try { if (sessionStorage.getItem(key)) return false; sessionStorage.setItem(key, "1"); return true; } catch { return false; } };

    // R1
    const nav = document.getElementById("project-nav");
    if (nav && nav.dataset.project) {
        const key = `ct-tab:${nav.dataset.project}`;
        const valid = (nav.dataset.tabs || "").split(",").filter(Boolean);
        const root = `/Projects/Details/${nav.dataset.project}`;
        const cameFromProject = document.referrer && new URL(document.referrer, location.href).pathname.startsWith(root);
        const remembered = read(key);
        if (location.pathname.replace(/\/$/, "") === root && !location.search && !cameFromProject
            && remembered && remembered !== nav.dataset.currentTab && valid.includes(remembered) && once(`ct-tab-opened:${root}`)) {
            location.replace(`${root}/${remembered}`);
            return;
        }

        write(key, valid.includes(nav.dataset.currentTab) ? nav.dataset.currentTab : null);
    }

    // R2, R3
    document.querySelectorAll("[data-remember]").forEach(part => {
        const key = `ct-filters:${part.dataset.remember}`;
        const names = (part.dataset.rememberParams || "").split(",").filter(Boolean);
        const address = new URL(location.href);
        const own = names.filter(n => address.searchParams.get(n));
        const reset = [...document.querySelectorAll("[data-reset-view]")].find(b => b.dataset.resetView === part.dataset.remember);

        if (own.length > 0) {
            const kept = new URLSearchParams();
            own.forEach(n => address.searchParams.getAll(n).forEach(v => kept.append(n, v)));
            write(key, kept.toString());
        } else if (read(key) && once(`ct-filters-restored:${key}:${location.pathname}`)) {
            new URLSearchParams(read(key)).forEach((v, n) => { if (names.includes(n)) address.searchParams.append(n, v); });
            location.replace(address.toString());
            return;
        }

        if (reset) {
            reset.hidden = !read(key);
            reset.addEventListener("click", () => {
                write(key, null);
                names.forEach(n => address.searchParams.delete(n));
                location.assign(address.toString());
            });
        }
    });
})();

// US-166: forms that interrupt a page open in the shared dialog. A part (div[data-dialog] opened by [data-dialog-open], or a
// <details data-dialog-title>) is moved into the dialog and back when it closes, so the form is the same one and works
// without JavaScript too (R5). R2: a title, the form's own primary button and Cancel; Esc and the backdrop close it.
// R3: Bootstrap moves the focus in; it goes back to the button that opened it. R4: after a broken rule the page comes
// back with the error, and the dialog opens again with the error in it.
(() => {
    const dialog = document.getElementById("shared-dialog");
    if (!dialog || !window.bootstrap || !window.bootstrap.Modal) {
        return;
    }

    const modal = window.bootstrap.Modal.getOrCreateInstance(dialog);
    const body = dialog.querySelector("#shared-dialog-body");
    const title = dialog.querySelector("#shared-dialog-title");
    let moved = null; // { nodes, home, opener, details }
    const save = key => { try { key ? sessionStorage.setItem("ct-dialog", key) : sessionStorage.removeItem("ct-dialog"); } catch { /* private window */ } };
    const saved = () => { try { return sessionStorage.getItem("ct-dialog"); } catch { return null; } };

    const open = (part, heading, opener, error) => {
        const details = part.tagName === "DETAILS" ? part : null;
        const nodes = details ? [...details.children].filter(c => c.tagName !== "SUMMARY") : [part];
        const home = document.createComment("dialog-home");
        (details ? details : part).after(home);
        title.textContent = heading;
        body.replaceChildren();
        if (error) {
            const alert = document.createElement("div");
            alert.className = "alert alert-danger py-2 small";
            alert.setAttribute("role", "alert");
            alert.textContent = error;
            body.append(alert);
        }

        nodes.forEach(n => body.append(n));
        part.querySelectorAll?.(".dialog-part-heading").forEach(h => h.hidden = true);
        moved = { nodes, home, opener, details, key: part.dataset.dialog || part.id || heading };
        body.querySelectorAll("form").forEach(f => f.addEventListener("submit", () => save(moved?.key), { once: true }));
        modal.show();
    };

    dialog.addEventListener("hidden.bs.modal", () => {
        if (!moved) return;
        if (moved.details) {
            moved.nodes.forEach(n => moved.details.append(n));
            moved.details.open = false;
        } else {
            moved.nodes.forEach(n => moved.home.before(n));
            moved.nodes.forEach(n => n.querySelectorAll?.(".dialog-part-heading").forEach(h => h.hidden = false));
        }

        moved.home.remove();
        moved.opener?.focus();
        moved = null;
    });

    document.querySelectorAll("[data-dialog-open]").forEach(opener => {
        const part = document.querySelector(`[data-dialog="${opener.dataset.dialogOpen}"]`);
        if (!part) return;
        opener.addEventListener("click", event => {
            event.preventDefault();
            open(part, part.dataset.dialogTitle || opener.textContent.trim(), opener);
        });
    });

    document.querySelectorAll("details[data-dialog-title] > summary").forEach(summary => {
        summary.addEventListener("click", event => {
            event.preventDefault();
            open(summary.parentElement, summary.parentElement.dataset.dialogTitle, summary);
        });
    });

    // R4: the page came back with an error after a dialog's form: open the same dialog with the error in it
    const key = saved();
    save(null);
    const error = document.querySelector(".validation-summary-errors li");
    if (key && error) {
        const part = document.querySelector(`[data-dialog="${key}"]`) || document.getElementById(key)
            || [...document.querySelectorAll("details[data-dialog-title]")].find(d => d.dataset.dialogTitle === key);
        if (part) {
            open(part, part.dataset.dialogTitle || key, null, error.textContent.trim());
        }
    }
})();

// US-147 R4: a risk opens in a side panel on the Risks tab, with its own address (?risk=…). The panel shows the risk's
// own page (R5), so every action of US-042 works there. Without JavaScript the link opens the page itself.
(() => {
    const panel = document.getElementById("risk-panel");
    if (!panel || !window.bootstrap || !window.bootstrap.Offcanvas || !window.DOMParser) {
        return;
    }

    const offcanvas = window.bootstrap.Offcanvas.getOrCreateInstance(panel);
    const body = panel.querySelector("#risk-panel-body");
    const title = panel.querySelector("#risk-panel-title");
    const page = panel.querySelector("#risk-panel-page");
    const address = id => {
        const url = new URL(location.href);
        id ? url.searchParams.set("risk", id) : url.searchParams.delete("risk");
        history.replaceState(null, "", url.toString());
    };

    const open = async id => {
        const path = `/Risks/Details/${id}`;
        page.href = path;
        body.textContent = "Loading…";
        offcanvas.show();
        address(id);
        try {
            const response = await fetch(path, { credentials: "same-origin" });
            if (!response.ok) throw new Error(String(response.status));
            const html = new DOMParser().parseFromString(await response.text(), "text/html");
            const main = html.querySelector("main");
            main.querySelector("#messages")?.remove();
            main.querySelector(".page-back")?.remove();
            title.textContent = main.querySelector("#risk-title")?.textContent || "Risk";
            body.replaceChildren(...main.children);
        } catch {
            body.textContent = "The risk could not be loaded. Open its page instead.";
        }
    };

    document.querySelectorAll("[data-risk-panel]").forEach(link => {
        link.addEventListener("click", event => {
            if (event.ctrlKey || event.metaKey || event.shiftKey) return; // a new tab or window gets the page
            event.preventDefault();
            open(link.dataset.riskPanel);
        });
    });

    panel.addEventListener("hidden.bs.offcanvas", () => address(null));
    if (panel.dataset.openRisk) {
        open(panel.dataset.openRisk);
    }
})();

// US-099 R4: "My tasks (3)": the number of tasks that are overdue or due within 7 days, fetched after the page has loaded
(() => {
    const badge = document.getElementById("nav-tasks-count");
    if (!badge?.dataset.countUrl) return;
    const show = count => {
        if (count > 0) {
            badge.textContent = String(count);
            badge.setAttribute("aria-label", `${count} due within 7 days`);
            badge.classList.remove("d-none");
        }
    };

    // Kept for 5 minutes in this tab, so moving between pages does not ask again (B117: no queries on every page)
    const key = "ct-task-count";
    try {
        const kept = JSON.parse(sessionStorage.getItem(key) || "null");
        if (kept && Date.now() - kept.at < 5 * 60 * 1000 && location.pathname.toLowerCase() !== "/review") {
            show(kept.count);
            return;
        }
    } catch { }

    fetch(badge.dataset.countUrl, { credentials: "same-origin", headers: { Accept: "application/json" } })
        .then(response => response.ok ? response.json() : null)
        .then(result => {
            const count = result?.count ?? 0;
            try { sessionStorage.setItem(key, JSON.stringify({ count, at: Date.now() })); } catch { }
            show(count);
        })
        .catch(() => { });
})();
