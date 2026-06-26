// settings.js — Settings page interactions
//
// Handles: avatar upload with optimistic preview, display name save,
// email save (stub), phone save, delete modal, inline status feedback.
// All fetch calls use CSRF tokens from the hidden __RequestVerificationToken input.

document.addEventListener('DOMContentLoaded', () => {
    setupAvatarUpload();
    trackExistingAvatar();
});

// ── Avatar upload ──────────────────────────────────────────────────────────────

function setupAvatarUpload() {
    const input = document.getElementById('avatar-input');
    if (!input) return; // settings page might not be visible

    input.addEventListener('change', async () => {
        const file = input.files[0];
        if (!file) return;

        const btn    = document.getElementById('upload-avatar-btn');
        const status = document.getElementById('avatar-upload-status');

        btn.classList.add('is-disabled');
        btn.textContent = 'Uploading…';
        hideStatusEl(status);

        let uploadFile = file;
        try {
            uploadFile = await compressAvatarFile(file);
            previewAvatarLocally(uploadFile);
        } catch {
            if (file.size > AVATAR_MAX_BYTES) {
                showStatusEl(status, 'Image too large — try a smaller photo', 'error');
                resetUploadBtn(btn, input);
                return;
            }
            previewAvatarLocally(file);
        }

        try {
            const token    = getCsrfToken();
            const formData = new FormData();
            formData.append('avatar', uploadFile, 'avatar.jpg');
            if (token) formData.append('__RequestVerificationToken', token);

            const res = await fetchWithTimeout('?handler=UploadAvatar', {
                method:      'POST',
                credentials: 'same-origin',
                headers:     token ? { 'RequestVerificationToken': token } : {},
                body:        formData
            }, FETCH_TIMEOUT_MS);

            const json = await res.json().catch(() => null);
            if (!res.ok || !json) {
                showStatusEl(status, json?.error || `Upload failed (${res.status})`, 'error');
                revertAvatarPreview();
                return;
            }

            if (json.success) {
                showStatusEl(status, '✓ Photo updated', 'success');
                const preview = document.getElementById('avatar-preview');
                if (preview?.src) syncSidebarAvatar(preview.src);
                if (preview) preview.dataset.hadPhoto = '1';
            } else {
                showStatusEl(status, json.error || 'Upload failed', 'error');
                revertAvatarPreview();
            }
        } catch (err) {
            const msg = err?.name === 'AbortError'
                ? 'Upload timed out — try a smaller photo'
                : 'Network error — please try again';
            showStatusEl(status, msg, 'error');
            revertAvatarPreview();
        } finally {
            resetUploadBtn(btn, input);
        }
    });
}

const AVATAR_MAX_PX = 512;
const AVATAR_MAX_BYTES = 400 * 1024;
const AVATAR_JPEG_QUALITY = 0.82;
const FETCH_TIMEOUT_MS = 45000;

// Resize to 512px max and JPEG ~400KB — keeps remote MSSQL writes fast
async function compressAvatarFile(file) {
    const bitmap = await createImageBitmap(file);
    const scale = Math.min(1, AVATAR_MAX_PX / Math.max(bitmap.width, bitmap.height));
    const w = Math.max(1, Math.round(bitmap.width * scale));
    const h = Math.max(1, Math.round(bitmap.height * scale));
    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    canvas.getContext('2d').drawImage(bitmap, 0, 0, w, h);
    bitmap.close();

    let quality = AVATAR_JPEG_QUALITY;
    let blob = await canvasToJpeg(canvas, quality);
    while (blob.size > AVATAR_MAX_BYTES && quality > 0.45) {
        quality -= 0.08;
        blob = await canvasToJpeg(canvas, quality);
    }
    if (blob.size > AVATAR_MAX_BYTES) {
        throw new Error('too large');
    }
    return new File([blob], 'avatar.jpg', { type: 'image/jpeg' });
}

function canvasToJpeg(canvas, quality) {
    return new Promise((resolve, reject) => {
        canvas.toBlob((blob) => blob ? resolve(blob) : reject(new Error('encode failed')), 'image/jpeg', quality);
    });
}

function fetchWithTimeout(url, options, ms) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), ms);
    return fetch(url, { ...options, signal: controller.signal })
        .finally(() => clearTimeout(timer));
}

function resetUploadBtn(btn, input) {
    if (!btn) return;
    btn.classList.remove('is-disabled');
    btn.textContent = 'Upload photo';
    if (input) input.value = '';
}

// Read file locally and swap the avatar image before server responds
function previewAvatarLocally(file) {
    const reader = new FileReader();
    reader.onload = (e) => {
        const preview  = document.getElementById('avatar-preview');
        const initials = document.getElementById('avatar-initials');
        if (preview) {
            preview.src = e.target.result;
            preview.classList.remove('avatar-preview-hidden');
        }
        if (initials) initials.classList.add('is-hidden');
    };
    reader.readAsDataURL(file);
}

