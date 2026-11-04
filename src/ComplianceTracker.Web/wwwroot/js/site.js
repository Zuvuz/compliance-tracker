// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// US-087: progress bars get their width from data-percent. The Content-Security-Policy forbids inline styles
// (style="width: 40%"), but a script from the tracker itself may set the width.
document.querySelectorAll("[data-percent]").forEach(bar => {
    const percent = Number(bar.dataset.percent);
    bar.style.width = `${Number.isFinite(percent) ? Math.min(Math.max(percent, 0), 100) : 0}%`;
});
