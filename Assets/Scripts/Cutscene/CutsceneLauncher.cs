using UnityEngine;
using UnityEngine.SceneManagement;

namespace FortressClash.Cutscene
{
    /// <summary>
    /// Entry points into the story: plays it once before the player ever sees the menu, and lets
    /// the menu play it again on demand.
    /// </summary>
    public static class CutsceneLauncher
    {
        /// <summary>Scene holding the book.  Must also be listed in the build settings.</summary>
        public const string CutsceneScene = "Cutscene";

        /// <summary>Scene the book hands over to when it is done.</summary>
        public const string MenuScene = "MainMenu";

        private const string SeenKey = "fortressclash.cutscene.seen.v1";

        private static bool _loading;

        /// <summary>True once the story has been played to the end (or skipped) on this device.</summary>
        public static bool HasSeen => PlayerPrefs.GetInt(SeenKey, 0) == 1;

        /// <summary>Set by the book.  Until it is set, MainMenu screens the story on first launch.</summary>
        public static void MarkSeen()
        {
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Forgets that the story was seen, so the next launch plays it again.</summary>
        public static void ResetSeen()
        {
            PlayerPrefs.DeleteKey(SeenKey);
            PlayerPrefs.Save();
        }

        /// <summary>Plays the story.  Safe to call from a button; does nothing if the scene is missing.</summary>
        public static void Play()
        {
            if (_loading)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(CutsceneScene))
            {
                Debug.LogWarning($"[Cutscene] Scene '{CutsceneScene}' is not in the build settings, so the story cannot play.");
                return;
            }

            _loading = true;
            SceneManager.LoadScene(CutsceneScene);
        }

        /// <summary>
        /// Screens the story the first time the player reaches the menu.  The flag is written
        /// before the scene is loaded, so a missing or broken cutscene can never trap the player
        /// in a loop.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HookFirstLaunch()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryScreenStory(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _loading = false;
            TryScreenStory(scene);
        }

        private static void TryScreenStory(Scene scene)
        {
            if (!scene.IsValid() || scene.name != MenuScene)
            {
                return;
            }

            if (HasSeen)
            {
                return;
            }

            MarkSeen();
            Debug.Log("[Cutscene] First launch: screening the story before the menu. "
                      + "Call CutsceneLauncher.ResetSeen() to see it again from the start.");
            Play();
        }
    }
}
