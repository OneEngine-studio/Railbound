# Lobby UI — Recommended Hierarchy & RectTransform Spec

**Scene:** `Assets/Project/GameLobby.unity`
**Controller:** `Assets/Project/Scripts/LobbyUIController.cs`
**Reference resolution:** 1920 × 1080 · **Canvas Scaler:** Scale With Screen Size · **Match:** 0.5 (width/height)

---

## Hierarchy tree

```
Canvas (Screen Space - Overlay)
├── TopBar (1) ─────────────────────────────── Anchors: Top-Stretch
│   ├── LogoBadge ─────────────────────────── Left, fixed size
│   ├── NavTabs ───────────────────────────── Center-Left, HorizontalLayoutGroup
│   │   ├── Tab_Lobby  (active)
│   │   ├── Tab_Seasons
│   │   ├── Tab_Challengers
│   │   ├── Tab_Equipment
│   │   └── Tab_Shop
│   ├── UtilityIcons ──────────────────────── Right, HorizontalLayoutGroup
│   │   ├── FriendsButton (+ FriendsCountText "5")
│   │   ├── NotificationButton
│   │   ├── MailButton
│   │   ├── SettingsButton
│   │   └── PowerButton
│   └── CurrencyCounter ───────────────────── Right, below UtilityIcons
│       ├── CurrencyIcon (green gem)
│       └── CurrencyText ("2350")
│
├── CharacterViewport (2) ──────────────────── Center, full-bleed background
│   ├── PlayerTag ─────────────────────────── Floating above model
│   │   ├── LevelBadge ("32")
│   │   ├── UsernameText ("wtxinc")
│   │   └── StatsSubtitleText ("Show statistics")
│   ├── PartySlotLeft ("+") ───────────────── Left of model
│   └── PartySlotRight ("+") ──────────────── Right of model
│
├── LeftPanel (3) ──────────────────────────── Anchors: Left-Stretch
│   ├── SeasonHeader
│   │   ├── SeasonTitleText ("SEASON 2")
│   │   └── WeekSubtitleText ("WEEK 3", red block)
│   ├── XpSection
│   │   ├── XpCounterText ("600 / 1000")
│   │   ├── XpBarBackground
│   │   └── XpFillImage (Filled, horizontal)
│   ├── CheckAwardsButton
│   └── QuestScrollView (ScrollRect)
│       └── QuestContent (VerticalLayoutGroup)
│           ├── QuestRow (prefab) × 6
│           │   ├── QuestTitleText
│           │   ├── QuestFractionText
│           │   ├── QuestRewardBadge
│           │   └── QuestFillImage
│           └── ...
│
└── BottomRightPanel (4) ──────────────────── Anchors: Bottom-Right
    ├── ChallengerCard
    │   ├── ChallengerTitleText ("New Challenger")
    │   ├── ChallengerNameText ("Luna")
    │   └── ChallengerPortrait
    ├── ReadyButton (large, gold)
    │   ├── ReadyGlow
    │   └── ReadyText ("READY")
    └── GameModeRow
        ├── GameModeText ("Game Mode : Solo")
        └── GameModeSettingsButton (gear)
```

---

## RectTransform specifications

### Canvas
| Property | Value |
|---|---|
| Render Mode | Screen Space - Overlay |
| Reference Resolution | 1920 × 1080 |
| Scale Mode | Scale With Screen Size |
| Match | 0.5 |
| Sorting Order | 0 |

---

### 1. TopBar — Top-Stretch
| Property | Value |
|---|---|
| Anchor Min | (0, 1) |
| Anchor Max | (1, 1) |
| Pivot | (0.5, 1) |
| Offset Min | (0, -120) |
| Offset Max | (0, 0) |
| Layout | HorizontalLayoutGroup: Spacing=24, Child Alignment=Middle Left, Control Child Size=Width/Height, Force Expand=Off |

**Children:**

| Element | Anchor | Pivot | Size Delta | Notes |
|---|---|---|---|---|
| LogoBadge | Middle Left | (0, 0.5) | (120, 48) | Fixed width, left margin 32 |
| NavTabs | Middle Left | (0, 0.5) | (auto, 48) | HorizontalLayoutGroup, Spacing=32 |
| UtilityIcons | Middle Right | (1, 0.5) | (auto, 48) | HorizontalLayoutGroup, Spacing=16 |
| CurrencyCounter | Middle Right | (1, 0.5) | (auto, 40) | Below UtilityIcons, offset Y=-56 |

**NavTab item structure:**
```
Tab_Lobby (Button)
├── LabelText (TMP) ─────────── "Lobby", white → goldAccent when active
├── ActiveIndicator (Image) ─── red bottom border, 4×2px, visible only when active
└── HoverGlow (Image) ───────── subtle cyan glow on hover
```

---

### 2. CharacterViewport — Center full-bleed
| Property | Value |
|---|---|
| Anchor Min | (0, 0) |
| Anchor Max | (1, 1) |
| Pivot | (0.5, 0.5) |
| Offset Min | (0, 0) |
| Offset Max | (0, 0) |
| Notes | Full-screen background; 3D model rendered via Render Texture or Camera |

