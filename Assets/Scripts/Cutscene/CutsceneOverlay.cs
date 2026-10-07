using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace FortressClash.Cutscene
{
    /// <summary>
    /// Screen furniture for the cutscene: the fade curtain, the vignette and the skip button.
    ///
    /// The curtain and the vignette hang off the camera, so they stay put whatever the framing is.
    /// The skip button is real UI on its own screen-space canvas (with an EventSystem to click
    /// it), so it behaves like every other button in the game instead of a hand-rolled hit test.
    /// </summary>
    public class CutsceneOverlay
    {
        private const int OrderVignette = 940;
        private const int OrderFade = 960;

        private const float SkipWidth = 220f;
        private const float SkipHeight = 104f;
        private const float SkipMargin = 26f;

        private readonly Camera _camera;
        private readonly SpriteRenderer _fade;
        private readonly Button _skip;

        /// <summary>Raised when the reader presses the skip button.</summary>
        public event Action SkipPressed;

        /// <summary>Current curtain opacity, so fades can start from wherever they are.</summary>
        public float FadeAlpha { get; private set; }

        /// <summary>True while the pointer sits on the UI, so a click there is not read as a tap.</summary>
        public bool PointerOverUi =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public CutsceneOverlay(Transform cameraTransform, Camera camera, TMP_FontAsset font, TMP_FontAsset fallbackFont,
                               Sprite skipSprite)
        {
            _camera = camera;

            SpriteRenderer vignette = CutsceneStage.Quad(cameraTransform, "Vignette", CutsceneArt.Vignette(),
                                                         Color.white, OrderVignette);
            vignette.transform.localPosition = new Vector3(0f, 0f, 2.2f);
            FitToFrustum(vignette, 2.2f, 1.06f);

            _skip = BuildSkipButton(skipSprite, font != null ? font : fallbackFont);
            _skip.onClick.AddListener(() => SkipPressed?.Invoke());

            _fade = CutsceneStage.Quad(cameraTransform, "Fade", CutsceneArt.Solid(), Color.black, OrderFade);
            _fade.transform.localPosition = new Vector3(0f, 0f, 2.6f);
            FitToFrustum(_fade, 2.6f, 1.15f);
        }

        public void SetFade(float alpha)
        {
            FadeAlpha = Mathf.Clamp01(alpha);
            Color c = _fade.color;
            c.a = FadeAlpha;
            _fade.color = c;
            _fade.enabled = c.a > 0.002f;
        }

        public void SetVisible(bool visible)
        {
            _skip.gameObject.SetActive(visible);
        }

        private void FitToFrustum(SpriteRenderer renderer, float distance, float margin)
        {
            float h = 2f * distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float w = h * Mathf.Max(0.5f, _camera.aspect);
            CutsceneStage.SizeTo(renderer, w * margin, h * margin);
        }

        /// <summary>
        /// Lays out the "bt1" skip button in the top-right corner of the screen.  The canvas is a
        /// root object on purpose: a screen-space canvas nested under the camera would not be laid
        /// out against the screen.
        /// </summary>
        private static Button BuildSkipButton(Sprite plateSprite, TMP_FontAsset font)
        {
            var canvasGo = new GameObject("CutsceneUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();

            var go = new GameObject("bt1", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(canvasGo.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-SkipMargin, -SkipMargin);
            rect.sizeDelta = new Vector2(SkipWidth, SkipHeight);

            var plate = go.GetComponent<Image>();
            if (plateSprite != null)
            {
                // the button artwork is a wide plate, so let it keep its own shape instead of
                // stretching it into whatever rectangle the anchors produce
                plate.sprite = plateSprite;
                plate.preserveAspect = true;
                plate.color = Color.white;
            }
            else
            {
                // nothing wired up: a plain dark plate beats an invisible button
                plate.sprite = CutsceneArt.SoftRect();
                plate.color = new Color(0.10f, 0.06f, 0.09f, 0.55f);
            }

            var button = go.GetComponent<Button>();
            button.targetGraphic = plate;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = CutsceneText.Skip;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 28f;
            // dark ink reads on the bright plate artwork; the plain fallback plate needs light text
            label.color = plateSprite != null ? CutsceneTuning.Ink : new Color(1f, 0.93f, 0.80f, 0.95f);
            label.raycastTarget = false;

            return button;
        }

        /// <summary>
        /// The scene owns no EventSystem, so the button needs one.  Matches the module the rest of
        /// the game uses when the Input System package is in play, and falls back to the legacy
        /// one otherwise.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
