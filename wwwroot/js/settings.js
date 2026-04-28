// =============================================================
// settings.js — Settings page logic
// =============================================================

document.addEventListener('DOMContentLoaded', () => {

    // ── Avatar upload ─────────────────────────────────────────
    const input = document.getElementById('avatar-input');
    if (!input) return;

    input.addEventListener('change', async () => {
        const file = input.files[0];
        if (!file) return;

        const btn    = document.getElementById('upload-avatar-btn');
        const status = document.getElementById('avatar-upload-status');

        // Optimistic preview
        const reader = new FileReader();
        reader.onload = (e) => {
            const preview  = document.getElementById('avatar-preview');
            const initials = document.getElementById('avatar-initials');
            if (preview) {
                preview.src           = e.target.result;
                preview.style.display = 'block';
            }
            if (initials) initials.style.display = 'none';
        };
        reader.readAsDataURL(file);

        btn.disabled    = true;
        btn.textContent = 'Uploading…';
        if (status) status.style.display = 'none';

        try {
            const formData = new FormData();
            formData.append('avatar', file);
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            const res  = await fetch('?handler=UploadAvatar', {
                method:  'POST',
                headers: token ? { 'RequestVerificationToken': token } : {},
                body:    formData
            });
            const json = await res.json();

            if (json.success) {
                if (status) {
                    status.textContent   = '✓ Photo updated';
                    status.style.color   = '#1DB954';
                    status.style.display = 'inline';
                }
                // Sync sidebar user card
                const sidebarAvatar = document.querySelector('.sidebar-user .user-avatar');
                if (sidebarAvatar && json.url) {
                    let sidebarImg = sidebarAvatar.querySelector('img');
                    if (sidebarImg) {
                        sidebarImg.src = json.url;
                    } else {
                        sidebarAvatar.innerHTML =
                            `<img src="${json.url}" alt="Profile photo" class="sidebar-avatar-img">`;
                    }
                }
            } else {
                if (status) {
                    status.textContent   = json.error || 'Upload failed';
                    status.style.color   = '#e74c3c';
                    status.style.display = 'inline';
                }
                const preview  = document.getElementById('avatar-preview');
                const initials = document.getElementById('avatar-initials');
                if (preview && !preview.dataset.hadPhoto) {
                    preview.style.display = 'none';
                    if (initials) initials.style.display = '';
                }
            }
        } catch (err) {
            if (status) {
                status.textContent   = 'Network error — please try again';
                status.style.color   = '#e74c3c';
                status.style.display = 'inline';
            }
        } finally {
            btn.disabled    = false;
            btn.textContent = 'Upload photo';
            input.value     = '';
        }
    });

    const existingPreview = document.getElementById('avatar-preview');
    if (existingPreview && existingPreview.src && existingPreview.style.display !== 'none') {
        existingPreview.dataset.hadPhoto = '1';
    }
});

// ── Delete modal (native <dialog>) ───────────────────────────
function confirmDelete() {
    document.getElementById('delete-modal').showModal();
}

// ── Save email ────────────────────────────────────────────────
function saveEmail() {
    const email = document.getElementById('email-input').value;
    alert('Email update coming soon: ' + email);
}

// ── Save phone ────────────────────────────────────────────────
async function savePhone() {
    const phoneInput = document.getElementById('phone-input');
    const phone = phoneInput.value;
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const btn = phoneInput.closest('.setting-actions')?.querySelector('.btn-green');

    if (btn) { btn.disabled = true; btn.textContent = 'Saving…'; }

    try {
        const res = await fetch('?handler=SavePhone', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token ?? ''
            },
            body: `phone=${encodeURIComponent(phone)}`
        });
        const json = await res.json();
        if (json.success) {
            showInlineStatus(phoneInput, '✓ Saved', 'var(--green)');
        } else {
            showInlineStatus(phoneInput, 'Failed to save', '#e74c3c');
        }
    } catch {
        showInlineStatus(phoneInput, 'Network error', '#e74c3c');
    } finally {
        if (btn) { btn.disabled = false; btn.textContent = 'Save'; }
    }
}

// ── showInlineStatus — creates/updates an inline status label next to the field ──
// Reuses the existing span by id to avoid creating duplicate elements on repeated saves.
// Fades out automatically after 2.5s via opacity transition.
function showInlineStatus(inputEl, message, color) {
    let status = document.getElementById('phone-save-status');
    if (!status) {
        status = document.createElement('span');
        status.id = 'phone-save-status';
        status.style.cssText = 'font-size:12px; margin-left:4px; transition:opacity 0.3s;';
        inputEl.closest('.setting-actions')?.appendChild(status);
    }
    status.textContent = message;
    status.style.color = color;
    status.style.opacity = '1';
    setTimeout(() => { status.style.opacity = '0'; }, 2500);
}
