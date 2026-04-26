// =============================================================
// settings.js — Settings page logic
//
// Responsibilities:
//   - Avatar upload: preview → POST → sync sidebar user card
//   - Delete account confirmation modal
//   - Save email (placeholder)
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

        // ── Optimistic preview ──
        // Show the selected image immediately in the large avatar circle
        // so the user gets instant feedback before the upload completes.
        const reader = new FileReader();
        reader.onload = (e) => {
            // The large avatar on the settings page may contain either:
            //   (a) an <img id="avatar-preview"> that is already visible, or
            //   (b) a <span id="avatar-initials"> + a hidden <img id="avatar-preview">
            const preview  = document.getElementById('avatar-preview');
            const initials = document.getElementById('avatar-initials');

            if (preview) {
                preview.src           = e.target.result;
                preview.style.display = 'block';        // make visible if hidden
            }

            // Hide the initial-letter fallback while the image is shown
            if (initials) initials.style.display = 'none';
        };
        reader.readAsDataURL(file);

        // ── Loading state ──
        btn.disabled    = true;
        btn.textContent = 'Uploading…';
        if (status) status.style.display = 'none';

        try {
            const formData = new FormData();
            formData.append('avatar', file);

            // CSRF token — required by ASP.NET Core antiforgery middleware
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            const res  = await fetch('?handler=UploadAvatar', {
                method:  'POST',
                headers: token ? { 'RequestVerificationToken': token } : {},
                body:    formData
            });

            const json = await res.json();

            if (json.success) {
                // ── Success: show confirmation ──
                if (status) {
                    status.textContent    = '✓ Photo updated';
                    status.style.color   = '#1DB954';
                    status.style.display = 'inline';
                }

                // ── Sync the sidebar user card ──
                // The bottom-left user card (.sidebar-user .user-avatar) shows the
                // same photo as the large profile avatar on this page.
                // Update it in-place so the change is visible without a page reload.
                const sidebarAvatar = document.querySelector('.sidebar-user .user-avatar');
                if (sidebarAvatar && json.url) {
                    // If an <img> already exists — update its src.
                    // If not (initial-letter state) — replace the text node with an <img>.
                    let sidebarImg = sidebarAvatar.querySelector('img');
                    if (sidebarImg) {
                        sidebarImg.src = json.url + '?t=' + Date.now(); // bust cache
                    } else {
                        sidebarAvatar.innerHTML =
                            `<img src="${json.url}?t=${Date.now()}"
                                  alt="Profile photo"
                                  style="width:100%;height:100%;object-fit:cover;border-radius:50%">`;
                    }
                }

            } else {
                // ── Error: show message and revert preview ──
                if (status) {
                    status.textContent    = json.error || 'Upload failed';
                    status.style.color   = '#e74c3c';
                    status.style.display = 'inline';
                }

                // Revert large avatar preview to the previous state
                const preview  = document.getElementById('avatar-preview');
                const initials = document.getElementById('avatar-initials');
                // Only revert if there was no photo before this attempt
                if (preview && !preview.dataset.hadPhoto) {
                    preview.style.display = 'none';
                    if (initials) initials.style.display = '';
                }
            }

        } catch (err) {
            // Network or parse error
            if (status) {
                status.textContent    = 'Network error — please try again';
                status.style.color   = '#e74c3c';
                status.style.display = 'inline';
            }
        } finally {
            btn.disabled    = false;
            btn.textContent = 'Upload photo';
            // Reset so the same file can be re-selected
            input.value = '';
        }
    });

    // Mark whether the large avatar already had a photo on page load,
    // so we know whether to revert on error.
    const existingPreview = document.getElementById('avatar-preview');
    if (existingPreview && existingPreview.style.display !== 'none' && existingPreview.src) {
        existingPreview.dataset.hadPhoto = '1';
    }

});

// ── Delete account modal ──────────────────────────────────────
// Opens the confirmation dialog when the user clicks "Delete account".
// The actual deletion is handled by the OnPostDeleteAccount handler
// in Settings.cshtml.cs — this JS just shows the confirmation step.
function confirmDelete() {
    document.getElementById('delete-modal').style.display = 'flex';
}

// ── Save email ────────────────────────────────────────────────
// TODO: replace alert with a real AJAX call to an API endpoint.
function saveEmail() {
    const email = document.getElementById('email-input').value;
    alert('Email update coming soon: ' + email);
}
