"""Render the GitClear user guide's markdown subset to print-ready HTML.

Handles this markdown subset only: h1/h2, paragraphs, horizontal rules,
pipe tables, fenced code blocks, blockquotes, bullet and ordered lists (with
multi-paragraph items), and inline bold / italic / code.
"""
import html
import re
import sys

CODE_TOKEN = "\x00CODE%d\x00"


def inline(text):
    """Escape HTML, then apply inline markup. Code spans are protected first."""
    spans = []

    def stash(match):
        spans.append(html.escape(match.group(1)))
        return CODE_TOKEN % (len(spans) - 1)

    text = re.sub(r"`([^`]+)`", stash, text)
    text = html.escape(text)
    text = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", text)
    text = re.sub(r"(?<!\*)\*([^*]+)\*(?!\*)", r"<em>\1</em>", text)
    for index, code in enumerate(spans):
        text = text.replace(CODE_TOKEN % index, "<code>%s</code>" % code)
    return text


def is_block_start(line):
    stripped = line.strip()
    return (
        not stripped
        or stripped.startswith(("```", "|", ">", "#", "---"))
        or re.match(r"^- ", stripped)
        or re.match(r"^\d+\. ", stripped)
    )


def render_table(rows):
    head = [cell.strip() for cell in rows[0].strip().strip("|").split("|")]
    body = [
        [cell.strip() for cell in row.strip().strip("|").split("|")]
        for row in rows[2:]
    ]
    out = ["<table>", "<thead><tr>"]
    out += ["<th>%s</th>" % inline(cell) for cell in head]
    out.append("</tr></thead><tbody>")
    for row in body:
        out.append("<tr>" + "".join("<td>%s</td>" % inline(c) for c in row) + "</tr>")
    out.append("</tbody></table>")
    return "".join(out)


def continues_list(lines, index, marker):
    """Index of the next item in this list, skipping blank lines; None if the list ended."""
    probe = index
    while probe < len(lines) and not lines[probe].strip():
        probe += 1
    if probe < len(lines) and re.match(marker, lines[probe]):
        return probe
    return None


def gather_item(lines, index, marker_len):
    """Collect one list item: its first line plus indented continuations."""
    first = lines[index].strip()
    body = [first[marker_len:].strip()]
    index += 1
    paragraphs = [body]
    while index < len(lines):
        line = lines[index]
        if not line.strip():
            # A blank line continues the item only if indented text follows.
            if index + 1 < len(lines) and lines[index + 1].startswith("   "):
                paragraphs.append([])
                index += 1
                continue
            break
        if line.startswith("  ") and not re.match(r"^\s*(- |\d+\. )", line):
            paragraphs[-1].append(line.strip())
            index += 1
            continue
        break
    chunks = [" ".join(p) for p in paragraphs if p]
    return "".join("<p>%s</p>" % inline(c) for c in chunks), index


