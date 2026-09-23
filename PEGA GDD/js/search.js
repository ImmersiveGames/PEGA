(function () {
  const input = document.querySelector("#docs-search-input");
  const status = document.querySelector("#docs-search-status");
  const globalToggle = document.querySelector("[data-action='toggle-search-mode']");
  const searchPanel = document.querySelector("[data-search-panel]");
  const searchResults = document.querySelector("[data-search-results]");
  const searchEmpty = document.querySelector("[data-search-empty]");
  const searchSummary = document.querySelector("[data-search-summary]");
  const documents = Array.from(document.querySelectorAll("[data-document]"));
  let globalMode = false;
  let searchActive = false;
  let previousScrollY = 0;

  if (!input) return;

  function getSearchPlaceholder() {
    return globalMode ? "Search all documents..." : "Search active document...";
  }

  function getIdleSearchSummary() {
    return globalMode
      ? "Type in the header search field to search all documents."
      : "Type in the header search field to search the active document.";
  }

  function syncSearchModeControls() {
    input.placeholder = getSearchPlaceholder();

    if (globalToggle) {
      globalToggle.setAttribute("aria-pressed", String(globalMode));
      globalToggle.textContent = globalMode ? "Active" : "Global";
    }

    if (searchSummary && searchPanel && searchPanel.hidden) {
      searchSummary.textContent = getIdleSearchSummary();
    }
  }

  function normalize(value) {
    return (value || "").trim().toLowerCase();
  }

  function getSnippet(text, query) {
    const clean = text.replace(/\s+/g, " ").trim();
    const index = clean.toLowerCase().indexOf(query);
    if (index < 0) return clean.slice(0, 140);
    return clean.slice(Math.max(0, index - 50), Math.min(clean.length, index + query.length + 90));
  }

  function getSectionTitle(section) {
    const heading = section.matches("h2") ? section : section.querySelector("h3, h2, h4");
    return section.dataset.title || (heading ? heading.textContent.trim() : section.id);
  }

  function getDocumentPath(documentTitle, title) {
    return title === documentTitle ? documentTitle : `${documentTitle} / ${title}`;
  }

  function getActiveDocument() {
    return window.docsNavigation ? window.docsNavigation.getActiveDocument() : document.querySelector(".docs-document.is-active");
  }

  function showSearchView() {
    if (!searchActive) {
      previousScrollY = window.scrollY;
    }

    searchActive = true;
    documents.forEach((doc) => {
      doc.hidden = true;
      doc.setAttribute("aria-hidden", "true");
    });
  }

  function restoreDocumentView(options = {}) {
    const active = getActiveDocument();
    searchActive = false;

    documents.forEach((doc) => {
      const visible = doc === active;
      doc.hidden = !visible;
      doc.setAttribute("aria-hidden", String(!visible));
    });

    if (options.restoreScroll) {
      window.scrollTo({ top: previousScrollY, behavior: "auto" });
    }
  }

  function createSearchResult(result) {
    const item = document.createElement("article");
    item.className = "docs-search-result";

    const link = document.createElement("a");
    link.href = `#${result.id}`;
    link.dataset.searchResult = "";
    link.dataset.documentId = result.documentId;
    link.dataset.targetId = result.id;
    link.textContent = result.title;

    const documentLabel = document.createElement("small");
    documentLabel.textContent = `Document: ${result.documentTitle}`;

    const section = document.createElement("span");
    section.className = "docs-search-result__section";
    section.textContent = `Section: ${result.sectionTitle}`;

    const path = document.createElement("span");
    path.className = "docs-search-result__path";
    path.textContent = result.path;

    const snippet = document.createElement("p");
    snippet.textContent = result.snippet;

    item.appendChild(link);
    item.appendChild(documentLabel);
    item.appendChild(section);
    item.appendChild(path);
    item.appendChild(snippet);

    return item;
  }

  function clearSearchPanel(options = {}) {
    if (searchPanel) searchPanel.hidden = true;
    if (searchEmpty) searchEmpty.hidden = true;
    if (searchResults) {
      searchResults.innerHTML = "";
    }
    if (searchSummary) {
      searchSummary.textContent = getIdleSearchSummary();
    }

    restoreDocumentView({ restoreScroll: Boolean(options.restoreScroll) });
  }

  function clearFiltering(options = {}) {
    document.querySelectorAll(".is-search-hidden").forEach((node) => node.classList.remove("is-search-hidden"));
    clearSearchPanel(options);
  }

  function renderResults(results, query) {
    if (!searchPanel || !searchResults || !searchEmpty || !searchSummary) return;

    showSearchView();
    searchPanel.hidden = false;
    searchResults.innerHTML = "";
    searchEmpty.hidden = results.length !== 0;

    searchSummary.textContent = `${results.length} Result${results.length === 1 ? "" : "s"}`;

    results.forEach((result) => {
      searchResults.appendChild(createSearchResult(result));
    });
  }

  function activeDocumentSearch(query) {
    const active = getActiveDocument();
    if (!active) return 0;

    const sections = Array.from(active.querySelectorAll(".docs-section"));
    const docTitle = active.dataset.documentTitle || "Document";
    const results = [];

    sections.forEach((section) => {
      const match = section.textContent.toLowerCase().includes(query);
      if (match) {
        const title = getSectionTitle(section);
        results.push({
          documentId: active.dataset.document,
          documentTitle: docTitle,
          title,
          sectionTitle: title,
          path: getDocumentPath(docTitle, title),
          id: section.id,
          snippet: getSnippet(section.textContent || "", query)
        });
      }
    });

    renderResults(results, query);
    return results.length;
  }

  function globalSearch(query) {
    const results = [];
    document.querySelectorAll("[data-document]").forEach((doc) => {
      const docTitle = doc.dataset.documentTitle || "Document";
      doc.querySelectorAll("h2[id], .docs-section[id]").forEach((section) => {
        const text = section.textContent || "";
        if (text.toLowerCase().includes(query)) {
          const title = getSectionTitle(section);
          results.push({
            documentId: doc.dataset.document,
            documentTitle: docTitle,
            title,
            sectionTitle: title,
            path: getDocumentPath(docTitle, title),
            id: section.id,
            snippet: getSnippet(text, query)
          });
        }
      });
    });

    renderResults(results, query);
    return results.length;
  }

  function runSearch() {
    const query = normalize(input.value);

    if (!query) {
      clearFiltering({ restoreScroll: searchActive });
      status.textContent = "Search is ready.";
      return;
    }

    const count = globalMode ? globalSearch(query) : activeDocumentSearch(query);
    status.textContent = count === 0
      ? "No results found."
      : globalMode
        ? `${count} results across documents.`
        : `${count} sections found.`;
  }

  input.addEventListener("input", runSearch);
  input.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      input.value = "";
      runSearch();
      input.blur();
    }
  });

  if (globalToggle) {
    globalToggle.addEventListener("click", () => {
      globalMode = !globalMode;
      syncSearchModeControls();
      runSearch();
    });
  }

  document.addEventListener("click", (event) => {
    const result = event.target.closest("[data-search-result]");
    if (result && window.docsNavigation) {
      event.preventDefault();
      window.docsNavigation.setActiveDocument(result.dataset.documentId, { preserveScroll: true });
      input.value = "";
      clearFiltering({ restoreScroll: false });
      status.textContent = "Search is ready.";

      window.requestAnimationFrame(() => {
        const target = document.getElementById(result.dataset.targetId);
        if (!target) return;
        history.replaceState(null, "", `#${result.dataset.targetId}`);
        target.scrollIntoView({ behavior: "smooth", block: "start" });
      });
    }
  });

  document.addEventListener("docs:document-changed", () => {
    input.value = "";
    clearFiltering({ restoreScroll: false });
    status.textContent = "Search reset for active document.";
  });

  syncSearchModeControls();
})();
