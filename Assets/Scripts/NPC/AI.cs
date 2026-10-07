using System;
using System.Collections;
using Unity.InferenceEngine;
using UnityEngine;

/// <summary>Chế độ ra quyết định của AI.</summary>
public enum AIMode
{
    /// <summary>Dùng bộ não luật <see cref="AIPolicy"/>. Luôn chơi hợp lý và không cần model.</summary>
    RuleBased,

    /// <summary>
    /// Dùng model ONNX đã train lại từ <see cref="AIPolicy"/>.
    /// Model thiếu / lỗi / input sai kích thước thì tự động quay về <see cref="AIMode.RuleBased"/>.
    /// </summary>
    NeuralNetwork
}

/// <summary>
/// AI điều khiển phe đỏ.
///
/// ============================ CÁCH SỬA LỖI CŨ ============================
/// Bản cũ có 3 vấn đề:
///   1. Model ONNX được train trên một dataset Random Nhiên (không học được gì) và luôn chọn
///      Catapult bất kể đội hình người chơi -> không thể "hợp lý" bằng cách sửa code.
///   2. Input đưa vào model KHÔNG được chuẩn hoá (scalerMean = 0, scalerScale = 1) trong khi
///      model được train trên dữ liệu ĐÃ chuẩn hoá bằng StandardScaler.
///   3. Không có đường lui: chỉ cần model lỗi là AI đứng im, không có AI dự phòng.
///
/// Bản mới:
///   - <see cref="AIPolicy"/> là bộ não luật, là nguồn chân lý về cách AI nên chơi.
///   - Dataset mới sinh ra từ chính bộ não đó (Assets/Editor/AIDatasetGenerator.cs) nên model
///     học được thật, và lúc nào cũng có thể quay về luật nếu model tệ.
///   - Scaler thật được nhúng vào <see cref="ScalerMean"/>/<see cref="ScalerScale"/>.
///   - Nhịp ra lính riêng (<see cref="spawnInterval"/>) để AI không bị người chơi áp đảo về số lượng.
///   - ĐỘ KHÓ THEO LEVEL: nhịp ra lính, trần lính, mức khắc chế... đọc từ hồ sơ của level đang chơi
///     (GameManager.ActiveLevelSettings). Vì model ONNX là cố định nên mặc định dùng RuleBased.
/// ==========================================================================
/// </summary>
public class AI : MonoBehaviour
{
    /// <summary>19 đặc trưng: 6 lính người chơi + 6 lính AI + 6 đơn giá + tiền AI.</summary>
    public const int InputSize = 19;

    /// <summary>7 đầu ra: index 0 = WAITING, index (1 + AIUnitType) = mua loại lính đó.</summary>
    public const int OutputSize = 1 + AIPolicy.UnitCount;

    [Header("Chế độ AI")]
    [Tooltip("RuleBased = bộ não luật (AIPolicy) - ĐÂY LÀ CHẾ ĐỘ MẶC ĐỊNH vì nó biết độ khó của level " +
             "(nhịp ra lính, trần lính, độ khắc chế...).\n" +
             "NeuralNetwork = model ONNX đã train từ bộ não luật; model cố định, KHÔNG đổi hành vi theo " +
             "level được (muốn vậy phải train lại). Model lỗi thì tự quay về luật.")]
    public AIMode mode = AIMode.RuleBased;

    [Header("Nhịp ra lính")]
    [Tooltip("Số giây giữa 2 lần AI ra lính - CHỈ dùng khi level đang chơi không có hồ sơ riêng. " +
             "Level có trong bảng độ khó thì lấy nhịp của level đó (LevelSettings.aiSpawnInterval). " +
             "Người chơi có cooldown riêng (GameManager.chooseDelay).")]
    [Min(0.5f)]
    public float spawnInterval = 2f;

    [Tooltip("Log quyết định của AI ra Console (tắt khi build cho đỡ spam).")]
    public bool logDecisions = true;

    [Header("ONNX Model (chỉ dùng khi mode = NeuralNetwork)")]
    public ModelAsset modelAsset;

    /// <summary>
    /// Chuẩn hoá input cho model: (x - mean) / scale.
    /// Lấy ĐÚNG giá trị từ fortress_ai_scaler.pkl — train.py in ra 2 mảng này sau mỗi lần train lại,
    /// dán đè vào đây rồi mới bật mode NeuralNetwork.
    /// </summary>
    private static readonly float[] ScalerMean =
    {
        4.9506483078f, 5.0138888359f, 4.9796295166f, 5.0144443512f, 5.0517592430f, 5.0111112595f,   // lính người chơi
        4.9794445038f, 5.0004630089f, 5.0669445992f, 5.0010185242f, 5.0158333778f, 4.9774074554f,   // lính AI
        30.0125007629f, 40.0138893127f, 20.0069446564f, 50.0152778625f, 10.0013885498f, 60.0208320618f,   // đơn giá
        229.7031555176f                                                                             // tiền AI
    };

