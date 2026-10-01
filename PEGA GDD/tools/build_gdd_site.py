"""Rebuild the Game Design Document panel of index.html from source/gdd.md.

Usage (from the "PEGA GDD" folder): python3 tools/build_gdd_site.py

Only the GDD panel is regenerated; the Art Book panel, layout, CSS and JS stay as they are.
"""
import html
import os
import re
import sys
import unicodedata

BLOCK_TITLES = {
    "objective": "Objetivo",
    "risk": "Risco",
    "flow": "Fluxo",
    "decision": "Decisão",
    "open-question": "Pergunta aberta",
    "configuration": "Configuração",
    "requirement": "Requisito",
}
CALLOUT_TITLES = {"NOTE": "Note", "INFO": "Info", "IMPORTANT": "Important", "WARNING": "Warning", "TIP": "Tip", "BEST_PRACTICE": "Best Practice"}


def slug(text):
    text = unicodedata.normalize("NFKD", text)
    text = "".join(c for c in text if not unicodedata.combining(c)).lower()
    text = re.sub(r"[^a-z0-9 -]", "", text)
    return text.replace(" ", "-")


def inline(text):
    out = []
    pos = 0
    for m in re.finditer(r"`([^`]+)`", text):
        out.append(inline_plain(text[pos:m.start()]))
        out.append("<code>" + html.escape(m.group(1), quote=False) + "</code>")
        pos = m.end()
    out.append(inline_plain(text[pos:]))
    return "".join(out)


def inline_plain(text):
    text = html.escape(text)
    text = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", text)

    def link(m):
        label, target = m.group(1), m.group(2)
        if target.startswith("ArtBook.md"):
            anchor = target.partition("#")[2]
            href = "#artbook-" + slug(anchor) if anchor else "#docs-panel-artbook"
            return f'<a href="{href}" data-document-link="artbook">{label}</a>'
        return f'<a href="{target}">{label}</a>'

    return re.sub(r"\[([^\]]+)\]\(([^)]+)\)", link, text)


def table(rows):
    cells = [[c.strip() for c in r.strip().strip("|").split("|")] for r in rows]
    head, body = cells[0], cells[2:]
    th = "".join(f"<th>{inline(c)}</th>" for c in head)
    tb = "".join("<tr>" + "".join(f"<td>{inline(c)}</td>" for c in r) + "</tr>" for r in body)
    return (f'<div class="docs-table-wrap"><table class="docs-table"><thead><tr>{th}</tr></thead>'
            f"<tbody>{tb}</tbody></table></div>")


def body_html(lines, prefix, kicker):
    return "".join(render("\n".join(lines), prefix, kicker))


def render(md, prefix, kicker):
    lines = md.replace("\r\n", "\n").split("\n")
    out = []
    in_section = False
    i = 0
    while i < len(lines):
        line = lines[i]
        s = line.strip()
        if not s:
            i += 1
            continue
        m = re.match(r"^(#{1,4}) (.+)$", s)
        if m:
            level, title = len(m.group(1)), m.group(2).strip()
            if level == 1:
                out.append(f'<header class="docs-page-header" id="{prefix}-{slug(title)}" data-title="{html.escape(title)}">')
                out.append(f'<p class="docs-kicker">{kicker}</p>')
                out.append(f"<h2>{inline(title)}</h2>")
                out.append("</header>")
            else:
                if in_section:
                    out.append("</section>")
                out.append(f'<section class="docs-section" id="{prefix}-{slug(title)}" data-title="{html.escape(title)}">')
                out.append(f"<h{level + 1}>{inline(title)}</h{level + 1}>")
                in_section = True
            i += 1
            continue
        if s.startswith(":::"):
            kind = s[3:].strip()
            body = []
            i += 1
            while lines[i].strip() != ":::":
                body.append(lines[i])
                i += 1
            i += 1
            out.append(f'<div class="docs-block docs-block-{kind}"><h4>{BLOCK_TITLES[kind]}</h4>{body_html(body, prefix, kicker)}</div>')
            continue
        m = re.match(r"^> \[!([A-Z_]+)\]\s*(.*)$", s)
        if m:
            kind, title = m.group(1), m.group(2).strip() or CALLOUT_TITLES[m.group(1)]
            body = []
            i += 1
            while i < len(lines) and lines[i].startswith(">"):
                body.append(lines[i][1:])
                i += 1
            out.append(f'<div class="docs-callout docs-callout-{kind.lower().replace("_", "-")}"><p class="docs-callout__title">{inline(title)}</p>{body_html(body, prefix, kicker)}</div>')
            continue
        if s.startswith("|"):
            rows = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                rows.append(lines[i])
                i += 1
            out.append(table(rows))
            continue
        if re.match(r"^(- |\d+\. )", s):
            ordered = bool(re.match(r"^\d+\. ", s))
            tag = "ol" if ordered else "ul"
            out.append(f"<{tag}>")
            while i < len(lines) and re.match(r"^(- |\d+\. )", lines[i].strip()):
                item = re.sub(r"^(- |\d+\. )", "", lines[i].strip())
                out.append(f"<li>{inline(item)}</li>")
                i += 1
            out.append(f"</{tag}>")
            continue
        para = []
        while i < len(lines) and lines[i].strip() and not re.match(r"^(#{1,4} |:::|> |\||- |\d+\. )", lines[i].strip()):
            para.append(lines[i].strip())
            i += 1
        if not para:
            raise ValueError(f"unhandled line {i + 1}: {s}")
        out.append("<p>" + inline(" ".join(para)) + "</p>")
    if in_section:
        out.append("</section>")
    return out


def build(root):
    with open(os.path.join(root, "source", "gdd.md"), encoding="utf-8") as f:
        panel = "\n".join(render(f.read(), "gdd", "GDD"))
    index_path = os.path.join(root, "index.html")
    with open(index_path, encoding="utf-8") as f:
        page = f.read()
    marker = '<section id="docs-panel-gdd"'
    start = page.index('<article class="docs-content">', page.index(marker)) + len('<article class="docs-content">')
    end = page.index("</article>", start)
    with open(index_path, "w", encoding="utf-8") as f:
        f.write(page[:start] + panel + page[end:])


if __name__ == "__main__":
    build(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
