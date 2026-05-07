// dashboard.js — Dashboard page logic
// Depends on: Chart.js (loaded via ViewData["ExtraHead"] in Dashboard.cshtml)
// Data globals injected by the @section Scripts block in Dashboard.cshtml:
//   actLabels — array of month strings  e.g. ["Jan 2024", "Feb 2024", ...]
//   actData   — array of play counts    e.g. [42, 67, ...]
//   todData   — array[24] of play counts per hour e.g. [2, 0, 1, ...]

// ── Activity line chart ──
// Renders the monthly listening history chart into <canvas id="activityChart">
function initActivityChart() {
    const ctx = document.getElementById('activityChart').getContext('2d');
    new Chart(ctx, {
        type: 'line',
        data: {
            labels: actLabels,
            datasets: [{
                data: actData,
                borderColor: '#1DB954',
                backgroundColor: 'rgba(29,185,84,0.08)',
                borderWidth: 2,
                pointRadius: 3,
                pointBackgroundColor: '#1DB954',
                tension: 0.4,  // slight curve on the line
                fill: true
            }]
        },
        options: {
            responsive: true,
            plugins: { legend: { display: false } },
            scales: {
                x: { grid: { color: 'rgba(255,255,255,0.04)' }, ticks: { color: '#555', font: { size: 11 } } },
                y: { grid: { color: 'rgba(255,255,255,0.04)' }, ticks: { color: '#555', font: { size: 11 } } }
            }
        }
    });
}

// ── Time-of-day bar chart ──
// Builds 24 divs (one per hour) inside <div id="todBars">.
// Bar height is proportional to play count relative to the busiest hour.
function initTodBars() {
    const container = document.getElementById('todBars');
    const maxTod = Math.max(...todData, 1); // avoid division by zero

    todData.forEach((val, i) => {
        const wrap = document.createElement('div');
        wrap.className = 'tod-bar-wrap';

        const bar = document.createElement('div');
        bar.className = 'tod-bar';
        // CSS reads --bar-height to set the bar's height — keeps sizing logic out of the stylesheet
        bar.style.setProperty('--bar-height', `${Math.max((val / maxTod) * 70, 4)}px`);
        bar.title = `${i}:00 — ${val} plays`;

        wrap.appendChild(bar);
        container.appendChild(wrap);
    });
}

// ── Smooth scroll navigation ──
// Called by onclick on sidebar nav items that link to sections on this page
// (e.g. Top Tracks, Top Artists, Top Albums).
//
// NOTE: These onclick handlers only exist when we ARE on the Dashboard page.
// When the user is on a different page (e.g. Settings), _Sidebar.cshtml renders
// plain <a href="/Dashboard#section-*"> links instead — no JS needed there.
function navScroll(e, id) {
    e.preventDefault(); // stop the browser from jumping to the anchor instantly

    if (id === 'top') {
        // "Dashboard" link — scroll the content area back to the very top
        document.querySelector('.main').scrollTo({ top: 0, behavior: 'smooth' });
    } else {
        // Section link — scrollIntoView handles both the .main scroll and
        // cases where the element is already partially visible
        const el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    // Immediately update the active nav highlight so it doesn't wait for the scroll spy
    document.querySelectorAll('.nav-item[href]').forEach(n => n.classList.remove('active'));
    e.currentTarget.classList.add('active');
}

// ── Scroll spy ──
// Watches the .main scroll position and highlights the matching sidebar nav item.
// Sections are identified by their id="section-*" attributes set in Dashboard.cshtml.
//
// Logic: we walk sections from BOTTOM to TOP. The first section whose top edge
// is above the current scroll position is considered "active". This way if you
// scroll to the bottom, the last section wins — not the first one.
function initScrollSpy() {
    const mainEl = document.querySelector('.main');
    const scrollSections = [
        { id: 'section-tracks',  href: '#section-tracks' },
        { id: 'section-artists', href: '#section-artists' },
        { id: 'section-albums',  href: '#section-albums' },
    ];

    mainEl.addEventListener('scroll', () => {
        const scrollTop = mainEl.scrollTop;

        // If near the top (before the first section), highlight Dashboard nav item
        if (scrollTop < 100) { setNavActiveByHref('#top'); return; }

        // Walk sections from bottom to top — first one scrolled past wins
        for (let i = scrollSections.length - 1; i >= 0; i--) {
            const el = document.getElementById(scrollSections[i].id);
            // offsetTop is relative to the .main container (since .main is the scroll parent)
            if (el && el.offsetTop - 120 <= scrollTop) {
                setNavActiveByHref(scrollSections[i].href);
                return;
            }
        }
    });
}

// Helper: removes .active from all nav items and adds it to the one with matching href
function setNavActiveByHref(href) {
    document.querySelectorAll('.nav-item[href]').forEach(n => {
        n.classList.remove('active');
        if (n.getAttribute('href') === href) n.classList.add('active');
    });
}

// ── Entry point ──
document.addEventListener('DOMContentLoaded', () => {
    initActivityChart();
    initTodBars();
    initScrollSpy();

    // ── Auto-scroll when arriving via anchor link ──
    // When the user clicks "Top Tracks" on a non-Dashboard page, the sidebar
    // generates a link like /Dashboard#section-tracks. After the page loads,
    // the browser tries to scroll to the anchor — but because .main is the scroll
    // container (not the page itself), the browser can't find it. We do it manually.
    if (location.hash) {
        const id = location.hash.slice(1); // strip the leading '#'
        const el = document.getElementById(id);
        if (el) {
            // 100ms delay lets Chart.js and the TOD bars finish rendering first
            // so the element's offsetTop is fully calculated before we scroll to it
            setTimeout(() => {
                el.scrollIntoView({ behavior: 'smooth', block: 'start' });
                setNavActiveByHref(location.hash); // highlight the correct sidebar item
            }, 100);
        }
    }
});
