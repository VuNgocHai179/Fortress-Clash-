using System;
using UnityEngine;

/// <summary>
/// Kỹ năng CHỦ ĐỘNG của người chơi. Đây chỉ là "loại" kỹ năng - còn tiền/cooldown/sát thương
/// nằm ở component <see cref="AbilityBar"/> gắn trong scene (chỉnh ở Inspector).
///
/// Giá trị SỐ được lưu trong <see cref="LevelSettings.abilities"/> nên KHÔNG đổi số của mục đã có,
/// chỉ thêm mục mới ở cuối.
/// </summary>
public enum ActiveAbilityType
{
    /// <summary>Thiên thạch: gây sát thương diện rộng quanh con lính ĐỎ đi đầu hàng (tuyến đầu phe địch).</summary>
    Meteor = 0,

    /// <summary>Tăng viện: gọi ngay vài lính XANH ra sân, không tốn tiền mua lính.</summary>
    Reinforce = 1,

    /// <summary>Hiệu triệu: hồi đầy máu lính XANH và giảm sát thương chúng nhận vào trong vài giây.</summary>
    Rally = 2
}

/// <summary>
/// Số lượng kỹ năng chủ động = số giá trị của <see cref="ActiveAbilityType"/>. Các mảng thông số / trạng
/// thái của kỹ năng đều dài đúng bằng số này (xem <c>AbilityBar</c> và <c>GameManager.abilityReadyAt</c>).
/// </summary>
public static class ActiveAbilities
{
    public const int Count = 3;

    /// <summary>Đổi chỉ số kỹ năng (0..Count-1) thành 1 kỹ năng; ngoài khoảng thì trả về <c>false</c>.</summary>
    public static bool TryParse(int index, out ActiveAbilityType ability)
    {
        if (index < 0 || index >= Count)
        {
            ability = ActiveAbilityType.Meteor;
            return false;
        }

        ability = (ActiveAbilityType)index;
        return true;
    }
}

/// <summary>
/// THÔNG SỐ của MỘT kỹ năng chủ động: tiền, thời gian hồi và hiệu ứng. Đây KHÔNG phải dữ liệu của level -
/// mọi level (và cả trận MultiPlayer) dùng chung bộ số này.
///
/// VÌ SAO TÁCH RA FILE NÀY: <c>AbilityBar</c> chỉ còn lo phần UI (nút, nhãn, hồi chiêu), còn
/// <c>GameManager</c> mới là nơi THỰC THI kỹ năng (trừ tiền, sinh lính, gây sát thương, đồng bộ mạng).
/// Bộ số phải nằm ở kiểu mà cả hai bên đọc được - nếu để riêng trong AbilityBar thì GameManager (và các
/// máy khác trong MultiPlayer) không biết giá / sát thương của kỹ năng.
///
/// Đây là bộ số CHỈNH TRONG INSPECTOR của component <c>AbilityBar</c> rồi đăng ký sang GameManager lúc
/// vào trận (xem <c>AbilityBar.Start</c> → <c>GameManager.RegisterAbilitySpec</c>).
/// </summary>
[Serializable]
public class AbilitySpec
{
    [Tooltip("Kỹ năng nào (thứ tự này cũng là thứ tự ô gán UI trong AbilityBar).")]
    public ActiveAbilityType ability = ActiveAbilityType.Meteor;

    [Tooltip("Tên hiện trên nút. Để trống thì lấy tên kỹ năng.")]
    public string displayName = "";

    [Tooltip("Tiền người chơi phải trả mỗi lần dùng.")]
    [Min(0)] public int cost = 80;

    [Tooltip("Số giây phải chờ giữa 2 lần dùng kỹ năng này.")]
    [Min(0f)] public float cooldown = 25f;

    [Tooltip("THIÊN THẠCH: bán kính sát thương (world unit). Đủ rộng để trùm cả cụm lính địch đang " +
             "giao chiến (xem thêm VfxConfig.meteor.spreadScale - viên rơi rải trong bán kính này).")]
    [Min(0.5f)] public float radius = 15f;

    [Tooltip("THIÊN THẠCH: sát thương MỖI VIÊN thiên thạch gây lên mỗi lính địch trong bán kính. " +
             "Để 300 = một viên đã hạ mọi lính (SpearMan trâu nhất: 100 máu × 1,2 nâng cấp " +
             "thành = 120), nên cả vùng nổ chết ngay từ viên đầu. " +
             "Một lính nằm giữa vùng nổ có thể ăn nhiều viên (số viên chỉnh ở VfxConfig.meteor).")]
    [Min(1)] public int damage = 300;

