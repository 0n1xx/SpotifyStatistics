// settings.js — Settings page logic

function confirmDelete() {
    document.getElementById('delete-modal').style.display = 'flex';
}

function saveEmail() {
    const email = document.getElementById('email-input').value;
    alert('Email update coming soon: ' + email);
}