def render(markdown):
    lines = markdown.replace("\r\n", "\n").split("\n")
    out = []
    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()

        if not stripped:
            i += 1
            continue

        if stripped.startswith("```"):
            i += 1
            code = []
            while i < len(lines) and not lines[i].strip().startswith("```"):
                code.append(lines[i])
                i += 1
            i += 1
            out.append("<pre>%s</pre>" % html.escape("\n".join(code)))
            continue

        if stripped == "---":
            out.append("<hr>")
            i += 1
            continue

        if stripped.startswith("#"):
            level = len(stripped) - len(stripped.lstrip("#"))
            out.append("<h%d>%s</h%d>" % (level, inline(stripped[level:].strip()), level))
            i += 1
            continue

        if stripped.startswith("|"):
            rows = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                rows.append(lines[i])
                i += 1
            out.append(render_table(rows))
            continue

        if stripped.startswith(">"):
            quoted = []
            while i < len(lines) and lines[i].strip().startswith(">"):
                quoted.append(re.sub(r"^\s*>\s?", "", lines[i]))
                i += 1
            out.append("<blockquote>%s</blockquote>" % render("\n".join(quoted)))
            continue

        if re.match(r"^- ", stripped):
            items = []
            while True:
                item, i = gather_item(lines, i, 2)
                items.append(item)
                following = continues_list(lines, i, r"^\s*- ")
                if following is None:
                    break
                i = following
            out.append("<ul>%s</ul>" % "".join("<li>%s</li>" % x for x in items))
            continue

        if re.match(r"^\d+\. ", stripped):
            items = []
            while True:
                marker = re.match(r"^\s*(\d+\. )", lines[i]).group(1)
                item, i = gather_item(lines, i, len(marker))
                items.append(item)
                following = continues_list(lines, i, r"^\s*\d+\. ")
                if following is None:
                    break
                i = following
            out.append("<ol>%s</ol>" % "".join("<li>%s</li>" % x for x in items))
            continue

        para = [stripped]
        i += 1
        while i < len(lines) and not is_block_start(lines[i]):
            para.append(lines[i].strip())
            i += 1
        out.append("<p>%s</p>" % inline(" ".join(para)))

    return "".join(out)


CSS = """
@page { size: A4; margin: 18mm 16mm 16mm 16mm; }
body { font-family: "Segoe UI", Calibri, sans-serif; font-size: 10.5pt;
       line-height: 1.5; color: #1a1a1a; margin: 0; }
h1 { font-size: 23pt; margin: 0 0 4pt; color: #14395c; letter-spacing: -0.4pt; }
h2 { font-size: 14pt; margin: 20pt 0 6pt; color: #14395c;
     border-bottom: 1px solid #cfd8e0; padding-bottom: 3pt;
     break-after: avoid; page-break-after: avoid; }
p { margin: 0 0 7pt; }
ul, ol { margin: 0 0 8pt; padding-left: 20pt; }
li { margin-bottom: 4pt; }
li > p { margin: 0 0 4pt; }
code { font-family: Consolas, "Courier New", monospace; font-size: 9.2pt;
       background: #f2f4f7; padding: 0.5pt 3pt; border-radius: 3px;
       border: 1px solid #e2e6ea; }
pre { font-family: Consolas, "Courier New", monospace; font-size: 8.4pt;
      line-height: 1.35; background: #f7f9fb; border: 1px solid #dde3e9;
      border-radius: 4px; padding: 8pt 10pt; overflow: visible;
      white-space: pre; break-inside: avoid; page-break-inside: avoid;
      margin: 0 0 9pt; }
pre code { background: none; border: none; padding: 0; font-size: inherit; }
table { border-collapse: collapse; width: 100%; margin: 0 0 10pt;
        font-size: 9.8pt; break-inside: avoid; page-break-inside: avoid; }
th, td { border: 1px solid #cfd8e0; padding: 5pt 7pt; text-align: left;
         vertical-align: top; }
th { background: #eef2f6; font-weight: 600; }
blockquote { margin: 0 0 9pt; padding: 7pt 11pt; background: #fbf7ec;
             border-left: 3px solid #d9ab3c; break-inside: avoid; }
blockquote p { margin: 0 0 4pt; }
blockquote p:last-child { margin-bottom: 0; }
hr { border: none; border-top: 1px solid #e4e9ee; margin: 14pt 0; }
strong { font-weight: 600; }
"""


def main():
    source, target, title = sys.argv[1], sys.argv[2], sys.argv[3]
    with open(source, encoding="utf-8") as handle:
        body = render(handle.read())
    page = (
        "<!doctype html><html><head><meta charset='utf-8'>"
        "<title>%s</title><style>%s</style></head><body>%s</body></html>"
        % (html.escape(title), CSS, body)
    )
    with open(target, "w", encoding="utf-8") as handle:
        handle.write(page)
    print("wrote %s (%d bytes)" % (target, len(page)))


if __name__ == "__main__":
    main()
