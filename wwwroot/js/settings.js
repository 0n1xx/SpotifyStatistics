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
    const phone = document.getElementById('phone-input').value;
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

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
        if (json.success) alert('Phone number saved!');
    } catch {
        alert('Failed to save phone number.');
    }
}
