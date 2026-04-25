// dashboard.js — Dashboard page logic

// actLabels, actData, todData are injected from the Razor page as global vars

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
                tension: 0.4,
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

function initTodBars() {
    const container = document.getElementById('todBars');
    const maxTod = Math.max(...todData, 1);
    todData.forEach((val, i) => {
        const wrap = document.createElement('div');
        wrap.className = 'tod-bar-wrap';
        const bar = document.createElement('div');
        bar.className = 'tod-bar';
        bar.style.height = `${Math.max((val / maxTod) * 70, 4)}px`;
        bar.title = `${i}:00 — ${val} plays`;
        wrap.appendChild(bar);
        container.appendChild(wrap);
    });
}

function navScroll(e, id) {
    e.preventDefault();
    if (id === 'top') {
        document.querySelector('.main').scrollTo({ top: 0, behavior: 'smooth' });
    } else {
        const el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    document.querySelectorAll('.nav-item[href]').forEach(n => n.classList.remove('active'));
    e.currentTarget.classList.add('active');
}

function initScrollSpy() {
    const mainEl = document.querySelector('.main');
    const scrollSections = [
        { id: 'section-tracks',  href: '#section-tracks' },
        { id: 'section-artists', href: '#section-artists' },
        { id: 'section-albums',  href: '#section-albums' },
    ];

    mainEl.addEventListener('scroll', () => {
        const scrollTop = mainEl.scrollTop;
        if (scrollTop < 100) { setNavActiveByHref('#top'); return; }
        for (let i = scrollSections.length - 1; i >= 0; i--) {
            const el = document.getElementById(scrollSections[i].id);
            if (el && el.offsetTop - 120 <= scrollTop) {
                setNavActiveByHref(scrollSections[i].href);
                return;
            }
        }
    });
}

function setNavActiveByHref(href) {
    document.querySelectorAll('.nav-item[href]').forEach(n => {
        n.classList.remove('active');
        if (n.getAttribute('href') === href) n.classList.add('active');
    });
}

document.addEventListener('DOMContentLoaded', () => {
    initActivityChart();
    initTodBars();
    initScrollSpy();
});
