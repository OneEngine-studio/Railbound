import os
import sys
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

def build_pdf(filename="payment-gateway_implementation.pdf"):
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
    title_style = ParagraphStyle(
        'DocTitle',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=24,
        leading=28,
        textColor=colors.HexColor("#0F172A")
    )
    subtitle_style = ParagraphStyle(
        'DocSubTitle',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=12,
        leading=16,
        textColor=colors.HexColor("#D97706")
    )
    h1_style = ParagraphStyle(
        'Heading1_Custom',
        parent=styles['Heading1'],
        fontName='Helvetica-Bold',
        fontSize=15,
        leading=19,
        textColor=colors.HexColor("#0F172A"),
        spaceBefore=12,
        spaceAfter=6,
        keepWithNext=True
    )
    h2_style = ParagraphStyle(
        'Heading2_Custom',
        parent=styles['Heading2'],
        fontName='Helvetica-Bold',
        fontSize=11,
        leading=15,
        textColor=colors.HexColor("#1E293B"),
        spaceBefore=8,
        spaceAfter=4,
        keepWithNext=True
    )
    body_style = ParagraphStyle(
        'Body_Custom',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=9,
        leading=13,
        textColor=colors.HexColor("#334155"),
        spaceBefore=2,
        spaceAfter=4
    )
    body_bold = ParagraphStyle(
        'Body_Bold',
        parent=body_style,
        fontName='Helvetica-Bold'
    )
    code_style = ParagraphStyle(
        'Code_Custom',
        parent=styles['Code'],
        fontName='Courier',
        fontSize=7.5,
        leading=10.5,
        textColor=colors.HexColor("#0F172A")
    )
    callout_style = ParagraphStyle(
        'Callout_Text',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=8.5,
        leading=12,
        textColor=colors.HexColor("#0F172A")
    )
    table_cell = ParagraphStyle(
        'TableCell',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=8,
        leading=10.5,
        textColor=colors.HexColor("#1E293B")
    )
    table_cell_bold = ParagraphStyle(
        'TableCellBold',
        parent=table_cell,
        fontName='Helvetica-Bold'
    )
    table_cell_header = ParagraphStyle(
        'TableCellHeader',
        parent=table_cell,
        fontName='Helvetica-Bold',
        textColor=colors.white
    )

    story = []

    # ----------------------------------------------------
    # COVER / HEADER
    # ----------------------------------------------------
    story.append(Spacer(1, 10))
    story.append(Paragraph("RAILBOUND BATTLE ROYALE", subtitle_style))
    story.append(Spacer(1, 4))
    story.append(Paragraph("Payment Gateway & Real Coin Top-Up Architecture", title_style))
    story.append(Spacer(1, 4))
    story.append(Paragraph("Comprehensive Technical Design, Gateway Integration, Database Schema, and Security Specifications", ParagraphStyle(
        'MetaSubtitle', fontName='Helvetica-Oblique', fontSize=9.5, leading=13, textColor=colors.HexColor("#64748B")
    )))
    story.append(Spacer(1, 8))
    story.append(HRFlowable(width="100%", thickness=2, color=colors.HexColor("#D97706"), spaceBefore=2, spaceAfter=10))

    # Meta Info Card
    meta_data = [
        [
            Paragraph("<b>Document Version:</b> 1.0 (Production Blueprint)", table_cell),
            Paragraph("<b>Author / Team:</b> Antigravity Systems & Economy Engineering", table_cell)
        ],
        [
            Paragraph("<b>Target Stack:</b> Unity 2023.1 + Nakama / REST + Reach UI", table_cell),
            Paragraph("<b>Supported Gateways:</b> Stripe, PayPal, Xsolla, Steam MicroTxn", table_cell)
        ],
        [
            Paragraph("<b>Target Currencies:</b> Game Coins (Soft, Future) | Real Coins (Hard, Current)", table_cell),
            Paragraph("<b>Target Goods:</b> Operator Skins, Weapon Camouflages, Gun Attachments", table_cell)
        ]
    ]
    t_meta = Table(meta_data, colWidths=[250, 254])
    t_meta.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), colors.HexColor("#F8FAFC")),
        ('BOX', (0,0), (-1,-1), 1, colors.HexColor("#E2E8F0")),
        ('INNERGRID', (0,0), (-1,-1), 0.5, colors.HexColor("#E2E8F0")),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('LEFTPADDING', (0,0), (-1,-1), 6),
        ('RIGHTPADDING', (0,0), (-1,-1), 6),
    ]))
    story.append(t_meta)
    story.append(Spacer(1, 10))

    # ----------------------------------------------------
    # SECTION 1: EXECUTIVE SUMMARY & CURRENCY TAXONOMY
    # ----------------------------------------------------
    story.append(Paragraph("1. Executive Summary & Dual Currency Taxonomy", h1_style))
    story.append(Paragraph(
        "Railbound operates a battle-royale video game economy requiring two distinct currency streams. "
        "The model guarantees a fair player experience by strictly decoupling gameplay progression rewards from monetization mechanics.",
        body_style
    ))

    # Currency Comparison Table
    curr_data = [
        [
            Paragraph("Attribute", table_cell_header),
            Paragraph("Game Coins (Soft Currency - Deferred)", table_cell_header),
            Paragraph("Real Coins (Hard Currency - IMPLEMENT NOW)", table_cell_header)
        ],
        [
            Paragraph("<b>Acquisition</b>", table_cell),
            Paragraph("Earned in-game (match wins, survival time, daily quests, battle pass free tiers).", table_cell),
            Paragraph("Purchased exclusively with real fiat money ($ USD / EUR / local) through Payment Gateway top-up.", table_cell)
        ],
        [
            Paragraph("<b>Purchasing Power</b>", table_cell),
            Paragraph("Standard tier cosmetics, player badges, temporary boosts, XP progression.", table_cell),
            Paragraph("Exclusive Operator Skins, Tactical Gun Attachments, High-tier Weapon Camouflages, Battle Pass Premium.", table_cell)
        ],
        [
            Paragraph("<b>Balance Source</b>", table_cell),
            Paragraph("Client/Server soft balance ledger.", table_cell),
            Paragraph("<b>Strictly Server-Authoritative</b>. Backed by encrypted transaction ledger and gateway receipts.", table_cell)
        ],
        [
            Paragraph("<b>Implementation State</b>", table_cell),
            Paragraph("Architecture and database columns prepared; gameplay reward hooks deferred.", table_cell),
            Paragraph("<b>ACTIVE DEVELOPMENT</b>: Full end-to-end checkout, webhook validation, and armory shop integration.", table_cell)
        ]
    ]
    t_curr = Table(curr_data, colWidths=[100, 202, 202])
    t_curr.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), colors.HexColor("#1E293B")),
        ('BOX', (0,0), (-1,-1), 1, colors.HexColor("#CBD5E1")),
        ('INNERGRID', (0,0), (-1,-1), 0.5, colors.HexColor("#E2E8F0")),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('LEFTPADDING', (0,0), (-1,-1), 6),
        ('RIGHTPADDING', (0,0), (-1,-1), 6),
        ('BACKGROUND', (0,2), (-1,2), colors.HexColor("#FEF3C7")), # Highlight Real Coins
    ]))
    story.append(t_curr)
    story.append(Spacer(1, 10))

    # ----------------------------------------------------
    # SECTION 2: END-TO-END ARCHITECTURAL FLOW
    # ----------------------------------------------------
    story.append(Paragraph("2. End-to-End Payment Flow & Security Model", h1_style))
    story.append(Paragraph(
        "A critical rule of game security is that <b>the client must NEVER be trusted with payment verification or wallet increments</b>. "
        "A malicious user running memory editors or proxy interceptors could easily fake a success response. "
        "Railbound adopts a 100% server-authoritative, webhook-verified payment pipeline.",
        body_style
    ))

    # Architecture steps card
    flow_steps = [
        [
            Paragraph("Step", table_cell_header),
            Paragraph("Actor / Layer", table_cell_header),
            Paragraph("Action & Architectural Mechanism", table_cell_header)
        ],
        [
            Paragraph("1", table_cell_bold),
            Paragraph("Unity Client", table_cell),
            Paragraph("Player clicks '+' next to Real Coins balance in Lobby Top Bar or Shop Screen. Selects a Coin Bundle (e.g., 1,000 Real Coins for $9.99).", table_cell)
        ],
        [
            Paragraph("2", table_cell_bold),
            Paragraph("Client -> Backend", table_cell),
            Paragraph("Client sends authenticated request: <code>POST /api/payment/create-session</code> with <code>bundle_id</code>, player <code>user_id</code>, and auth token. (Or calls Nakama RPC <code>create_checkout_session</code>).", table_cell)
        ],
        [
            Paragraph("3", table_cell_bold),
            Paragraph("Backend Server", table_cell),
            Paragraph("Validates bundle price server-side. Generates pending order record in database with unique <code>order_id</code>. Calls Stripe/PayPal/Xsolla API to initiate a Checkout Session. Returns secure checkout URL.", table_cell)
        ],
        [
            Paragraph("4", table_cell_bold),
            Paragraph("Unity Client", table_cell),
            Paragraph("Client opens the secure payment page in default system browser (or Steam overlay if Steam build) and starts listening via Nakama WebSocket / polling order status endpoint.", table_cell)
        ],
        [
            Paragraph("5", table_cell_bold),
            Paragraph("Payment Gateway", table_cell),
            Paragraph("Player enters payment details (Card, Apple Pay, Google Pay, PayPal). Gateway processes transaction with bank / card network.", table_cell)
        ],
        [
            Paragraph("6", table_cell_bold),
            Paragraph("Gateway -> Backend", table_cell),
            Paragraph("Gateway dispatches secure server-to-server HTTP POST webhook (e.g. <code>checkout.session.completed</code>) containing HMAC-SHA256 signature in headers.", table_cell)
        ],
        [
            Paragraph("7", table_cell_bold),
            Paragraph("Backend Server", table_cell),
            Paragraph("Verifies cryptographic webhook signature using secret signing key. Updates database order status to 'COMPLETED'. Atomically credits player's Real Coins wallet (<code>wallet_update</code>). Writes immutable audit record.", table_cell)
        ],
        [
            Paragraph("8", table_cell_bold),
            Paragraph("Backend -> Client", table_cell),
            Paragraph("Server pushes real-time notification to active client via Nakama WebSocket (or client order status poller completes). Client plays celebratory audio and smoothly animates coin counter increment.", table_cell)
        ]
    ]
    t_flow = Table(flow_steps, colWidths=[30, 95, 379])
    t_flow.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), colors.HexColor("#0F172A")),
        ('BOX', (0,0), (-1,-1), 1, colors.HexColor("#CBD5E1")),
        ('INNERGRID', (0,0), (-1,-1), 0.5, colors.HexColor("#E2E8F0")),
        ('TOPPADDING', (0,0), (-1,-1), 3.5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3.5),
        ('LEFTPADDING', (0,0), (-1,-1), 5),
        ('RIGHTPADDING', (0,0), (-1,-1), 5),
    ]))
    story.append(t_flow)
    story.append(Spacer(1, 10))

    # ----------------------------------------------------
    # SECTION 3: REAL COIN TOP-UP BUNDLES & PRICING
    # ----------------------------------------------------
    story.append(Paragraph("3. Real Coin Top-Up Bundle Matrix", h1_style))
    story.append(Paragraph(
        "Standard industry bundle pricing using tactical naming and progressive bonus percentages to drive average revenue per paying user (ARPPU):",
        body_style
    ))

    bundles_data = [
        [
            Paragraph("Bundle SKU", table_cell_header),
            Paragraph("Base Real Coins", table_cell_header),
            Paragraph("Bonus Coins", table_cell_header),
            Paragraph("Total Coins", table_cell_header),
            Paragraph("Fiat Price ($USD)", table_cell_header),
            Paragraph("Value Tag", table_cell_header)
        ],
        [
            Paragraph("<code>bundle_rc_100</code>", table_cell),
            Paragraph("100", table_cell),
            Paragraph("0", table_cell),
            Paragraph("100", table_cell_bold),
            Paragraph("$0.99", table_cell),
            Paragraph("Starter Pouch", table_cell)
        ],
        [
            Paragraph("<code>bundle_rc_500</code>", table_cell),
            Paragraph("500", table_cell),
            Paragraph("+50 (10%)", table_cell),
            Paragraph("550", table_cell_bold),
            Paragraph("$4.99", table_cell),
            Paragraph("Tactical Stash", table_cell)
        ],
        [
            Paragraph("<code>bundle_rc_1000</code>", table_cell),
            Paragraph("1,000", table_cell),
            Paragraph("+150 (15%)", table_cell),
            Paragraph("1,150", table_cell_bold),
            Paragraph("$9.99", table_cell),
            Paragraph("<b>POPULAR / BEST VALUE</b>", table_cell)
        ],
        [
            Paragraph("<code>bundle_rc_2500</code>", table_cell),
            Paragraph("2,500", table_cell),
            Paragraph("+500 (20%)", table_cell),
            Paragraph("3,000", table_cell_bold),
            Paragraph("$24.99", table_cell),
            Paragraph("Supply Crate", table_cell)
        ],
        [
            Paragraph("<code>bundle_rc_5000</code>", table_cell),
            Paragraph("5,000", table_cell),
            Paragraph("+1,200 (24%)", table_cell),
            Paragraph("6,200", table_cell_bold),
            Paragraph("$49.99", table_cell),
            Paragraph("Operator Arsenal", table_cell)
        ],
        [
            Paragraph("<code>bundle_rc_10000</code>", table_cell),
            Paragraph("10,000", table_cell),
            Paragraph("+3,000 (30%)", table_cell),
            Paragraph("13,000", table_cell_bold),
            Paragraph("$99.99", table_cell),
            Paragraph("Ironline Vault", table_cell)
        ]
    ]
    t_bundles = Table(bundles_data, colWidths=[95, 75, 75, 75, 84, 100])
    t_bundles.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), colors.HexColor("#1E293B")),
        ('BOX', (0,0), (-1,-1), 1, colors.HexColor("#CBD5E1")),
        ('INNERGRID', (0,0), (-1,-1), 0.5, colors.HexColor("#E2E8F0")),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 5),
        ('RIGHTPADDING', (0,0), (-1,-1), 5),
        ('BACKGROUND', (0,3), (-1,3), colors.HexColor("#FEF3C7")), # Highlight Best Value
    ]))
    story.append(t_bundles)
    story.append(Spacer(1, 10))

    # ----------------------------------------------------
    # SECTION 4: PURCHASABLE ITEMS (SKINS & ATTACHMENTS)
    # ----------------------------------------------------
    story.append(Paragraph("4. Real Coin Shop Catalog (Skins & Gun Attachments)", h1_style))
    story.append(Paragraph(
        "Real Coins can be spent directly in the Lobby Shop Screen. The purchase flow verifies the server-authoritative wallet balance, "
        "deducts the price, and writes the unlocked item into the player's permanent inventory.",
        body_style
    ))

    items_data = [
        [
            Paragraph("Item ID / SKU", table_cell_header),
            Paragraph("Category", table_cell_header),
            Paragraph("Display Name", table_cell_header),
            Paragraph("Target Equipment", table_cell_header),
            Paragraph("Real Coins Price", table_cell_header)
        ],
        [
            Paragraph("<code>skin_op_shadow_operative</code>", table_cell),
            Paragraph("Operator Skin", table_cell),
            Paragraph("Shadow Recon Operative", table_cell_bold),
            Paragraph("Vanguard Character Model", table_cell),
            Paragraph("1,200 Real Coins", table_cell)
        ],
        [
            Paragraph("<code>skin_op_desert_nomad</code>", table_cell),
            Paragraph("Operator Skin", table_cell),
            Paragraph("Desert Nomad Camo", table_cell_bold),
            Paragraph("Assault Character Model", table_cell),
            Paragraph("850 Real Coins", table_cell)
        ],
        [
            Paragraph("<code>att_scope_tactical_acog</code>", table_cell),
            Paragraph("Gun Attachment", table_cell),
            Paragraph("ACOG 4x Tactical Scope", table_cell_bold),
            Paragraph("Assault Rifles & DMRs", table_cell),
            Paragraph("450 Real Coins", table_cell)
        ],
        [
            Paragraph("<code>att_barrel_suppressor_carbon</code>", table_cell),
            Paragraph("Gun Attachment", table_cell),
            Paragraph("Carbon Suppressor (Silencer)", table_cell_bold),
            Paragraph("All Pistols, SMGs, Rifles", table_cell),
            Paragraph("350 Real Coins", table_cell)
        ],
        [
            Paragraph("<code>att_mag_extended_drum</code>", table_cell),
            Paragraph("Gun Attachment", table_cell),
            Paragraph("50-Round Extended Drum Mag", table_cell_bold),
            Paragraph("P226 / Rifles", table_cell),
            Paragraph("400 Real Coins", table_cell)
        ],
        [
            Paragraph("<code>att_grip_vertical_ergonomic</code>", table_cell),
            Paragraph("Gun Attachment", table_cell),
            Paragraph("Ergonomic Vertical Foregrip", table_cell_bold),
            Paragraph("Assault Rifles", table_cell),
            Paragraph("250 Real Coins", table_cell)
        ],
        [
            Paragraph("<code>skin_wp_gold_damascus</code>", table_cell),
            Paragraph("Weapon Camo", table_cell),
            Paragraph("Gold Damascus Finish", table_cell_bold),
            Paragraph("P226 Handgun / Rifle", table_cell),
            Paragraph("650 Real Coins", table_cell)
        ]
    ]
    t_items = Table(items_data, colWidths=[130, 85, 125, 95, 69])
    t_items.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), colors.HexColor("#0F172A")),
        ('BOX', (0,0), (-1,-1), 1, colors.HexColor("#CBD5E1")),
        ('INNERGRID', (0,0), (-1,-1), 0.5, colors.HexColor("#E2E8F0")),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 5),
        ('RIGHTPADDING', (0,0), (-1,-1), 5),
    ]))
    story.append(t_items)
    story.append(Spacer(1, 10))

    # ----------------------------------------------------
    # SECTION 5: PAYMENT GATEWAYS COMPARISON
    # ----------------------------------------------------
    story.append(Paragraph("5. Payment Gateway Provider Analysis", h1_style))
    story.append(Paragraph(
        "Comparison of candidate payment gateways based on technical integration complexity, platform support, fees, and compliance:",
        body_style
    ))

    pg_data = [
        [
            Paragraph("Gateway", table_cell_header),
            Paragraph("Best Use Case", table_cell_header),
            Paragraph("Fee Structure", table_cell_header),
            Paragraph("Pros", table_cell_header),
            Paragraph("Cons", table_cell_header)
        ],
        [
            Paragraph("<b>Stripe</b><br/>(Recommended)", table_cell),
            Paragraph("PC Standalone, WebGL, direct credit/debit card, Apple/Google Pay.", table_cell),
            Paragraph("2.9% + $0.30 per transaction", table_cell),
            Paragraph("World-class Checkout API, instant webhooks, hosted UI minimizes PCI-DSS scope to zero.", table_cell),
            Paragraph("Requires registered business bank account in supported country.", table_cell)
        ],
        [
            Paragraph("<b>PayPal</b>", table_cell),
            Paragraph("Secondary global checkout method for players without credit cards.", table_cell),
            Paragraph("3.49% + $0.49 per transaction", table_cell),
            Paragraph("Huge global user base, trusted brand, simple v2 Orders REST API.", table_cell),
            Paragraph("Higher dispute rates and slightly slower webhook delivery.", table_cell)
        ],
        [
            Paragraph("<b>Xsolla</b>", table_cell),
            Paragraph("Dedicated video game Merchant of Record (MoR).", table_cell),
            Paragraph("5% + processing (MoR handles VAT/Sales Tax)", table_cell),
            Paragraph("Handles global digital tax compliance, fraud filters, 700+ local payment methods worldwide.", table_cell),
            Paragraph("Higher fee percentage, slightly heavier setup process.", table_cell)
        ],
        [
            Paragraph("<b>Steam MicroTxn</b>", table_cell),
            Paragraph("Essential if game is distributed via Valve Steam platform.", table_cell),
            Paragraph("30% standard Steam platform cut", table_cell),
            Paragraph("Seamless one-click Steam Wallet overlay, zero external browser redirect.", table_cell),
            Paragraph("Only usable for Steam builds; 30% revenue share.", table_cell)
        ],
        [
            Paragraph("<b>Mock / Sandbox</b>", table_cell),
            Paragraph("Internal developer testing, CI/CD, QA without real charges.", table_cell),
            Paragraph("$0.00 (Local simulated engine)", table_cell),
            Paragraph("Instant roundtrip testing, triggers webhooks immediately, 100% offline testable.", table_cell),
            Paragraph("For testing/staging only.", table_cell)
        ]
    ]
    t_pg = Table(pg_data, colWidths=[74, 105, 95, 120, 110])
    t_pg.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), colors.HexColor("#1E293B")),
        ('BOX', (0,0), (-1,-1), 1, colors.HexColor("#CBD5E1")),
        ('INNERGRID', (0,0), (-1,-1), 0.5, colors.HexColor("#E2E8F0")),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 5),
        ('RIGHTPADDING', (0,0), (-1,-1), 5),
        ('BACKGROUND', (0,1), (-1,1), colors.HexColor("#EFF6FF")), # Highlight Stripe
    ]))
    story.append(t_pg)
    story.append(Spacer(1, 10))

    # ----------------------------------------------------
    # SECTION 6: DATABASE SCHEMA
    # ----------------------------------------------------
    story.append(Paragraph("6. Database Schema & Data Models", h1_style))
    story.append(Paragraph(
        "A relational SQLite / PostgreSQL schema maintaining atomic ledger transactions and strict idempotency:",
        body_style
    ))

    sql_schema = (
        "-- 1. User Wallets Table (Authoritative Balance)\n"
        "CREATE TABLE user_wallets (\n"
        "    user_id VARCHAR(64) PRIMARY KEY,\n"
        "    real_coins INTEGER NOT NULL DEFAULT 0 CHECK(real_coins >= 0),\n"
        "    game_coins INTEGER NOT NULL DEFAULT 0 CHECK(game_coins >= 0),\n"
        "    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP\n"
        ");\n\n"
        "-- 2. Payment Orders Table (Tracking Gateway Checkout Sessions)\n"
        "CREATE TABLE payment_orders (\n"
        "    order_id VARCHAR(64) PRIMARY KEY,             -- Internal GUID\n"
        "    user_id VARCHAR(64) NOT NULL REFERENCES user_wallets(user_id),\n"
        "    gateway_name VARCHAR(32) NOT NULL,            -- 'stripe', 'paypal', 'xsolla'\n"
        "    gateway_session_id VARCHAR(128) UNIQUE,       -- Stripe cs_xxx or PayPal order_id\n"
        "    bundle_id VARCHAR(64) NOT NULL,               -- e.g. 'bundle_rc_1000'\n"
        "    coins_to_credit INTEGER NOT NULL,             -- e.g. 1150\n"
        "    amount_cents INTEGER NOT NULL,                -- e.g. 999 ($9.99)\n"
        "    currency VARCHAR(3) NOT NULL DEFAULT 'USD',\n"
        "    status VARCHAR(20) NOT NULL DEFAULT 'PENDING',-- 'PENDING', 'COMPLETED', 'FAILED'\n"
        "    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,\n"
        "    completed_at TIMESTAMP NULL\n"
        ");\n\n"
        "-- 3. Immutable Transactions Ledger (Audit Trail & Anti-Replay)\n"
        "CREATE TABLE transaction_ledger (\n"
        "    transaction_id VARCHAR(64) PRIMARY KEY,\n"
        "    user_id VARCHAR(64) NOT NULL REFERENCES user_wallets(user_id),\n"
        "    tx_type VARCHAR(20) NOT NULL,                 -- 'TOPUP', 'PURCHASE', 'REFUND'\n"
        "    currency_type VARCHAR(10) NOT NULL,           -- 'REAL_COIN', 'GAME_COIN'\n"
        "    amount INTEGER NOT NULL,                      -- Delta (+1150 or -450)\n"
        "    balance_after INTEGER NOT NULL,\n"
        "    reference_id VARCHAR(64) NOT NULL,            -- order_id or item_id\n"
        "    metadata_json TEXT,\n"
        "    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP\n"
        ");\n\n"
        "-- 4. Player Inventory Table (Unlocked Skins & Attachments)\n"
        "CREATE TABLE player_inventory (\n"
        "    id INTEGER PRIMARY KEY AUTOINCREMENT,\n"
        "    user_id VARCHAR(64) NOT NULL REFERENCES user_wallets(user_id),\n"
        "    item_id VARCHAR(64) NOT NULL,                 -- e.g. 'att_scope_tactical_acog'\n"
        "    item_category VARCHAR(32) NOT NULL,           -- 'SKIN', 'ATTACHMENT', 'CAMO'\n"
        "    is_equipped BOOLEAN DEFAULT 0,\n"
        "    acquired_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,\n"
        "    UNIQUE(user_id, item_id)\n"
        ");"
    )

    t_code = Table([[Paragraph(f"<pre>{sql_schema}</pre>", code_style)]], colWidths=[504])
    t_code.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), colors.HexColor("#F1F5F9")),
        ('BOX', (0,0), (-1,-1), 1, colors.HexColor("#CBD5E1")),
        ('TOPPADDING', (0,0), (-1,-1), 5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 5),
        ('LEFTPADDING', (0,0), (-1,-1), 8),
        ('RIGHTPADDING', (0,0), (-1,-1), 8),
    ]))
    story.append(t_code)
    story.append(Spacer(1, 10))

    # ----------------------------------------------------
    # SECTION 7: API ENDPOINT SPECIFICATIONS
    # ----------------------------------------------------
    story.append(Paragraph("7. Server REST / Nakama RPC API Specifications", h1_style))
    story.append(Paragraph(
        "Detailed contracts for all client-facing and gateway webhook endpoints:",
        body_style
    ))

    api_specs = [
        [
            Paragraph("Method / Endpoint", table_cell_header),
            Paragraph("Auth", table_cell_header),
            Paragraph("Payload (JSON) & Description", table_cell_header)
        ],
        [
            Paragraph("<b>POST</b><br/><code>/api/payment/create-session</code><br/>(or Nakama RPC <code>create_checkout</code>)", table_cell),
            Paragraph("Player JWT", table_cell),
            Paragraph(
                "<b>Request:</b><br/>"
                "<code>{\"bundle_id\": \"bundle_rc_1000\", \"gateway\": \"stripe\"}</code><br/>"
                "<b>Response:</b><br/>"
                "<code>{\"success\": true, \"order_id\": \"ord_98a72b\", \"checkout_url\": \"https://checkout.stripe.com/c/pay/cs_live_...\"}</code>"
                , table_cell
            )
        ],
        [
            Paragraph("<b>POST</b><br/><code>/api/payment/webhook/stripe</code>", table_cell),
            Paragraph("Header:<br/><code>Stripe-Signature</code>", table_cell),
            Paragraph(
                "Webhook endpoint verified with HMAC-SHA256 signature secret. Extracts <code>client_reference_id</code> (user_id) and <code>order_id</code>. "
                "Credits wallet atomically via database transaction or <code>nk.wallet_update</code>. Idempotent.",
                table_cell
            )
        ],
        [
            Paragraph("<b>GET</b><br/><code>/api/payment/status/{orderId}</code>", table_cell),
            Paragraph("Player JWT", table_cell),
            Paragraph(
                "Polling endpoint for clients when browser redirect returns:<br/>"
                "<code>{\"order_id\": \"ord_98a72b\", \"status\": \"COMPLETED\", \"new_balance\": 1150}</code>",
                table_cell
            )
        ],
        [
            Paragraph("<b>POST</b><br/><code>/api/shop/purchase</code><br/>(or Nakama RPC <code>purchase_shop_item</code>)", table_cell),
            Paragraph("Player JWT", table_cell),
            Paragraph(
                "<b>Request:</b><br/>"
                "<code>{\"item_id\": \"att_scope_tactical_acog\"}</code><br/>"
                "<b>Behavior:</b> Verifies item price (450 Real Coins). Checks wallet balance. Deducts 450. Inserts into <code>player_inventory</code>.<br/>"
                "<b>Response:</b><br/>"
                "<code>{\"success\": true, \"new_balance\": 700, \"item_id\": \"att_scope_tactical_acog\"}</code>"
                , table_cell
            )
        ]
    ]
    t_api = Table(api_specs, colWidths=[120, 75, 309])
    t_api.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), colors.HexColor("#0F172A")),
        ('BOX', (0,0), (-1,-1), 1, colors.HexColor("#CBD5E1")),
        ('INNERGRID', (0,0), (-1,-1), 0.5, colors.HexColor("#E2E8F0")),
        ('TOPPADDING', (0,0), (-1,-1), 3.5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3.5),
        ('LEFTPADDING', (0,0), (-1,-1), 5),
        ('RIGHTPADDING', (0,0), (-1,-1), 5),
    ]))
    story.append(t_api)
    story.append(Spacer(1, 10))

    # ----------------------------------------------------
    # SECTION 8: UNITY CLIENT INTEGRATION
    # ----------------------------------------------------
    story.append(Paragraph("8. Unity Client Implementation & UI Design", h1_style))
    story.append(Paragraph(
        "The client architecture connects seamlessly with Reach UI and the existing <code>LobbyUIController</code>:",
        body_style
    ))

    # Client components bullet list
    client_comp_data = [
        [
            Paragraph("Component", table_cell_header),
            Paragraph("File Path / Location", table_cell_header),
            Paragraph("Functionality & UI Behavior", table_cell_header)
        ],
        [
            Paragraph("<b>Top Bar Currency HUD</b>", table_cell_bold),
            Paragraph("<code>LobbyUIController.cs</code><br/><code>GameLobby.unity</code>", table_cell),
            Paragraph(
                "Displays Game Coins (soft, grayed out '0' placeholder) and Real Coins (tactical yellow gold coin icon + dynamic counter + '+' Top-Up button). "
                "Features smooth numeric tweening on balance change.",
                table_cell
            )
        ],
        [
            Paragraph("<b>Payment Service Manager</b>", table_cell_bold),
            Paragraph("<code>Assets/Project/Scripts/Economy/PaymentManager.cs</code>", table_cell),
            Paragraph(
                "Singleton handling HTTP/RPC requests to create checkout sessions, opens browser URLs via <code>Application.OpenURL()</code>, "
                "listens to Nakama wallet updates, and triggers celebratory popups.",
                table_cell
            )
        ],
        [
            Paragraph("<b>Shop & Top-Up UI Modal</b>", table_cell_bold),
            Paragraph("<code>Assets/Project/Scripts/Economy/LobbyShopUI.cs</code><br/>Prefab: <code>Shop Screen.prefab</code>", table_cell),
            Paragraph(
                "Full-featured Reach UI modal featuring two main tabs: <b>1. Top-Up (Buy Real Coins)</b> with bundle cards and real money prices; "
                "<b>2. Armory (Skins & Attachments)</b> with 3D character/weapon preview, purchase confirmation, and equip state toggles.",
                table_cell
            )
        ],
        [
            Paragraph("<b>Gun Attachment & Skin Equip Bridge</b>", table_cell_bold),
            Paragraph("<code>Assets/Project/Scripts/Economy/ArmoryEquipBridge.cs</code>", table_cell),
            Paragraph(
                "Applies purchased attachments (ACOG scope, suppressor) to the player's weapon prefabs (e.g. <code>Weapon.cs</code>) "
                "and character skin retargeting (<code>CharacterSkinRetargeter.cs</code>).",
                table_cell
            )
        ]
    ]
    t_client = Table(client_comp_data, colWidths=[120, 130, 254])
    t_client.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), colors.HexColor("#1E293B")),
        ('BOX', (0,0), (-1,-1), 1, colors.HexColor("#CBD5E1")),
        ('INNERGRID', (0,0), (-1,-1), 0.5, colors.HexColor("#E2E8F0")),
        ('TOPPADDING', (0,0), (-1,-1), 3.5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3.5),
        ('LEFTPADDING', (0,0), (-1,-1), 5),
        ('RIGHTPADDING', (0,0), (-1,-1), 5),
    ]))
    story.append(t_client)
    story.append(Spacer(1, 10))

    # ----------------------------------------------------
    # SECTION 9: SECURITY, FRAUD PREVENTION & COMPLIANCE
    # ----------------------------------------------------
    story.append(Paragraph("9. Security, Fraud Prevention & PCI Compliance", h1_style))
    security_points = [
        "<b>1. Zero-PCI Scope:</b> The Unity game client never collects, transmits, or stores credit card numbers. All card handling happens on Stripe/PayPal's PCI-DSS Level 1 certified hosted checkout pages.",
        "<b>2. Cryptographic Webhook Signatures:</b> Webhooks are validated using HMAC-SHA256 signature verification (e.g., Stripe <code>Stripe-Signature</code> header) using the secret webhook key. Untrusted or forged webhooks are rejected immediately.",
        "<b>3. Idempotent Processing:</b> The backend uses the gateway's unique transaction/event ID as an idempotency key. Duplicate webhook deliveries will never double-credit coins.",
        "<b>4. Database Atomic Transactions:</b> Order status changes and wallet increments occur within a single database ACID transaction (<code>BEGIN TRANSACTION ... COMMIT</code>).",
        "<b>5. Anti-Replay Nonce:</b> Every payment session has a short time-to-live (e.g. 30 minutes) and is invalidated once completed.",
        "<b>6. Server-Authoritative Price Validation:</b> The client sends only <code>bundle_id</code>, never the coin count or price amount. Prices are resolved strictly from server-side configurations."
    ]
    for p in security_points:
        story.append(Paragraph(p, body_style))
    story.append(Spacer(1, 10))

    # ----------------------------------------------------
    # SECTION 10: IMPLEMENTATION PHASES & ROADMAP
    # ----------------------------------------------------
    story.append(Paragraph("10. Execution Roadmap & Verification Milestones", h1_style))

    roadmap_data = [
        [
            Paragraph("Phase", table_cell_header),
            Paragraph("Core Objectives", table_cell_header),
            Paragraph("Deliverables & Verification Criteria", table_cell_header)
        ],
        [
            Paragraph("<b>Phase 1</b><br/>Economy Foundation", table_cell_bold),
            Paragraph("Database tables, C# data models, and local mock payment simulator.", table_cell),
            Paragraph("<code>user_wallets</code>, <code>payment_orders</code>, <code>PaymentManager.cs</code> with Mock Sandbox toggle. Unit test verifying simulated checkout and balance crediting.", table_cell)
        ],
        [
            Paragraph("<b>Phase 2</b><br/>Top Bar & Shop UI", table_cell_bold),
            Paragraph("Reach UI integration for Top Bar Real Coins display and Shop Modal.", table_cell),
            Paragraph("Top Bar '+' button opens Shop Modal. Coin Bundles rendered with prices. Real Coins counter animates smoothly when balance changes. Audio SFX plays.", table_cell)
        ],
        [
            Paragraph("<b>Phase 3</b><br/>Armory Item Shop", table_cell_bold),
            Paragraph("Skin and gun attachment catalog in Shop Screen.", table_cell),
            Paragraph("Item purchase confirmation modal. Server deduction of Real Coins. Instant equip/preview in Lobby character viewport.", table_cell)
        ],
        [
            Paragraph("<b>Phase 4</b><br/>Live Gateway Webhooks", table_cell_bold),
            Paragraph("Stripe / PayPal sandbox backend webhook integration.", table_cell),
            Paragraph("End-to-end sandbox purchase with test credit card (4242...). Webhook verified and wallet credited automatically. Production deployment checklist.", table_cell)
        ]
    ]
    t_roadmap = Table(roadmap_data, colWidths=[80, 160, 264])
    t_roadmap.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), colors.HexColor("#0F172A")),
        ('BOX', (0,0), (-1,-1), 1, colors.HexColor("#CBD5E1")),
        ('INNERGRID', (0,0), (-1,-1), 0.5, colors.HexColor("#E2E8F0")),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('LEFTPADDING', (0,0), (-1,-1), 5),
        ('RIGHTPADDING', (0,0), (-1,-1), 5),
    ]))
    story.append(t_roadmap)

    # Build PDF with custom NumberedCanvas
    doc.build(story, canvasmaker=NumberedCanvas)
    print(f"Generated PDF successfully: {filename}")

if __name__ == "__main__":
    out_pdf = "payment-gateway_implementation.pdf"
    if len(sys.argv) > 1:
        out_pdf = sys.argv[1]
    build_pdf(out_pdf)
