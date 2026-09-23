(function () {
  const root = document.documentElement;
  const topbar = document.querySelector(".docs-topbar");
  const sidebar = document.querySelector("#docs-sidebar");

  function updateTopbarHeight() {
    if (!topbar) return;
    const height = Math.ceil(topbar.getBoundingClientRect().height);
    root.style.setProperty("--docs-layout-topbar-height", `${height}px`);
  }

  function safeGetTheme() {
    try {
      return localStorage.getItem("docs-theme");
    } catch (error) {
      return null;
    }
  }

  function safeSetTheme(theme) {
    try {
      localStorage.setItem("docs-theme", theme);
    } catch (error) {
      // The template remains usable without localStorage.
    }
  }

  function fallbackCopyText(text) {
    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.setAttribute("readonly", "");
    textarea.style.position = "fixed";
    textarea.style.left = "-9999px";
    document.body.appendChild(textarea);
    textarea.select();

    try {
      return document.execCommand("copy");
    } finally {
      textarea.remove();
    }
  }

  async function copyText(text) {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text);
      return true;
    }

    return fallbackCopyText(text);
  }

  function setupCodeCopyButtons() {
    document.querySelectorAll(".docs-code").forEach((codeBlock, index) => {
      if (codeBlock.querySelector(".docs-code-copy")) return;

      const code = codeBlock.querySelector("code");
      const button = document.createElement("button");
      button.className = "docs-code-copy";
      button.type = "button";
      button.textContent = "Copy";
      button.dataset.action = "copy-code";
      button.dataset.codeBlockIndex = String(index);
      button.setAttribute("aria-label", "Copy code to clipboard");

      button.addEventListener("click", async () => {
        const text = code ? code.textContent : codeBlock.textContent;

        try {
          const copied = await copyText(text);
          button.textContent = copied ? "Copied" : "Copy failed";
        } catch (error) {
          button.textContent = "Copy failed";
        }

        window.setTimeout(() => {
          button.textContent = "Copy";
        }, 1600);
      });

      codeBlock.appendChild(button);
    });
  }

  const savedTheme = safeGetTheme();
  if (savedTheme === "dark" || savedTheme === "light") {
    root.dataset.theme = savedTheme;
  }

  updateTopbarHeight();
  window.addEventListener("resize", updateTopbarHeight);

  if ("ResizeObserver" in window && topbar) {
    const topbarObserver = new ResizeObserver(updateTopbarHeight);
    topbarObserver.observe(topbar);
  }

  setupCodeCopyButtons();

  document.addEventListener("click", (event) => {
    const themeButton = event.target.closest("[data-action='toggle-theme']");
    if (themeButton) {
      const next = root.dataset.theme === "dark" ? "light" : "dark";
      root.dataset.theme = next;
      safeSetTheme(next);
      updateTopbarHeight();
    }

    const sidebarButton = event.target.closest("[data-action='toggle-sidebar']");
    if (sidebarButton && sidebar) {
      const open = !sidebar.classList.contains("is-open");
      sidebar.classList.toggle("is-open", open);
      sidebarButton.setAttribute("aria-expanded", String(open));
    }

  });
})();