    [Tooltip("TĂNG VIỆN: gọi thêm bao nhiêu lính (bị chặn bởi trần lính).")]
    [Min(1)] public int unitCount = 2;

    [Tooltip("TĂNG VIỆN: gọi loại lính nào. Đây là loại lính của PHE ĐANG DÙNG kỹ năng - phe đỏ gọi " +
             "đúng loại đó của phe đỏ (xem GameManager.SpawnReinforcement).")]
    public AIUnitType reinforceUnit = AIUnitType.Sword;

    [Tooltip("HIỆU TRIỆU: hệ số SÁT THƯƠNG NHẬN VÀO của lính mình (0.5 = chỉ nhận nửa đòn, 1 = không giảm).")]
    [Range(0f, 1f)] public float damageReduction = 0.5f;

    [Tooltip("HIỆU TRIỆU: số giây giảm sát thương có hiệu lực.")]
    [Min(0.5f)] public float duration = 10f;

    /// <summary>Tên để hiện trên nút và ghi log.</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? ability.ToString() : displayName;
}

/// <summary>
/// LUẬT CHƠI + ĐỘ KHÓ của MỘT level. Mỗi level là một trận mới nên các chỉ số này được áp lại từ đầu
/// trận (xem <c>GameManager.ApplyLevelDifficulty</c>).
///
/// Bố cục gợi ý:
///   - Level 1 (người mới) : AI ra quân thưa, ít tiền, ít lính, gần như không khắc chế (không phép).
///   - Level 2 (thường)    : AI ra quân nhanh hơn, người chơi có vài kỹ năng.
///   - Level 3 (khó)       : AI ra quân dày, đủ kỹ năng, luật chơi chặt hơn.
///
/// QUY ƯỚC AN TOÀN KHI THÊM Ô MỚI: mọi ô mới phải có giá trị mặc định = GIỮ NGUYÊN hành vi cũ
/// (0 / rỗng / false), vì các level đã lưu trong scene có thể chưa có ô đó.
/// </summary>
[Serializable]
public class LevelSettings
{
    [Tooltip("Level áp dụng (1 = level đầu tiên).")]
    public int level = 1;

    [Header("Độ khó cơ bản")]
    [Tooltip("Máu tối đa của King ĐỎ ở level này (0 = giữ nguyên máu ghi trong prefab King).")]
    public int redKingHealth = 0;

    [Tooltip("Tiền AI được cộng THÊM ngay khi bắt đầu trận (ngoài AIMoney khai báo ở trên).")]
    public int aiStartMoneyBonus = 0;

    [Tooltip("Tiền AI được cộng thêm mỗi giây (ngoài moneyPersecond và nâng cấp thành).")]
    public int aiExtraMoneyPerSecond = 0;

    [Header("Hồ sơ AI của level (cân bằng độ khó)")]
    [Tooltip("Hệ số nhân thu nhập AI mỗi giây ở level này. < 1 = AI nghèo hơn (dễ hơn cho người mới), " +
             "> 1 = AI giàu hơn. Nhân với moneyPersecond và thưởng nâng cấp thành đỏ.")]
    [Min(0f)]
    public float aiIncomeMultiplier = 1f;

    [Tooltip("Số giây giữa 2 lần AI ra lính ở level này. Lớn hơn = AI ra quân thưa hơn, người chơi dễ thở hơn. " +
             "Có tác dụng ở MỌI level, kể cả level có khai đợt tấn công (đợt quân là phần cộng thêm).")]
    [Min(0.5f)]
    public float aiSpawnInterval = 2f;

    [Tooltip("Số lính tối đa AI giữ cùng lúc ở level này. Bị chặn trên bởi maxUnitsPerSide của scene.")]
    [Min(1)]
    public int aiUnitLimit = 8;

    [Tooltip("0..1: mức khắc chế đội hình người chơi. 0 = chỉ giữ đội hình cân bằng (không đọc bài đối thủ), " +
             "1 = khắc chế tối đa. Dùng để hạ độ khó AI mà không khiến nó chọn lính vô lý.")]
    [Range(0f, 1f)]
    public float aiCounterAwareness = 1f;

    [Tooltip("AI có được cộng thêm tiền/giây khi đang ít lính hơn người chơi ở level này không (chống snowball).")]
    public bool aiCatchUpEnabled = true;

