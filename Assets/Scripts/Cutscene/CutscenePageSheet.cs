// NOTE: the file name no longer matches its main class on purpose.  Unity's asset database had
// a stale record for the old path (Assets/Scripts/Cutscene/CutscenePage.cs) that kept the file
// out of the Assembly-CSharp compile, so the file was moved to a fresh path.  Neither type here
// derives from MonoBehaviour, so Unity does not require the name to match.  Renaming this file
// back would bring the broken record into play again.
using UnityEngine;

namespace FortressClash.Cutscene
{
    /// <summary>
    /// Page geometry: turns slide pixels into page-local world units.  The page's origin is the
    /// centre of the parchment sheet, X to the right and Y up, so the artwork sits inside the
    /// margins and everything is measured against the printed slide.
    /// </summary>
    public struct CutsceneGeometry
    {
        public int SlideW;
        public int SlideH;
        public float ArtW;
        public float ArtH;
        public float PageW;
        public float PageH;
        public float Ppu;             // slide pixels per world unit

        public Vector2 ToPage(float px, float py) =>
            new Vector2((px - SlideW * 0.5f) / Ppu, (SlideH * 0.5f - py) / Ppu);
    }

    /// <summary>
    /// One page of the book: a parchment sheet with the whole slide printed flat on it.  The
    /// artwork is drawn in one piece - never cut into panels, never tilted off the page - so it
    /// reads exactly like the slide file does.  The page hinges at its left edge, so it turns like
    /// a real page instead of sliding away.
    /// </summary>
    public class CutscenePage
    {
        /// <summary>Draw order within a page; the page's base order offsets the whole group.</summary>
        public const int OrderSheet = 0;
        public const int OrderSlide = 2;
        public const int OrderShade = 16;

        public readonly Transform TurnPivot;
        public readonly Transform Body;
        public SpriteRenderer Sheet;
        public SpriteRenderer Slide;
        public SpriteRenderer TurnShade;

        /// <summary>Story beats printed on this page, which sets how long the reader is left with it.</summary>
        public int Beats { get; private set; }

        public float PopDuration { get; private set; }
        public int BaseOrder { get; private set; }

        private bool _visible = true;

        public CutscenePage(string name, Transform bookRoot, Vector3 pivotPosition)
        {
            TurnPivot = new GameObject($"{name}_hinge").transform;
            TurnPivot.SetParent(bookRoot, false);
            TurnPivot.localPosition = pivotPosition;

            Body = new GameObject($"{name}_body").transform;
            Body.SetParent(TurnPivot, false);
        }

        /// <summary>Builds the sheet and prints the slide on it.  Returns false when the page has no layout.</summary>
        public bool Build(CutscenePageLayout layout, Sprite slide, CutsceneGeometry geo)
        {
            if (layout == null || slide == null)
            {
                return false;
            }

            Body.localPosition = new Vector3(geo.PageW * 0.5f, 0f, 0f);
            Body.localScale = Vector3.one;

            var sheetGo = new GameObject("Parchment");
            sheetGo.transform.SetParent(Body, false);
            Sheet = sheetGo.AddComponent<SpriteRenderer>();
            Sheet.sprite = CutsceneArt.Paper();
            CutsceneStage.SizeTo(Sheet, geo.PageW, geo.PageH);

            var slideGo = new GameObject("Slide");
            slideGo.transform.SetParent(Body, false);
            slideGo.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            Slide = slideGo.AddComponent<SpriteRenderer>();
            Slide.sprite = slide;
            FitInside(Slide, geo.ArtW, geo.ArtH);

            var shadeGo = new GameObject("TurnShade");
            shadeGo.transform.SetParent(Body, false);
            shadeGo.transform.localPosition = new Vector3(0f, 0f, 0.004f);
            TurnShade = shadeGo.AddComponent<SpriteRenderer>();
            TurnShade.sprite = CutsceneArt.TurnShade();
            TurnShade.color = new Color(1f, 1f, 1f, 0f);
            CutsceneStage.SizeTo(TurnShade, geo.PageW, geo.PageH);

            Beats = layout.panels != null && layout.panels.Length > 0 ? layout.panels.Length : 1;
            PopDuration = CutsceneTuning.CardPop;
            SetBaseOrder(0);
            SetFolded();
            return true;
        }

        /// <summary>
        /// Scales the slide to fill the artwork area without cropping or squashing it: the printed
        /// picture keeps its own aspect whatever shape the screen is.
        /// </summary>
        private static void FitInside(SpriteRenderer renderer, float maxWidth, float maxHeight)
        {
            Vector3 size = renderer.sprite.bounds.size;
            float scale = Mathf.Min(maxWidth / Mathf.Max(0.0001f, size.x),
                                    maxHeight / Mathf.Max(0.0001f, size.y));
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Places the page in the stack: higher base order draws closer to the reader.</summary>
        public void SetBaseOrder(int baseOrder)
        {
            BaseOrder = baseOrder;
            Sheet.sortingOrder = baseOrder + OrderSheet;
            Slide.sortingOrder = baseOrder + OrderSlide;
            TurnShade.sortingOrder = baseOrder + OrderShade;
        }

        /// <summary>Depth of this sheet inside the page block.</summary>
        public void SetDepth(float z)
        {
            TurnPivot.localPosition = new Vector3(TurnPivot.localPosition.x, 0f, z);
        }

        /// <summary>0 = the print has not developed yet, 1 = the whole slide is showing.</summary>
        public void SetProgress(float t)
        {
            float alpha = CutsceneTween.Clamp01(t);
            Color c = Slide.color;
            c.a = alpha;
            Slide.color = c;
            Slide.enabled = _visible && alpha > 0.002f;
        }

        public void SetFolded() => SetProgress(0f);

        public void SetPopped() => SetProgress(1f);

        public void SetVisible(bool visible)
        {
            _visible = visible;
            Sheet.enabled = visible;
            TurnShade.enabled = visible;
            Slide.enabled = visible && Slide.color.a > 0.002f;
        }

        /// <summary>
        /// Yaw of the sheet around its left edge, in degrees.  0 is flat on the book, 180 is fully
        /// turned over.  Positive swings the free edge towards the reader and over the spine - the
        /// way a real page turns - so the turn direction is set by the sign of the angle.
        /// </summary>
        public void SetTurnAngle(float degrees)
        {
            TurnPivot.localRotation = Quaternion.Euler(0f, degrees, 0f);
        }

        /// <summary>Bow of the sheet while it is in the air, so the turn does not look like a board.</summary>
        public void SetBow(float amount)
        {
            Body.localScale = new Vector3(1f - amount * 0.02f, 1f + amount * 0.06f, 1f);
        }

        public void SetShade(float alpha)
        {
            TurnShade.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }
    }
}
