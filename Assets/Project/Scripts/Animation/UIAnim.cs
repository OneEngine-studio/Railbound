namespace Railbound.UI.Anim
{
    /// <summary>
    /// Central timing / easing rules for all UI animation.
    /// Durations follow the design spec:
    ///   hover 0.12-0.20, click 0.08-0.15, popup 0.20-0.35, screen 0.30-0.50,
    ///   important event 0.50-1.00, ambient slow + subtle.
    /// Easing: Ease Out for hover, Ease In Out for transitions, Back Out for pops,
    ///   Elastic sparingly, Linear only for continuous loops (spinners).
    /// </summary>
    public static class UIAnim
    {
        // ---------------------------------------------------------------- durations (seconds)

        public const float Hover = 0.15f;            // hover in / hover out   (0.12-0.20)
        public const float HoverRing = 0.12f;        // glow appears slightly ahead of the scale
        public const float PressDown = 0.08f;        // press                  (0.08-0.15)
        public const float PressRelease = 0.10f;
        public const float ClickDown = 0.05f;        // click: 1.04 -> 0.96    (0.08-0.15 total)
        public const float ClickUp = 0.08f;          // click: 0.96 -> 1.00
        public const float Flash = 0.12f;            // click glow/flash
        public const float StateChange = 0.15f;      // selected / disabled / locked / focus
        public const float ErrorShake = 0.30f;
        public const float Success = 0.45f;
        public const float Popup = 0.28f;            // pop effects            (0.20-0.35)
        public const float MenuOpen = 0.30f;
        public const float MenuClose = 0.20f;
        public const float NotificationSlide = 0.30f;
        public const float Screen = 0.40f;           // screen transitions     (0.30-0.50)
        public const float Important = 0.70f;        // important events       (0.50-1.00)
        public const float AmbientCycle = 2.4f;      // breathing / pulse loops (slow, subtle)
        public const float SpinnerCycle = 0.9f;      // loading rotation per turn (Linear)

        // ---------------------------------------------------------------- scales / distances

        public const float HoverScale = 1.025f;      // sleek tactical hover elevation (1.02-1.03)
        public const float HoverScaleLight = 1.02f;  // light style (small icon tiles)
        public const float PressScale = 0.975f;      // tactile micro-compression depth (no rubbery sag)
        public const float CardHoverScale = 1.02f;   // experience level cards
        public const float CardSelectedScale = 1.03f;
        public const float CardHoverLift = 8f;       // cards rise 5-10px on hover
        public const float CardArtZoom = 1.04f;      // artwork zoom inside its mask
        public const float IconNudge = 3f;           // icons move 2-4px on hover
        public const float ArrowSlide = 6f;          // arrows move 4-8px
        public const float BrightnessHover = 1.15f;
        public const float BrightnessHoverLight = 1.12f;   // light style icon tiles
        public const float BrightnessPressed = 0.90f;
        public const float BrightnessDisabled = 0.45f;
        public const float BrightnessLocked = 0.50f;
        public const float BrightnessLoading = 0.75f;

        // ---------------------------------------------------------------- glow (soft halo replaces hard outlines)
        //
        // Hover: a radial gold halo fades in behind the button - no hard edges, so it never
        // reads as a floating box on small tiles or a muddy shadow on big buttons.

        public const float GlowOutset = 16f;            // halo extends 16px past the button rect
        public const float GlowHover = 0.55f;
        public const float GlowHoverLight = 0.40f;
        public const float GlowSelected = 0.50f;
        public const float GlowHoverSelected = 0.75f;
        public const float GlowFocused = 0.35f;
        public const float GlowLoading = 0.25f;
        public const float GlowFlash = 0.90f;           // click flash

        // ---------------------------------------------------------------- easing

        public const Ease HoverEase = Ease.OutCubic;         // Ease Out for hover
        public const Ease TransitionEase = Ease.InOutCubic;   // Ease In Out for transitions
        public const Ease PopEase = Ease.BackOut;             // Back Out for pops
        public const Ease LoopEase = Ease.Linear;             // Linear for continuous loops only

        // ---------------------------------------------------------------- spinner ring (loading only)

        public const float RingSpinner = 0.70f;         // rotating ring while SetLoading(true)

        // ---------------------------------------------------------------- glow ambient (selected-card breathing)

        public const float GlowAmbientMin = 0.42f;      // slow, subtle
    }
}