    private static readonly float[] ScalerScale =
    {
        3.1842412949f, 3.1668472290f, 3.1730289459f, 3.1512088776f, 3.1684818268f, 3.1448647976f,
        3.1755280495f, 3.1809141636f, 3.1707315445f, 3.1729638577f, 3.1820030212f, 3.1598188877f,
        6.2118158340f, 8.4630279541f, 4.2315139771f, 10.7244195938f, 2.2752916813f, 12.6945514679f,
        180.0689392090f
    };

    private Worker worker;
    private bool modelReady;

    /// <summary>Mô tả quyết định gần nhất của AI (panel cheat hiển thị để kiểm tra).</summary>
    public string LastDecision { get; private set; } = "—";

    /// <summary>AI đang thực sự dùng bộ não nào (model có thể hỏng giữa chừng và tự lùi về luật).</summary>
    public bool IsUsingModel => mode == AIMode.NeuralNetwork && modelReady;

    /// <summary>Hồ sơ độ khó của level đang chơi; null nếu level không có trong bảng độ khó.</summary>
    private static LevelSettings LevelProfile =>
        (GameManager.Instance != null) ? GameManager.Instance.ActiveLevelSettings : null;

    /// <summary>
    /// Nhịp ra lính thực tế: theo hồ sơ của level nếu có, ngược lại dùng <see cref="spawnInterval"/>
    /// trên Inspector. Đọc lại mỗi nhịp nên không sợ GameManager.Start() chạy sau AI.Start().
    /// Luôn kẹp >= 0.5s để một giá trị 0 lỡ cấu hình không làm AI ra lính mỗi frame.
    /// </summary>
    public float CurrentSpawnInterval
    {
        get
        {
            LevelSettings profile = LevelProfile;
            float interval = (profile != null) ? profile.aiSpawnInterval : spawnInterval;
            return Mathf.Max(0.5f, interval);
        }
    }

    /// <summary>Mức khắc chế đội hình người chơi theo level (0..1). Level không có hồ sơ thì tối đa (1).</summary>
    public float CurrentCounterAwareness
    {
        get
        {
            LevelSettings profile = LevelProfile;
            return (profile != null) ? profile.aiCounterAwareness : 1f;
        }
    }

    private void Start()
    {
        // MultiPlayer: phe đỏ do NGƯỜI CHƠI thứ hai (client) điều khiển, không còn là AI.
        // Tắt hẳn bộ não để nó không tranh mua lính với người chơi.
        if (NetworkRole.IsMultiplayer)
        {
            Debug.Log("[AI] Đang ở chế độ multiplayer (phe đỏ do người chơi điều khiển) => tắt AI.");
            enabled = false;
            return;
        }

        if (mode == AIMode.NeuralNetwork) LoadModel();

        StartCoroutine(RunAI());
    }

    private void LoadModel()
    {
        if (modelAsset == null)
        {
            Debug.LogWarning("AI: mode = NeuralNetwork nhưng chưa gán modelAsset => quay về bộ não luật.");
            return;
        }

        try
        {
            Model model = ModelLoader.Load(modelAsset);
            worker = new Worker(model, BackendType.GPUCompute);
            modelReady = true;
            Debug.Log("AI: đã nạp model ONNX.");
        }
        catch (Exception e)
        {
            modelReady = false;
            worker = null;
            Debug.LogWarning($"AI: nạp model ONNX thất bại ({e.Message}) => quay về bộ não luật.");
        }
    }

    private IEnumerator RunAI()
    {
        // Nhường 1 frame để MỌI Start() chạy xong (gồm GameManager.Start áp độ khó của level) rồi mới
        // đọc nhịp ra lính - tránh đọc nhầm nhịp mặc định khi GameManager.Start() chạy sau AI.Start().
        yield return null;

        Debug.Log($"AI: bộ não {(IsUsingModel ? "MODEL ONNX" : "LUẬT (RuleBased)")}, " +
                  $"ra lính mỗi {CurrentSpawnInterval:0.##}s, khắc chế x{CurrentCounterAwareness:0.##}.");

        while (true)
        {
            yield return new WaitForSeconds(CurrentSpawnInterval);
            Tick();
        }
    }

    private void Tick()
    {
        if (GameManager.Instance == null) return;

        AIState state = GameManager.Instance.GetAIState();
        int unit = Decide(state);
        LastDecision = Describe(unit);

        if (!GameManager.Instance.aiAutoSpawnEnabled)
        {
            // Cheat (DebugCheatPanel - F1): vẫn chạy não để xem nó nghĩ gì, nhưng không ra lính.
            if (logDecisions) Debug.Log($"[Cheat] AI auto-spawn đang TẮT => bỏ qua hành động '{Describe(unit)}'.");
            return;
        }

        // Đã đủ lính trên sân thì giữ tiền, chờ lính chết rồi ra con mới.
        if (GameManager.Instance.IsAIAtUnitLimit)
        {
            if (logDecisions)
            {
                Debug.Log($"AI: đã đủ {GameManager.Instance.maxUnitsPerSide} lính trên sân => chờ.");
            }

            return;
        }

        if (logDecisions) Debug.Log($"AI: {Describe(unit)} (tiền {state.money})");

        Spawn(unit);
    }

