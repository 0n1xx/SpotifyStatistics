// settings.js — Settings page logic

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
