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
        public const float HoverRing = 0.12f;        // border/glow appears slightly ahead of the scale
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

        public const float HoverScale = 1.04f;       // buttons
        public const float PressScale = 0.96f;
        public const float CardHoverScale = 1.02f;   // experience level cards
        public const float CardSelectedScale = 1.03f;
        public const float CardHoverLift = 8f;       // cards rise 5-10px on hover
        public const float CardArtZoom = 1.04f;      // artwork zoom inside its mask
        public const float IconNudge = 3f;           // icons move 2-4px on hover
        public const float ArrowSlide = 6f;          // arrows move 4-8px
        public const float BrightnessHover = 1.15f;
        public const float BrightnessPressed = 0.90f;
        public const float BrightnessDisabled = 0.45f;
        public const float BrightnessLocked = 0.50f;
        public const float BrightnessLoading = 0.75f;

        // ---------------------------------------------------------------- easing

        public const Ease HoverEase = Ease.OutCubic;         // Ease Out for hover
        public const Ease TransitionEase = Ease.InOutCubic;   // Ease In Out for transitions
        public const Ease PopEase = Ease.BackOut;             // Back Out for pops
        public const Ease LoopEase = Ease.Linear;             // Linear for continuous loops only

        // ---------------------------------------------------------------- ring (border/glow) alpha

        public const float RingHover = 0.55f;
        public const float RingSelected = 0.70f;
        public const float RingHoverSelected = 0.90f;
        public const float RingFlash = 0.95f;
        public const float RingAmbientMin = 0.70f;            // selected-card breathing
    }
}
