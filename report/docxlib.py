"""Спільні функції оформлення звітів ЛР з БПтД (python-docx), за зразком звітів ПЗ1–2."""
import os

from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # папка Labs
C = WD_ALIGN_PARAGRAPH.CENTER
L = WD_ALIGN_PARAGRAPH.LEFT
REPO = "https://github.com/NureMamishevElmir/bptd-labs"


class Report:
    def __init__(self, num):
        self.num = num
        self.doc = d = Document()
        s = d.sections[0]
        s.page_height, s.page_width = Cm(29.7), Cm(21.0)
        s.left_margin, s.right_margin = Cm(2.5), Cm(1.5)
        s.top_margin, s.bottom_margin = Cm(2.0), Cm(2.0)
        st = d.styles["Normal"]
        st.font.name = "Times New Roman"
        st.font.size = Pt(14)
        st.element.rPr.rFonts.set(qn("w:eastAsia"), "Times New Roman")
        st.paragraph_format.line_spacing = 1.5
        st.paragraph_format.space_after = Pt(0)
        self.fig = self.tab = self.lst = 0

    # ------------------------------------------------------------ текст
    def p(self, text="", bold=False, align=None, size=None, indent=True, italic=False, keep=False, after=0, spacing=None):
        p = self.doc.add_paragraph()
        parts = text.split("**")
        for i, part in enumerate(parts):
            if part:
                r = p.add_run(part)
                r.bold = bold or (i % 2 == 1)
                r.italic = italic
                if size:
                    r.font.size = Pt(size)
        pf = p.paragraph_format
        pf.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY if align is None else align
        pf.first_line_indent = Cm(1.25) if indent and align is None else Cm(0)
        pf.space_after = Pt(after)
        pf.keep_with_next = keep
        if spacing:
            pf.line_spacing = spacing
        return p

    def h1(self, text):
        p = self.p(text.upper(), bold=True, align=C, keep=True, after=12)

    def h2(self, text):
        p = self.p(text, keep=True)
        p.paragraph_format.space_before = Pt(12)
        p.paragraph_format.space_after = Pt(6)

    def bullets(self, items):
        for t in items:
            self.p("– " + t)

    def formula(self, text):
        self.p(text, align=C)

    def page_break(self):
        self.doc.add_paragraph().add_run().add_break(WD_BREAK.PAGE)

    # ------------------------------------------------------------ рисунки
    def image(self, path, caption, width=16):
        self.fig += 1
        p = self.doc.add_paragraph()
        p.paragraph_format.alignment = C
        p.paragraph_format.keep_with_next = True
        p.paragraph_format.space_before = Pt(6)
        p.add_run().add_picture(os.path.join(ROOT, path), width=Cm(width))
        self.p(f"Рисунок {self.num}.{self.fig} – {caption}", align=C, after=6)

    # ------------------------------------------------------------ таблиці
    def table(self, caption, header, rows, widths=None, size=11):
        self.tab += 1
        self.p(f"Таблиця {self.num}.{self.tab} – {caption}", align=L, keep=True, size=12, spacing=1.0)
        t = self.doc.add_table(rows=1, cols=len(header))
        t.style = "Table Grid"
        t.alignment = WD_TABLE_ALIGNMENT.CENTER
        for i, h in enumerate(header):
            self._cell(t.rows[0].cells[i], h, size, True, C)
        trpr = t.rows[0]._tr.get_or_add_trPr()
        th = OxmlElement("w:tblHeader"); th.set(qn("w:val"), "true"); trpr.append(th)
        for row in rows:
            cells = t.add_row().cells
            for i, v in enumerate(row):
                self._cell(cells[i], v, size)
        if widths:
            for row in t.rows:
                for i, w in enumerate(widths):
                    row.cells[i].width = Cm(w)
        self.p("", spacing=1.0)
        return t

    @staticmethod
    def _cell(cell, text, size, bold=False, align=L):
        cell.text = ""
        p = cell.paragraphs[0]
        r = p.add_run(str(text))
        r.bold = bold
        r.font.size = Pt(size)
        p.paragraph_format.alignment = align
        p.paragraph_format.first_line_indent = Cm(0)
        p.paragraph_format.line_spacing = 1.0

    # ------------------------------------------------------------ код
    def code(self, caption, text, size=8):
        self.lst += 1
        self.p(f"Лістинг {self.num}.{self.lst} – {caption}", align=L, keep=True, size=12, spacing=1.0)
        t = self.doc.add_table(rows=1, cols=1)
        t.style = "Table Grid"
        cell = t.rows[0].cells[0]
        cell.text = ""
        tcpr = cell._tc.get_or_add_tcPr()
        shd = OxmlElement("w:shd"); shd.set(qn("w:val"), "clear"); shd.set(qn("w:fill"), "F3F3F3"); tcpr.append(shd)
        for i, ln in enumerate(text.rstrip("\n").replace("\t", "    ").split("\n")):
            p = cell.paragraphs[0] if i == 0 else cell.add_paragraph()
            r = p.add_run(ln if ln else " ")
            r.font.name = "Consolas"
            r.element.rPr.rFonts.set(qn("w:eastAsia"), "Consolas")
            r.font.size = Pt(size)
            pf = p.paragraph_format
            pf.line_spacing = 1.0
            pf.first_line_indent = Cm(0)
            pf.alignment = L
        self.p("", spacing=1.0)

    def file(self, rel, caption=None):
        """Лістинг цілого файлу проєкту (шлях відносно папки Labs)."""
        with open(os.path.join(ROOT, rel), encoding="utf-8-sig") as f:
            src = f.read()
        self.code(caption or f"Файл {rel.split('/')[-1]}", src)

    # ------------------------------------------------------------ титул
    def title(self, topic):
        for line in ("МІНІСТЕРСТВО ОСВІТИ І НАУКИ УКРАЇНИ", "", "ХАРКІВСЬКИЙ НАЦІОНАЛЬНИЙ",
                     "УНІВЕРСИТЕТ РАДІОЕЛЕКТРОНІКИ", "", "Кафедра «Програмна інженерія»"):
            self.p(line, align=C, spacing=1.15)
        for _ in range(6):
            self.p("", align=C)
        self.p("Звіт", align=C, spacing=1.15)
        self.p(f"з лабораторної роботи №{self.num}", align=C, spacing=1.15)
        self.p(f"з дисципліни: «Безпека програм та даних» з теми: «{topic}»", align=C, spacing=1.15)
        for _ in range(5):
            self.p("", align=C)
        t = self.doc.add_table(rows=1, cols=2)
        left = ["Виконав:", "ст. гр. ПЗПІ-23-6", "Мамішев Е. І.", "Варіант №13"]
        right = ["Прийняв:", "ас. каф. ПІ Божко І. К."]
        for cell, lines in ((t.rows[0].cells[0], left), (t.rows[0].cells[1], right)):
            cell.text = ""
            for i, ln in enumerate(lines):
                p = cell.paragraphs[0] if i == 0 else cell.add_paragraph()
                p.add_run(ln)
                p.paragraph_format.alignment = L
                p.paragraph_format.line_spacing = 1.15
        t.rows[0].cells[0].width = Cm(10.5)
        t.rows[0].cells[1].width = Cm(6.5)
        for _ in range(5):
            self.p("", align=C)
        self.p("Харків 2026", align=C)
        self.page_break()

    # ------------------------------------------------------------ контрольні питання
    def questions(self, qa):
        for i, (q, a) in enumerate(qa, 1):
            self.p(f"{i}. {q}", bold=True, keep=True)
            for para in a.strip().split("\n"):
                self.p(para.strip())

    def save(self, rel):
        path = os.path.join(ROOT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.doc.save(path)
        return path