// If the upload fails and there was no pre-existing photo, undo the preview
function revertAvatarPreview() {
    const preview  = document.getElementById('avatar-preview');
    const initials = document.getElementById('avatar-initials');
    if (preview && !preview.dataset.hadPhoto) {
        preview.classList.add('avatar-preview-hidden');
        if (initials) initials.classList.remove('is-hidden');
    }
}

// Remember whether an avatar was present on page load so we know whether
// to restore initials if an upload fails
function trackExistingAvatar() {
    const preview = document.getElementById('avatar-preview');
    if (preview && preview.src && !preview.classList.contains('avatar-preview-hidden')) {
        preview.dataset.hadPhoto = '1';
    }
}

// Update the sidebar user card to show the newly uploaded photo without a reload
function syncSidebarAvatar(url) {
    if (!url) return;
    const card = document.querySelector('.sidebar-user .user-avatar');
    if (!card) return;

    const existing = card.querySelector('img');
    if (existing) {
        existing.src = url;
    } else {
        card.innerHTML = `<img src="${url}" alt="Profile photo" class="user-avatar__img">`;
    }
}

// ── Delete confirmation modal ──────────────────────────────────────────────────

// Opens the native <dialog> element — no custom overlay needed
function confirmDelete() {
    document.getElementById('delete-modal').showModal();
}



// ── Save display name ──────────────────────────────────────────────────────────

// POSTs to ?handler=SaveUsername and shows inline feedback.
// Client-side length check mirrors the 50-char limit enforced on the server.
async function saveUsername() {
    const input    = document.getElementById('username-input');
    const username = input.value.trim();
    const btn      = input.closest('.setting-actions')?.querySelector('.btn-green');

    if (username.length > 50) {
        showInlineStatus(input, 'Max 50 characters', 'error');
        return;
    }

    setBtn(btn, true, 'Saving…');

    try {
        const res  = await fetch('?handler=SaveUsername', {
            method:  'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': getCsrfToken()
            },
            body: `username=${encodeURIComponent(username)}`
        });
        const json = await res.json();

        if (json.success) {
            showInlineStatus(input, '✓ Saved', 'success');
            // Reflect the new name in the profile section without a reload
            const nameEl = document.querySelector('.avatar-name');
            if (nameEl && username) nameEl.textContent = username;
        } else {
            showInlineStatus(input, json.error ?? 'Failed to save', 'error');
        }
    } catch {
        showInlineStatus(input, 'Network error', 'error');
    } finally {
        setBtn(btn, false, 'Save');
    }
}

// ── Save phone ─────────────────────────────────────────────────────────────────

async function savePhone() {
    const input = document.getElementById('phone-input');
    const btn   = input.closest('.setting-actions')?.querySelector('.btn-green');

    setBtn(btn, true, 'Saving…');

    try {
        const res  = await fetch('?handler=SavePhone', {
            method:  'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': getCsrfToken()
            },
            body: `phone=${encodeURIComponent(input.value)}`
        });
        const json = await res.json();
        showInlineStatus(input, json.success ? '✓ Saved' : 'Failed to save', json.success ? 'success' : 'error');
    } catch {
        showInlineStatus(input, 'Network error', 'error');
    } finally {
        setBtn(btn, false, 'Save');
    }
}

// ── Inline status label ────────────────────────────────────────────────────────

// Creates or reuses a <span> next to the input's parent .setting-actions container.
// Fades out automatically after 2.5s. Type is 'success' | 'error'.
function showInlineStatus(inputEl, message, type) {
    let status = inputEl.closest('.setting-actions')?.querySelector('.save-status');
    if (!status) {
        status = document.createElement('span');
        status.className = 'save-status';
        inputEl.closest('.setting-actions')?.appendChild(status);
    }

    // Remove old state classes before applying the new one
    status.classList.remove('save-status--success', 'save-status--error', 'is-hidden');
    status.classList.add(`save-status--${type}`);
    status.textContent   = message;
    status.style.display = 'inline';
    status.style.opacity = '1';

    // Fade out after 2.5s, then hide completely so it doesn't take up space
    setTimeout(() => { status.style.opacity = '0'; }, 2500);
    setTimeout(() => { status.style.display  = 'none'; }, 2800);
}

// ── Shared helpers ─────────────────────────────────────────────────────────────

// Reads the ASP.NET CSRF token injected by @Html.AntiForgeryToken()
function getCsrfToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
}

function setBtn(btn, disabled, label) {
    if (!btn) return;
    btn.disabled    = disabled;
    btn.textContent = label;
}

function showStatusEl(el, message, type) {
    if (!el) return;
    el.textContent = message;
    el.classList.remove('save-status--success', 'save-status--error');
    el.classList.add(`save-status--${type}`);
    el.classList.remove('is-hidden');
}

function hideStatusEl(el) {
    if (el) el.classList.add('is-hidden');
}
