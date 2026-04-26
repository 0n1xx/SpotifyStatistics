// settings.js — Settings page logic

// ── Avatar upload ──
document.addEventListener('DOMContentLoaded', () => {
    const input = document.getElementById('avatar-input');
    if (!input) return;

    input.addEventListener('change', async () => {
        const file = input.files[0];
        if (!file) return;

        const btn = document.getElementById('upload-avatar-btn');
        const status = document.getElementById('avatar-upload-status');

        // Optimistic preview
        const reader = new FileReader();
        reader.onload = (e) => {
            const preview = document.getElementById('avatar-preview');
            const initials = document.getElementById('avatar-initials');
            if (preview) {
                preview.src = e.target.result;
                preview.style.display = 'block';
            }
            if (initials) initials.style.display = 'none';
        };
        reader.readAsDataURL(file);

        // Show loading state
        btn.disabled = true;
        btn.textContent = 'Uploading…';
        status.style.display = 'none';

        try {
            const formData = new FormData();
            formData.append('avatar', file);

            // Get antiforgery token
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            const res = await fetch('?handler=UploadAvatar', {
                method: 'POST',
                headers: token ? { 'RequestVerificationToken': token } : {},
                body: formData
            });

            const json = await res.json();

            if (json.success) {
                status.textContent = '✓ Photo updated';
                status.style.color = '#1DB954';
            } else {
                status.textContent = json.error || 'Upload failed';
                status.style.color = '#e74c3c';
                // Revert preview on error
                const preview = document.getElementById('avatar-preview');
                if (preview && !preview.dataset.hadPhoto) {
                    preview.style.display = 'none';
                    const initials = document.getElementById('avatar-initials');
                    if (initials) initials.style.display = '';
                }
            }
        } catch (err) {
            status.textContent = 'Network error, please try again';
            status.style.color = '#e74c3c';
        } finally {
            btn.disabled = false;
            btn.textContent = 'Upload photo';
            status.style.display = 'inline';
            input.value = ''; // Reset so same file can be re-selected
        }
    });
});

// ── Delete account modal ──
// Opens the confirmation modal (#delete-modal) when the user clicks "Delete account".
// The actual deletion is handled server-side by the DeleteAccount POST handler
// in Settings.cshtml.cs — this just shows the confirmation step.
function confirmDelete() {
    document.getElementById('delete-modal').style.display = 'flex';
}

// ── Save email ──
// Placeholder for saving the updated email address.
// TODO: replace alert with a real AJAX call to an API endpoint.
function saveEmail() {
    const email = document.getElementById('email-input').value;
    alert('Email update coming soon: ' + email);
}
