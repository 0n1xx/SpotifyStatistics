// recently-played.js — Recently Played page logic
// Both functions below are called directly via onclick/oninput attributes in RecentlyPlayed.cshtml

// ── Live search filter ──
// Called on every keystroke in the search input (oninput="filterRows()").
// Reads data-song / data-artist / data-album attributes set on each .track-row,
// and hides rows that don't match the current query.
function filterRows() {
    const q = document.getElementById('search').value.toLowerCase();
    document.querySelectorAll('.track-row').forEach(row => {
        const match = row.dataset.song.includes(q)
            || row.dataset.artist.includes(q)
            || row.dataset.album.includes(q);
        row.style.display = match ? '' : 'none';
    });
}

// ── Time range filter ──
// Called when a filter button is clicked (e.g. "7 days", "30 days").
// Moves the .active class to the clicked button and hides rows
// whose data-played timestamp falls outside the selected window.
function setFilter(btn, range) {
    // Update active button style
    document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');

    // Calculate cutoff date — null means show all
    const now = new Date();
    let cutoff = null;
    if (range === '7d')  cutoff = new Date(now - 7   * 86400000);
    if (range === '30d') cutoff = new Date(now - 30  * 86400000);
    if (range === '6m')  cutoff = new Date(now - 182 * 86400000);

    // Show/hide rows based on when the track was played
    document.querySelectorAll('.track-row').forEach(row => {
        if (!cutoff) { row.style.display = ''; return; } // "All time" — show everything
        const played = new Date(row.dataset.played); // ISO 8601 string from data-played
        row.style.display = played >= cutoff ? '' : 'none';
    });
}
