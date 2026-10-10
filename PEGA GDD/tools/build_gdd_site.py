"""Rebuild both documentation panels in index.html from canonical Markdown.

Usage (from the "PEGA GDD" folder): python3 tools/build_gdd_site.py
"""
import html
import os
import re
import unicodedata

BLOCK_TITLES = {
    "objective": "Objetivo", "risk": "Risco", "flow": "Fluxo",
    "decision": "Decisão", "open-question": "Pergunta aberta",
    "configuration": "Configuração", "requirement": "Requisito",
}
CALLOUT_TITLES = {"NOTE": "Note", "INFO": "Info", "IMPORTANT": "Important",
                  "WARNING": "Warning", "TIP": "Tip", "BEST_PRACTICE": "Best Practice"}


def slug(text):
    text = unicodedata.normalize("NFKD", text)
    text = "".join(c for c in text if not unicodedata.combining(c)).lower()
    text = re.sub(r"[^a-z0-9 -]", "", text)
    return text.replace(" ", "-")


def inline_plain(text, prefix):
    text = html.escape(text)
    text = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", text)

    def link(match):
        label, target = match.group(1), match.group(2)
        document = os.path.basename(target.split("#", 1)[0]).lower()
        if document in ("gdd.md", "artbook.md"):
            name = "gdd" if document == "gdd.md" else "artbook"
            anchor = target.partition("#")[2]
            href = f"#{name}-{slug(anchor)}" if anchor else f"#docs-panel-{name}"
            return f'<a href="{href}" data-document-link="{name}">{label}</a>'
        return f'<a href="{target}">{label}</a>'

    def image(match):
        alt, src = match.group(1), match.group(2)
        return f'<img src="{src}" alt="{alt}" loading="lazy">'

    text = re.sub(r"!\[([^\]]*)\]\(([^)]+)\)", image, text)
    return re.sub(r"\[([^\]]+)\]\(([^)]+)\)", link, text)


def inline(text, prefix):
    out, pos = [], 0
    for match in re.finditer(r"`([^`]+)`", text):
        out.append(inline_plain(text[pos:match.start()], prefix))
        out.append("<code>" + html.escape(match.group(1), quote=False) + "</code>")
        pos = match.end()
    out.append(inline_plain(text[pos:], prefix))
    return "".join(out)


def table(rows, prefix):
    cells = [[c.strip() for c in row.strip().strip("|").split("|")] for row in rows]
    head, body = cells[0], cells[2:]
    th = "".join(f"<th>{inline(cell, prefix)}</th>" for cell in head)
    tb = "".join("<tr>" + "".join(f"<td>{inline(cell, prefix)}</td>" for cell in row) + "</tr>" for row in body)
    return (f'<div class="docs-table-wrap"><table class="docs-table"><thead><tr>{th}</tr></thead>'
            f"<tbody>{tb}</tbody></table></div>")


def render(md, prefix, kicker):
    lines, out, in_section, in_fence, code = md.replace("\r\n", "\n").split("\n"), [], False, False, []
    i = 0
    while i < len(lines):
        line, s = lines[i], lines[i].strip()
        if s.startswith("```"):
            if in_fence:
                out.append("<pre><code>" + html.escape("\n".join(code), quote=False) + "</code></pre>")
                code, in_fence = [], False
            else:
                in_fence = True
            i += 1
            continue
        if in_fence:
            code.append(line)
            i += 1
            continue
        if not s:
            i += 1
            continue
        heading = re.match(r"^(#{1,4}) (.+)$", s)
        if heading:
            level, title = len(heading.group(1)), heading.group(2).strip()
            if level == 1:
                out.extend([f'<header class="docs-page-header" id="{prefix}-{slug(title)}" data-title="{html.escape(title)}">',
                            f'<p class="docs-kicker">{kicker}</p>', f"<h2>{inline(title, prefix)}</h2>", "</header>"])
            else:
                if in_section:
                    out.append("</section>")
                out.extend([f'<section class="docs-section" id="{prefix}-{slug(title)}" data-title="{html.escape(title)}">',
                            f"<h{level + 1}>{inline(title, prefix)}</h{level + 1}>"])
                in_section = True
            i += 1
            continue
        if s.startswith(":::"):
            kind, body = s[3:].strip(), []
            i += 1
            while i < len(lines) and lines[i].strip() != ":::":
                body.append(lines[i])
                i += 1
            if i >= len(lines):
                raise ValueError(f"unclosed ::: block at line {i + 1}")
            if kind not in BLOCK_TITLES:
                raise ValueError(f"unknown ::: block '{kind}'")
            i += 1
            rendered_body = render("\n".join(body), prefix, kicker)
            out.append(f'<div class="docs-block docs-block-{kind}"><h4>{BLOCK_TITLES[kind]}</h4>{"".join(rendered_body)}</div>')
            continue
        callout = re.match(r"^> \[!([A-Z_]+)\]\s*(.*)$", s)
        if callout:
            kind, title, body = callout.group(1), callout.group(2).strip() or CALLOUT_TITLES[callout.group(1)], []
            i += 1
            while i < len(lines) and lines[i].startswith(">"):
                body.append(lines[i][1:].lstrip())
                i += 1
            out.append(f'<div class="docs-callout docs-callout-{kind.lower().replace("_", "-")}"><p class="docs-callout__title">{inline(title, prefix)}</p>{"".join(render("\n".join(body), prefix, kicker))}</div>')
            continue
        if s.startswith("|"):
            rows = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                rows.append(lines[i])
                i += 1
            out.append(table(rows, prefix))
            continue
        if re.match(r"^(- |\d+\. )", s):
            ordered = bool(re.match(r"^\d+\. ", s))
            tag = "ol" if ordered else "ul"
            out.append(f"<{tag}>")
            while i < len(lines) and re.match(r"^(- |\d+\. )", lines[i].strip()):
                item = re.sub(r"^(- |\d+\. )", "", lines[i].strip())
                out.append(f"<li>{inline(item, prefix)}</li>")
                i += 1
            out.append(f"</{tag}>")
            continue
        para = []
        while i < len(lines) and lines[i].strip() and not re.match(r"^(#{1,4} |:::|> |\||- |\d+\. |```)", lines[i].strip()):
            para.append(lines[i].strip())
            i += 1
        if not para:
            raise ValueError(f"unhandled line {i + 1}: {s}")
        out.append("<p>" + inline(" ".join(para), prefix) + "</p>")
    if in_fence:
        raise ValueError("unclosed fenced code block")
    if in_section:
        out.append("</section>")
    return out


def replace_panel(page, panel_id, content):
    marker = f'<section id="docs-panel-{panel_id}"'
    panel_start = page.index(marker)
    start_marker = '<article class="docs-content">'
    start = page.index(start_marker, panel_start) + len(start_marker)
    end = page.index("</article>", start)
    return page[:start] + content + page[end:]


def build(root):
    index_path = os.path.join(root, "index.html")
    with open(index_path, encoding="utf-8") as f:
        page = f.read()
    for name, prefix, kicker in (("GDD.md", "gdd", "GDD"), ("ArtBook.md", "artbook", "ART BOOK")):
        with open(os.path.join(root, name), encoding="utf-8") as f:
            panel = "\n".join(render(f.read(), prefix, kicker))
        page = replace_panel(page, prefix, panel)
    with open(index_path, "w", encoding="utf-8") as f:
        f.write(page)


if __name__ == "__main__":
    build(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
