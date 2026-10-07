using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FortressClash.Cutscene
{
    /// <summary>
    /// Directs the pop-up fairy tale book: the hard cover swings open, each comic page turns, and
    /// every page shows its slide whole and flat, printed on the parchment.
    ///
    /// Drop this on a single empty GameObject - it builds the camera framing, the book and the
    /// printed pages at runtime, and falls back to the MainMenu scene when it is done or skipped.
    /// </summary>
    [DisallowMultipleComponent]
    public class CutsceneBook : MonoBehaviour
    {
        /// <summary>Draw order of the page lying open, and of the one waiting underneath it.</summary>
        private const int OrderCurrent = 200;
        private const int OrderNext = 100;
        private const int OrderTurnShadow = 190;

        [Header("Artwork")]
        [Tooltip("Front cover of the book (Slide_Cover).")]
        public Sprite cover;

        [Tooltip("The comic pages, in reading order (Slide1..Slide5).")]
        public Sprite[] pages = new Sprite[0];

        [Tooltip("Plate artwork for the skip button (bt1). Falls back to a plain dark plate when empty.")]
        public Sprite skipButton;

        [Header("Presentation")]
        [Tooltip("SDF font for the hint line, page counter and skip label. Defaults to the TMP default.")]
        public TMP_FontAsset font;

        [Tooltip("Scene to load when the story ends or is skipped.")]
        public string nextScene = "MainMenu";

        [Tooltip("Play as soon as the scene starts. Turn off to trigger the book by hand.")]
        public bool playOnStart = true;

        private readonly List<CutscenePage> _book = new List<CutscenePage>();

        private CutsceneStage _stage;
        private CutsceneOverlay _overlay;
        private Camera _camera;

        private float _clock;
        private float _speed = 1f;
        private Vector3 _cameraHome;
        private bool _advanceRequested;
        private bool _skipRequested;
        private bool _running;
        private bool _finished;

        private void Start()
        {
            if (playOnStart)
            {
                Begin();
            }
        }

        private void OnDestroy()
        {
            Cursor.visible = true;
        }

        /// <summary>Builds the scene and starts the story.</summary>
        public void Begin()
        {
            if (_running || _finished)
            {
                return;
            }

            if (!BuildScene())
            {
                Finish();
                return;
            }

            _running = true;
            StartCoroutine(Run());
        }

        /// <summary>Leaves the cutscene right away (used by the skip label and the Escape key).</summary>
        public void Skip()
        {
            _skipRequested = true;
        }

        private bool BuildScene()
        {
            CutsceneLayoutDocument layout = CutsceneLayoutData.Load();
            if (layout == null || layout.pages == null || layout.pages.Length == 0)
            {
                return false;
            }

            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("CutsceneCamera");
                _camera = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }

            if (FindFirstObjectByType<AudioListener>() == null)
            {
                _camera.gameObject.AddComponent<AudioListener>();
            }

            Sprite first = FindSlide(layout.pages[0].slide, 0);
            if (first == null)
            {
                Debug.LogError("[Cutscene] No page sprites are assigned, so there is no book to show.");
                return false;
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            _stage = new CutsceneStage(transform, _camera, cover,
                                       (int)first.rect.width, (int)first.rect.height,
                                       layout.pages[0].w / (float)layout.pages[0].h);
            _overlay = new CutsceneOverlay(_camera.transform, _camera, font, TMP_Settings.defaultFontAsset, skipButton);
            _overlay.SkipPressed += Skip;
            _overlay.SetVisible(false);
            _overlay.SetFade(1f);

            float pivotX = -_stage.Geometry.PageW * 0.5f;
            for (int i = 0; i < layout.pages.Length; i++)
            {
                Sprite slide = FindSlide(layout.pages[i].slide, i);
                var page = new CutscenePage($"Page{i + 1}", _stage.BookRoot, new Vector3(pivotX, 0f, -0.004f * i));
                if (!page.Build(layout.pages[i], slide, _stage.Geometry))
                {
                    Debug.LogWarning($"[Cutscene] Page '{layout.pages[i].slide}' could not be built and is skipped.");
                    continue;
                }

                _book.Add(page);
            }

            if (_book.Count == 0)
            {
                return false;
            }

            _stage.SetTurnShadowOrder(OrderTurnShadow);
            _stage.SetCoverAngle(0f);
            PrepareStack(0);
            return true;
        }

        /// <summary>Matches a layout entry to the sprite assigned for it, by name first.</summary>
        private Sprite FindSlide(string slideName, int index)
        {
            if (pages != null && !string.IsNullOrEmpty(slideName))
            {
                foreach (Sprite sprite in pages)
                {
                    if (sprite != null && string.Equals(sprite.name, slideName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return sprite;
                    }
                }

                foreach (Sprite sprite in pages)
                {
                    if (sprite != null && sprite.name.StartsWith(slideName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return sprite;
                    }
                }
            }

            var usable = new List<Sprite>();
            if (pages != null)
            {
                foreach (Sprite sprite in pages)
                {
                    if (sprite != null)
                    {
                        usable.Add(sprite);
                    }
                }
            }

            return index < usable.Count ? usable[index] : null;
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            _stage?.AnimateAmbient(_clock);

            if (!_running)
            {
                return;
            }

            DriftCamera();

            if (CutsceneInput.EscapeDown)
            {
                _skipRequested = true;
                return;
            }

            // the story turns its own pages; a tap outside the skip button only hurries it along
            if (CutsceneInput.TapDown && !(_overlay?.PointerOverUi ?? false))
            {
                _advanceRequested = true;
            }
        }

        /// <summary>
        /// Nudges the camera with the pointer (plus a slow idle breath on touch devices).  The
        /// page, the cards floating above it and the dust behind all move by different amounts,
        /// which is what sells the depth of the layers.
        /// </summary>
        private void DriftCamera()
        {
            if (_camera == null)
            {
                return;
            }

            if (_cameraHome == Vector3.zero)
            {
                _cameraHome = _camera.transform.position;
            }

            float sway = _stage.VisibleWidth * 0.016f;
            Vector2 pointer = CutsceneInput.PointerPosition;
            float px = Screen.width > 0 ? Mathf.Clamp(pointer.x / Screen.width * 2f - 1f, -1f, 1f) : 0f;
            float py = Screen.height > 0 ? Mathf.Clamp(pointer.y / Screen.height * 2f - 1f, -1f, 1f) : 0f;

            var target = _cameraHome + new Vector3(
                (px * 0.7f + Mathf.Sin(_clock * 0.23f) * 0.3f) * sway,
                (py * 0.5f + Mathf.Sin(_clock * 0.17f) * 0.3f) * sway * 0.7f,
                0f);

            _camera.transform.position = Vector3.Lerp(_camera.transform.position, target,
                                                       1f - Mathf.Exp(-3.2f * Time.deltaTime));
        }

        private IEnumerator Run()
        {
            _overlay.SetFade(1f);
            yield return FadeTo(0f, CutsceneTuning.FadeIn);

            _overlay.SetVisible(true);
            yield return HoldFor(CutsceneTuning.CoverPause);
            yield return OpenCover();

            for (int i = 0; i < _book.Count; i++)
            {
                if (_skipRequested)
                {
                    break;
                }

                if (i > 0)
                {
                    yield return TurnTo(i);
                }

                yield return PopPage(_book[i]);
                if (_skipRequested)
                {
                    break;
                }

                yield return HoldFor(ReadSeconds(_book[i]));
                yield return FoldPage(_book[i]);
            }

            if (!_skipRequested)
            {
                yield return TurnLastAway();
                yield return CloseCover();
            }

            yield return FadeTo(1f, _skipRequested ? 0.35f : CutsceneTuning.FadeOut);
            Finish();
        }

        // ---- story beats -------------------------------------------------------------------

        private IEnumerator OpenCover()
        {
            _stage.SetCoverAlpha(1f);
            yield return Animate(CutsceneTuning.CoverOpen, t =>
            {
                _stage.SetCoverAngle(Mathf.Lerp(0f, 168f, CutsceneTween.EaseInOutCubic(t)));
            });

            // the cover has swung off to the left of the book; let it go so the page reads clean
            yield return Animate(0.32f, t => _stage.SetCoverAlpha(1f - t));
        }

        private IEnumerator CloseCover()
        {
            _stage.SetCoverAlpha(0f);
            float fade = 0.22f;
            float start = 168f;
            yield return Animate(fade, t => _stage.SetCoverAlpha(t));
            yield return Animate(CutsceneTuning.BookClose, t =>
            {
                float e = CutsceneTween.SmoothStep(t);
                _stage.SetCoverAngle(Mathf.Lerp(start, 0f, e));
            });
        }

        private IEnumerator TurnTo(int index)
        {
            CutscenePage leaving = _book[index - 1];
            CutscenePage arriving = _book[index];

            // the arriving page waits underneath in the back of the stack while the old one lifts
            // away, and only takes the front slot once the turn is over
            arriving.SetBaseOrder(OrderNext);
            arriving.SetDepth(-0.004f * index);
            arriving.SetTurnAngle(0f);
            arriving.SetBow(0f);
            arriving.SetShade(0f);
            arriving.SetFolded();
            arriving.SetVisible(true);
            for (int k = index + 1; k < _book.Count; k++)
            {
                _book[k].SetVisible(false);
            }

            yield return Animate(CutsceneTuning.PageTurn, t =>
            {
                float e = CutsceneTween.EaseInOutCubic(t);
                float bow = CutsceneTween.Bump(t);
                leaving.SetTurnAngle(176f * e);
                leaving.SetBow(bow);
                leaving.SetShade(0.62f * bow);
                _stage.SetTurnShadowAlpha(0.55f * bow);
            });

            leaving.SetVisible(false);
            leaving.SetTurnAngle(0f);
            leaving.SetBow(0f);
            leaving.SetShade(0f);
            _stage.SetTurnShadowAlpha(0f);
            PrepareStack(index);
        }

        private IEnumerator TurnLastAway()
        {
            if (_book.Count == 0)
            {
                yield break;
            }

            CutscenePage last = _book[_book.Count - 1];
            yield return Animate(CutsceneTuning.PageTurn, t =>
            {
                float e = CutsceneTween.EaseInOutCubic(t);
                float bow = CutsceneTween.Bump(t);
                last.SetTurnAngle(176f * e);
                last.SetBow(bow);
                last.SetShade(0.62f * bow);
                _stage.SetTurnShadowAlpha(0.55f * bow);
            });

            last.SetVisible(false);
            _stage.SetTurnShadowAlpha(0f);
        }

        private IEnumerator PopPage(CutscenePage page)
        {
            yield return Animate(page.PopDuration, t => page.SetProgress(t));
        }

        private IEnumerator FoldPage(CutscenePage page)
        {
            _advanceRequested = false;
            float duration = Mathf.Max(0.35f, page.PopDuration * 0.45f);
            yield return Animate(duration, t => page.SetProgress(1f - CutsceneTween.EaseInOutCubic(t)));
        }

        private float ReadSeconds(CutscenePage page) =>
            Mathf.Max(CutsceneTuning.ReadTimeMin, page.Beats * CutsceneTuning.ReadTimePerPanel);

        // ---- plumbing ----------------------------------------------------------------------

        /// <summary>Shows the page at <paramref name="index"/> over the one behind it, everything folded.</summary>
        private void PrepareStack(int index)
        {
            for (int k = 0; k < _book.Count; k++)
            {
                CutscenePage page = _book[k];
                bool isCurrent = k == index;
                bool isNext = k == index + 1;

                page.SetVisible(isCurrent || isNext);
                page.SetBaseOrder(isCurrent ? OrderCurrent : OrderNext);
                page.SetDepth(-0.004f * k);
                page.SetTurnAngle(0f);
                page.SetBow(0f);
                page.SetShade(0f);
                page.SetFolded();
            }
        }

        private IEnumerator Animate(float duration, System.Action<float> apply)
        {
            float t = 0f;
            if (duration <= 0f)
            {
                apply(1f);
                yield break;
            }

            while (t < duration)
            {
                if (_skipRequested)
                {
                    apply(1f);
                    yield break;
                }

                _speed = _advanceRequested ? CutsceneTuning.FastForwardRate : 1f;
                t += Time.deltaTime * _speed;
                apply(CutsceneTween.Clamp01(t / duration));
                yield return null;
            }

            apply(1f);
            _advanceRequested = false;
        }

        private IEnumerator FadeTo(float alpha, float duration)
        {
            float from = _overlay.FadeAlpha;
            yield return Animate(duration, t => _overlay.SetFade(Mathf.Lerp(from, alpha, t)));
        }

        /// <summary>
        /// Holds the beat for the given time and lets the story carry on by itself: it is a plain
        /// timer, not a gate.  Tapping (outside the skip button) only cuts the hold short, so a
        /// reader who is in a hurry can move things along without having to.
        /// </summary>
        private IEnumerator HoldFor(float seconds)
        {
            float t = 0f;
            _advanceRequested = false;
            while (t < seconds)
            {
                if (_skipRequested || _advanceRequested)
                {
                    break;
                }

                t += Time.deltaTime;
                yield return null;
            }

            _advanceRequested = false;
        }

        private void Finish()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            _running = false;
            StopAllCoroutines();
            CutsceneLauncher.MarkSeen();

            if (!string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene))
            {
                SceneManager.LoadScene(nextScene);
                return;
            }

            Debug.LogWarning($"[Cutscene] '{nextScene}' is not in the build settings; staying in this scene.");
        }
    }
}
