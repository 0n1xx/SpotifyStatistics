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
        bar.style.height = `${Math.max((val / maxTod) * 70, 4)}px`; // min 4px so empty hours still show
        bar.title = `${i}:00 — ${val} plays`;

        wrap.appendChild(bar);
        container.appendChild(wrap);
    });
}

// ── Smooth scroll navigation ──
// Called by onclick on sidebar nav items that link to sections on this page
// (e.g. Top Tracks, Top Artists, Top Albums)
function navScroll(e, id) {
    e.preventDefault();
    if (id === 'top') {
        document.querySelector('.main').scrollTo({ top: 0, behavior: 'smooth' });
    } else {
        const el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    // Update the active nav item highlight immediately
    document.querySelectorAll('.nav-item[href]').forEach(n => n.classList.remove('active'));
    e.currentTarget.classList.add('active');
}

// ── Scroll spy ──
// Watches the .main scroll position and highlights the matching sidebar nav item.
// Sections are identified by their id="section-*" attributes.
function initScrollSpy() {
    const mainEl = document.querySelector('.main');
    const scrollSections = [
        { id: 'section-tracks',  href: '#section-tracks' },
        { id: 'section-artists', href: '#section-artists' },
        { id: 'section-albums',  href: '#section-albums' },
    ];

    mainEl.addEventListener('scroll', () => {
        const scrollTop = mainEl.scrollTop;

        // If near the top, highlight the Dashboard nav item
        if (scrollTop < 100) { setNavActiveByHref('#top'); return; }

        // Walk sections from bottom to top — first one above the fold wins
        for (let i = scrollSections.length - 1; i >= 0; i--) {
            const el = document.getElementById(scrollSections[i].id);
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
});
