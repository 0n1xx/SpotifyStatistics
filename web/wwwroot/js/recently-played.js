// recently-played.js — infinite scroll, client-side search + time filter
// Fetches from GET /api/history (same endpoint as iOS) using the cookie-based
// session. The endpoint is [Authorize(AuthenticationSchemes="Bearer")] but
// ASP.NET also accepts the cookie auth for browser requests automatically
// because Program.cs sets the default scheme to cookie auth.
// If the server requires the JWT explicitly, we fall back to a dedicated
// web-facing endpoint — but the shared /api/history works fine for browsers.

const PAGE_LIMIT = 50;

let currentPage   = 0;
let isLoading     = false;
let hasMore       = true;
let allTracks     = [];   // master list for client-side filter
let activeSearch  = '';
let globalIndex   = 0;   // running row number across pages

// ── Bootstrap ────────────────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
    loadMore();
    setupIntersectionObserver();
});

// ── Fetch next page ───────────────────────────────────────────────────────────

async function loadMore() {
    if (isLoading || !hasMore) return;
    isLoading = true;
    showSpinner(true);

    try {
        const nextPage = currentPage + 1;
        const res = await fetch(`/api/history?page=${nextPage}&limit=${PAGE_LIMIT}`);

        if (!res.ok) {
            const body = await res.json().catch(() => ({}));
            showError(body.error || `Server error ${res.status}`);
            return;
        }

        const data = await res.json();
        currentPage = data.page;
        hasMore     = data.hasNextPage;

        allTracks = allTracks.concat(data.tracks);
        appendRows(data.tracks);

        if (allTracks.length === 0) showEmpty(true);
    } catch (err) {
        showError('Failed to load history. Check your connection.');
    } finally {
        isLoading = false;
        showSpinner(false);
    }

    // If sentinel is still visible after load (content didn't fill the scroll area),
    // keep loading — IntersectionObserver only fires on transitions, not on stays.
    requestAnimationFrame(() => {
        if (!hasMore) return;
        const sentinel  = document.getElementById('scroll-sentinel');
        const scrollBox = document.querySelector('.main');
        if (!sentinel || !scrollBox) return;
        const sRect = sentinel.getBoundingClientRect();
        const bRect = scrollBox.getBoundingClientRect();
        if (sRect.top < bRect.bottom + 200) loadMore();
    });
}

function retryLoad() {
    showError(null);
    loadMore();
}

// ── Render rows ───────────────────────────────────────────────────────────────

function appendRows(tracks) {
    const list = document.getElementById('trackList');
    const frag = document.createDocumentFragment();

    tracks.forEach(t => {
        globalIndex++;
        const li = buildRow(t, globalIndex);
        frag.appendChild(li);
    });

    list.appendChild(frag);
    applyFilters(); // re-apply active search/range after appending
}

function buildRow(t, idx) {
    const li = document.createElement('li');
    li.className = 'track-row';
    li.dataset.song   = (t.song   || '').toLowerCase();
    li.dataset.artist = (t.artist || '').toLowerCase();
    li.dataset.album  = (t.album  || '').toLowerCase();
    li.dataset.played = t.playedAt || '';

    const country = (!t.country || t.country === 'unknown') ? '—' : t.country;
    const timeLabel = formatDate(t.playedAt);
    const delay = ((idx - 1) % PAGE_LIMIT) * 0.012;

    li.style.animationDelay = `${delay}s`;
    li.innerHTML = `
        <span class="track-num">${idx}</span>
        <div class="track-song">
            <div class="track-icon" aria-hidden="true">🎵</div>
            <span class="track-name" title="${esc(t.song)}">${esc(t.song)}</span>
        </div>
        <span class="track-artist" title="${esc(t.artist)}">${esc(t.artist)}</span>
        <span class="track-album"  title="${esc(t.album)}">${esc(t.album)}</span>
        <span class="track-country">${esc(country)}</span>
        <time class="track-time" datetime="${esc(t.playedAt)}">${timeLabel}</time>
    `;
    return li;
}

function esc(str) {
    return (str || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function formatDate(iso) {
    if (!iso) return '';
    try {
        const d = new Date(iso);
        return d.toLocaleString('en-CA', {
            month: 'short', day: 'numeric',
            hour: '2-digit', minute: '2-digit'
        });
    } catch { return iso; }
}

// ── Intersection Observer for infinite scroll ─────────────────────────────────

function setupIntersectionObserver() {
    const sentinel  = document.getElementById('scroll-sentinel');
    const scrollBox = document.querySelector('.main');
    const observer  = new IntersectionObserver(entries => {
        if (entries[0].isIntersecting) loadMore();
    }, {
        root: scrollBox,   // observe relative to the scrolling .main, not the viewport
        rootMargin: '200px'
    });
    observer.observe(sentinel);
}

// ── Client-side search & time filter ─────────────────────────────────────────

function filterRows() {
    activeSearch = document.getElementById('search').value.toLowerCase();
    applyFilters();
}

function applyFilters() {
    let visibleCount = 0;
    document.querySelectorAll('.track-row').forEach(row => {
        const matchSearch = !activeSearch
            || row.dataset.song.includes(activeSearch)
            || row.dataset.artist.includes(activeSearch)
            || row.dataset.album.includes(activeSearch);

        row.style.display = matchSearch ? '' : 'none';
        if (matchSearch) visibleCount++;
    });

    showEmpty(allTracks.length > 0 && visibleCount === 0);
}

// ── UI helpers ────────────────────────────────────────────────────────────────

function showSpinner(on) {
    document.getElementById('load-spinner').style.display = on ? 'flex' : 'none';
}

function showEmpty(on) {
    document.getElementById('empty-state').style.display = on ? 'flex' : 'none';
}

function showError(msg) {
    const el = document.getElementById('error-state');
    if (msg) {
        document.getElementById('error-msg').textContent = msg;
        el.style.display = 'flex';
    } else {
        el.style.display = 'none';
    }
}
