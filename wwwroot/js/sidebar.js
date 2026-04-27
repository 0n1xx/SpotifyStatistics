// sidebar.js — Mobile sidebar drawer toggle
// Used by both _AppLayout.cshtml and _ManageLayout.cshtml.
//
// Requires the following elements in the DOM:
//   #hamburger-btn   — the burger button (defined in the layout)
//   #sidebar         — the <aside> element
//   #sidebar-overlay — the dark overlay behind the open drawer
//
// CSS classes toggled:
//   .sidebar.open         — slides the drawer into view (see app.css)
//   .sidebar-overlay.active — shows the overlay
//   .hamburger.open       — animates burger → × icon

(function () {
    'use strict';

    const btn     = document.getElementById('hamburger-btn');
    const sidebar = document.getElementById('sidebar');
    const overlay = document.getElementById('sidebar-overlay');

    if (!btn || !sidebar || !overlay) return; // Guard — desktop has no hamburger

    /** Open the sidebar drawer and lock background scroll */
    function openSidebar() {
        sidebar.classList.add('open');
        overlay.classList.add('active');
        btn.classList.add('open');
        btn.setAttribute('aria-expanded', 'true');
        document.body.style.overflow = 'hidden';
        // Hide the floating X when sidebar slides in — prevents it
        // from overlapping the sidebar logo. User closes via overlay tap or Escape.
        btn.style.opacity = '0';
        btn.style.pointerEvents = 'none';
    }

    /** Close the sidebar drawer and restore scroll */
    function closeSidebar() {
        sidebar.classList.remove('open');
        overlay.classList.remove('active');
        btn.classList.remove('open');
        btn.setAttribute('aria-expanded', 'false');
        document.body.style.overflow = '';
        btn.style.opacity = '';
        btn.style.pointerEvents = '';
    }

    // Toggle on hamburger click
    btn.addEventListener('click', function () {
        sidebar.classList.contains('open') ? closeSidebar() : openSidebar();
    });

    // Close when the overlay (dim background) is tapped
    overlay.addEventListener('click', closeSidebar);

    // Close on Escape key (accessibility)
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') closeSidebar();
    });

    // Close drawer automatically when resizing back to desktop
    window.addEventListener('resize', function () {
        if (window.innerWidth > 900) closeSidebar();
    });
})();