    [Tooltip("Trần tiền catch-up AI được cộng thêm mỗi giây ở level này.")]
    [Min(0)]
    public int aiCatchUpMaxBonus = 6;

    // ==========================================
    // LUẬT CHƠI RIÊNG CỦA LEVEL
    // Mấy ô dưới đây là thứ khiến 2 level khác nhau về CÁCH CHƠI, không chỉ khác độ khó AI.
    // ==========================================

    [Header("Luật chơi riêng của level")]
    [Tooltip("Tiền NGƯỜI CHƠI được cộng THÊM ngay khi bắt đầu trận (0 = như bình thường). " +
             "Dùng cho level 'trong thành hết lương' (số âm sẽ bị bỏ qua) hoặc level ưu đãi.")]
    [Min(0)]
    public int playerStartMoneyBonus = 0;

    [Tooltip("Trần số lính NGƯỜI CHƠI giữ cùng lúc ở level này (0 = dùng maxUnitsPerSide của scene). " +
             "Khác aiUnitLimit để tạo bất đối xứng: ví dụ level vây thành cho AI 8 lính nhưng người chơi 6.")]
    [Min(0)]
    public int playerUnitLimit = 0;

    [Tooltip("Hệ số nhân tiền mà KING XANH (phe người chơi) nhả ra ở level này (0 = 1 = như cũ).\n" +
             "Đây là NGUỒN THU CHÍNH của người chơi trong chơi đơn: AI có moneyPersecond riêng, còn người " +
             "chơi chỉ sống bằng tiền King nhả + thưởng hạ lính. Vì vậy đây chính là nút cân lại để người " +
             "chơi không bị hụt hơi trước số lính AI gọi ra.\n" +
             "KHÔNG ảnh hưởng King ĐỎ (kinh tế AI chỉnh bằng aiIncomeMultiplier/aiExtraMoneyPerSecond) và " +
             "KHÔNG ảnh hưởng MultiPlayer (scene đó không có hồ sơ level).")]
    [Min(0f)]
    public float playerKingIncomeMultiplier = 0f;

    [Tooltip("Lính NGƯỜI CHƠI được phép dùng ở level này. ĐỂ TRỐNG = cho phép cả 6 loại (giữ nguyên như cũ). " +
             "Loại không có trong danh sách sẽ bị làm xám trên HUD và không mua được.")]
    public AIUnitType[] playerUnitWhitelist;

    [Tooltip("Lính AI được phép dùng ở level này. ĐỂ TRỐNG = cả 6 loại. " +
             "Loại không có trong danh sách thì bộ não AI coi như không tồn tại (không bao giờ mua).")]
    public AIUnitType[] aiUnitWhitelist;

    [Tooltip("Kỹ năng CHỦ ĐỘNG mở cho người chơi ở level này. ĐỂ TRỐNG = không có kỹ năng nào. " +
             "Tiền/cooldown/sát thương của từng kỹ năng chỉnh ở component AbilityBar trong scene.")]
    public ActiveAbilityType[] abilities;

    /// <summary>Người chơi có được dùng loại lính này không (danh sách trống = cho phép tất cả).</summary>
    public bool IsPlayerUnitAllowed(int unit)
    {
        return IsUnitAllowed(playerUnitWhitelist, unit);
    }

    /// <summary>AI có được dùng loại lính này không (danh sách trống = cho phép tất cả).</summary>
    public bool IsAIUnitAllowed(int unit)
    {
        return IsUnitAllowed(aiUnitWhitelist, unit);
    }

    /// <summary>Level này có mở kỹ năng <paramref name="ability"/> cho người chơi không.</summary>
    public bool IsAbilityEnabled(ActiveAbilityType ability)
    {
        if (abilities == null) return false;

        for (int i = 0; i < abilities.Length; i++)
        {
            if (abilities[i] == ability) return true;
        }

        return false;
    }

    /// <summary>Danh sách trắng rỗng/null nghĩa là "cho phép tất cả" - nhờ vậy level chưa khai gì vẫn chạy như cũ.</summary>
    private static bool IsUnitAllowed(AIUnitType[] whitelist, int unit)
    {
        if (whitelist == null || whitelist.Length == 0) return true;

        for (int i = 0; i < whitelist.Length; i++)
        {
            if ((int)whitelist[i] == unit) return true;
        }

        return false;
    }
}
