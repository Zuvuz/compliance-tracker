// US-018 R7: keeps the project page's user stories up to date.
// The server sends "workItemsChanged" when a sync finds changes; the page then fetches the section again.
(function () {
    const section = document.getElementById("project-work-items");
    const target = document.getElementById("work-items-live");
    if (!section || !target || !window.signalR) {
        return;
    }

    const projectId = section.dataset.projectId;
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/work-items")
        .withAutomaticReconnect()
        .build();

    async function reload() {
        const response = await fetch(`/Projects/WorkItems/${projectId}`, { credentials: "same-origin" });
        if (response.ok) {
            target.innerHTML = await response.text();
        }
    }

    connection.on("workItemsChanged", reload);

    // After a reconnect the server has forgotten which project the page follows
    connection.onreconnected(() => connection.invoke("Follow", projectId));

    connection.start()
        .then(() => connection.invoke("Follow", projectId))
        .catch(error => console.warn("Live updates are off:", error));
})();
