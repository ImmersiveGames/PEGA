(function () {
  const documents = Array.from(document.querySelectorAll("[data-document]"));
  const tabs = Array.from(document.querySelectorAll("[data-document-target]"));
  const sidebarNav = document.querySelector("[data-sidebar-nav]");
  const main = document.querySelector("#docs-main");

  function getActiveDocument() {
    return documents.find((doc) => doc.classList.contains("is-active")) || documents[0];
  }

  function buildNavigation(documentPanel) {
    if (!documentPanel || !sidebarNav) return;

    const sections = Array.from(documentPanel.querySelectorAll("h2[id], .docs-section[id]"));
    const documentTitle = documentPanel.dataset.documentTitle || "Document";

    sidebarNav.innerHTML = "";

    const group = document.createElement("div");
    group.className = "docs-nav-group";
    const title = document.createElement("div");
    title.className = "docs-nav-group-title";
    title.textContent = documentTitle;
    group.appendChild(title);

    sections.forEach((section) => {
      const id = section.id;
      const heading = section.matches("h2") ? section : section.querySelector("h3, h2, h4");
      const label = section.dataset.title || (heading ? heading.textContent.trim() : id);
      const link = document.createElement("a");
      link.href = `#${id}`;
      link.textContent = label;
      link.dataset.navTarget = id;
      group.appendChild(link);

    });

    sidebarNav.appendChild(group);
  }

  function setActiveDocument(documentId, options = {}) {
    documents.forEach((doc) => {
      const active = doc.dataset.document === documentId;
      doc.hidden = !active;
      doc.classList.toggle("is-active", active);
      doc.setAttribute("aria-hidden", String(!active));
      if (active) {
        doc.removeAttribute("tabindex");
      } else {
        doc.setAttribute("tabindex", "-1");
      }
    });

    tabs.forEach((tab) => {
      const active = tab.dataset.documentTarget === documentId;
      tab.classList.toggle("is-active", active);
      tab.setAttribute("aria-selected", String(active));
      tab.setAttribute("tabindex", active ? "0" : "-1");
    });

    buildNavigation(getActiveDocument());

    document.dispatchEvent(new CustomEvent("docs:document-changed", { detail: { documentId } }));

    if (!options.preserveScroll) {
      window.scrollTo({ top: 0, behavior: "smooth" });
      if (main) main.focus({ preventScroll: true });
    }
  }

  function setActiveAnchor(id) {
    document.querySelectorAll("[data-nav-target]").forEach((link) => {
      const active = link.dataset.navTarget === id;
      link.classList.toggle("is-active", active);
    });
  }

  function observeSections() {
    const observer = new IntersectionObserver((entries) => {
      const visible = entries
        .filter((entry) => entry.isIntersecting)
        .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
      if (visible && visible.target.id) setActiveAnchor(visible.target.id);
    }, { rootMargin: "-25% 0px -60% 0px", threshold: [0.1, 0.25, 0.5] });

    document.querySelectorAll("[data-document] h2[id], [data-document] .docs-section[id]").forEach((section) => {
      observer.observe(section);
    });
  }

  tabs.forEach((tab) => {
    tab.addEventListener("click", () => setActiveDocument(tab.dataset.documentTarget));
    tab.addEventListener("keydown", (event) => {
      const currentIndex = tabs.indexOf(tab);
      const nextKey = event.key === "ArrowRight" || event.key === "ArrowDown";
      const previousKey = event.key === "ArrowLeft" || event.key === "ArrowUp";
      if (!nextKey && !previousKey) return;

      event.preventDefault();
      const direction = nextKey ? 1 : -1;
      const nextIndex = (currentIndex + direction + tabs.length) % tabs.length;
      tabs[nextIndex].focus();
      setActiveDocument(tabs[nextIndex].dataset.documentTarget);
    });
  });

  document.addEventListener("click", (event) => {
    const backToTop = event.target.closest("[data-action='back-to-top']");
    if (backToTop) {
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  });

  window.docsNavigation = {
    getActiveDocument,
    setActiveDocument,
    buildNavigation
  };

  buildNavigation(getActiveDocument());
  setActiveDocument(getActiveDocument().dataset.document, { preserveScroll: true });
  observeSections();
})();
