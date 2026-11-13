// #52: the activity log's "Hide" checkboxes are remembered in this browser (a view setting, nothing in the database).
// When the page is opened without any filter, the remembered groups are applied by opening the address with them, so
// the default for a new browser is still the whole log. "Clear filters" forgets the choice.
(function () {
    const form = document.getElementById("activity-filters");
    if (!form) {
        return;
    }

    const key = "activity-hide";
    const read = () => {
        try {
            return JSON.parse(localStorage.getItem(key) || "[]");
        } catch {
            return [];
        }
    };
    const write = (groups) => {
        try {
            if (groups.length === 0) {
                localStorage.removeItem(key);
            } else {
                localStorage.setItem(key, JSON.stringify(groups));
            }
        } catch {
            // private window or storage switched off: the choice is simply not remembered
        }
    };

    form.addEventListener("submit", () => {
        write(Array.from(form.querySelectorAll("input[name=hide]:checked")).map(i => i.value));
    });

    const clear = document.getElementById("activity-clear");
    if (clear) {
        clear.addEventListener("click", () => write([]));
    }

    const address = new URL(window.location.href);
    const filtered = ["q", "from", "to", "group", "user", "sort", "page", "project", "hide"].some(name => address.searchParams.has(name));
    const remembered = read();
    if (!filtered && remembered.length > 0) {
        remembered.forEach(group => address.searchParams.append("hide", group));
        window.location.replace(address.toString());
    }
})();
