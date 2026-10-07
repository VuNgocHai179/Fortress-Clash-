using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Script TẠM để kiểm tra tự động bằng Unity batch mode. XOÁ sau khi dùng.</summary>
public static class VerifyAudio
{
    private const string SeenKey = "fortressclash.cutscene.seen.v1";

    private static readonly string[] BuildScenes =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/Gameplay.unity",
        "Assets/Scenes/MultiPlayer.unity",
        "Assets/Scenes/MultiPlayerConnect.unity",
        "Assets/Scenes/Cutscene.unity",
    };

    private static int failures;
    private static bool playEntered;
    private static int waitTicks;
    private static int playFrames;
    private static bool hadSeenKey;
    private static int originalSeen;

    private static void Line(string text) => Debug.Log("[VERIFY] " + text);
    private static void Fail(string text) { failures++; Debug.LogError("[VERIFY] FAIL " + text); }

    public static void Run()
    {
        failures = 0;

        Line("---- 1. prefab trong Resources ----");
        GameObject prefab = Resources.Load<GameObject>("AudioManager");
        if (prefab == null)
        {
            Fail("Resources.Load<GameObject>(\"AudioManager\") == null -> game se khong co tieng");
        }
        else
        {
            Line($"OK   prefab: {AssetDatabase.GetAssetPath(prefab)}");
            AudioManager component = prefab.GetComponent<AudioManager>();
            if (component == null)
            {
                Fail("prefab thieu component AudioManager");
            }
            else
            {
                SerializedObject so = new SerializedObject(component);
                foreach (string field in new[]
                         { "bgmClip", "cutsceneClip", "attackClip", "buttonClickClip", "loseClip", "upgradeClip", "winClip" })
                {
                    Object value = so.FindProperty(field)?.objectReferenceValue;
                    if (value == null) Fail($"{field} chua duoc gan");
                    else Line($"OK   {field} = {value.name}");
                }

                Object move = so.FindProperty("moveClip")?.objectReferenceValue;
                Line($"info moveClip = {(move != null ? move.name : "<trong>")} (da chon de trong)");
                Line($"info musicVolume={so.FindProperty("musicVolume").floatValue} sfxVolume={so.FindProperty("sfxVolume").floatValue}");
            }
        }

        Line("---- 2. scene trong Build Settings ----");
        bool sceneHasAudioManager = false;
        foreach (string path in BuildScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int missing = 0, total = 0, audioManagers = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component c in root.GetComponentsInChildren<Component>(true))
                {
                    total++;
                    if (c == null) missing++;
                }

                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.GetComponent("AudioManager") != null) audioManagers++;
                }
            }

            sceneHasAudioManager |= audioManagers > 0;
            string status = missing == 0 ? "OK  " : "FAIL";
            if (missing != 0) failures++;
            Line($"{status} {path} components={total} missingScripts={missing} AudioManagerComponents={audioManagers}");
        }

        if (sceneHasAudioManager) Line("info co scene van con gam tay component AudioManager (khong sao, ban trung tu huy)");

        Line("---- 3. chay THAT: gia lap lan choi thu 2 (Cutscene bi bo qua) ----");
        hadSeenKey = PlayerPrefs.HasKey(SeenKey);
        originalSeen = PlayerPrefs.GetInt(SeenKey, 0);
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
        Line($"info cutscene.seen duoc dat = 1 (truoc do hadKey={hadSeenKey} value={originalSeen})");

        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += OnUpdate;
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) playEntered = true;
    }

    private static void OnUpdate()
    {
        if (!playEntered)
        {
            if (++waitTicks > 20000) Fail("khong vao duoc Play Mode trong batch mode");
            return;
        }

        playFrames++;

        // Frame 1: dung ngay sau MainMenu load xong (Bootstrap da chay o AfterSceneLoad).
        if (playFrames == 1)
        {
            CheckInstance("sau khi MainMenu load (Cutscene bi bo qua)");
            Line("info dang load scene Gameplay...");
            SceneManager.LoadScene("Gameplay");
            return;
        }

        // Cho vai frame cho scene Gameplay load xong roi kiem tra lai.
        if (playFrames >= 8)
        {
            CheckInstance("sau khi da sang scene Gameplay");
            Finish();
        }
    }

    private static void CheckInstance(string label)
    {
        AudioManager instance = AudioManager.Instance;
        if (instance == null)
        {
            Fail($"{label}: AudioManager.Instance == null -> game KHONG co tieng");
            return;
        }

        SerializedObject so = new SerializedObject(instance);
        AudioSource music = so.FindProperty("musicSource")?.objectReferenceValue as AudioSource;
        AudioSource sfx = so.FindProperty("sfxSource")?.objectReferenceValue as AudioSource;
        string clip = music != null && music.clip != null ? music.clip.name : "<null>";

        Line($"OK   {label}: Instance!=null, scene={SceneManager.GetActiveScene().name}, " +
             $"musicClip={clip}, musicIsPlaying={music != null && music.isPlaying}, sfxCreated={sfx != null}, " +
             $"dontDestroyOnLoad={instance.gameObject.scene.name == "DontDestroyOnLoad"}");
    }

    private static void Finish()
    {
        EditorApplication.update -= OnUpdate;

        if (hadSeenKey) PlayerPrefs.SetInt(SeenKey, originalSeen);
        else PlayerPrefs.DeleteKey(SeenKey);
        PlayerPrefs.Save();
        Line("info da khoi phuc cutscene.seen ve gia tri ban dau");

        Line($"RESULT failures={failures}");
        if (failures == 0) Debug.Log("[VERIFY] ALL PASS");
        else Debug.LogError("[VERIFY] CO LOI");

        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
