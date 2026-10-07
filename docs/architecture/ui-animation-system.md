# UI Animation System

Battle-royale UI motion framework for Railbound. Lives in `Assets/Project/Scripts/Animation/`
(namespace `Railbound.UI.Anim`), runs on `Assembly-CSharp`, needs no external tween library.

All timing follows the design spec: hover 0.12–0.20s · click 0.08–0.15s · popup 0.20–0.35s ·
screen 0.30–0.50s · important event 0.50–1.00s · ambient slow and subtle. Ease Out for hover,
Ease In Out for transitions, Back Out for pops, Linear only for continuous loops.

## Files

| File | Purpose |
|---|---|
| `UITween.cs` | Tween engine + easing library (Linear, Out/In/InOut Quad/Cubic/Quint, BackOut, BackInOut, ElasticOut, OutExpo). Runs on **unscaled** time. Outside Play Mode every tween settles instantly, so edit-mode previews are always correct. |
| `UIAnim.cs` | All durations, scales, distances and default eases in one place. Change a constant, everything follows. |
| `UISfx.cs` | Click sound: synthesized 55 ms sweep on first use (no audio asset needed); optional `clickClip`/`hoverClip` overrides, `Muted`, `Volume`. |
| `AnimatedButton.cs` | Button state machine (14 states) + hover ring, group brightness, icon nudge/rotation, loading spinner. |
| `LevelCardAnimator.cs` | Experience level card hover/selected/ambient behavior + checkmark pop. |
| `IconAnimator.cs` | Reusable icon animations (Pulse, Shake, Spin, Bounce, Fade, Slide, ColorTo, RotateTo, PopIn, Flip, GlowPulse) + `AmbientPulse` component for continuous badge pulses. |
| `MenuAnimator.cs` | Menu open (fade + scale 0.95→1 + slide) / close (fade + scale 1→0.97) / step-swap pop. |
| `ScreenTransition.cs` | Screen-to-screen: Fade, Crossfade, SlideLeft, SlideRight, Zoom, Blur (dark-overlay approximation of a blur). `Directional()` derives slide direction from forward/back navigation. |
| `PopupAnimator.cs` | Dialog open 0.85 → 1.05 → 1.00 (Back Out, ~0.28s); close scale 1→0.95 + fade. |
| `NotificationPresenter.cs` | Toast stack (`Notifications.Show` / `Notifications.ShowCountdown`): slide from top/bottom, icon pop + pulse, progress bar, countdown, auto fade-out. |

## Button states (`AnimatedButton`)

`Idle · HoverIn · HoverOut · PressDown · PressRelease · Click · Selected · Deselected ·
Disabled · Locked · Loading · Success · Error · Focused` — exposed via `State`.

| Interaction | Behavior |
|---|---|
| Hover in/out | scale 1.00 → **1.04**, brightness ×1.15 over the whole visual group (fill, children, labels), gold ring fades to 0.55, icon nudges 3px (+15° rotation if `rotateIconOnHover`), 0.15s Ease Out |
| Press / release | scale → 0.96 (0.08s) → back (0.10s), brightness ×0.90 |
| Click | 1.04 → 0.96 → 1.00 in 0.13s, ring flash, click sound (via `UISfx`) |
| Selected | persistent ring 0.70 (0.90 while hovered) |
| Disabled | brightness ×0.45, no hover response, no ring — synced automatically when `Button.interactable` flips |
| Locked | brightness ×0.50; hovering shakes the icon (unavailable feedback) — `SetLocked(bool)` |
| Loading | brightness ×0.75, ring rotates **Linear** (continuous loop), input blocked — `SetLoading(bool)` |
| Success / Error | green flash / red flash + shake, then settles — `SetSuccess()`, `SetError()` |
| Focused | white-ish ring 0.40 on select (controller navigation) |

Notes:

- The component forces `Button.transition = None` and owns all visuals. Brightness is applied
  as a **group tint** (`GetComponentsInChildren<Graphic>` minus the ring), so icon-only
  buttons respond too. Colors are re-cached only from a neutral state so external recolors
  (e.g. `LobbyUIController.SetActiveTab` gold labels) are picked up without drift.
- Tween channels are separate tokens (`scale/color/ring/rotation/fx`) — a new state kills only
  its own channel, so nothing ever gets stuck mid-tween.
