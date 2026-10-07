using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sinh lại dataset huấn luyện cho AI từ chính bộ não luật <see cref="AIPolicy"/>.
///
/// VÌ SAO CẦN: dataset cũ (dataset.csv) là dữ liệu ngẫu nhiên tổng hợp — nhãn không liên quan gì
/// tới đặc trưng, nên model chỉ học vẹt và luôn chọn Catapult. Nay nhãn do <see cref="AIPolicy"/>
/// sinh ra, tức model được "chưng cất" (distill) từ một chính sách chơi hợp lý => học được thật.
///
/// QUY TRÌNH CẬP NHẬT AI:
///   1. Menu  Tools ▸ Fortress Clash ▸ Generate AI Dataset   (sinh lại dataset.csv)
///   2. Chạy   python train.py                                (train + export fortress_ai_model.onnx)
///   3. Copy fortress_ai_model.onnx đè vào Assets/Scripts/NPC/
///   4. Dán 2 mảng ScalerMean / ScalerScale mà train.py in ra vào AI.cs
///   5. Đặt AI.mode = NeuralNetwork
///
/// Nếu chỉ muốn AI chơi hợp lý ngay thì không cần làm gì: để AI.mode = RuleBased là xong.
/// </summary>
public static class AIDatasetGenerator
{
    private const string OutputPath = "dataset.csv";

    /// <summary>Số dòng dataset sinh ra. Dataset cũ chỉ 100 dòng nên model overfit hoàn toàn.</summary>
    private const int RowCount = 12000;

    private const int Seed = 20260101;

    /// <summary>Số lính tối đa mỗi loại khi lấy mẫu (khớp khoảng thực tế trên sân).</summary>
    private const int MaxUnitsPerType = 10;

    /// <summary>Tiền tối đa khi lấy mẫu.</summary>
    private const int MaxMoney = 600;

    /// <summary>Các hệ số nhân giá dùng khi lấy mẫu, để model học được cảm giác về giá.</summary>
    private static readonly float[] PriceFactors = { 0.7f, 0.85f, 1f, 1.15f, 1.3f };

    /// <summary>Đường dẫn 6 prefab lính đỏ — nguồn giá duy nhất, khớp với GameManager.</summary>
    private static readonly string[] RedPrefabPaths =
    {
        "Assets/Prefab/Red/SwordEnemy.prefab",
        "Assets/Prefab/Red/ArcherEnemy.prefab",
        "Assets/Prefab/Red/SlingShotEnemy.prefab",
        "Assets/Prefab/Red/SpearManEnemy.prefab",
        "Assets/Prefab/Red/RedBaseSlime.prefab",
        "Assets/Prefab/Red/CatapultEnemy.prefab"
    };

    /// <summary>Tên nhãn theo đúng thứ tự <see cref="AIUnitType"/>. PHẢI khớp danh sách actions trong train.py.</summary>
    private static readonly string[] ActionNames =
    {
        "CHOOSESWORD",
        "CHOOSEARCHER",
        "CHOOSESLINGER",
        "CHOOSESPEARMAN",
        "CHOOSEBASESLIME",
        "CHOOSECATAPULT"
    };

