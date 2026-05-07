// sidebar.js — Mobile sidebar drawer toggle
//
// Works with _AppLayout.cshtml and _ManageLayout.cshtml.
// Required DOM elements:
//   #hamburger-btn   — the burger button in the layout
//   #sidebar         — the <aside> navigation element
//   #sidebar-overlay — dark backdrop behind the open drawer
//
// State is driven purely through CSS classes (no inline style manipulation):
//   .sidebar.open          — slides the drawer into view (see app.css)
//   .sidebar-overlay.active — makes the backdrop visible
//   .hamburger.open        — morphs burger icon into ×
//   body.sidebar-open      — locks background scroll and hides the hamburger button

(function () {
    'use strict';

    const btn     = document.getElementById('hamburger-btn');
    const sidebar = document.getElementById('sidebar');
    const overlay = document.getElementById('sidebar-overlay');

    // Nothing to wire up on desktop — elements may not exist in some layouts
    if (!btn || !sidebar || !overlay) return;

    function openSidebar() {
        sidebar.classList.add('open');
        overlay.classList.add('active');
        btn.classList.add('open');
        btn.setAttribute('aria-expanded', 'true');
        // body.sidebar-open: disables scroll and hides the hamburger while the
        // drawer is visible so it doesn't overlap the sidebar logo (see app.css)
        document.body.classList.add('sidebar-open');
    }

    function closeSidebar() {
        sidebar.classList.remove('open');
        overlay.classList.remove('active');
        btn.classList.remove('open');
        btn.setAttribute('aria-expanded', 'false');
        document.body.classList.remove('sidebar-open');
    }

    btn.addEventListener('click', () => {
        sidebar.classList.contains('open') ? closeSidebar() : openSidebar();
    });

    // Tap the backdrop to close
    overlay.addEventListener('click', closeSidebar);

    // Keyboard accessibility — Escape closes the drawer
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') closeSidebar();
    });

    // Auto-close when resizing to desktop so state doesn't get stuck
    window.addEventListener('resize', () => {
        if (window.innerWidth > 900) closeSidebar();
    });
})();
