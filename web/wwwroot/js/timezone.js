// timezone.js — client-only display timezone preference.
// DB timestamps stay as stored (Toronto-local semantics from the pipeline).
// This only changes how times LOOK on screen.
(function (global) {
  const STORAGE_KEY = "statify.displayTimeZone";

  // Short curated pool — enough for most friends (Toronto / Egypt / EU / US).
  const TIMEZONE_OPTIONS = [
    { id: "America/Toronto", label: "Toronto (Eastern)" },
    { id: "America/New_York", label: "New York (Eastern)" },
    { id: "America/Chicago", label: "Chicago (Central)" },
    { id: "America/Denver", label: "Denver (Mountain)" },
    { id: "America/Los_Angeles", label: "Los Angeles (Pacific)" },
    { id: "America/Sao_Paulo", label: "São Paulo" },
    { id: "UTC", label: "UTC" },
    { id: "Europe/London", label: "London" },
    { id: "Europe/Berlin", label: "Berlin / Paris / Rome" },
    { id: "Europe/Moscow", label: "Moscow" },
    { id: "Africa/Cairo", label: "Cairo (Egypt)" },
    { id: "Africa/Johannesburg", label: "Johannesburg" },
    { id: "Asia/Dubai", label: "Dubai" },
    { id: "Asia/Kolkata", label: "India" },
    { id: "Asia/Tokyo", label: "Tokyo" },
    { id: "Australia/Sydney", label: "Sydney" }
  ];

  function detectBrowserTimeZone() {
    try {
      return Intl.DateTimeFormat().resolvedOptions().timeZone || "America/Toronto";
    } catch {
      return "America/Toronto";
    }
  }

  function isKnownOption(id) {
    return TIMEZONE_OPTIONS.some((z) => z.id === id);
  }

  function getDisplayTimeZone() {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved && saved.trim()) return saved.trim();
    } catch {
      // ignore storage errors (private mode)
    }

    const detected = detectBrowserTimeZone();
    // Prefer browser zone even if not in the short pool (toLocaleString accepts any IANA).
    return detected || "America/Toronto";
  }

  function setDisplayTimeZone(id) {
    if (!id || !String(id).trim()) return;
    try {
      localStorage.setItem(STORAGE_KEY, String(id).trim());
    } catch {
      // ignore
    }
  }

  // Formats an ISO timestamp from the API into the user's display timezone.
  // API currently emits Toronto-offset ISO via TorontoIso — Date parses that to a real Instant.
  function formatPlayedAt(iso, options) {
    if (!iso) return "";
    try {
      const dt = new Date(iso);
      if (Number.isNaN(dt.getTime())) return iso;

      const opts = Object.assign(
        {
          month: "short",
          day: "numeric",
          hour: "2-digit",
          minute: "2-digit",
          timeZone: getDisplayTimeZone()
        },
        options || {}
      );

      return dt.toLocaleString("en-CA", opts);
    } catch {
      return iso;
    }
  }

  function fillTimezoneSelect(selectEl) {
    if (!selectEl) return;

    const current = getDisplayTimeZone();
    selectEl.innerHTML = "";

    const pool = [...TIMEZONE_OPTIONS];
    if (current && !isKnownOption(current)) {
      pool.unshift({ id: current, label: `${current} (detected)` });
    }

    for (const z of pool) {
      const opt = document.createElement("option");
      opt.value = z.id;
      opt.textContent = z.label;
      if (z.id === current) opt.selected = true;
      selectEl.appendChild(opt);
    }
  }

  global.StatifyTimeZone = {
    STORAGE_KEY,
    TIMEZONE_OPTIONS,
    detectBrowserTimeZone,
    getDisplayTimeZone,
    setDisplayTimeZone,
    formatPlayedAt,
    fillTimezoneSelect
  };
})(window);