**Children:**

| Element | Anchor | Pivot | Size Delta | Notes |
|---|---|---|---|---|
| PlayerTag | Top Center | (0.5, 1) | (280, 100) | Offset Y=-160 from top |
| PartySlotLeft | Middle Left | (0.5, 0.5) | (64, 64) | Offset X=-320, circular mask |
| PartySlotRight | Middle Right | (0.5, 0.5) | (64, 64) | Offset X=+320, circular mask |

**PlayerTag structure:**
```
PlayerTag (VerticalLayoutGroup, Spacing=4)
├── LevelBadge (Image + TMP "32") ── 40×40, cyan border
├── UsernameText (TMP) ───────────── "wtxinc", bold, white
└── StatsSubtitleText (TMP) ──────── "Show statistics", small, gray
```

---

### 3. LeftPanel — Left-Stretch
| Property | Value |
|---|---|
| Anchor Min | (0, 0) |
| Anchor Max | (0, 1) |
| Pivot | (0, 0.5) |
| Offset Min | (32, -180) |
| Offset Max | (420, -40) |
| Layout | VerticalLayoutGroup: Spacing=16, Child Alignment=Upper Left, Control Child Size=Width, Force Expand=Width |

**Children:**

| Element | Layout | Notes |
|---|---|---|
| SeasonHeader | HorizontalLayoutGroup | SeasonTitleText + WeekSubtitleText side by side |
| XpSection | VerticalLayoutGroup | XpCounterText + XpBarBackground(200×12) + XpFillImage |
| CheckAwardsButton | — | Full width, 40px height |
| QuestScrollView | — | ScrollRect, Viewport mask, QuestContent inside |

**QuestRow prefab structure:**
```
QuestRow (HorizontalLayoutGroup, Spacing=12)
├── QuestTitleText (TMP) ────────── flex width, wraps
├── QuestFractionText (TMP) ─────── "2 / 10", fixed 80px
├── QuestRewardBadge (Image+TMP) ── "500 XP", cyan border
└── QuestFillImage (Image) ──────── thin progress bar at bottom, Filled
```

---

### 4. BottomRightPanel — Bottom-Right
| Property | Value |
|---|---|
| Anchor Min | (1, 0) |
| Anchor Max | (1, 0) |
| Pivot | (1, 0) |
| Offset Min | (-520, 32) |
| Offset Max | (-32, 320) |
| Layout | VerticalLayoutGroup: Spacing=16, Child Alignment=Lower Right |

**Children:**

| Element | Size | Notes |
|---|---|---|
| ChallengerCard | (480 × 160) | HorizontalLayoutGroup: portrait (120×120) + text column |
| ReadyButton | (480 × 72) | Full width, gold fill with dark label, glow effect |
| GameModeRow | (auto × 32) | HorizontalLayoutGroup: GameModeText + gear button (32×32) |

---

## Component checklist per element

| Element | Components |
|---|---|
| Canvas | Canvas (Overlay), CanvasScaler, GraphicRaycaster |
| TopBar | RectTransform, HorizontalLayoutGroup, Image (background) |
| NavTab | Button, Image (indicator), TextMeshProUGUI |
| UtilityIcons | HorizontalLayoutGroup |
| CurrencyCounter | HorizontalLayoutGroup, Image (icon), TextMeshProUGUI |
| CharacterViewport | RectTransform, RawImage (Render Texture) or Camera |
| PlayerTag | VerticalLayoutGroup, Image (badge), TextMeshProUGUI ×2 |
| PartySlot | Button, Image (circular mask), TextMeshProUGUI ("+") |
| LeftPanel | VerticalLayoutGroup, Image (background) |
| XpBar | Image (background), Image (fill, Type=Filled, Fill Method=Horizontal) |
| QuestScrollView | ScrollRect, Mask (or RectMask2D), Image (background) |
| QuestContent | VerticalLayoutGroup, ContentSizeFitter (Vertical=PreferredSize) |
| ChallengerCard | HorizontalLayoutGroup, Image (portrait), TextMeshProUGUI ×2 |
| ReadyButton | Button, Image (glow), TextMeshProUGUI |
| GameModeRow | HorizontalLayoutGroup, TextMeshProUGUI, Button (gear) |

---

## Styling notes

- **Fonts:** Use a cyberpunk-style font (e.g., "Rajdhani", "Orbitron", or "Chakra Petch") for headers; "Inter" or "Roboto" for body text.
- **Colors:** goldAccent `#FFC033`, neonRed `#FF1A26`, neonGreen `#1AFF73`, neonCyan `#00E5FF`, darkPanel `#0A0A14` (85% opacity).
- **Glow:** Use Unity's built-in URP Bloom post-processing or a simple UI glow via duplicated text/images with blur.
- **Borders:** 1px neon-colored borders on panels/badges via Image components with sprite borders.
- **Animations:** Use DOTween or Unity's Animation system for tab hover glow, READY button pulse, and quest progress fill tweening.
