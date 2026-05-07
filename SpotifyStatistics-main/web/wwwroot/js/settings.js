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

        // Show a local preview before the upload finishes (optimistic UI)
        previewAvatarLocally(file);

        btn.disabled    = true;
        btn.textContent = 'Uploading…';
        hideStatusEl(status);

        try {
            const token    = getCsrfToken();
            const formData = new FormData();
            formData.append('avatar', file);

            const res  = await fetch('?handler=UploadAvatar', {
                method:  'POST',
                headers: token ? { 'RequestVerificationToken': token } : {},
                body:    formData
            });
            const json = await res.json();

            if (json.success) {
                showStatusEl(status, '✓ Photo updated', 'success');
                syncSidebarAvatar(json.url);
            } else {
                showStatusEl(status, json.error || 'Upload failed', 'error');
                revertAvatarPreview();
            }
        } catch {
            showStatusEl(status, 'Network error — please try again', 'error');
        } finally {
            btn.disabled    = false;
            btn.textContent = 'Upload photo';
            input.value     = ''; // reset so same file can be re-selected
        }
    });
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

// ── Save email (stub) ──────────────────────────────────────────────────────────

function saveEmail() {
    const email = document.getElementById('email-input').value;
    alert('Email update coming soon: ' + email);
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
    status.textContent  = message;
    status.style.opacity = '1'; // trigger the CSS transition on re-show

    setTimeout(() => { status.style.opacity = '0'; }, 2500);
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
