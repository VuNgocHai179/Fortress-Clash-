using UnityEngine;

namespace FortressClash.Cutscene
{
    /// <summary>
    /// One piece of paper that lifts off a page - a comic panel or a caption card.
    ///
    /// The card is built around a hinge transform sitting on its bottom edge, so rotating the
    /// hinge swings the paper up out of the page exactly like a pop-up flap.  A separate shadow
    /// quad lives in the page's own space (it must not tilt with the card) and is resized as the
    /// card rises, which is what makes the lift read as depth.
    /// </summary>
    public class PopUpCard
    {
        private const float PeakAt = 0.55f;   // fraction of the pop spent flying up

        public readonly Transform Hinge;
        public readonly SpriteRenderer Art;
        public readonly SpriteRenderer Shadow;

        public readonly float Width;
        public readonly float Height;
        public readonly float RestTilt;
        public readonly float RestLift;
        public readonly float PeakTilt;
        public readonly float Twist;
        public readonly float ShadowAlpha;
        public readonly float ShadowZ;

        /// <summary>Draw order offset within its page (see <see cref="CutscenePage"/>).</summary>
        public readonly int LayerOffset;

        /// <summary>Draw order while the card lies folded behind the sheet.</summary>
        public int OrderFolded;

        /// <summary>Draw order once the card has lifted clear of the sheet.</summary>
        public int OrderPopped;

        private readonly Vector3 _anchor;      // where the hinge sits in the page (z is animated)
        private readonly bool _hasShadow;

        public PopUpCard(string name, Transform pageRoot, Sprite sprite, float width, float height,
                         Vector2 anchor, float restTilt, float peakTilt, float restLift, float shadowZ,
                         int layerOffset, float shadowAlpha, float twist)
        {
            Width = width;
            Height = height;
            RestTilt = restTilt;
            PeakTilt = peakTilt;
            RestLift = restLift;
            ShadowZ = shadowZ;
            Twist = twist;
            ShadowAlpha = shadowAlpha;
            LayerOffset = layerOffset;
            _anchor = new Vector3(anchor.x, anchor.y, 0f);

            Hinge = new GameObject(name).transform;
            Hinge.SetParent(pageRoot, false);
            Hinge.localPosition = _anchor;

            var artGo = new GameObject("Art");
            artGo.transform.SetParent(Hinge, false);
            Art = artGo.AddComponent<SpriteRenderer>();
            Art.sprite = sprite;

            _hasShadow = shadowAlpha > 0.001f;
            if (!_hasShadow)
            {
                Shadow = null;
                return;
            }

            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(pageRoot, false);
            Shadow = shadowGo.AddComponent<SpriteRenderer>();
            Shadow.sprite = CutsceneArt.SoftRect();
            Shadow.color = new Color(0.09f, 0.05f, 0.08f, 0f);
        }

        /// <summary>Re-layers the card inside its page; used when the page changes its place in the stack.</summary>
        public void SetBaseOrder(int pageBaseOrder)
        {
            OrderFolded = pageBaseOrder - 3;
            OrderPopped = pageBaseOrder + LayerOffset;
            Art.sortingOrder = OrderFolded;
            if (Shadow != null)
            {
                Shadow.sortingOrder = OrderFolded - 1;
            }
        }

        /// <summary>0 = flat against the page (hidden behind the sheet), 1 = popped open.</summary>
        public void Apply(float t)
        {
            t = CutsceneTween.Clamp01(t);

            float rise = CutsceneTween.Clamp01(t / PeakAt);
            float settle = CutsceneTween.Clamp01((t - PeakAt) / (1f - PeakAt));

            float tilt = Mathf.Lerp(0f, PeakTilt, CutsceneTween.EaseOutBack(rise, 1.15f));
            tilt = Mathf.Lerp(tilt, RestTilt, CutsceneTween.EaseInOutCubic(settle));

            float lift = Mathf.Lerp(CutsceneTuning.FoldedZ, RestLift, CutsceneTween.SpringOut(t, 0.62f, 9f));
            float scale = Mathf.Lerp(0.966f, 1f, CutsceneTween.SpringOut(t, 0.5f, 10f));
            float twist = Twist * (0.35f * CutsceneTween.Bump(t) + 0.65f * CutsceneTween.SmoothStep(t));

            float rad = tilt * Mathf.Deg2Rad;
            Hinge.localPosition = _anchor + new Vector3(0f, 0f, lift);
            Hinge.localRotation = Quaternion.Euler(tilt, 0f, twist);

            // undo the vertical squash the tilt introduces, so the paper still lines up with the
            // artwork printed underneath it once it has settled
            float cos = Mathf.Max(0.2f, Mathf.Cos(rad));
            Art.transform.localScale = new Vector3(scale, scale / cos, 1f);

            // a card behind the sheet must also draw behind it; the switch happens while the card
            // is level with the sheet, which is exactly when the two would coincide anyway
            bool behind = lift < 0f;
            Art.sortingOrder = behind ? OrderFolded : OrderPopped;

            if (!_hasShadow)
            {
                return;
            }

            Shadow.sortingOrder = (behind ? OrderFolded : OrderPopped) - 1;

            float foot = Height * cos * scale;
            float lifted = Mathf.Max(0f, lift);
            float grow = 1f + lifted * 0.55f;
            Shadow.transform.localPosition = new Vector3(
                _anchor.x + lifted * 0.22f + Width * 0.015f,
                _anchor.y + foot * 0.5f - Width * 0.018f - lifted * 0.5f,
                ShadowZ);
            Shadow.transform.localScale = new Vector3(Width * 1.04f * grow, foot * 1.08f * grow, 1f);

            Color c = Shadow.color;
            c.a = ShadowAlpha * CutsceneTween.Clamp01((lift + 0.03f) / (RestLift + 0.03f));
            Shadow.color = c;
        }

        /// <summary>Paper lying flat, hidden behind the page sheet.</summary>
        public void SetFolded() => Apply(0f);

        /// <summary>Paper lifted into its resting place.</summary>
        public void SetPopped() => Apply(1f);

        public void SetVisible(bool visible)
        {
            Art.enabled = visible;
            if (Shadow != null)
            {
                Shadow.enabled = visible;
            }
        }

        /// <summary>Sprite rendered from a slide texture, pivot on the bottom edge of the piece.</summary>
        public static Sprite Slice(Sprite sheet, CutsceneRect r, float ppu, int slideHeight)
        {
            Texture2D tex = sheet.texture;
            int x = Mathf.Clamp(r.x, 0, tex.width - 1);
            int y = Mathf.Clamp(r.y, 0, slideHeight - 1);
            int w = Mathf.Clamp(r.w, 1, tex.width - x);
            int h = Mathf.Clamp(r.h, 1, slideHeight - y);

            // slide pixels are measured from the top; textures from the bottom
            var rect = new Rect(x, slideHeight - (y + h), w, h);
            var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);
            sprite.name = $"{sheet.name}[{x},{y},{w},{h}]";
            return sprite;
        }
    }
}