    /// <summary>Chọn hành động: ưu tiên model nếu bật và chạy được, ngược lại dùng bộ não luật.</summary>
    public int Decide(AIState state)
    {
        if (mode == AIMode.NeuralNetwork && modelReady)
        {
            int predicted = PredictWithModel(state);
            if (predicted != int.MinValue) return predicted;

            // Model hỏng giữa chừng -> từ giờ dùng luật cho tới hết ván.
            modelReady = false;
            Debug.LogWarning("AI: model ONNX không dùng được nữa => chuyển sang bộ não luật.");
        }

        return AIPolicy.Decide(state, CurrentCounterAwareness);
    }

    /// <summary>
    /// Chạy model. Trả về chỉ số trong <see cref="AIUnitType"/>, hoặc <see cref="AIPolicy.Waiting"/>.
    /// Trả về <see cref="int.MinValue"/> khi model không dùng được (để phía gọi quay về luật).
    /// </summary>
    private int PredictWithModel(AIState state)
    {
        Tensor<float> inputTensor = null;
        Tensor<float> outputTensor = null;

        try
        {
            float[] input = BuildFeatureVector(state);
            for (int i = 0; i < InputSize; i++)
            {
                float scale = ScalerScale[i];
                input[i] = (scale == 0f) ? 0f : (input[i] - ScalerMean[i]) / scale;
            }

            inputTensor = new Tensor<float>(new TensorShape(1, InputSize), input);
            worker.Schedule(inputTensor);

            outputTensor = worker.PeekOutput() as Tensor<float>;
            if (outputTensor == null) return int.MinValue;

            float[] output = outputTensor.DownloadToArray();
            if (output == null || output.Length != OutputSize) return int.MinValue;

            // index 0 = WAITING luôn hợp lệ; các index còn lại chỉ hợp lệ khi đủ tiền.
            int bestIndex = 0;
            float bestScore = output[0];

            for (int u = 0; u < AIPolicy.UnitCount; u++)
            {
                if (!CanAfford(state, u)) continue;

                int index = 1 + u;
                if (output[index] > bestScore)
                {
                    bestScore = output[index];
                    bestIndex = index;
                }
            }

            return bestIndex - 1;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AI: lỗi khi chạy model ONNX ({e.Message}).");
            return int.MinValue;
        }
        finally
        {
            inputTensor?.Dispose();
            outputTensor?.Dispose();
        }
    }

    /// <summary>
    /// Vector đặc trưng đưa vào model. THỨ TỰ PHẢI KHỚP dataset.csv do AIDatasetGenerator sinh ra:
    /// 0-5 số lính người chơi, 6-11 số lính AI, 12-17 đơn giá 6 loại, 18 tiền AI.
    /// </summary>
    public static float[] BuildFeatureVector(AIState state)
    {
        float[] features = new float[InputSize];

        for (int u = 0; u < AIPolicy.UnitCount; u++)
        {
            features[u] = state.playerCounts[u];
            features[AIPolicy.UnitCount + u] = state.ownCounts[u];
            features[(2 * AIPolicy.UnitCount) + u] = state.prices[u];
        }

        features[InputSize - 1] = state.money;
        return features;
    }

    private static bool CanAfford(AIState state, int unit)
    {
        int price = state.prices[unit];
        return AIPolicy.IsSanePrice(price) && state.money >= price;
    }

    private void Spawn(int unit)
    {
        if (unit < 0) return;   // WAITING

        GameManager gm = GameManager.Instance;

        switch ((AIUnitType)unit)
        {
            case AIUnitType.Sword: gm.RedSwordChoosen(); break;
            case AIUnitType.Archer: gm.RedArcherChoosen(); break;
            case AIUnitType.Slinger: gm.RedSlingerChoosen(); break;
            case AIUnitType.SpearMan: gm.RedSpearManChoosen(); break;
            case AIUnitType.BaseSlime: gm.RedBaseSlimeChoosen(); break;
            case AIUnitType.Catapult: gm.RedCatapultChoosen(); break;
        }
    }

    /// <summary>Tên hành động để log cho dễ đọc.</summary>
    private static string Describe(int unit)
    {
        if (unit < 0) return "WAITING";
        return "MUA " + ((AIUnitType)unit);
    }

    private void OnDestroy()
    {
        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }

        modelReady = false;
    }
}
