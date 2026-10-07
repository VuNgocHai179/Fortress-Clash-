using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FortressClash.Cutscene.EditorTools
{
    /// <summary>
    /// Editor helpers for the pop-up book cutscene: (re)create its scene and check that everything
    /// the book needs is actually wired up.
    /// </summary>
    public static class CutsceneSceneTool
    {
        private const string Menu = "Tools/Fortress Clash/Cutscene/";

        private const string ScenePath = "Assets/Scenes/Cutscene.unity";
        private const string SlideFolder = "Assets/Textures/CutScene";
        private const string CoverName = "Slide_Cover";
        private const string FontPath = "Assets/Font/Mitr-SemiBold SDF.asset";
        /// <summary>Plate artwork for the cutscene's skip button.</summary>
        private const string SkipButtonPath = "Assets/Textures/button/bt1.png";

        [MenuItem(Menu + "Create or Refresh Cutscene Scene")]
        public static void CreateScene()
        {
            var slides = LoadSlides();
            if (slides.Count == 0)
            {
                EditorUtility.DisplayDialog("Cutscene",
                    $"No slide sprites found in {SlideFolder}.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = CutsceneTuning.CameraFov;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CutsceneTuning.RoomDark;
            camera.transform.position = new Vector3(0f, 0f, -CutsceneTuning.CameraDepth);
            cameraGo.AddComponent<AudioListener>();

            var bookGo = new GameObject("CutsceneBook");
            CutsceneBook book = bookGo.AddComponent<CutsceneBook>();
            book.cover = LoadSprite(CoverName);
            book.pages = slides.ToArray();
            book.skipButton = LoadSpriteAt(SkipButtonPath);
            book.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            book.nextScene = CutsceneLauncher.MenuScene;
            book.playOnStart = true;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Cutscene] Wrote {ScenePath} with {slides.Count} pages and registered it in the build settings.");
            VerifySetup();
        }

        [MenuItem(Menu + "Verify Setup")]
        public static void VerifySetup()
        {
            var problems = new List<string>();

            if (!File.Exists(ScenePath))
            {
                problems.Add($"{ScenePath} does not exist.");
            }
            else if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled))
            {
                problems.Add($"{ScenePath} is not enabled in the build settings, so the story cannot be loaded.");
            }

            if (LoadSprite(CoverName) == null)
            {
                problems.Add($"Missing cover sprite {SlideFolder}/{CoverName}.png");
            }

            if (LoadSpriteAt(SkipButtonPath) == null)
            {
                problems.Add($"Missing skip button sprite {SkipButtonPath}; the button will fall back to a plain plate.");
            }

            List<Sprite> slides = LoadSlides();
            if (slides.Count == 0)
            {
                problems.Add($"No slide sprites in {SlideFolder}.");
            }

            var layout = Resources.Load<TextAsset>(CutsceneLayoutData.ResourcePath);
            if (layout == null)
            {
                problems.Add($"Missing Resources/{CutsceneLayoutData.ResourcePath}.json");
            }
            else
            {
                var doc = JsonUtility.FromJson<CutsceneLayoutDocument>(layout.text);
                int panels = doc.pages.Sum(p => p.panels.Length);
                int captions = doc.pages.Sum(p => p.panels.Sum(panel => panel.captions.Length));
                if (slides.Count > 0 && doc.pages.Length != slides.Count)
                {
                    problems.Add($"The layout has {doc.pages.Length} pages but {slides.Count} page sprites are present.");
                }

                Debug.Log($"[Cutscene] Layout: {doc.pages.Length} pages, {panels} pop-up panels, {captions} caption cards.");
            }

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) == null)
            {
                problems.Add($"Font {FontPath} not found; the overlay will fall back to the TMP default.");
            }

            if (problems.Count == 0)
            {
                Debug.Log("[Cutscene] Setup looks good: scene, build settings, artwork, layout and font are all in place.");
                return;
            }

            foreach (string problem in problems)
            {
                Debug.LogWarning($"[Cutscene] {problem}");
            }
        }

        [MenuItem(Menu + "Replay Story On Next Launch")]
        public static void ReplayNextLaunch()
        {
            CutsceneLauncher.ResetSeen();
            Debug.Log("[Cutscene] The story will play again the next time the MainMenu scene loads.");
        }

        private static List<Sprite> LoadSlides()
        {
            var slides = new List<Sprite>();
            if (!AssetDatabase.IsValidFolder(SlideFolder))
            {
                return slides;
            }

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { SlideFolder });
            var found = new List<(string name, Sprite sprite)>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null && Path.GetFileNameWithoutExtension(path).StartsWith("Slide") &&
                    !Path.GetFileNameWithoutExtension(path).Contains("Cover"))
                {
                    found.Add((Path.GetFileNameWithoutExtension(path), sprite));
                }
            }

            // Slide1, Slide2, ... in numeric order
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach ((string _, Sprite sprite) in found)
            {
                slides.Add(sprite);
            }

            return slides;
        }

        private static Sprite LoadSprite(string name)
        {
            string path = $"{SlideFolder}/{name}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null && File.Exists(path))
            {
                Debug.LogWarning($"[Cutscene] {path} is not imported as a Sprite; set its Texture Type to 'Sprite (2D and UI)'.");
            }

            return sprite;
        }

        /// <summary>
        /// Loads a sprite by asset path.  A texture sliced into separate sprites (sprite mode
        /// "Multiple") has no main Sprite asset to load, so the first sub-sprite is taken instead -
        /// which is what bt1.png is.
        /// </summary>
        private static Sprite LoadSpriteAt(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sub)
                {
                    return sub;
                }
            }

            if (File.Exists(path))
            {
                Debug.LogWarning($"[Cutscene] {path} is not imported as a Sprite; set its Texture Type to 'Sprite (2D and UI)'.");
            }

            return null;
        }

        private static void AddToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
