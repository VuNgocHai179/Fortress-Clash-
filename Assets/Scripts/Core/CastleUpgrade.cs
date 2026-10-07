using System.Collections;
using UnityEngine;

/// <summary>
/// Hệ thống NÂNG CẤP THÀNH. Một component gắn cho MỘT thành; phe sở hữu do
/// <see cref="aiSide"/> quyết định (tắt = thành người chơi/xanh, bật = thành AI/đỏ).
///
/// - Thành bắt đầu ở level 0 và nâng tối đa <see cref="maxLevel"/> lần (mặc định 2).
/// - Mỗi lần nâng tốn tiền (<see cref="upgradeCosts"/>), đổi lại 2 thứ:
///     1. Tài nguyên về nhiều hơn: tiền King slime nhả ra (người chơi không còn tiền/giây) và
///        tiền mỗi giây của phe AI.
///     2. Lính MỚI SPAWN sau khi nâng có thêm MÁU và SÁT THƯƠNG.
/// - Lính đã ra trận KHÔNG đổi chỉ số; chỉ lính spawn sau mới được cộng.
/// - Thành NGƯỜI CHƠI (xanh) đọc/trừ tiền của người chơi và nâng bằng nút UI.
///   Thành AI (đỏ) đọc/trừ tiền của AI và tự nâng khi đủ tiền (xem <see cref="autoUpgrade"/>).
/// - Hình ảnh thành cũng đổi theo level: mỗi level có thể gắn 1 object riêng
///   (xem <see cref="levelVisuals"/>, ví dụ thành xanh: level 1 = upgrade_2, level 2 = magic_ring).
/// </summary>
public class CastleUpgrade : MonoBehaviour
{
    [Header("Số lần nâng cấp")]
    [Tooltip("Số lần nâng cấp tối đa của thành (0 = không cho nâng).")]
    public int maxLevel = 2;

    [Tooltip("Chi phí từng lần nâng: phần tử 0 = lên level 1, phần tử 1 = lên level 2, ...")]
    public int[] upgradeCosts = { 100, 250 };

    [Header("Tài nguyên cộng thêm mỗi level")]
    [Tooltip("Tỉ lệ cộng thêm vào thu nhập của phe xanh (0.25 = +25% mỗi level). " +
             "Áp dụng cho tiền King slime nhả ra (và tiền mỗi giây của phe AI).")]
    public float incomeBonusPerLevel = 0.25f;

    [Header("Chỉ số cơ bản cộng cho lính mỗi level")]
    [Tooltip("Tỉ lệ cộng thêm MÁU của lính mới spawn (0.1 = +10% mỗi level).")]
    public float healthBonusPerLevel = 0.1f;

    [Tooltip("Tỉ lệ cộng thêm SÁT THƯƠNG của lính mới spawn (0.1 = +10% mỗi level).")]
    public float damageBonusPerLevel = 0.1f;

    [Tooltip("Bật để nâng cấp thành cũng cộng MÁU cho King (thành) phe xanh. Mặc định tắt.")]
    public bool applyToKing = false;

    [Header("Phe sở hữu thành")]
    [Tooltip("TẮT = thành NGƯỜI CHƠI (xanh): tiêu tiền của người chơi, nâng bằng nút UI.\n" +
             "BẬT = thành AI (đỏ): tiêu tiền của AI, tự nâng khi đủ tiền.")]
    public bool aiSide = false;

    [Header("Tự động nâng (dùng cho thành AI)")]
    [Tooltip("Bật để thành tự nâng khi phe sở hữu đủ tiền. Thành người chơi nâng bằng nút nên để TẮT.")]
    public bool autoUpgrade = false;

    [Tooltip("Số giây giữa 2 lần tự kiểm tra nâng cấp.")]
    [Min(0.1f)]
    public float autoUpgradeInterval = 1f;

    [Tooltip("Khi tự nâng phải dư thêm chừng này tiền ngoài chi phí mới nâng " +
             "(để AI còn tiền ra lính, không dồn hết vào thành).")]
    [Min(0)]
    public int autoUpgradeSurplus = 50;

    [Header("Hình ảnh đổi theo level")]
    [Tooltip("Object bật lên khi thành đạt từng level: phần tử 0 = level 1, phần tử 1 = level 2, ... " +
             "Để trống ({fileID: 0}) nếu level đó không có object riêng.\n" +
             "Dùng Transform (không phải GameObject) để gắn được cả object nằm trong prefab, ví dụ magic_ring.\n" +
             "Ví dụ thành xanh: [ upgrade_2, magic_ring_03 ].")]
    public Transform[] levelVisuals;

    private int currentLevel = 0;

    /// <summary>Level hiện tại của thành (0 = chưa nâng lần nào).</summary>
    public int CurrentLevel => currentLevel;

    /// <summary>Thành đã nâng tối đa chưa.</summary>
    public bool IsMaxLevel => currentLevel >= maxLevel;

    /// <summary>Chi phí cho lần nâng kế tiếp; đã max hoặc thiếu cấu hình thì trả về <see cref="int.MaxValue"/>.</summary>
    public int NextUpgradeCost => GetUpgradeCost(currentLevel + 1);

    /// <summary>Hệ số thu nhập hiện tại (1 = chưa nâng).</summary>
    public float IncomeMultiplier => 1f + incomeBonusPerLevel * currentLevel;

    /// <summary>Hệ số máu của lính mới spawn (1 = chưa nâng).</summary>
    public float HealthMultiplier => 1f + healthBonusPerLevel * currentLevel;

    /// <summary>Hệ số sát thương của lính mới spawn (1 = chưa nâng).</summary>
    public float DamageMultiplier => 1f + damageBonusPerLevel * currentLevel;

