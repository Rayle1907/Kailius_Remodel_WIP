import json
from pathlib import Path
from xml.sax.saxutils import escape

from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle


def build_styles():
    styles = getSampleStyleSheet()
    styles.add(ParagraphStyle(name="RecordTitle", fontName="Helvetica-Bold", fontSize=22, leading=26, spaceAfter=13, textColor=colors.black))
    styles.add(ParagraphStyle(name="RecordBody", fontName="Helvetica", fontSize=10, leading=14, spaceAfter=7, textColor=colors.black))
    styles.add(ParagraphStyle(name="RecordHeading", fontName="Helvetica-Bold", fontSize=13, leading=17, spaceBefore=13, spaceAfter=7, keepWithNext=True, textColor=colors.black))
    styles.add(ParagraphStyle(name="RecordCode", fontName="Courier", fontSize=8, leading=11, spaceAfter=7, wordWrap="CJK"))
    styles.add(ParagraphStyle(name="RecordSmall", fontName="Helvetica", fontSize=8, leading=11, spaceAfter=4))
    return styles


def build_table(rows, styles):
    data = [[Paragraph(escape(str(cell)), styles["RecordSmall"]) for cell in row] for row in rows]
    widths = [57 * mm, 117 * mm] if len(data[0]) == 2 else None
    table = Table(data, colWidths=widths, repeatRows=1, hAlign="LEFT")
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#e8edf2")),
        ("GRID", (0, 0), (-1, -1), .4, colors.HexColor("#d9d9d9")),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 7), ("RIGHTPADDING", (0, 0), (-1, -1), 7),
        ("TOPPADDING", (0, 0), (-1, -1), 6), ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
    ]))
    return table


def build_footer(record):
    footer_text = f"{record['project']} analytics preprocessing | {record['record_date']}"

    def footer(canvas, doc):
        canvas.setFont("Helvetica", 8)
        canvas.setFillColor(colors.HexColor("#555555"))
        canvas.drawString(18 * mm, 12 * mm, footer_text)
        canvas.drawRightString(192 * mm, 12 * mm, str(doc.page))

    return footer


def render(source, output):
    record = json.loads(Path(source).read_text(encoding="utf-8"))
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    styles = build_styles()
    story = [Paragraph(escape(record["title"]), styles["RecordTitle"]),
             Paragraph(escape(record["subtitle"]), styles["RecordBody"])]
    for section in record["sections"]:
        story.append(Paragraph(escape(section["title"]), styles["RecordHeading"]))
        for text in section.get("paragraphs", []):
            story.append(Paragraph(escape(text), styles["RecordBody"]))
        for command in section.get("commands", []):
            story.append(Paragraph(escape(command), styles["RecordCode"]))
        if section.get("rows"):
            story.extend([build_table(section["rows"], styles), Spacer(1, 3 * mm)])
    footer = build_footer(record)
    SimpleDocTemplate(str(output), pagesize=(210 * mm, 297 * mm), leftMargin=18 * mm,
                      rightMargin=18 * mm, topMargin=18 * mm, bottomMargin=22 * mm,
                      title=record["title"], author="Kailius project").build(story, onFirstPage=footer, onLaterPages=footer)


if __name__ == "__main__":
    root = Path(__file__).resolve().parents[2]
    render(root / "Docs/Analytics/implementation-record.json", root / "Docs/Analytics/Preprocessing-Implementation.pdf")
