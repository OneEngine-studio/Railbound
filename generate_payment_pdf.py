import os
import sys
from datetime import datetime
from reportlab.lib.pagesizes import letter
from reportlab.lib import colors
from reportlab.lib.units import inch
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, KeepTogether, HRFlowable
)
from reportlab.pdfgen import canvas

class NumberedCanvas(canvas.Canvas):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self._saved_page_states)
        for state in self._saved_page_states:
            self.__dict__.update(state)
            self.draw_page_decorations(num_pages)
            super().showPage()
        super().save()

    def draw_page_decorations(self, page_count):
        self.saveState()
        # Top running header (pages > 1)
        if self._pageNumber > 1:
            self.setFont("Helvetica", 8)
            self.setFillColor(colors.HexColor("#7A8B9E"))
            self.drawString(54, 11 * inch - 36, "RAILBOUND  |  Game Payment Gateway & Real Coin Top-Up Specification")
            self.setStrokeColor(colors.HexColor("#2A3B4C"))
            self.setLineWidth(0.5)
            self.line(54, 11 * inch - 42, 8.5 * inch - 54, 11 * inch - 42)

        # Bottom footer (all pages)
        self.setFont("Helvetica-Bold", 8)
        self.setFillColor(colors.HexColor("#FFB800"))
        self.drawString(54, 30, "CONFIDENTIAL & ARCHITECTURAL BLUEPRINT")
        self.setFont("Helvetica", 8)
        self.setFillColor(colors.HexColor("#7A8B9E"))
        page_str = f"Page {self._pageNumber} of {page_count}"
        self.drawRightString(8.5 * inch - 54, 30, page_str)
        self.setStrokeColor(colors.HexColor("#2A3B4C"))
        self.setLineWidth(0.5)
        self.line(54, 42, 8.5 * inch - 54, 42)
        self.restoreState()

def build_pdf(filename="payment-gateway_implementation_v2.pdf"):
    doc = SimpleDocTemplate(
        filename,
        pagesize=letter,
        leftMargin=54,
        rightMargin=54,
        topMargin=54,
        bottomMargin=54
    )

    styles = getSampleStyleSheet()

    # Custom styles
    title_style = ParagraphStyle('DocTitle', parent=styles['Normal'], fontName='Helvetica-Bold', fontSize=24, leading=28, textColor=colors.HexColor("#0F172A"))
    subtitle_style = ParagraphStyle('DocSubTitle', parent=styles['Normal'], fontName='Helvetica', fontSize=12, leading=16, textColor=colors.HexColor("#D97706"))
    h1_style = ParagraphStyle('Heading1_Custom', parent=styles['Heading1'], fontName='Helvetica-Bold', fontSize=15, leading=19, textColor=colors.HexColor("#0F172A"), spaceBefore=12, spaceAfter=6, keepWithNext=True)
    body_style = ParagraphStyle('Body_Custom', parent=styles['Normal'], fontName='Helvetica', fontSize=9, leading=13, textColor=colors.HexColor("#334155"), spaceBefore=2,
