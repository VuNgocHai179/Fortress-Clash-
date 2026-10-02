using UnityEngine;

/// <summary>
/// Hệ thống NÂNG CẤP THÀNH của phe người chơi (xanh).
///
/// - Thành bắt đầu ở level 0 và nâng tối đa <see cref="maxLevel"/> lần (mặc định 3).
/// - Mỗi lần nâng tốn tiền (<see cref="upgradeCosts"/>), đổi lại 2 thứ:
///     1. Tài nguyên về nhiều hơn: tiền mỗi giây của người chơi + tiền King slime nhả ra.
///     2. Lính MỚI SPAWN sau khi nâng có thêm MÁU và SÁT THƯƠNG.
/// - Lính đã ra trận KHÔNG đổi chỉ số; chỉ lính spawn sau mới được cộng.
/// - Chỉ ảnh hưởng phe xanh, AI (phe đỏ) giữ nguyên thu nhập gốc.
/// </summary>
public class CastleUpgrade : MonoBehaviour
{
    [Header("Số lần nâng cấp")]
    [Tooltip("Số lần nâng cấp tối đa của thành (0 = không cho nâng).")]
    public int maxLevel = 3;

    [Tooltip("Chi phí từng lần nâng: phần tử 0 = lên level 1, phần tử 1 = lên level 2, ...")]
    public int[] upgradeCosts = { 100, 250, 500 };

    [Header("Tài nguyên cộng thêm mỗi level")]
    [Tooltip("Tỉ lệ cộng thêm vào thu nhập của phe xanh (0.25 = +25% mỗi level). " +
             "Áp dụng cho cả tiền mỗi giây của GameManager lẫn tiền King slime nhả ra.")]
    public float incomeBonusPerLevel = 0.25f;

    [Header("Chỉ số cơ bản cộng cho lính mỗi level")]
    [Tooltip("Tỉ lệ cộng thêm MÁU của lính mới spawn (0.1 = +10% mỗi level).")]
    public float healthBonusPerLevel = 0.1f;

    [Tooltip("Tỉ lệ cộng thêm SÁT THƯƠNG của lính mới spawn (0.1 = +10% mỗi level).")]
    public float damageBonusPerLevel = 0.1f;

    [Tooltip("Bật để nâng cấp thành cũng cộng MÁU cho King (thành) phe xanh. Mặc định tắt.")]
    public bool applyToKing = false;

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

        if (GameManager.Instance.Money < cost)
        {
            Debug.Log($"Không đủ tiền nâng cấp thành! Cần {cost}, đang có {GameManager.Instance.Money}");
            return false;
        }

        // Tăng level TRƯỚC khi trừ tiền: Addmoney sẽ tự cập nhật lại UI theo level mới
        // (nhãn chi phí + trạng thái nút nâng cấp).
        currentLevel++;
        GameManager.Instance.Addmoney(-cost);

        Debug.Log($"Nâng cấp thành lên level {currentLevel}/{maxLevel} " +
                  $"(thu nhập x{IncomeMultiplier:0.##}, máu lính x{HealthMultiplier:0.##}, sát thương x{DamageMultiplier:0.##})");

        return true;
    }
}
