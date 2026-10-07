namespace FortressClash.Cutscene
{
    /// <summary>
    /// Every number that shapes the look of the pop-up book, gathered in one place so the
    /// staging can be re-tuned without hunting through the animation code.  Units are world
    /// units; the book is built at a size derived from the camera framing (see
    /// <see cref="CutsceneStage"/>).
    /// </summary>
    public static class CutsceneTuning
    {
        // ---- camera / framing ------------------------------------------------------------
        /// <summary>Camera field of view, in degrees.  Deliberately narrow: the camera sits far
        /// back so a page swinging towards the reader stays in front of it.  At 45 degrees the
        /// sheet swept straight through the lens at the halfway point of every turn.</summary>
        public const float CameraFov = 20f;
        /// <summary>Distance from the camera to the page plane along Z.  Only meaningful together
        /// with <see cref="CameraFov"/> - the pair sets how much of the screen the book fills, so
        /// retune both at once (45 / 8 and 20 / 18.793 frame the book identically).</summary>
        public const float CameraDepth = 18.793f;
        /// <summary>Degrees the book is pitched back from the camera.  Kept at 0 so the page lies
        /// square to the screen; raise it to tilt the book away again.</summary>
        public const float CameraElevation = 0f;
        /// <summary>Fraction of the visible height the book may fill.</summary>
        public const float BookFitHeight = 0.88f;
        /// <summary>Fraction of the visible width the book may fill.</summary>
        public const float BookFitWidth = 0.92f;

        // ---- page ------------------------------------------------------------------------
        /// <summary>Artwork aspect (the slides are 1536 x 1024).</summary>
        public const float ArtAspect = 1.5f;
        /// <summary>Parchment margin around the artwork, as a fraction of the page.</summary>
        public const float PaperMargin = 0.055f;
        /// <summary>Hard cover overhang over the page block.</summary>
        public const float CoverOverhang = 1.03f;

        // ---- pop-up layers ---------------------------------------------------------------
        // Retained for the PopUpCard path, which the book no longer uses: pages now print the
        // whole slide flat instead of slicing it into panels that lift off the paper.  Only
        // FoldedZ is still read (by PopUpCard.FoldedZ).
        /// <summary>Tilt of a panel card off the page once it has popped open. Kept small: the
        /// panels have to keep tiling into the printed page.</summary>
        public const float PanelRestTilt = 6f;
        /// <summary>Tilt of a caption card at rest (bigger: it floats clear of the panel).</summary>
        public const float CaptionRestTilt = 22f;
        /// <summary>Peak tilt reached while a card is flying up, before it settles.</summary>
        public const float PopPeakTilt = 48f;
        /// <summary>How far the front layer is lifted off the page at rest.</summary>
        public const float PanelLift = 0.08f;
        /// <summary>Extra lift per band, so the artwork sits in layers instead of one flat sheet.</summary>
        public const float LayerStep = 0.09f;
        /// <summary>How far a caption card floats above its own panel.</summary>
        public const float CaptionLift = 0.18f;
        /// <summary>Where a folded card hides: just behind the page sheet.</summary>
        public const float FoldedZ = -0.03f;

        // ---- timing (seconds) ------------------------------------------------------------
        public const float FadeIn = 0.9f;
        /// <summary>Beat on the closed cover before it swings open.</summary>
        public const float CoverPause = 2.6f;
        public const float CoverOpen = 1.7f;
        public const float CardPop = 0.62f;
        public const float PanelStagger = 0.30f;
        public const float CaptionGap = 0.16f;
        public const float CardFold = 0.34f;
        public const float PageTurn = 1.15f;
        public const float ReadTimePerPanel = 1.5f;
        public const float ReadTimeMin = 5f;
        public const float BookClose = 1.5f;
        public const float FadeOut = 0.9f;
        /// <summary>Animation speed multiplier while the reader is tapping to hurry on.</summary>
        public const float FastForwardRate = 4.5f;

        // ---- palette ---------------------------------------------------------------------
        // Brightened from the original near-black room, which read a bit gloomy.  These are the
        // knobs for the overall mood: the three "room" tones set how dark the hall behind the book
        // is, and Leather/LeatherEdge how warm the boards are.  Raise them for more light, and the
        // lamp's alpha in CutsceneStage plus the vignette in CutsceneArt for more glow.
        public static readonly UnityEngine.Color Ink = new UnityEngine.Color(0.11f, 0.08f, 0.10f);
        public static readonly UnityEngine.Color Paper = new UnityEngine.Color(0.98f, 0.941f, 0.867f);
        public static readonly UnityEngine.Color PaperDeep = new UnityEngine.Color(0.863f, 0.784f, 0.643f);
        public static readonly UnityEngine.Color Leather = new UnityEngine.Color(0.463f, 0.239f, 0.271f);
        public static readonly UnityEngine.Color LeatherEdge = new UnityEngine.Color(0.267f, 0.125f, 0.149f);
        public static readonly UnityEngine.Color RoomDark = new UnityEngine.Color(0.098f, 0.078f, 0.137f);
        public static readonly UnityEngine.Color RoomWarm = new UnityEngine.Color(0.341f, 0.216f, 0.243f);
        public static readonly UnityEngine.Color LampGlow = new UnityEngine.Color(1f, 0.804f, 0.541f);
        public static readonly UnityEngine.Color Dust = new UnityEngine.Color(1f, 0.906f, 0.729f);
    }
}
