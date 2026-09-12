(() => {
  // Floating "Ask Statify" chat — works on desktop + mobile web.
  // Talks to backend: POST /api/chat
  const fab = document.getElementById("chat-fab");
  const panel = document.getElementById("chat-panel");
  const backdrop = document.getElementById("chat-backdrop");
  const closeBtn = document.getElementById("chat-close");
  const form = document.getElementById("chat-form");
  const input = document.getElementById("chat-input");
  const sendBtn = form?.querySelector(".chat-panel__send");
  const messages = document.getElementById("chat-messages");

  if (!fab || !panel || !form || !input || !messages) return;

  let busy = false;

  function isMobile() {
    return window.matchMedia("(max-width: 900px)").matches;
  }

  function openChat() {
    panel.classList.add("open");
    panel.setAttribute("aria-hidden", "false");
    fab.setAttribute("aria-expanded", "true");
    fab.classList.add("is-hidden");
    backdrop?.classList.add("open");
    backdrop?.setAttribute("aria-hidden", "false");
    document.body.classList.add("chat-open");
    // Delay focus slightly so the sheet paints first on mobile
    setTimeout(() => input.focus(), isMobile() ? 80 : 0);
  }

  function closeChat() {
    panel.classList.remove("open");
    panel.setAttribute("aria-hidden", "true");
    fab.setAttribute("aria-expanded", "false");
    fab.classList.remove("is-hidden");
    backdrop?.classList.remove("open");
    backdrop?.setAttribute("aria-hidden", "true");
    document.body.classList.remove("chat-open");
    input.blur();
  }

  function addMessage(text, type) {
    const div = document.createElement("div");
    div.className = `chat-msg chat-msg--${type}`;
    if (type === "bot" && text === "Thinking…") {
      div.classList.add("chat-msg--pending");
    }
    div.textContent = text;
    messages.appendChild(div);
    messages.scrollTop = messages.scrollHeight;
    return div;
  }

  function removePending() {
    const pending = messages.querySelector(".chat-msg--pending");
    if (pending) pending.remove();
  }

  fab.addEventListener("click", () => {
    if (panel.classList.contains("open")) closeChat();
    else openChat();
  });

  closeBtn?.addEventListener("click", closeChat);
  backdrop?.addEventListener("click", closeChat);

  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape" && panel.classList.contains("open")) {
      closeChat();
    }
  });

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    if (busy) return;

    const text = input.value.trim();
    if (!text) return;

    busy = true;
    if (sendBtn) sendBtn.disabled = true;

    addMessage(text, "user");
    input.value = "";
    addMessage("Thinking…", "bot");

    try {
      const response = await fetch("/api/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "same-origin",
        body: JSON.stringify({ message: text }),
      });

      removePending();

      if (response.status === 401) {
        addMessage("Please sign in to use Ask Statify.", "bot");
        return;
      }

      if (!response.ok) {
        const err = await response.json().catch(() => ({}));
        addMessage(err.reply || "Chat request failed. Try again.", "bot");
        return;
      }

      const data = await response.json();
      addMessage(data.reply || "No reply.", "bot");
    } catch {
      removePending();
      addMessage("Network error — check your connection and try again.", "bot");
    } finally {
      busy = false;
      if (sendBtn) sendBtn.disabled = false;
      if (!isMobile()) input.focus();
    }
  });
})();
