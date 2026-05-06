// recently-played.js — pagination (9 tracks per page) + client-side search
//
// Data flow: fetches all tracks from /api/history in batches, stores them in
// memory, then renders one page at a time. No server round-trips after load.
//
// Globals expected from the page: none (all data comes from the API).

const PAGE_SIZE = 9;    // rows visible per page
const API_LIMIT = 200;  // batch size per API request

let allTracks   = [];   // master list — never filtered, grows as pages load
let filtered    = [];   // what's currently shown (after search)
let currentPage = 1;

// ── Bootstrap ─────────────────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => { fetchAll(); });

// ── Data fetching ──────────────────────────────────────────────────────────────

// Loads all tracks upfront so pagination and search both work client-side.
// We batch requests (API_LIMIT at a time) to avoid huge payloads.
async function fetchAll() {
    showSpinner(true);
    try {
        let page = 1, hasMore = true;
        while (hasMore) {
            const res = await fetch(`/api/history?page=${page}&limit=${API_LIMIT}`);
            if (!res.ok) {
                const body = await res.json().catch(() => ({}));
                showError(body.error || `Server error ${res.status}`);
                return;
            }
            const data = await res.json();
            allTracks  = allTracks.concat(data.tracks);
            hasMore    = data.hasNextPage;
            page++;
        }
        filtered = [...allTracks];
        renderPage(1);
    } catch (err) {
        showError('Failed to load history. Check your connection.');
    } finally {
        showSpinner(false);
    }
}

function retryLoad() {
    showError(null);
    allTracks = [];
    filtered  = [];
    fetchAll();
}

// ── Rendering ──────────────────────────────────────────────────────────────────

function renderPage(page) {
    currentPage = page;
    const list  = document.getElementById('trackList');
    list.innerHTML = '';

    const start = (page - 1) * PAGE_SIZE;
    const slice = filtered.slice(start, start + PAGE_SIZE);

    if (filtered.length === 0) {
        showEmpty(true);
        renderPagination(0, 0);
        return;
    }

    showEmpty(false);

    const frag = document.createDocumentFragment();
    slice.forEach((t, i) => frag.appendChild(buildRow(t, start + i + 1)));
    list.appendChild(frag);

    const totalPages = Math.ceil(filtered.length / PAGE_SIZE);
    renderPagination(page, totalPages);

    // Jump content area back to the top so the user sees row #1 after page change
    const main = document.querySelector('.main');
    if (main) main.scrollTop = 0;
}

// ── Pagination UI ──────────────────────────────────────────────────────────────

function renderPagination(page, totalPages) {
    // Create the nav once, reuse it on every re-render
    let nav = document.getElementById('pagination');
    if (!nav) {
        nav = document.createElement('nav');
        nav.id = 'pagination';
        nav.className = 'pagination';
        nav.setAttribute('aria-label', 'Page navigation');
        document.querySelector('.main').appendChild(nav);
    }

    if (totalPages <= 1) { nav.innerHTML = ''; return; }

    const pages = buildPageList(page, totalPages);

    nav.innerHTML = `
        <span class="page-info">Page ${page} of ${totalPages}</span>
        <div class="page-btns">
            <button class="page-btn" ${page <= 1 ? 'disabled' : ''} onclick="renderPage(${page - 1})">‹ Prev</button>
            ${pages.map(p => p === '…'
                ? `<span class="page-ellipsis">…</span>`
                : `<button class="page-btn ${p === page ? 'active' : ''}" onclick="renderPage(${p})">${p}</button>`
            ).join('')}
            <button class="page-btn" ${page >= totalPages ? 'disabled' : ''} onclick="renderPage(${page + 1})">Next ›</button>
        </div>`;
}

// Generates the page number list with ellipsis compression.
// Always shows first, last, and a ±2 window around the current page.
function buildPageList(current, total) {
    const range = [];
    for (let i = Math.max(2, current - 2); i <= Math.min(total - 1, current + 2); i++) {
        range.push(i);
    }

    const pages = [1];
    if (range.length && range[0] > 2) pages.push('…');
    pages.push(...range);
    if (range.length && range[range.length - 1] < total - 1) pages.push('…');
    if (total > 1) pages.push(total);
    return pages;
}

// ── Row builder ────────────────────────────────────────────────────────────────

function buildRow(t, idx) {
    const li = document.createElement('li');
    li.className = 'track-row';

    const country = (!t.country || t.country === 'unknown') ? '—' : t.country;

    li.innerHTML = `
        <span class="track-num">${idx}</span>
        <div class="track-song">
            <div class="track-icon" aria-hidden="true">🎵</div>
            <span class="track-name" title="${esc(t.song)}">${esc(t.song)}</span>
        </div>
        <span class="track-artist" title="${esc(t.artist)}">${esc(t.artist)}</span>
        <span class="track-album"  title="${esc(t.album)}">${esc(t.album)}</span>
        <span class="track-country">${esc(country)}</span>
        <time class="track-time" datetime="${esc(t.playedAt)}">${formatDate(t.playedAt)}</time>`;

    return li;
}

// HTML-encodes a string to prevent XSS when injecting track data into innerHTML
function esc(s) {
    return (s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function formatDate(iso) {
    if (!iso) return '';
    try {
        return new Date(iso).toLocaleString('en-CA', {
            month: 'short', day: 'numeric',
            hour: '2-digit', minute: '2-digit'
        });
    } catch { return iso; }
}

// ── Search ─────────────────────────────────────────────────────────────────────

// Filters the in-memory track list and re-renders from page 1.
// Called on every keystroke via oninput in the HTML.
function filterRows() {
    const q = document.getElementById('search').value.toLowerCase();
    filtered = q
        ? allTracks.filter(t =>
            (t.song   || '').toLowerCase().includes(q) ||
            (t.artist || '').toLowerCase().includes(q) ||
            (t.album  || '').toLowerCase().includes(q))
        : [...allTracks];
    renderPage(1);
}

// ── UI state helpers ───────────────────────────────────────────────────────────
// Use CSS classes instead of inline style.display to keep presentation in CSS.

function showSpinner(on) {
    document.getElementById('load-spinner').classList.toggle('is-hidden', !on);
}

function showEmpty(on) {
    document.getElementById('empty-state').classList.toggle('is-hidden', !on);
}

function showError(msg) {
    const el = document.getElementById('error-state');
    if (msg) {
        document.getElementById('error-msg').textContent = msg;
        el.classList.remove('is-hidden');
    } else {
        el.classList.add('is-hidden');
    }
}