    /// <summary>Tiền hiện có của phe sở hữu thành (xanh = người chơi, đỏ = AI).</summary>
    public int OwnerMoney => (GameManager.Instance == null) ? 0
        : (aiSide ? GameManager.Instance.AI.Money : GameManager.Instance.Money);

    /// <summary>Tên phe sở hữu (để log cho dễ đọc).</summary>
    private string SideName => aiSide ? "ĐỎ/AI" : "XANH/người chơi";

    /// <summary>Trừ tiền của phe sở hữu thành (dùng chung cho cả 2 phe).</summary>
    private void SpendOwnerMoney(int amount)
    {
        if (GameManager.Instance == null) return;

        if (aiSide) GameManager.Instance.SpendAIMoney(amount);
        else GameManager.Instance.Addmoney(-amount);
    }

    /// <summary>
    /// Chi phí để lên <paramref name="level"/>. Level không hợp lệ (≤ 0 hoặc &gt; maxLevel)
    /// hoặc bảng giá thiếu phần tử thì trả về <see cref="int.MaxValue"/> để không bao giờ nâng được.
    /// </summary>
    public int GetUpgradeCost(int level)
    {
        if (level <= 0 || level > maxLevel) return int.MaxValue;
        if (upgradeCosts == null || level > upgradeCosts.Length) return int.MaxValue;

        return Mathf.Max(0, upgradeCosts[level - 1]);
    }

    /// <summary>Với số tiền đang có thì có nâng cấp được không (UI dùng để bật/tắt nút).</summary>
    public bool CanUpgrade(int availableMoney)
    {
        return !IsMaxLevel && availableMoney >= NextUpgradeCost;
    }

    /// <summary>
    /// Đồng bộ hình ảnh thành với level hiện tại ngay khi scene vừa load (level 0 = tắt hết).
    /// </summary>
    private void Awake()
    {
        ApplyLevelVisuals();
    }

    /// <summary>Thành AI bật tự nâng thì chạy vòng lặp nâng cấp.</summary>
    private void Start()
    {
        if (autoUpgrade) StartCoroutine(AutoUpgradeLoop());
    }

    /// <summary>
    /// Vòng lặp tự nâng cấp (dành cho thành AI): cứ mỗi <see cref="autoUpgradeInterval"/> giây
    /// kiểm tra một lần, đủ tiền (dư thêm <see cref="autoUpgradeSurplus"/>) thì nâng.
    /// </summary>
    private IEnumerator AutoUpgradeLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(autoUpgradeInterval);

        while (true)
        {
            yield return wait;

            if (!autoUpgrade || IsMaxLevel) continue;

            int cost = NextUpgradeCost;
            if (cost == int.MaxValue) continue;

            if (OwnerMoney >= cost + autoUpgradeSurplus) TryUpgrade();
        }
    }

    /// <summary>
    /// Đặt cấp nâng theo giá trị HOST đồng bộ sang (client dùng để vẽ lại hình thành cho khớp).
    /// Không trừ tiền, không kiểm tra gì - chỉ để hiển thị.
    /// </summary>
    public void SetLevelFromNetwork(int level)
    {
        currentLevel = Mathf.Clamp(level, 0, maxLevel);
        ApplyLevelVisuals();
    }

    /// <summary>
    /// Bật/tắt các object hình ảnh nâng cấp cho khớp <see cref="currentLevel"/>.
    /// Phần tử thứ i trong <see cref="levelVisuals"/> ứng với level i + 1.
    /// Object của level ĐÃ ĐẠT được bật và giữ nguyên khi lên level cao hơn
    /// (lên level 3 thì cả object của level 2 lẫn level 3 đều sáng).
    /// </summary>
    private void ApplyLevelVisuals()
    {
        if (levelVisuals == null) return;

        for (int i = 0; i < levelVisuals.Length; i++)
        {
            Transform visual = levelVisuals[i];
            if (visual == null) continue;   // level đó không có object riêng, hoặc reference đã mất

            visual.gameObject.SetActive(i + 1 <= currentLevel);
        }
    }

    /// <summary>
    /// Thử nâng cấp thành. Đủ tiền thì tăng level và trừ tiền (việc trừ tiền cũng làm UI
    /// tự cập nhật lại nhãn chi phí + trạng thái nút nâng cấp).
    /// Trả về true nếu nâng thành công.
    /// </summary>
    public bool TryUpgrade()
    {
        if (IsMaxLevel)
        {
            Debug.Log($"Thành đã đạt cấp tối đa ({maxLevel})!");
            return false;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("CastleUpgrade: chưa có GameManager trong scene!");
            return false;
        }

        int cost = NextUpgradeCost;
        if (cost == int.MaxValue)
        {
            Debug.LogError($"CastleUpgrade: thiếu chi phí nâng cấp cho level {currentLevel + 1} trong bảng upgradeCosts!");
            return false;
        }

        int money = OwnerMoney;
        if (money < cost)
        {
            Debug.Log($"Không đủ tiền nâng cấp thành ({SideName})! Cần {cost}, đang có {money}");
            return false;
        }

        // Tăng level TRƯỚC khi trừ tiền: Addmoney sẽ tự cập nhật lại UI theo level mới
        // (nhãn chi phí + trạng thái nút nâng cấp).
        currentLevel++;
        SpendOwnerMoney(cost);
        ApplyLevelVisuals();   // bật object hình ảnh của (các) level đã đạt

        Debug.Log($"Nâng cấp thành {SideName} lên level {currentLevel}/{maxLevel} " +
                  $"(thu nhập x{IncomeMultiplier:0.##}, máu lính x{HealthMultiplier:0.##}, sát thương x{DamageMultiplier:0.##})");

        AudioManager.PlayUpgrade();   // tiếng nâng cấp thành (cả thành người chơi lẫn thành AI)

        return true;
    }
}
