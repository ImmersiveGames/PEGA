(function () {
  const filter = document.querySelector("#docs-glossary-filter");
  if (!filter) return;

  function runFilter() {
    const query = filter.value.trim().toLowerCase();
    document.querySelectorAll(".docs-glossary-term").forEach((term) => {
      const matches = !query || term.textContent.toLowerCase().includes(query);
      term.classList.toggle("is-search-hidden", !matches);
    });
  }

  filter.addEventListener("input", runFilter);

  document.addEventListener("click", (event) => {
    const link = event.target.closest('a[href^="#glossary-"]');
    if (link && window.docsNavigation) {
      window.docsNavigation.setActiveDocument("glossary", { preserveScroll: true });
    }
  });
})();
