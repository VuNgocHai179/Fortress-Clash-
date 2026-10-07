using System.Collections.Generic;
using UnityEngine;

namespace FortressClash.Cutscene
{
    /// <summary>
    /// The staged scene the book sits in: camera framing, the dark room behind it, the leather
    /// boards, the page block, the hard cover and the dust drifting through the lamp light.
    /// </summary>
    public class CutsceneStage
    {
        private const int OrderBackdrop = -300;
        private const int OrderLamp = -250;
        private const int OrderTableShadow = -60;
        private const int OrderBackBoard = -50;
        private const int OrderPageEdges = -44;
        private const int OrderSpine = -40;
        private const int OrderSheetShadow = -10;
        public const int OrderCover = 900;

        public readonly Transform Root;
        public readonly Transform BookRoot;
        public readonly Camera Camera;

        public readonly Transform CoverHinge;
        public readonly SpriteRenderer CoverBoard;
        public readonly SpriteRenderer CoverArt;
        public readonly SpriteRenderer TurnShadow;

        public CutsceneGeometry Geometry;
        public float VisibleHeight { get; private set; }
        public float VisibleWidth { get; private set; }

        private readonly List<Transform> _dust = new List<Transform>();
        private readonly List<Vector3> _dustHome = new List<Vector3>();
        private readonly List<float> _dustSeed = new List<float>();
        private readonly List<float> _dustAmp = new List<float>();