- Hover ring sprite = `Resources/UI/Ring_Square` (square buttons) or `Ring_Chamfer`
  (corner-cut onboarding buttons), stretched 3px outside the button rect.

## Experience level cards (`LevelCardAnimator`)

| State | Behavior |
|---|---|
| Idle (unselected) | dimmed (art tint 0.45), stable, no ring |
| Hover | lift **+8px**, scale **1.02**, gold ring 0.55, artwork zooms **1.04** inside its `ArtClip` mask, art brightens to 0.6 (description baked into the art becomes clearer) |
| Selected | gold frame strips, ring 0.70 + **ambient breathing** (0.70↔0.90, 2.4s, InOutCubic), checkmark **pops 0 → 1 with Back Out (0.28s)**, scale 1.03, art full white |
| Deselected | glow/checkmark fade, scale returns, art returns to dim (controller dims the other cards) |

Cards keep a transparent root hit-area; artwork lives in `ArtClip/Art` (child, `RectMask2D`)
so the zoom is clipped. The ring and checkmark hang off `SelectionFrame` (stays active, its
strips toggle) so they always align to the artwork bounds.

## Menu / popup / screen / notification

```csharp
MenuAnimator.Open(canvasGroup, panel);        // fade + 0.95→1 + slide from +24px
MenuAnimator.Close(canvasGroup, panel);       // fade + 1→0.97
MenuAnimator.SwapStep(panel);                 // quick Back Out pop when content swaps

PopupAnimator.Open(dialogRect, overlayGroup); // overlay fade + 0.85→1.05→1.00
PopupAnimator.Close(dialogRect, overlayGroup);

ScreenTransition.Play(from, to, ScreenTransitionType.SlideLeft, forward: true); // 0.40s InOut
ScreenTransition.Directional(from, to, forward); // direction from navigation

Notifications.Show(canvas, title, body, seconds: 4f, important: true);
Notifications.ShowCountdown(canvas, "TRAIN CAR DESTROYING", 10); // demo on the bell button
```

Notification toasts stack top-right: slide in 0.30s (from top), icon pops (Back Out) then
pulses (0.6s), progress bar depletes, `important` toasts get the red rail/alert icon and a
stronger pulse, auto fade-out 0.25s + slide away, remaining toasts shift up.

## Wiring in the scene (`GameLobby.unity`)

- **15 lobby buttons** carry `AnimatedButton`: 5 tabs (`Panel_TopNav/LayoutGroup_Tabs`),
  5 utilities (Settings + Game Mode gear have `SetIcon(icon, rotateOnHover: true)`),
  both party slots (label nudges), CHECK AWARDS, READY.
- `LobbyUIController.SetActiveTab` drives `AnimatedButton.SetSelected` → active tab keeps a
  gold ring; `HandleNotifications` shows the countdown toast.
- `Badge_Notification` has `AmbientPulse` (slow 1.2s pulse).
- Onboarding buttons get `AnimatedButton` automatically inside
  `PlayerOnboardingController.CreateStyledButton` (chamfered ring); save flow uses
  `SetLoading` via `SetBusy`. Cards get `LevelCardAnimator` inside `CreateLevelCard`.
- Overlay open/close/step-swap run through `MenuAnimator`
  (`ShowOverlay`/`HideOverlay`/`ShowLevelStep`).

## Sprite assets (`Assets/Project/Resources/UI/`)

| Asset | Use | Border |
|---|---|---|
| `Ring_Square.png` | 9-sliced hover/glow ring, square buttons | 5 |
| `Ring_Chamfer.png` | corner-cut ring for onboarding chrome | 20 |
| `Onboarding_Chamfer.png` | button/card chrome + toast panels | 14 |
| `Checkmark.png` | selected-card check glyph (AA polyline) | — |

All loaded via `Resources.Load` so they work in edit mode, play mode and builds.

## Gotchas

- Never run tween logic in edit mode expecting frames — `UITween.To` applies the final value
  instantly and `UITween.Loop` is a no-op outside Play Mode (previews stay correct).
- No top-level `using` in `execute_code` snippets; fully qualify instead.
- New state changes always go through `Refresh(...)` — it captures the current value as the
  tween start, so interrupted animations always recover from where they are.
