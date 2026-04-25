// recently-played.js — Recently Played page logic

function filterRows() {
    const q = document.getElementById('search').value.toLowerCase();
    document.querySelectorAll('.track-row').forEach(row => {
        const match = row.dataset.song.includes(q)
            || row.dataset.artist.includes(q)
            || row.dataset.album.includes(q);
        row.style.display = match ? '' : 'none';
    });
}

function setFilter(btn, range) {
    document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');

    const now = new Date();
    let cutoff = null;
    if (range === '7d')  cutoff = new Date(now - 7   * 86400000);
    if (range === '30d') cutoff = new Date(now - 30  * 86400000);
    if (range === '6m')  cutoff = new Date(now - 182 * 86400000);

    document.querySelectorAll('.track-row').forEach(row => {
        if (!cutoff) { row.style.display = ''; return; }
        const played = new Date(row.dataset.played);
        row.style.display = played >= cutoff ? '' : 'none';
    });
}