        public CutsceneStage(Transform parent, Camera camera, Sprite coverSprite, int slideW, int slideH, float artAspect)
        {
            Root = new GameObject("Stage").transform;
            Root.SetParent(parent, false);
            Camera = camera;

            // ---- framing ---------------------------------------------------------------------
            VisibleHeight = 2f * CutsceneTuning.CameraDepth * Mathf.Tan(CutsceneTuning.CameraFov * 0.5f * Mathf.Deg2Rad);
            VisibleWidth = VisibleHeight * Mathf.Max(0.5f, Camera.aspect);

            float elevation = CutsceneTuning.CameraElevation * Mathf.Deg2Rad;
            float pageH = CutsceneTuning.BookFitHeight * VisibleHeight / Mathf.Cos(elevation);
            float pageW = pageH * artAspect;
            if (pageW > CutsceneTuning.BookFitWidth * VisibleWidth)
            {
                pageW = CutsceneTuning.BookFitWidth * VisibleWidth;
                pageH = pageW / artAspect;
            }

            float margin = 1f - 2f * CutsceneTuning.PaperMargin;
            Geometry = new CutsceneGeometry
            {
                SlideW = slideW,
                SlideH = slideH,
                PageW = pageW,
                PageH = pageH,
                ArtW = pageW * margin,
                ArtH = pageH * margin,
                Ppu = slideW / (pageW * margin),
            };

            Camera.transform.position = new Vector3(0f, 0f, -CutsceneTuning.CameraDepth);
            Camera.transform.rotation = Quaternion.identity;
            Camera.orthographic = false;
            Camera.fieldOfView = CutsceneTuning.CameraFov;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = CutsceneTuning.RoomDark;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 200f;

            // ---- the room --------------------------------------------------------------------
            SpriteRenderer backdrop = Quad(Root, "Backdrop", CutsceneArt.Backdrop(), Color.white, OrderBackdrop);
            backdrop.transform.localPosition = new Vector3(0f, 0f, 9f);
            SizeTo(backdrop, VisibleWidth * 4f, VisibleHeight * 4f);

            SpriteRenderer lamp = Quad(Root, "LampGlow", CutsceneArt.SoftBlob(1.3f, 0.05f),
                                       CutsceneTuning.LampGlow, OrderLamp);
            lamp.transform.localPosition = new Vector3(-1.6f, 3.4f, 3.2f);
            SizeTo(lamp, VisibleWidth * 1.3f, VisibleHeight * 1.15f);
            lamp.color = new Color(CutsceneTuning.LampGlow.r, CutsceneTuning.LampGlow.g, CutsceneTuning.LampGlow.b, 0.45f);

            // ---- the book --------------------------------------------------------------------
            BookRoot = new GameObject("Book").transform;
            BookRoot.SetParent(Root, false);
            // no yaw at all, and pitch only if CameraElevation is dialled up: at 0 the page lies
            // square to the screen, so the printed slide lines up with the game view instead of
            // reading as a board tilted away from the reader
            BookRoot.localRotation = Quaternion.Euler(-CutsceneTuning.CameraElevation, 0f, 0f);
            BookRoot.localPosition = new Vector3(0f, -0.03f * VisibleHeight, 0f);

            SpriteRenderer tableShadow = Quad(BookRoot, "TableShadow", CutsceneArt.SoftBlob(1.1f, 0.05f),
                                              new Color(0.02f, 0.01f, 0.03f, 0.62f), OrderTableShadow);
            tableShadow.transform.localPosition = new Vector3(0f, -pageH * 0.62f, -0.55f);
            SizeTo(tableShadow, pageW * 2.1f, pageH * 0.95f);

            SpriteRenderer back = Quad(BookRoot, "BackBoard", CutsceneArt.Leather(), Color.white, OrderBackBoard);
            back.transform.localPosition = new Vector3(0f, 0f, -0.09f);
            SizeTo(back, pageW * 1.08f, pageH * 1.09f);

            SpriteRenderer edges = Quad(BookRoot, "PageEdges", CutsceneArt.PageEdge(), Color.white, OrderPageEdges);
            edges.transform.localPosition = new Vector3(-pageW * 0.5f - pageW * 0.022f, 0f, -0.05f);
            SizeTo(edges, pageW * 0.05f, pageH * 1.0f);

            SpriteRenderer spine = Quad(BookRoot, "Spine", CutsceneArt.Leather(), CutsceneTuning.LeatherEdge, OrderSpine);
            spine.transform.localPosition = new Vector3(-pageW * 0.52f, 0f, -0.04f);
            SizeTo(spine, pageW * 0.06f, pageH * 1.09f);

            SpriteRenderer sheetShadow = Quad(BookRoot, "SheetShadow", CutsceneArt.SoftBlob(1.35f, 0.05f),
                                              new Color(0.04f, 0.02f, 0.05f, 0.55f), OrderSheetShadow);
            sheetShadow.transform.localPosition = new Vector3(pageW * 0.01f, -pageH * 0.03f, -0.02f);
            SizeTo(sheetShadow, pageW * 1.12f, pageH * 1.14f);

            TurnShadow = Quad(BookRoot, "TurnShadow", CutsceneArt.SoftRect(), new Color(0.06f, 0.03f, 0.07f, 0f), 99);
            TurnShadow.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            SizeTo(TurnShadow, pageW, pageH);

            // ---- hard cover ------------------------------------------------------------------
            CoverHinge = new GameObject("CoverHinge").transform;
            CoverHinge.SetParent(BookRoot, false);
            CoverHinge.localPosition = new Vector3(-pageW * 0.5f, 0f, 0.02f);

            float coverW = pageW * CutsceneTuning.CoverOverhang;
            float coverH = pageH * CutsceneTuning.CoverOverhang;
            CoverBoard = Quad(CoverHinge, "CoverBoard", CutsceneArt.Leather(), Color.white, OrderCover);
            CoverBoard.transform.localPosition = new Vector3(coverW * 0.5f, 0f, 0f);
            SizeTo(CoverBoard, coverW, coverH);

            if (coverSprite != null)
            {
                float artAspectCover = coverSprite.rect.width / Mathf.Max(1f, coverSprite.rect.height);
                float artW = coverW, artH = coverW / artAspectCover;
                if (artH > coverH)
                {
                    artH = coverH;
                    artW = coverH * artAspectCover;
                }

                CoverArt = Quad(CoverHinge, "CoverArt", coverSprite, Color.white, OrderCover + 1);
                CoverArt.transform.localPosition = new Vector3(coverW * 0.5f, 0f, 0.012f);
                SizeTo(CoverArt, artW, artH);
            }

            // ---- dust in the lamplight -------------------------------------------------------
            Sprite dustSprite = CutsceneArt.SoftBlob(1.6f, 0.0f);
            for (int i = 0; i < 42; i++)
            {
                float fx = Hash(i * 7 + 1);
                float fy = Hash(i * 13 + 5);
                float fz = Hash(i * 23 + 9);

                SpriteRenderer mote = Quad(Root, $"Dust{i}", dustSprite, CutsceneTuning.Dust, 700 + (i % 6));
                var home = new Vector3(
                    (fx - 0.5f) * VisibleWidth * 1.15f,
                    (fy - 0.42f) * VisibleHeight * 1.1f,
                    -3.4f + fz * 5.2f);
                mote.transform.localPosition = home;
                float size = 0.035f + Hash(i * 31 + 3) * 0.075f;
                SizeTo(mote, size, size);
                mote.color = new Color(CutsceneTuning.Dust.r, CutsceneTuning.Dust.g, CutsceneTuning.Dust.b,
                                       0.10f + Hash(i * 37 + 7) * 0.32f);

                _dust.Add(mote.transform);
                _dustHome.Add(home);
                _dustSeed.Add(Hash(i * 41 + 11) * 6.28f);
                _dustAmp.Add(0.06f + Hash(i * 43 + 13) * 0.22f);
            }
        }