    [MenuItem("Tools/Fortress Clash/Generate AI Dataset")]
    public static void Generate()
    {
        int[] basePrices = ReadPricesFromPrefabs();
        if (basePrices == null) return;

        Random.InitState(Seed);

        string[] headers =
        {
            "PlayerSwordNumber", "PlayerArcherNumber", "PlayerSlingerNumber",
            "PlayerSpearManNumber", "PlayerBaseSlimeNumber", "PlayerCatapultNumber",

            "AISwordNumber", "AIArcherNumber", "AISlingerNumber",
            "AISpearManNumber", "AIBaseSlimeNumber", "AICatapultNumber",

            "AISwordPrice", "AIArcherPrice", "AISlingerPrice",
            "AISpearManPrice", "AIBaseSlimePrice", "AICatapultPrice",

            "AIMoney", "Action"
        };

        StringBuilder csv = new StringBuilder();
        csv.Append(string.Join(",", headers)).Append('\n');

        int[] labelCounts = new int[AIPolicy.UnitCount + 1];   // +1 cho WAITING

        for (int row = 0; row < RowCount; row++)
        {
            int[] playerCounts = SampleCounts();
            int[] ownCounts = SampleCounts();
            int[] prices = SamplePrices(basePrices);

            // Tiền nghiêng về khoảng thấp cho giống ván đấu thật, nhưng vẫn phủ tới MaxMoney.
            int money = Mathf.RoundToInt(Mathf.Pow(Random.value, 1.6f) * MaxMoney);

            int decision = AIPolicy.Decide(new AIState(playerCounts, ownCounts, prices, money));
            labelCounts[decision + 1]++;

            for (int u = 0; u < AIPolicy.UnitCount; u++) csv.Append(playerCounts[u]).Append(',');
            for (int u = 0; u < AIPolicy.UnitCount; u++) csv.Append(ownCounts[u]).Append(',');
            for (int u = 0; u < AIPolicy.UnitCount; u++) csv.Append(prices[u]).Append(',');

            csv.Append(money).Append(',');
            csv.Append(decision < 0 ? "WAITING" : ActionNames[decision]);
            csv.Append('\n');
        }

        File.WriteAllText(Path.Combine(ProjectRoot(), OutputPath), csv.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();

        LogLabelCounts(labelCounts);
        Debug.Log($"[AIDatasetGenerator] Đã ghi {RowCount} dòng vào {OutputPath}. " +
                  "Bước tiếp theo: chạy `python train.py`.");
    }

    [MenuItem("Tools/Fortress Clash/Log AI Policy Samples")]
    public static void LogSamples()
    {
        int[] prices = ReadPricesFromPrefabs();
        if (prices == null) return;

        int[][] playerSamples = { new[] { 0, 0, 0, 0, 0, 0 }, new[] { 0, 5, 0, 0, 0, 2 }, new[] { 6, 0, 0, 3, 0, 0 } };
        int[][] ownSamples = { new[] { 0, 0, 0, 0, 0, 0 }, new[] { 3, 0, 0, 1, 0, 0 }, new[] { 0, 2, 2, 0, 0, 1 } };
        int[] moneySamples = { 0, 20, 60, 200 };

        foreach (int[] player in playerSamples)
        {
            foreach (int[] own in ownSamples)
            {
                foreach (int money in moneySamples)
                {
                    int decision = AIPolicy.Decide(new AIState(player, own, prices, money));
                    Debug.Log($"Player[{string.Join(",", player)}] vs AI[{string.Join(",", own)}] " +
                              $"tiền {money} -> {(decision < 0 ? "WAITING" : ActionNames[decision])}");
                }
            }
        }
    }

    private static int[] SampleCounts()
    {
        int[] counts = new int[AIPolicy.UnitCount];
        for (int u = 0; u < AIPolicy.UnitCount; u++)
        {
            counts[u] = Random.Range(0, MaxUnitsPerType + 1);
        }

        return counts;
    }

    private static int[] SamplePrices(int[] basePrices)
    {
        float factor = PriceFactors[Random.Range(0, PriceFactors.Length)];

        int[] prices = new int[AIPolicy.UnitCount];
        for (int u = 0; u < AIPolicy.UnitCount; u++)
        {
            prices[u] = Mathf.Max(1, Mathf.RoundToInt(basePrices[u] * factor));
        }

        return prices;
    }

    /// <summary>Đọc đơn giá từ 6 prefab lính đỏ. Trả về null nếu thiếu prefab.</summary>
    private static int[] ReadPricesFromPrefabs()
    {
        int[] prices = new int[AIPolicy.UnitCount];

        for (int u = 0; u < AIPolicy.UnitCount; u++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RedPrefabPaths[u]);
            if (prefab == null)
            {
                Debug.LogError($"[AIDatasetGenerator] Không tìm thấy prefab '{RedPrefabPaths[u]}'.");
                return null;
            }

            Slime slime = prefab.GetComponent<Slime>();
            if (slime == null)
            {
                Debug.LogError($"[AIDatasetGenerator] Prefab '{RedPrefabPaths[u]}' không có component Slime.");
                return null;
            }

            prices[u] = slime.price;
        }

        Debug.Log($"[AIDatasetGenerator] Đơn giá lính đỏ: {string.Join(", ", prices)}");
        return prices;
    }

    private static void LogLabelCounts(int[] counts)
    {
        StringBuilder sb = new StringBuilder("[AIDatasetGenerator] Phân bố nhãn: WAITING=");
        sb.Append(counts[0]);

        for (int u = 0; u < AIPolicy.UnitCount; u++)
        {
            sb.Append($", {ActionNames[u]}={counts[u + 1]}");
        }

        Debug.Log(sb.ToString());
    }

    private static string ProjectRoot()
    {
        // Application.dataPath = <project>/Assets
        return Directory.GetParent(Application.dataPath).FullName;
    }
}
