(() => {
  // Floating widget UI for the "Ask Statify" chat.
  // Talks to backend: POST /api/chat
  const fab = document.getElementById("chat-fab");
  const panel = document.getElementById("chat-panel");
  const closeBtn = document.getElementById("chat-close");
  const form = document.getElementById("chat-form");
  const input = document.getElementById("chat-input");
  const messages = document.getElementById("chat-messages");

  // If widget is not present on a page, do nothing.
  if (!fab || !panel || !form || !input || !messages) return;

  function openChat() {
    panel.classList.add("open");
    panel.setAttribute("aria-hidden", "false");
    input.focus();
  }

  function closeChat() {
    panel.classList.remove("open");
    panel.setAttribute("aria-hidden", "true");
  }

  function addMessage(text, type) {
    const div = document.createElement("div");
    div.className = `chat-msg chat-msg--${type}`;
    div.textContent = text;
    messages.appendChild(div);
    messages.scrollTop = messages.scrollHeight;
  }

  function removeLastBotMessage() {
    const last = messages.lastElementChild;
    if (last && last.classList.contains("chat-msg--bot")) {
      last.remove();
    }
  }

  fab.addEventListener("click", () => {
    if (panel.classList.contains("open")) closeChat();
    else openChat();
  });

  closeBtn?.addEventListener("click", closeChat);

  form.addEventListener("submit", async (e) => {
    e.preventDefault();

    const text = input.value.trim();
    if (!text) return;

    addMessage(text, "user");
    input.value = "";

    addMessage("Thinking...", "bot");

    try {
      const response = await fetch("/api/chat", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        credentials: "same-origin",
        body: JSON.stringify({ message: text })
      });

      removeLastBotMessage();

      if (!response.ok) {
        addMessage("Chat request failed. Are you logged in?", "bot");
        return;
      }

      const data = await response.json();
      addMessage(data.reply || "No reply.", "bot");
    } catch {
      removeLastBotMessage();
      addMessage("Network error while calling chat API.", "bot");
    }
  });
})();