        /// <summary>
        /// Yaw of the cover around the spine.  0 is closed over the pages, 180 is laid open to the
        /// left of them.  Positive swings the board towards the reader and over the spine, which is
        /// the way a cover actually opens - the negative direction used to send it through the
        /// book instead.
        /// </summary>
        public void SetCoverAngle(float degrees)
        {
            CoverHinge.localRotation = Quaternion.Euler(0f, degrees, 0f);

            // once the cover has swung past edge-on we are looking at its inside, so the printed
            // artwork must stop drawing over the leather board
            bool innerFace = Mathf.Abs(degrees) > 90f;
            CoverBoard.sortingOrder = innerFace ? OrderCover + 1 : OrderCover;
            if (CoverArt != null)
            {
                CoverArt.sortingOrder = innerFace ? OrderCover : OrderCover + 1;
            }
        }

        public void SetCoverAlpha(float alpha)
        {
            Color c = CoverBoard.color;
            c.a = alpha;
            CoverBoard.color = c;

            if (CoverArt != null)
            {
                Color a = CoverArt.color;
                a.a = alpha;
                CoverArt.color = a;
            }
        }

        public void SetTurnShadowAlpha(float alpha)
        {
            Color c = TurnShadow.color;
            c.a = Mathf.Clamp01(alpha);
            TurnShadow.color = c;
        }

        public void SetTurnShadowOrder(int order)
        {
            TurnShadow.sortingOrder = order;
        }

        public void AnimateAmbient(float time)
        {
            for (int i = 0; i < _dust.Count; i++)
            {
                float seed = _dustSeed[i];
                float amp = _dustAmp[i];
                Vector3 home = _dustHome[i];
                _dust[i].localPosition = home + new Vector3(
                    Mathf.Sin(time * 0.31f + seed) * amp,
                    Mathf.Sin(time * 0.21f + seed * 1.7f) * amp * 1.4f,
                    Mathf.Sin(time * 0.13f + seed * 2.3f) * amp * 0.5f);
            }
        }

        /// <summary>Scales a sprite renderer so the sprite covers the requested world size.</summary>
        public static void SizeTo(SpriteRenderer renderer, float width, float height)
        {
            Vector3 size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(
                width / Mathf.Max(0.0001f, size.x),
                height / Mathf.Max(0.0001f, size.y),
                1f);
        }

        /// <summary>Creates a plain sprite quad.  Scale it with <see cref="SizeTo"/>.</summary>
        public static SpriteRenderer Quad(Transform parent, string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        private static float Hash(int n)
        {
            unchecked
            {
                n = (n << 13) ^ n;
                int m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
                return m / (float)0x7fffffff;
            }
        }
    }
}
