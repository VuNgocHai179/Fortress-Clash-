#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FortressClash.UI
{
    /// <summary>
    /// Cheat panel dùng để kiểm soát việc spawn lính ĐỎ (phe AI):
    ///   - BẬT/TẮT AI tự động spawn.
    ///   - Tự tay spawn từng loại lính đỏ (miễn phí).
    ///
    /// Panel tự tạo ra khi vào scene có <see cref="GameManager"/> (ví dụ Gameplay) nên KHÔNG cần
    /// sửa scene hay gán reference trong Inspector. Bấm F1 để ẩn/hiện panel.
    /// Toàn bộ file bị loại khỏi bản build phát hành (chỉ chạy ở Editor / Development Build).
    /// </summary>
    public class DebugCheatPanel : MonoBehaviour
    {
        private const string WindowTitle = "CHEAT - Điều khiển Slime Đỏ (F1: ẩn/hiện)";
        private const float MinButtonWidth = 260f;

        private static DebugCheatPanel instance;
        private static AI cachedAi;

        private static readonly RedUnitType[] UnitTypes =
        {
            RedUnitType.Sword,
            RedUnitType.Archer,
            RedUnitType.Slinger,
            RedUnitType.SpearMan,
            RedUnitType.BaseSlime,
            RedUnitType.Catapult
        };

        // Nhãn có cả tiếng Việt lẫn tiếng Anh để đọc được ngay cả khi font IMGUI thiếu dấu.
        private static readonly string[] UnitLabels =
        {
            "Kiếm / Sword",
            "Cung / Archer",
            "Ná / Slinger",
            "Giáo / SpearMan",
            "Slime cơ bản / BaseSlime",
            "Máy bắn đá / Catapult"
        };

        private bool visible = true;
        private Rect windowRect = new Rect(12f, 12f, 300f, 320f);

        /// <summary>
        /// Tự dựng panel sau khi scene đầu tiên load và sau MỖI lần đổi scene (để vào được Gameplay
        /// từ MainMenu). Chỉ tạo khi scene hiện tại thật sự có GameManager.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreate();
        }

        private static void TryCreate()
        {
            if (instance != null) return;

            // Chỉ có ý nghĩa ở màn chơi (scene có GameManager), bỏ qua MainMenu...
            if (FindAnyObjectByType<GameManager>() == null) return;

            GameObject panelObject = new GameObject("~DebugCheatPanel");
            DontDestroyOnLoad(panelObject);
            instance = panelObject.AddComponent<DebugCheatPanel>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (GameManager.Instance == null) return;   // đang ở scene không có game

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, WindowTitle);
        }

        private void DrawWindow(int windowId)
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null) return;

            GUILayout.Space(4f);
            GUILayout.Label($"Tiền AI: {gameManager.AIMoney}");

            GUILayout.Space(4f);
            DrawAiSpawnToggle(gameManager);

            GUILayout.Space(4f);
            DrawAiModeToggle();

            GUILayout.Space(8f);
            GUILayout.Label("Spawn lính đỏ:");
            DrawSpawnButtons(gameManager);

            GUILayout.Space(6f);
            GUILayout.Label("Kéo thanh tiêu đề để di chuyển panel.");
            GUILayout.Label("Lính spawn bằng cheat không trừ tiền AI.");

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawAiSpawnToggle(GameManager gameManager)
        {
            bool isOn = gameManager.aiAutoSpawnEnabled;
            string label = isOn ? "AI tự spawn: BẬT" : "AI tự spawn: TẮT";

            if (GUILayout.Button(label, GUILayout.Height(30f), GUILayout.MinWidth(MinButtonWidth)))
            {
                gameManager.aiAutoSpawnEnabled = !isOn;
                Debug.Log($"[Cheat] AI tự động spawn lính đỏ: {(gameManager.aiAutoSpawnEnabled ? "BẬT" : "TẮT")}");
            }
        }

        /// <summary>
        /// Đổi bộ não của AI ngay lúc đang chơi để so sánh:
        /// LUẬT (AIPolicy, đúng 100%) và MODEL ONNX (bản chưng cất từ chính bộ luật đó).
        /// </summary>
        private void DrawAiModeToggle()
        {
            AI ai = FindAi();
            if (ai == null)
            {
                GUILayout.Label("Không tìm thấy AI trong scene.");
                return;
            }

            string label = ai.mode == AIMode.RuleBased ? "AI bộ não: LUẬT" : "AI bộ não: MODEL ONNX";

            if (GUILayout.Button(label, GUILayout.Height(30f), GUILayout.MinWidth(MinButtonWidth)))
            {
                ai.mode = (ai.mode == AIMode.RuleBased) ? AIMode.NeuralNetwork : AIMode.RuleBased;
                Debug.Log($"[Cheat] AI chuyển sang bộ não: {ai.mode}");
            }

            GUILayout.Label($"Đang chạy: {(ai.IsUsingModel ? "model ONNX" : "bộ não luật")}");
            GUILayout.Label($"Quyết định gần nhất: {ai.LastDecision}");
        }

        private static AI FindAi()
        {
            if (cachedAi == null) cachedAi = FindAnyObjectByType<AI>();
            return cachedAi;
        }

        private void DrawSpawnButtons(GameManager gameManager)
        {
            for (int i = 0; i < UnitTypes.Length; i++)
            {
                if (GUILayout.Button(UnitLabels[i], GUILayout.Height(26f), GUILayout.MinWidth(MinButtonWidth)))
                {
                    GameObject spawned = gameManager.SpawnRedUnit(UnitTypes[i], gameManager.cheatSpawnIsFree);

                    if (spawned != null)
                    {
                        Debug.Log($"[Cheat] Đã spawn lính đỏ: {UnitLabels[i]}");
                    }
                }
            }
        }
    }
}
#endif
