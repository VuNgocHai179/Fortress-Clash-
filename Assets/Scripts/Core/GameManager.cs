using System;
using System.Collections;
using System.Collections.Generic;
using FortressClash.Core;
using FortressClash.UI;
using UnityEngine;

public struct PlayerHas
{
    public int SwordNumber;
    public int ArcherNumber;
    public int SlingerNumber;
    public int SpearManNumber;
    public int BaseSlimeNumber;
    public int CatapultNumber;
}

public struct AIHas
{
    public int SwordNumber;
    public int ArcherNumber;
    public int SlingerNumber;
    public int SpearManNumber;
    public int RedBaseSlimeNumber;
    public int CatapultNumber;
    public int SwordMoney;
    public int ArcherMoney;
    public int SlingerMoney;
    public int SpearManMoney;
    public int CatapultMoney;
    public int BaseSlimeMoney;
    public int Money;
}

/// <summary>6 loại lính ĐỎ (phe AI) - dùng cho việc spawn thủ công bằng cheat.</summary>
public enum RedUnitType
{
    Sword,
    Archer,
    Slinger,
    SpearMan,
    BaseSlime,
    Catapult
}

// LUẬT CHƠI + ĐỘ KHÓ CỦA LEVEL giờ nằm ở Assets/Scripts/Level/LevelRules.cs (LevelSettings,
// ActiveAbilityType). Tách ra file riêng vì mỗi level không chỉ khác độ khó AI mà
// còn khác về luật chơi: lính được dùng, kỹ năng chủ động...

public class GameManager : Singleton<GameManager>
{
    [Header("Spawn Points")]
    public Transform SpawnSlime;
    public Transform RedSpawnSlime;

    [Header("Đích của lính (King phe địch)")]
    [Tooltip("Lính ra trận sẽ đi về phía King ĐỐI PHƯƠNG. Để trống thì GameManager tự tìm KingSlime " +
             "trong scene lúc ra lính, nên nới map / thay cụm thành cũng không sợ mất reference.")]
    public KingSlime BlueKing;
    public KingSlime RedKing;

    [Header("Blue Prefabs (6 Loại)")]
    public GameObject Sword;
    public GameObject Archer;
    public GameObject Slinger;
    public GameObject SpearMan;
    public GameObject BaseSlime;
    public GameObject Catapult;

    [Header("Red Prefabs (6 Loại)")]
    public GameObject RedSword;
    public GameObject RedArcher;
    public GameObject RedSlinger;
    public GameObject RedSpearMan;
    public GameObject RedBaseSlime;
    public GameObject RedCatapult;

    public int Money = 0;
    public int AIMoney = 0;

    /// <summary>Tiền mỗi giây cộng cho AI. Người chơi KHÔNG nhận tiền/giây nữa - xem AddMoneyPerSecond.</summary>
    [SerializeField] private int moneyPersecond = 10;

    /// <summary>
    /// Hệ số nhân NHỊP KIẾM TIỀN của CẢ 2 PHE (1 = giữ nguyên tốc độ cũ).
    ///
    /// VÌ SAO CẦN: cả 2 nguồn thu định kỳ đều chậm nên trận đấu ì ra lúc đầu - người chơi chỉ sống bằng
    /// tiền King nhả (mỗi lượt cách nhau 8-10 giây, xem <c>KingSlime.GenerateResourceRoutine</c>) còn AI
    /// nhận <see cref="moneyPersecond"/> mỗi giây. Tăng ô này là 2 bên ra lính sớm hơn, mau vào trận đánh.
    ///
    /// Áp cho CẢ 2 PHE với CÙNG một hệ số nên độ khó tương đối giữa 2 bên không đổi. KHÔNG áp cho những
    /// khoản MỘT LẦN: tiền khởi đầu của level (playerStartMoneyBonus / aiStartMoneyBonus) và thưởng hạ
    /// lính (killRewardPriceRatio) - chúng là khoản cộng thêm, không phải sản lượng.
    /// </summary>
    [Min(0f)]
    [SerializeField] private float incomeMultiplier = 3f;

    [SerializeField] private float chooseDelay = 5f;

    /// <summary>Đã hết thời gian chờ chọn lính của phe XANH chưa.</summary>
    private bool canChooseBlue = true;

    /// <summary>
    /// Đã hết thời gian chờ chọn lính của phe ĐỎ chưa. Chơi đơn phe đỏ là AI (ra lính theo nhịp riêng)
    /// nên cờ này chỉ có ý nghĩa ở MultiPlayer, nơi phe đỏ do người chơi thứ hai điều khiển.
    /// </summary>
    private bool canChooseRed = true;

    public PlayerHas player;
    public AIHas AI;

    // ==========================================
    // PHE CỦA NGƯỜI CHƠI CỤC BỘ & TIỀN CỦA 2 PHE
    //
    // Chơi đơn / host = phe XANH, client = phe ĐỎ (xem NetworkRole). Mọi thay đổi tiền đều đi qua
    // ChangeMoney(): ở MultiPlayer HOST là bên duy nhất được ghi, client chỉ nhận giá trị đã chốt
    // qua mạng - nhờ vậy 2 máy không bao giờ lệch số tiền.
    // ==========================================

    /// <summary>Phe của NGƯỜI CHƠI CỤC BỘ: chơi đơn/host = XANH, client = ĐỎ.</summary>
    public bool LocalPlayerIsBlue => NetworkRole.LocalIsBlue;

    /// <summary>Tiền mà NGƯỜI CHƠI CỤC BỘ đang có (HUD hiển thị đúng số này).</summary>
    public int LocalMoney => LocalPlayerIsBlue ? Money : AI.Money;

    /// <summary>Tiền đang có của 1 phe.</summary>
    public int MoneyOf(bool blueSide) => blueSide ? Money : AI.Money;

    /// <summary>Thành của NGƯỜI CHƠI CỤC BỘ (nút nâng cấp trên HUD thao tác lên thành này).</summary>
    public CastleUpgrade LocalCastle => CastleFor(LocalPlayerIsBlue);

    [Header("Level & kết quả trận đấu")]
    [Tooltip("Bảng độ khó theo level. Level không có trong bảng sẽ chơi với chỉ số mặc định của scene.")]
    [SerializeField] private LevelSettings[] levelSettings;

    [Tooltip("Kỹ năng chủ động MỞ SẴN ở MultiPlayer. Trận 2 người không có hồ sơ level nên không đọc " +
             "được LevelSettings.abilities; muốn tắt bớt kỹ năng thì bỏ mục ra khỏi danh sách này.\n" +
             "ĐỂ TRỐNG = mở CẢ 3 kỹ năng (mặc định cho scene chưa có ô này).")]
    [SerializeField] private ActiveAbilityType[] multiplayerAbilities =
        { ActiveAbilityType.Meteor, ActiveAbilityType.Reinforce, ActiveAbilityType.Rally };

    [Tooltip("Tên dialog hiện kết quả thắng/thua (đăng ký trong DialogConfig).")]
    [SerializeField] private string resultDialogName = DlgResult.DialogName;

    [Tooltip("Chờ bao lâu sau khi King chết rồi mới hiện popup kết quả (cho animation chết chạy xong).")]
    [Min(0f)]
    [SerializeField] private float resultPopupDelay = 0.8f;

    /// <summary>Trận này đã có King chết chưa - chặn xử lý thắng/thua 2 lần cho cùng 1 trận.</summary>
    private bool matchOver;

    /// <summary>
    /// Trận đã có kết quả chưa. Dùng cho các hệ thống chạy nền để thôi tác động lên trận đấu sau khi King
    /// đã chết - lúc đó popup kết quả đang hiện nhưng game vẫn chạy tiếp.
    /// </summary>
    public bool IsMatchOver => matchOver;

    /// <summary>Tiền/giây cộng thêm cho AI theo độ khó của level đang chơi.</summary>
    private int levelAIBonusPerSecond;

    [Header("Castle Upgrade (nâng cấp thành)")]
    [Tooltip("Component quản lý nâng cấp thành của NGƯỜI CHƠI. Để trống sẽ tự tìm trên cùng GameObject với GameManager.")]
    [SerializeField] private CastleUpgrade castleUpgrade;

    [Tooltip("Component nâng cấp thành của phe ĐỎ/AI (nằm trên Castle của RedBase). " +
             "Để trống nếu phe đỏ không nâng cấp.")]
    [SerializeField] private CastleUpgrade redCastleUpgrade;

    /// <summary>Hệ thống nâng cấp thành của phe người chơi (tự tìm nếu chưa gán trong Inspector).</summary>
    public CastleUpgrade Castle
    {
        get
        {
            if (castleUpgrade == null) castleUpgrade = GetComponent<CastleUpgrade>();
            return castleUpgrade;
        }
    }

    /// <summary>Hệ thống nâng cấp thành của phe đỏ/AI (null nếu phe đỏ không nâng cấp).</summary>
    public CastleUpgrade RedCastle => redCastleUpgrade;

    /// <summary>Thành nâng cấp của 1 phe: xanh = người chơi, đỏ = AI. Có thể null.</summary>
    public CastleUpgrade CastleFor(bool blueSide) => blueSide ? Castle : RedCastle;

    /// <summary>Hồ sơ độ khó của level đang chơi; null nếu level không có trong bảng (dùng mặc định của scene).</summary>
    public LevelSettings ActiveLevelSettings { get; private set; }

    // ==========================================
    // LUẬT CHƠI CỦA LEVEL (xem Assets/Scripts/Level/LevelRules.cs)
    //
    // Đây là phần khiến 2 level khác nhau về CÁCH CHƠI chứ không chỉ khác độ khó AI. Mọi thứ đọc từ
    // ActiveLevelSettings; level không có hồ sơ (hoặc đang chơi MultiPlayer) trả về giá trị mặc định
    // đúng như game chạy trước đây, nên scene MultiPlayer không bị ảnh hưởng.
    // ==========================================

    /// <summary>
    /// Kỹ năng chủ động <paramref name="ability"/> có được mở ở trận đang chơi không.
    ///
    /// Chơi đơn: do hồ sơ level quyết định (level 1 không mở kỹ năng nào, level 3 mở cả 3).
    /// MultiPlayer: trận 2 người không có hồ sơ level nên đọc <see cref="multiplayerAbilities"/>; danh
    /// sách để trống nghĩa là mở CẢ 3, để scene lưu từ trước (chưa có ô này) vẫn có đủ kỹ năng.
    /// </summary>
    public bool IsAbilityEnabled(ActiveAbilityType ability)
    {
        if (NetworkRole.IsMultiplayer) return IsMultiplayerAbilityEnabled(ability);

        return ActiveLevelSettings != null && ActiveLevelSettings.IsAbilityEnabled(ability);
    }

    /// <summary>Kỹ năng có nằm trong danh sách mở của MultiPlayer không (danh sách trống = mở cả 3).</summary>
    private bool IsMultiplayerAbilityEnabled(ActiveAbilityType ability)
    {
        if (multiplayerAbilities == null || multiplayerAbilities.Length == 0) return true;

        for (int i = 0; i < multiplayerAbilities.Length; i++)
        {
            if (multiplayerAbilities[i] == ability) return true;
        }

        return false;
    }

    /// <summary>Người chơi có được dùng loại lính <paramref name="unit"/> ở level này không (danh sách trắng rỗng = tất cả).</summary>
    public bool IsPlayerUnitAllowed(int unit)
    {
        return ActiveLevelSettings == null || ActiveLevelSettings.IsPlayerUnitAllowed(unit);
    }

    /// <summary>AI có được dùng loại lính <paramref name="unit"/> ở level này không (danh sách trắng rỗng = tất cả).</summary>
    public bool IsAIUnitAllowed(int unit)
    {
        return ActiveLevelSettings == null || ActiveLevelSettings.IsAIUnitAllowed(unit);
    }

    /// <summary>
    /// Level này CÓ giới hạn bộ lính của AI không. AI dùng cờ này để biết có cần lọc đơn giá trước khi
    /// đưa thế trận cho <see cref="AIPolicy"/> hay không (lọc thì phải sao chép mảng giá, xem AI.Tick).
    /// </summary>
    public bool HasAIUnitRestriction
    {
        get
        {
            LevelSettings settings = ActiveLevelSettings;
            if (settings == null) return false;
            if (settings.aiUnitWhitelist == null || settings.aiUnitWhitelist.Length == 0) return false;

            return settings.aiUnitWhitelist.Length < AIPolicy.UnitCount;
        }
    }

    /// <summary>
    /// Trần số lính của NGƯỜI CHƠI ở level này. playerUnitLimit = 0 (mặc định) thì dùng
    /// <see cref="maxUnitsPerSide"/> của scene như trước.
    /// </summary>
    public int PlayerUnitLimit
    {
        get
        {
            if (ActiveLevelSettings == null) return maxUnitsPerSide;
            if (ActiveLevelSettings.playerUnitLimit <= 0) return maxUnitsPerSide;

            return Mathf.Clamp(ActiveLevelSettings.playerUnitLimit, 1, maxUnitsPerSide);
        }
    }

    /// <summary>
    /// Hệ số nhân tiền KING XANH (phe người chơi) nhả ra ở level này (0 = 1 = không đổi).
    ///
    /// Đây là nút chính để cân lại kinh tế của NGƯỜI CHƠI: trong chơi đơn người chơi KHÔNG có tiền/giây
    /// như AI (xem AddMoneyPerSecond) mà chỉ sống bằng tiền King nhả + thưởng hạ lính, nên khi AI ra quân
    /// dày hơn thì phải nâng hệ số này lên mới theo kịp.
    ///
    /// Chỉ áp cho King XANH - xem <c>KingSlime.CurrentResourcePerTick</c>. King đỏ giữ nguyên vì kinh tế
    /// của AI đã có <see cref="LevelSettings.aiIncomeMultiplier"/> và aiExtraMoneyPerSecond.
    /// </summary>
    public float PlayerKingIncomeMultiplier
    {
        get
        {
            float multiplier = (ActiveLevelSettings != null) ? ActiveLevelSettings.playerKingIncomeMultiplier : 0f;
            return (multiplier > 0f) ? multiplier : 1f;
        }
    }

    /// <summary>Tiền mỗi giây của AI, đã cộng thưởng nâng cấp thành phe đỏ, hệ số thu nhập của level và
    /// hệ số nhịp độ trận (<see cref="IncomeMultiplier"/> - áp cho cả 2 phe).</summary>
    public int AIMoneyPerSecond
    {
        get
        {
            float castleMultiplier = (RedCastle != null) ? RedCastle.IncomeMultiplier : 1f;
            float levelMultiplier = (ActiveLevelSettings != null) ? Mathf.Max(0f, ActiveLevelSettings.aiIncomeMultiplier) : 1f;

            return Mathf.RoundToInt(moneyPersecond * castleMultiplier * levelMultiplier * IncomeMultiplier);
        }
    }

    /// <summary>
    /// Hệ số thu nhập đang áp cho CẢ 2 PHE - đọc bởi <see cref="AIMoneyPerSecond"/> (thu nhập mỗi giây
    /// của AI) và <c>KingSlime.CurrentResourcePerTick</c> (tiền King nhả ra, cả King xanh lẫn King đỏ).
    ///
    /// Đặt 0 hoặc số âm trong Inspector thì coi như 1: hệ số này là "chỉnh cho nhanh/chậm", không phải
    /// công tắc tắt kinh tế - tắt kinh tế sẽ làm trận đấu không thể kết thúc.
    /// </summary>
    public float IncomeMultiplier => (incomeMultiplier > 0f) ? incomeMultiplier : 1f;

    [Header("AI để dành tiền nâng cấp thành")]
    [Tooltip("Bật để phe ĐỎ để dành tiền cho lần nâng cấp thành kế tiếp thay vì tiêu hết vào lính.\n" +
             "Việc NÂNG vẫn do chính thành ĐỎ làm (CastleUpgrade.autoUpgrade) - ô này chỉ quyết định AI có " +
             "GIỮ LẠI tiền hay không, nhờ vậy tiền mới tích được tới mức nâng.")]
    public bool aiSavesForCastleUpgrade = true;

    [Tooltip("Chỉ bắt đầu để dành khi phe ĐỎ đã có ít nhất chừng này lính trên sân. Đầu trận sân còn trống " +
             "thì nó vẫn ra quân giữ tuyến như cũ, chỉ tích tiền khi đã có đội hình.\n" +
             "Đặt 0 = để dành ngay từ đầu (AI sẽ đứng yên chờ đủ tiền nâng ở đầu trận - coi chừng bị đè).")]
    [Min(0)]
    public int aiCastleUpgradeSaveFromUnits = 3;

    [Header("AI Catch-up (bù khi AI bị dẫn quân)")]
    [Tooltip("Bật để AI được cộng thêm tiền mỗi giây khi số lính ít hơn người chơi, tránh bị snowball.")]
    public bool aiCatchUpEnabled = true;

    [Tooltip("Mỗi lính ít hơn người chơi thì AI được cộng thêm chừng này tiền/giây.")]
    public float aiCatchUpMoneyPerMissingUnit = 0.8f;

    [Tooltip("Trần tiền catch-up cộng thêm mỗi giây.")]
    public int aiCatchUpMaxBonus = 6;

    [Header("Giới hạn số lính trên sân")]
    [Tooltip("Mỗi phe chỉ giữ tối đa chừng này lính cùng lúc. Map khá bé (camera thấy ~43 đơn vị " +
             "ngang, thân lính rộng 5-10 đơn vị) nên cần chặn để lính không dồn đống.")]
    [Min(1)]
    public int maxUnitsPerSide = 8;

    /// <summary>
    /// Người chơi đã đạt giới hạn số lính chưa (UI dùng để làm xám nút chọn lính).
    /// Trần lấy theo luật chơi của level - xem <see cref="PlayerUnitLimit"/>.
    /// </summary>
    public bool IsPlayerAtUnitLimit => CountAll(GetPlayerUnitCounts()) >= PlayerUnitLimit;

    /// <summary>
    /// Số lính đang THỰC SỰ sống trên sân của 1 phe (đếm trực tiếp, không dùng bộ đếm cache).
    /// Dùng cho UI ở client - nơi bộ đếm cache có thể cũ vì lính do host sinh ra.
    /// </summary>
    public int CountLiveUnits(bool blueSide)
    {
        int total = 0;

        foreach (Slime slime in FindObjectsByType<Slime>(FindObjectsSortMode.None))
        {
            if (slime == null || slime is KingSlime) continue;   // King không tính vào giới hạn quân
            if (slime.blueSide != blueSide) continue;
            if (GetUnitTypeIndex(slime) < 0) continue;

            total++;
        }

        return total;
    }

    /// <summary>
    /// NGƯỜI CHƠI CỤC BỘ đã đạt giới hạn số lính chưa. Khác <see cref="IsPlayerAtUnitLimit"/> ở chỗ
    /// biết phe của người chơi cục bộ (client là phe đỏ) và đếm trực tiếp trên sân.
    /// Trần lấy theo luật chơi của level: phe xanh dùng <see cref="PlayerUnitLimit"/>,
    /// phe đỏ dùng <see cref="AIUnitLimit"/>.
    /// </summary>
    public bool IsLocalAtUnitLimit => CountLiveUnits(LocalPlayerIsBlue) >= LocalUnitLimit;

    /// <summary>Trần số lính của PHE NGƯỜI CHƠI CỤC BỘ (xanh = người chơi, đỏ = AI/người chơi 2).</summary>
    public int LocalUnitLimit => UnitLimitFor(LocalPlayerIsBlue);

    /// <summary>
    /// Trần số lính của một phe: xanh dùng <see cref="PlayerUnitLimit"/> (luật chơi của level), đỏ dùng
    /// <see cref="AIUnitLimit"/>. Kỹ năng TĂNG VIỆN ở MultiPlayer gọi lính cho phe nào thì phải so với trần
    /// của chính phe đó.
    /// </summary>
    public int UnitLimitFor(bool blueSide) => blueSide ? PlayerUnitLimit : AIUnitLimit;

    [Header("Thưởng khi hạ lính đối phương")]
    [Tooltip("Bật để NGƯỜI CHƠI được cộng tiền mỗi khi hạ được 1 lính ĐỎ. " +
             "AI không nhận khoản này vì đã có thu nhập riêng mỗi giây (moneyPersecond).")]
    public bool killRewardEnabled = true;

    [Tooltip("Tiền thưởng = giá lính bị hạ × tỉ lệ này (làm tròn). " +
             "Ví dụ 0.3: hạ Slime (10) được 3, hạ Catapult (60) được 18. " +
             "Đặt 0 để không thưởng dù killRewardEnabled vẫn bật.")]
    [Range(0f, 1f)]
    public float killRewardPriceRatio = 0.3f;

    /// <summary>Trần lính của AI ở level hiện tại, không vượt quá <see cref="maxUnitsPerSide"/> của scene.</summary>
    public int AIUnitLimit => (ActiveLevelSettings != null)
        ? Mathf.Clamp(ActiveLevelSettings.aiUnitLimit, 1, maxUnitsPerSide)
        : maxUnitsPerSide;

    /// <summary>AI đã đạt giới hạn số lính chưa (theo hồ sơ độ khó của level).</summary>
    public bool IsAIAtUnitLimit => CountAll(GetOwnUnitCounts()) >= AIUnitLimit;

    /// <summary>
    /// Số tiền AI phải GIỮ LẠI (không được tiêu vào lính) cho lần nâng cấp thành kế tiếp.
    ///
    /// Vì sao cần: bộ não của AI mua lính ngay khi đủ tiền loại rẻ nhất (xem AIPolicy), nên tiền không bao
    /// giờ tích đủ cho một lần nâng thành - thành ĐỎ gần như không bao giờ lên cấp. Trừ khoản để dành này
    /// khỏi số tiền mà AI "nhìn thấy" khi quyết định khiến nó thôi tiêu hết, và thành tự nâng khi tích đủ
    /// (CastleUpgrade.autoUpgrade + autoUpgradeSurplus).
    ///
    /// Trả về 0 - tức AI được tiêu hết như trước giờ - khi: tính năng tắt, không có thành ĐỎ, thành đã tối
    /// đa, thành không bật tự nâng, bảng giá nâng cấp thiếu, hoặc phe ĐỎ chưa đủ lính để dám buông việc mua
    /// quân (xem aiCastleUpgradeSaveFromUnits).
    /// </summary>
    public int AICastleUpgradeReserve
    {
        get
        {
            if (!aiSavesForCastleUpgrade) return 0;

            CastleUpgrade castle = RedCastle;
            if (castle == null || castle.IsMaxLevel) return 0;
            if (!castle.autoUpgrade) return 0;   // thành không tự nâng thì giữ tiền lại cũng vô ích

            int cost = castle.NextUpgradeCost;
            if (cost == int.MaxValue) return 0;   // thiếu cấu hình giá nâng cấp

            // Chưa đủ lính thì ưu tiên giữ tuyến: không để dành gì, mua quân như bình thường.
            if (CountLiveUnits(false) < aiCastleUpgradeSaveFromUnits) return 0;

            // Giữ đúng giá nâng cấp + khoản dư của thành, khớp với điều kiện thành tự nâng - nhờ vậy khi AI
            // tích đủ là thành nâng được NGAY ở nhịp kiểm tra kế tiếp (autoUpgradeInterval).
            return cost + Mathf.Max(0, castle.autoUpgradeSurplus);
        }
    }

    /// <summary>Đơn giá 6 loại lính đỏ, theo thứ tự <see cref="AIUnitType"/> (lấy trực tiếp từ prefab).</summary>
    private int[] unitPrices;

    public int[] UnitPrices
    {
        get
        {
            if (unitPrices == null)
            {
                unitPrices = new[]
                {
                    GetPrice(RedSword), GetPrice(RedArcher), GetPrice(RedSlinger),
                    GetPrice(RedSpearMan), GetPrice(RedBaseSlime), GetPrice(RedCatapult)
                };
            }

            return unitPrices;
        }
    }

    /// <summary>Số lính hiện có của người chơi, theo thứ tự <see cref="AIUnitType"/>.</summary>
    public int[] GetPlayerUnitCounts()
    {
        return new[]
        {
            player.SwordNumber, player.ArcherNumber, player.SlingerNumber,
            player.SpearManNumber, player.BaseSlimeNumber, player.CatapultNumber
        };
    }

    /// <summary>Số lính hiện có của AI, theo thứ tự <see cref="AIUnitType"/>.</summary>
    public int[] GetOwnUnitCounts()
    {
        return new[]
        {
            AI.SwordNumber, AI.ArcherNumber, AI.SlingerNumber,
            AI.SpearManNumber, AI.RedBaseSlimeNumber, AI.CatapultNumber
        };
    }

    /// <summary>Thế trận hiện tại, đóng gói theo dạng mà <see cref="AIPolicy"/> hiểu.</summary>
    public AIState GetAIState()
    {
        return new AIState(GetPlayerUnitCounts(), GetOwnUnitCounts(), UnitPrices, AI.Money);
    }

    /// <summary>
    /// Tiền cộng thêm mỗi giây cho AI khi nó đang ít lính hơn người chơi.
    /// Mục đích: giảm snowball, để ván đấu còn căng thẳng thay vì vỡ trận một chiều.
    /// Hồ sơ của level có thể TẮT hoặc hạ trần khoản này (level dễ thì AI không được bù quân).
    /// </summary>
    private int GetAICatchUpBonus()
    {
        bool enabled = (ActiveLevelSettings != null) ? ActiveLevelSettings.aiCatchUpEnabled : aiCatchUpEnabled;
        if (!enabled) return 0;

        int maxBonus = (ActiveLevelSettings != null) ? ActiveLevelSettings.aiCatchUpMaxBonus : aiCatchUpMaxBonus;
        if (maxBonus <= 0) return 0;

        int missing = CountAll(GetPlayerUnitCounts()) - CountAll(GetOwnUnitCounts());
        if (missing <= 0) return 0;

        return Mathf.Min(maxBonus, Mathf.RoundToInt(missing * aiCatchUpMoneyPerMissingUnit));
    }

    private static int CountAll(int[] counts)
    {
        int total = 0;
        for (int i = 0; i < counts.Length; i++) total += counts[i];
        return total;
    }

    [Header("Debug / Cheat (chỉ dùng khi test)")]
    [Tooltip("AI tự động spawn lính đỏ theo model ONNX. TẮT để tự tay spawn bằng DebugCheatPanel (F1).")]
    public bool aiAutoSpawnEnabled = true;

    [Tooltip("Lính đỏ spawn bằng cheat KHÔNG trừ tiền AI (để vẫn spawn được khi AI hết tiền).")]
    public bool cheatSpawnIsFree = true;

    /// <summary>
    /// GameManager là trạng thái của ĐÚNG 1 TRẬN ĐẤU nên KHÔNG DontDestroyOnLoad: mỗi lần vào Gameplay
    /// (chơi level mới hoặc chơi lại sau khi thua) là một instance mới, nhờ vậy tiền, quân số và nâng cấp
    /// thành đều bắt đầu lại từ đầu trận thay vì dồn từ level trước sang.
    /// </summary>
    protected override void Awake()
    {
        if (Instance != null && Instance != this) Destroy(Instance.gameObject);

        Instance = this;
    }

    void Start()
    {
        if (NetworkRole.IsMultiplayer)
        {
            // MultiPlayer = 2 người chơi: KHÔNG áp bảng độ khó của level (bảng đó để cân bằng trận
            // người vs AI) và cả 2 phe bắt đầu với CÙNG số tiền cho công bằng. HOST là bên chốt số.
            if (NetworkRole.IsHost)
            {
                SetMoneyField(true, Money);
                SetMoneyField(false, Money);
            }

            UpdateMoneyUI();
            StartCoroutine(AddMoneyPerSecond());
            return;
        }

        ApplyLevelDifficulty();

        UpdateMoneyUI();
        setMoneyforAI();
        StartCoroutine(AddMoneyPerSecond());
    }

    /// <summary>
    /// Áp độ khó của level đang chơi (<see cref="LevelProgress.SelectedLevel"/>) cho trận vừa bắt đầu:
    /// tiền khởi đầu của AI, tiền AI được cộng thêm mỗi giây và máu King đỏ.
    /// Hồ sơ của level cũng được lưu lại ở <see cref="ActiveLevelSettings"/> để AI và phần tính
    /// tiền/catch-up đọc ra (nhịp ra lính, trần lính, độ khắc chế, hệ số thu nhập...).
    /// </summary>
    private void ApplyLevelDifficulty()
    {
        int level = LevelProgress.SelectedLevel;
        LevelSettings settings = GetLevelSettings(level);

        ActiveLevelSettings = settings;
        levelAIBonusPerSecond = 0;

        if (settings == null)
        {
            Debug.Log($"Gameplay: level {level} không có trong bảng độ khó, chơi với chỉ số mặc định.");
            return;
        }

        AIMoney += Mathf.Max(0, settings.aiStartMoneyBonus);
        AI.Money = AIMoney;
        levelAIBonusPerSecond = Mathf.Max(0, settings.aiExtraMoneyPerSecond);

        // LUẬT CHƠI CỦA LEVEL: tiền khởi đầu cộng thêm cho NGƯỜI CHƠI (0 = như cũ). Đi qua ChangeMoney
        // như mọi thay đổi tiền khác để UI và (nếu có) mạng luôn khớp.
        int playerBonus = Mathf.Max(0, settings.playerStartMoneyBonus);
        if (playerBonus > 0) ChangeMoney(true, playerBonus);

        if (settings.redKingHealth > 0)
        {
            // King đỏ chính là "thành" của AI. SetLevelHealth ghi cả máu hiện tại lẫn máu tối đa nên
            // đúng dù KingSlime.Start() chạy trước hay sau Start() của GameManager.
            KingSlime redKing = FindKing(false);
            if (redKing != null) redKing.SetLevelHealth(settings.redKingHealth);
        }

        Debug.Log($"Gameplay: level {level} - máu King đỏ {settings.redKingHealth}, " +
                  $"tiền AI khởi đầu +{settings.aiStartMoneyBonus}, AI +{levelAIBonusPerSecond} tiền/giây, " +
                  $"thu nhập x{settings.aiIncomeMultiplier:0.##}, ra lính mỗi {settings.aiSpawnInterval:0.##}s, " +
                  $"trần lính {settings.aiUnitLimit}, khắc chế x{settings.aiCounterAwareness:0.##}, " +
                  $"catch-up {(settings.aiCatchUpEnabled ? $"+{settings.aiCatchUpMaxBonus}/s" : "TẮT")}.");

        LogLevelRules(settings, playerBonus);
    }

    /// <summary>Độ khó cấu hình cho 1 level; null nếu level đó không có trong bảng.</summary>
    private LevelSettings GetLevelSettings(int level)
    {
        if (levelSettings == null) return null;

        foreach (LevelSettings settings in levelSettings)
        {
            if (settings != null && settings.level == level) return settings;
        }

        return null;
    }

    /// <summary>
    /// Log phần LUẬT CHƠI riêng của level (lính nào được dùng, kỹ năng nào mở, trần lính
    /// của người chơi, hệ số tiền King nhả). Chỉ để đọc cho dễ khi test - không đụng tới gameplay.
    /// </summary>
    private void LogLevelRules(LevelSettings settings, int playerBonus)
    {
        if (settings == null) return;

        string abilities = FormatAbilityList(settings.abilities);

        Debug.Log($"Gameplay: luật chơi level {settings.level} - " +
                  $"lính người chơi [{FormatUnitList(settings.playerUnitWhitelist)}], " +
                  $"lính AI [{FormatUnitList(settings.aiUnitWhitelist)}], " +
                  $"kỹ năng [{(abilities.Length == 0 ? "không có" : abilities)}], " +
                  $"tiền khởi đầu người chơi +{playerBonus}, " +
                  $"trần lính người chơi {PlayerUnitLimit}, hệ số tiền King xanh x{PlayerKingIncomeMultiplier:0.##}.");
    }

    /// <summary>Tên các loại lính trong danh sách trắng (danh sách rỗng = cho phép cả 6 loại).</summary>
    private static string FormatUnitList(AIUnitType[] whitelist)
    {
        if (whitelist == null || whitelist.Length == 0) return "cả 6 loại";

        string text = "";
        for (int i = 0; i < whitelist.Length; i++)
        {
            if (text.Length > 0) text += ", ";
            text += whitelist[i];
        }

        return text;
    }

    /// <summary>Tên các kỹ năng chủ động được mở (rỗng = không có kỹ năng nào).</summary>
    private static string FormatAbilityList(ActiveAbilityType[] abilities)
    {
        if (abilities == null || abilities.Length == 0) return "";

        string text = "";
        for (int i = 0; i < abilities.Length; i++)
        {
            if (text.Length > 0) text += ", ";
            text += abilities[i];
        }

        return text;
    }

    private IEnumerator AddMoneyPerSecond()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            // Đếm lại quân thật trên sân trước khi tính tiền, để mọi thứ (giới hạn lính, tiền
            // catch-up, input cho model AI) luôn khớp với thực tế.
            RecountUnitsOnField();

            if (NetworkRole.IsMultiplayer)
            {
                // MultiPlayer: không còn AI nên KHÔNG cộng tiền/giây cho phe nào. Cả 2 phe kiếm tiền
                // giống hệt nhau: King nhả tiền định kỳ + thưởng khi hạ lính địch.
                UpdateMoneyUI();
                continue;
            }

            // CHỈ AI được cộng tiền tự động mỗi giây (kèm tiền catch-up nếu đang ít lính hơn người chơi).
            // Người chơi KHÔNG còn tiền/giây: tiền của người chơi chỉ tăng khi King xanh nhả ra, nhờ vậy
            // số trên box_money luôn khớp với text "+N" bay lên ở mỗi lần King nhả.
            AI.Money += AIMoneyPerSecond + GetAICatchUpBonus() + levelAIBonusPerSecond;
            AIMoney = AI.Money;

            UpdateMoneyUI();
        }
    }

    /// <summary>
    /// Đếm lại số lính ĐANG THẬT SỰ sống trên sân rồi ghi đè vào PlayerHas/AIHas.
    ///
    /// Vì sao cần: GameManager là Singleton có DontDestroyOnLoad nên khi thoát ra menu rồi vào lại
    /// màn chơi, bộ đếm cũ vẫn còn nguyên trong khi lính cũ đã bị huỷ. Không đếm lại thì ván mới
    /// sẽ bị "kẹt" ở giới hạn lính ngay từ đầu, và model AI nhận input sai.
    /// Chạy mỗi giây một lần nên chi phí không đáng kể.
    /// </summary>
    private void RecountUnitsOnField()
    {
        int[] playerCounts = new int[AIPolicy.UnitCount];
        int[] ownCounts = new int[AIPolicy.UnitCount];

        foreach (Slime slime in FindObjectsByType<Slime>(FindObjectsSortMode.None))
        {
            if (slime == null) continue;
            if (slime is KingSlime) continue;   // King không tính vào giới hạn quân

            int type = GetUnitTypeIndex(slime);
            if (type < 0) continue;

            if (slime.blueSide) playerCounts[type]++;
            else ownCounts[type]++;
        }

        player.SwordNumber = playerCounts[(int)AIUnitType.Sword];
        player.ArcherNumber = playerCounts[(int)AIUnitType.Archer];
        player.SlingerNumber = playerCounts[(int)AIUnitType.Slinger];
        player.SpearManNumber = playerCounts[(int)AIUnitType.SpearMan];
        player.BaseSlimeNumber = playerCounts[(int)AIUnitType.BaseSlime];
        player.CatapultNumber = playerCounts[(int)AIUnitType.Catapult];

        AI.SwordNumber = ownCounts[(int)AIUnitType.Sword];
        AI.ArcherNumber = ownCounts[(int)AIUnitType.Archer];
        AI.SlingerNumber = ownCounts[(int)AIUnitType.Slinger];
        AI.SpearManNumber = ownCounts[(int)AIUnitType.SpearMan];
        AI.RedBaseSlimeNumber = ownCounts[(int)AIUnitType.BaseSlime];
        AI.CatapultNumber = ownCounts[(int)AIUnitType.Catapult];
    }

    /// <summary>Loại lính (theo <see cref="AIUnitType"/>) của một Slime, -1 nếu không thuộc 6 loại lính.</summary>
    private static int GetUnitTypeIndex(Slime slime)
    {
        if (slime is Catapult) return (int)AIUnitType.Catapult;
        if (slime is SpearMan) return (int)AIUnitType.SpearMan;
        if (slime is Archer) return (int)AIUnitType.Archer;
        if (slime is Slinger) return (int)AIUnitType.Slinger;
        if (slime is BaseSlime) return (int)AIUnitType.BaseSlime;
        if (slime is Sword) return (int)AIUnitType.Sword;

        return -1;
    }

    private void UpdateMoneyUI()
    {
        if (UIManager.Instance != null)
        {
            // HUD luôn hiển thị tiền của NGƯỜI CHƠI CỤC BỘ, không phải lúc nào cũng là phe xanh.
            UIManager.Instance.UpdateMoneyDisplay(LocalMoney);
        }
    }

    // ==========================================
    // TIỀN - MỌI THAY ĐỔI ĐI QUA ĐÂY
    // ==========================================

    /// <summary>
    /// Đổi tiền của 1 phe (dương = cộng, âm = trừ). Ở MultiPlayer CHỈ HOST được đổi: client nhận số
    /// tiền đã chốt qua mạng nên mọi lời gọi cộng/trừ tiền ở client đều bị bỏ qua.
    /// </summary>
    private void ChangeMoney(bool blueSide, int delta)
    {
        if (NetworkRole.IsMultiplayer && !NetworkRole.IsHost) return;

        SetMoneyField(blueSide, MoneyOf(blueSide) + delta);
    }

    /// <summary>Ghi số tiền của 1 phe rồi cập nhật UI và công bố cho client (nếu là host).</summary>
    private void SetMoneyField(bool blueSide, int value)
    {
        ApplyMoneyLocal(blueSide, value);
        NetworkGame.Instance?.PublishMoney(blueSide, value);
    }

    /// <summary>Chỉ ghi số tiền vào field cục bộ (không công bố) - dùng khi nhận giá trị từ host.</summary>
    private void ApplyMoneyLocal(bool blueSide, int value)
    {
        value = Mathf.Max(0, value);

        if (blueSide)
        {
            Money = value;
        }
        else
        {
            AI.Money = value;
            AIMoney = value;
        }

        if (LocalPlayerIsBlue == blueSide) UpdateMoneyUI();
    }

    /// <summary>
    /// Nhận số tiền do HOST đồng bộ sang (xem <see cref="NetworkGame"/>): giữ <see cref="Money"/> và
    /// <c>AI.Money</c> khớp với giá trị chốt trên mạng ở CẢ HAI máy.
    /// </summary>
    public void SetSyncedMoney(bool blueSide, int value)
    {
        ApplyMoneyLocal(blueSide, value);
    }

    private bool HasEnoughMoney(bool blueSide, int price)
    {
        if (MoneyOf(blueSide) >= price) return true;
        Debug.Log($"Không đủ tiền (phe {(blueSide ? "xanh" : "đỏ")})!");
        return false;
    }

    // ==========================================
    // THỜI GIAN CHỜ CHỌN LÍNH (mỗi phe một cờ riêng)
    // ==========================================

    private bool CanChoose(bool blueSide)
    {
        if (blueSide ? canChooseBlue : canChooseRed) return true;
        Debug.Log("Đang chờ, chưa thể chọn lính!");
        return false;
    }

    /// <summary>Người chơi cục bộ đã hết thời gian chờ chọn lính chưa (UI dùng để bật/tắt nút).</summary>
    public bool IsChooseReady => CanChoose(LocalPlayerIsBlue);

    private IEnumerator ChooseCooldown(bool blueSide)
    {
        if (blueSide) canChooseBlue = false;
        else canChooseRed = false;

        UpdateMoneyUI();          // làm xám các nút chọn lính ngay khi vừa ra lính

        yield return new WaitForSeconds(chooseDelay);

        if (blueSide) canChooseBlue = true;
        else canChooseRed = true;

        UpdateMoneyUI();          // sáng lại nút khi hết thời gian chờ
    }

    // ==========================================
    // NGƯỜI CHƠI CỤC BỘ CHỌN LÍNH
    //
    // Chơi đơn: 6 hàm dưới đây mua lính XANH y như trước giờ.
    // MultiPlayer: máy này KHÔNG tự sinh lính mà gửi yêu cầu lên HOST (xem RequestUnitForLocalPlayer).
    //   Nút trên HUD vẫn gọi đúng 6 hàm này nên phía UI không phải biết gì về mạng.
    //
    // Chỉ cộng số lượng + bắt đầu cooldown KHI THỰC SỰ ra được lính. Trước đây các hàm này cộng
    // số lượng và chạy lại cooldown kể cả khi bấm lúc đang chờ / không đủ tiền, làm số lượng lính
    // lệch với thực tế trên sân và cooldown bị reset liên tục nên không bao giờ hết.
    // ==========================================

    /// <summary>
    /// MultiPlayer: gửi yêu cầu mua lính lên HOST (máy này không tự sinh lính).
    ///
    /// Trả về false khi đang chơi đơn - để hàm gọi chạy tiếp đường chơi đơn như cũ. Trả về true khi
    /// yêu cầu đã được xử lý theo đường mạng (kể cả trường hợp đang trong thời gian chờ).
    /// </summary>
    private bool RequestUnitForLocalPlayer(AIUnitType unitType)
    {
        NetworkGame net = NetworkGame.Instance;
        if (net == null || !net.IsSpawned) return false;   // chơi đơn

        if (!CanChoose(LocalPlayerIsBlue)) return true;    // đang chờ: phản hồi UI ngay, khỏi gửi

        // HOST: KHÔNG bật nhịp chờ ở đây. Đường sinh lính thật (spawnSoldier/spawnEnemy) đã tự bật
        // cooldown sau khi trừ tiền và Instantiate xong. Bật thêm ở đây thì cờ chờ bị hạ xuống FALSE
        // TRƯỚC khi HandleLocalSpawn kịp kiểm tra lại, nên mọi cú bấm của host đều bị chặn kèm log
        // "Đang chờ, chưa thể chọn lính!" - host không bao giờ ra được lính phe XANH.
        // CLIENT: máy này không sinh lính nên tự giữ nhịp chờ để nút trên HUD xám đi ngay, không phải
        // đợi RPC trả lời.
        if (!net.IsServer) StartCoroutine(ChooseCooldown(LocalPlayerIsBlue));

        net.RequestSpawn(LocalPlayerIsBlue, (int)unitType);
        return true;
    }

    public virtual void SwordChoosen()
    {
        if (RequestUnitForLocalPlayer(AIUnitType.Sword)) return;
        if (!spawnSoldier(Sword)) return;

        player.SwordNumber++;
    }

    public virtual void ArcherChoosen()
    {
        if (RequestUnitForLocalPlayer(AIUnitType.Archer)) return;
        if (!spawnSoldier(Archer)) return;

        player.ArcherNumber++;
    }

    public virtual void SpearChoosen()
    {
        if (RequestUnitForLocalPlayer(AIUnitType.SpearMan)) return;
        if (!spawnSoldier(SpearMan)) return;

        player.SpearManNumber++;
    }

    public virtual void SlingshotChoosen()
    {
        if (RequestUnitForLocalPlayer(AIUnitType.Slinger)) return;
        if (!spawnSoldier(Slinger)) return;

        player.SlingerNumber++;
    }

    public virtual void BaseSlimeChoosen()
    {
        if (RequestUnitForLocalPlayer(AIUnitType.BaseSlime)) return;
        if (!spawnSoldier(BaseSlime)) return;

        player.BaseSlimeNumber++;
    }

    public virtual void CatapultChoosen()
    {
        if (RequestUnitForLocalPlayer(AIUnitType.Catapult)) return;
        if (!spawnSoldier(Catapult)) return;

        player.CatapultNumber++;
    }

    /// <summary>
    /// HOST (hoặc chơi đơn) thực sự mua 1 lính cho phe được yêu cầu - dùng chung cho yêu cầu của
    /// người chơi cục bộ lẫn yêu cầu gửi từ client qua <see cref="NetworkGame.RequestSpawn"/>.
    /// </summary>
    public void HandleLocalSpawn(bool blueSide, int unitType)
    {
        if (NetworkRole.IsMultiplayer && !NetworkRole.IsHost) return;   // client không tự sinh lính

        // Thời gian chờ do MÁY GỬI YÊU CẦU tự giữ (xem RequestUnitForLocalPlayer) nên chỉ kiểm tra
        // lại ở đây khi yêu cầu đến từ chính máy này; yêu cầu của client đã được nó tự chặn.
        if (blueSide == LocalPlayerIsBlue && !CanChoose(blueSide)) return;

        GameObject prefab = blueSide ? GetBluePrefab(unitType) : GetRedPrefab((RedUnitType)unitType);
        if (prefab == null)
        {
            Debug.LogError($"GameManager: không có prefab lính {(blueSide ? "XANH" : "ĐỎ")} loại {unitType}!");
            return;
        }

        if (blueSide) spawnSoldier(prefab);
        else spawnEnemy(prefab);
    }

    /// <summary>Prefab tương ứng với từng loại lính XANH (theo <see cref="AIUnitType"/>).</summary>
    public GameObject GetBluePrefab(int unitType)
    {
        switch ((AIUnitType)unitType)
        {
            case AIUnitType.Sword: return Sword;
            case AIUnitType.Archer: return Archer;
            case AIUnitType.Slinger: return Slinger;
            case AIUnitType.SpearMan: return SpearMan;
            case AIUnitType.BaseSlime: return BaseSlime;
            case AIUnitType.Catapult: return Catapult;
            default: return null;
        }
    }

    // ==========================================
    // RED CHOOSEN
    // ==========================================
    public void RedSwordChoosen()
    {
        if (!spawnEnemy(RedSword)) return;

        AI.SwordNumber++;
    }

    public void RedArcherChoosen()
    {
        if (!spawnEnemy(RedArcher)) return;

        AI.ArcherNumber++;
    }

    public void RedSlingerChoosen()
    {
        if (!spawnEnemy(RedSlinger)) return;

        AI.SlingerNumber++;
    }

    public void RedSpearManChoosen()
    {
        if (!spawnEnemy(RedSpearMan)) return;

        AI.SpearManNumber++;
    }

    public void RedBaseSlimeChoosen()
    {
        if (!spawnEnemy(RedBaseSlime)) return;

        AI.RedBaseSlimeNumber++;
    }

    public void RedCatapultChoosen()
    {
        if (!spawnEnemy(RedCatapult)) return;

        AI.CatapultNumber++;
    }

    public void Addmoney(int money)
    {
        ChangeMoney(true, money);
    }

    /// <summary>
    /// King nhả tiền định kỳ -> cộng cho ĐÚNG phe của King đó (King xanh = host, King đỏ = client).
    ///
    /// Phải đi qua <see cref="ChangeMoney"/> như mọi thay đổi tiền khác: ở MultiPlayer đây là nguồn thu
    /// nhập DUY NHẤT của cả 2 phe (trận 2 người không còn tiền/giây). Ghi thẳng vào <c>AI.Money</c> như
    /// trước thì host không công bố cho client, mà client lại bị giá trị host chốt ghi đè - nên phe ĐỎ
    /// coi như không hề có thu nhập từ King.
    /// </summary>
    public void AddKingIncome(bool blueSide, int amount)
    {
        ChangeMoney(blueSide, amount);
    }

    /// <summary>
    /// Lính của phe <paramref name="deadBlueSide"/> vừa bị hạ -> cộng tiền thưởng cho phe ĐỐI PHƯƠNG
    /// theo <see cref="killRewardPriceRatio"/>.
    ///
    /// Chơi đơn: phe đỏ là AI, đã có thu nhập riêng mỗi giây (moneyPersecond) nên KHÔNG nhận thưởng
    /// hạ lính; chỉ người chơi (xanh) được thưởng. MultiPlayer: cả 2 đều là người nên cả 2 đều được
    /// thưởng như nhau.
    ///
    /// Bỏ qua lính có giá &lt;= 0: King có price = 0 nên cái chết của King (kết thúc trận) không sinh
    /// ra khoản thưởng vô nghĩa.
    /// </summary>
    /// <param name="deadBlueSide">Phe của lính VỪA CHẾT.</param>
    /// <param name="killedUnitPrice">Giá của lính vừa bị hạ (lấy từ <c>Slime.price</c>).</param>
    public void AwardKillReward(bool deadBlueSide, int killedUnitPrice)
    {
        if (!killRewardEnabled) return;
        if (killedUnitPrice <= 0) return;   // King / prefab thiếu giá -> không thưởng

        bool killerIsBlue = !deadBlueSide;

        // Chơi đơn: phe đỏ là AI -> không thưởng cho AI.
        if (!NetworkRole.IsMultiplayer && !killerIsBlue) return;

        int reward = Mathf.RoundToInt(killedUnitPrice * Mathf.Max(0f, killRewardPriceRatio));
        if (reward <= 0) return;

        ChangeMoney(killerIsBlue, reward);
    }

    /// <summary>
    /// Trừ tiền của phe ĐỎ (dùng cho nâng cấp thành phe đỏ). Đi qua <see cref="ChangeMoney"/> nên ở
    /// MultiPlayer chỉ host thực sự trừ, client nhận số đã chốt qua mạng.
    /// </summary>
    public void SpendAIMoney(int amount)
    {
        ChangeMoney(false, -amount);
    }

    /// <summary>
    /// Nút "Nâng cấp thành" trên UI gọi hàm này (nâng thành của NGƯỜI CHƠI CỤC BỘ).
    /// Xem <see cref="CastleUpgrade"/> để biết chi tiết chi phí và phần thưởng từng level.
    /// </summary>
    public bool UpgradeCastle()
    {
        return UpgradeCastle(LocalPlayerIsBlue);
    }

    /// <summary>
    /// Nâng thành của 1 phe. MultiPlayer: máy này chỉ gửi yêu cầu, HOST mới thực sự nâng (tiền là
    /// do host chốt) rồi đồng bộ cấp mới sang client.
    /// </summary>
    public bool UpgradeCastle(bool blueSide)
    {
        NetworkGame net = NetworkGame.Instance;

        if (net != null && net.IsSpawned)
        {
            net.RequestUpgrade(blueSide);
            return true;
        }

        return HandleLocalUpgrade(blueSide);
    }

    /// <summary>HOST (hoặc chơi đơn) thực sự nâng thành của 1 phe.</summary>
    public bool HandleLocalUpgrade(bool blueSide)
    {
        if (NetworkRole.IsMultiplayer && !NetworkRole.IsHost) return false;

        CastleUpgrade castle = CastleFor(blueSide);
        if (castle == null)
        {
            Debug.LogError("GameManager: chưa gắn component CastleUpgrade vào GameObject này!");
            return false;
        }

        bool upgraded = castle.TryUpgrade();

        // Đồng bộ cấp thành cho client (client chỉ vẽ hình thành, không tự tính gì).
        if (upgraded) NetworkGame.Instance?.PublishCastleLevel(blueSide, castle.CurrentLevel);

        return upgraded;
    }

    /// <summary>
    /// Một King đã chết → kết thúc trận. Người chơi thắng thì MỞ KHÓA level kế tiếp rồi mới hiện popup
    /// kết quả; thua thì chỉ hiện popup chơi lại. Chỉ xử lý lần gọi đầu tiên của mỗi trận.
    ///
    /// Ở MultiPlayer chỉ HOST quyết định kết quả (client nhận qua mạng, xem
    /// <see cref="HandleRemoteMatchResult"/>) và KHÔNG đụng tới tiến trình level - đó là chuyện của
    /// chế độ chơi đơn.
    /// </summary>
    public void Winner(string winner)
    {
        if (NetworkRole.IsMultiplayer && !NetworkRole.IsHost) return;
        if (matchOver) return;
        matchOver = true;

        bool blueWon = (winner == "blue");

        if (NetworkRole.IsMultiplayer)
        {
            Debug.Log($"MultiPlayer: {(blueWon ? "xanh" : "đỏ")} thắng.");
            StartCoroutine(ShowMultiplayerResultAfterDelay(blueWon));
            NetworkGame.Instance?.PublishMatchResult(blueWon);
            return;
        }

        int level = LevelProgress.SelectedLevel;

        if (blueWon)
        {
            int newlyUnlocked = LevelProgress.CompleteLevel(level);
            Debug.Log($"Blue win - thắng level {level}, mở khóa tới level {LevelProgress.UnlockedLevel}");
            StartCoroutine(ShowResultAfterDelay(true, level, newlyUnlocked));
        }
        else
        {
            Debug.Log($"Red win - thua level {level}");
            StartCoroutine(ShowResultAfterDelay(false, level, 0));
        }
    }

    /// <summary>
    /// Nhận kết quả trận do HOST đồng bộ sang (client). Chỉ hiện popup, không mở khóa level.
    /// </summary>
    public void HandleRemoteMatchResult(bool blueWon)
    {
        if (matchOver) return;
        matchOver = true;

        StartCoroutine(ShowMultiplayerResultAfterDelay(blueWon));
    }

    /// <summary>
    /// MultiPlayer: hiện popup THẮNG/THUA theo góc nhìn của NGƯỜI CHƠI CỤC BỘ (cả 2 máy đều hiện).
    /// </summary>
    private IEnumerator ShowMultiplayerResultAfterDelay(bool blueWon)
    {
        yield return new WaitForSeconds(resultPopupDelay);

        bool localPlayerWon = (blueWon == LocalPlayerIsBlue);

        BaseDialog dialog = (UIManager.Instance != null) ? UIManager.Instance.ShowDialog(resultDialogName) : null;
        DlgResult result = (dialog != null) ? dialog.GetComponent<DlgResult>() : null;

        if (result == null)
        {
            Debug.LogWarning($"GameManager: không hiện được popup kết quả, kiểm tra dialog '{resultDialogName}' trong DialogConfig!");
            yield break;
        }

        result.ShowMultiplayerResult(localPlayerWon);

        // VFX win/lose phát cùng lúc popup vừa hiện, ở giữa màn hình (mỗi máy tự phát theo kết quả của mình).
        PlayResultVfx(localPlayerWon);
    }

    // ==========================================
    // MÁU KING ĐỒNG BỘ QUA MẠNG
    // ==========================================

    /// <summary>King của 1 phe vừa đổi máu -> host công bố cho client (chơi đơn: no-op).</summary>
    public void PublishKingHealth(bool blueSide, int health, int maxHealth, bool playHit)
    {
        NetworkGame.Instance?.PublishKingHealth(blueSide, health, maxHealth, playHit);
    }

    /// <summary>Client nhận máu King do host đồng bộ sang và áp lên King tương ứng.</summary>
    public void ApplySyncedKingHealth(bool blueSide, int health, int maxHealth, bool playHit)
    {
        KingSlime king = FindKing(blueSide);
        if (king != null) king.ApplySyncedHealth(health, maxHealth, playHit);
    }

    /// <summary>Client nhận cấp nâng thành do host đồng bộ sang (chỉ để vẽ hình thành cho đúng).</summary>
    public void ApplySyncedCastleLevel(bool blueSide, int level)
    {
        CastleUpgrade castle = CastleFor(blueSide);
        if (castle != null) castle.SetLevelFromNetwork(level);
    }

    /// <summary>
    /// Chờ một nhịp cho animation King chết chạy xong rồi mới hiện popup kết quả.
    /// KHÔNG dùng Time.timeScale = 0 vì dialog Loading đếm tiến trình bằng Time.deltaTime, đặt timeScale
    /// về 0 sẽ làm màn loading đứng im.
    /// </summary>
    private IEnumerator ShowResultAfterDelay(bool playerWon, int level, int newlyUnlocked)
    {
        yield return new WaitForSeconds(resultPopupDelay);

        BaseDialog dialog = (UIManager.Instance != null) ? UIManager.Instance.ShowDialog(resultDialogName) : null;
        DlgResult result = (dialog != null) ? dialog.GetComponent<DlgResult>() : null;

        if (result == null)
        {
            Debug.LogWarning($"GameManager: không hiện được popup kết quả, kiểm tra dialog '{resultDialogName}' trong DialogConfig!");
            yield break;
        }

        if (playerWon) result.ShowWin(level, newlyUnlocked);
        else result.ShowLose(level);

        // VFX win/lose phát cùng lúc popup vừa hiện, ở giữa màn hình.
        PlayResultVfx(playerWon);
    }

    /// <summary>
    /// Phát hiệu ứng VFX cho popup kết quả: THẮNG -> CFXR Magic Poof, THUA -> CFXR3 Hit Leaves A (Lit),
    /// đặt ở GIỮA MÀN HÌNH và phát ngay khi popup vừa hiện (xem <see cref="DlgResult"/>).
    ///
    /// Chơi đơn: gọi từ <see cref="ShowResultAfterDelay"/> theo kết quả của người chơi.
    /// MultiPlayer: gọi từ <see cref="ShowMultiplayerResultAfterDelay"/> ở CẢ HAI máy, mỗi máy theo
    /// thắng/thua của chính mình nên KHÔNG cần đồng bộ qua mạng (khác với VFX spawn/chết).
    /// </summary>
    private void PlayResultVfx(bool playerWon)
    {
        Vector3 position = VfxManager.ScreenCenterWorldPosition();

        if (playerWon) VfxManager.PlayWinPoof(position);
        else VfxManager.PlayLoseLeaves(position);
    }

    /// <summary>Ra 1 lính xanh (trừ tiền + có thời gian chờ). Trả về true nếu thực sự ra được lính.</summary>
    private bool spawnSoldier(GameObject spawn)
    {
        return SpawnBlueUnit(spawn, true) != null;
    }

    /// <summary>
    /// Ra 1 lính xanh tại <see cref="SpawnSlime"/> rồi cho nó đi về phía King đỏ. Trả về lính vừa ra,
    /// hoặc null nếu không ra được.
    ///
    /// <paramref name="chargeMoney"/> = true : đường MUA LÍNH thường (trừ tiền + bắt đầu thời gian chờ).
    /// = false : dùng cho kỹ năng TĂNG VIỆN - tiền đã thu ở chính kỹ năng và người chơi không phải chờ
    /// cooldown chọn lính, nhưng vẫn tôn trọng trần lính của level.
    ///
    /// Đây cũng là chốt chặn LUẬT CHƠI CỦA LEVEL: loại lính không có trong danh sách trắng của level
    /// thì không ra được, dù đi đường nào (nút HUD, hay yêu cầu gửi từ client qua mạng).
    /// </summary>
    private GameObject SpawnBlueUnit(GameObject spawn, bool chargeMoney)
    {
        if (spawn == null) return null;

        if (IsPlayerAtUnitLimit)
        {
            Debug.Log($"Đã đủ {PlayerUnitLimit} lính trên sân, phải chờ lính chết mới ra thêm được!");
            return null;
        }

        Slime slimePrefab = spawn.GetComponent<Slime>();
        if (slimePrefab == null)
        {
            Debug.LogError($"{spawn.name} không có component Slime!");
            return null;
        }

        int unitType = GetUnitTypeIndex(slimePrefab);
        if (!IsPlayerUnitAllowed(unitType))
        {
            Debug.Log($"Level này không cho dùng {(AIUnitType)unitType}!");
            return null;
        }

        if (SpawnSlime == null)
        {
            Debug.LogError("GameManager: chưa gán SpawnSlime (điểm ra lính xanh) trong Inspector!");
            return null;
        }

        if (chargeMoney)
        {
            if (!CanChoose(true)) return null;

            int price = slimePrefab.price;
            if (!HasEnoughMoney(true, price)) return null;

            ChangeMoney(true, -price);
        }

        Vector3 spawnPos = SpawnSlime.position;

        GameObject spawnedObject = Instantiate(spawn, spawnPos, Quaternion.identity);

        Slime spawnedSlime = spawnedObject.GetComponent<Slime>();
        if (spawnedSlime != null)
        {
            spawnedSlime.blueSide = true;
            spawnedSlime.SpawnPosition = spawnPos;
            spawnedSlime.Destination = GetEnemyKingDestination(true, spawnPos);
        }

        // MultiPlayer: phải đăng ký lính với Netcode thì client mới thấy được (chơi đơn: no-op).
        NetworkRole.SpawnIfServer(spawnedObject);

        // Chỉ đường mua lính mới bắt đầu thời gian chờ (tăng viện không chiếm nhịp chọn lính).
        if (chargeMoney) StartCoroutine(ChooseCooldown(true));

        return spawnedObject;
    }

    /// <summary>Ra 1 lính đỏ. Trả về true nếu thực sự ra được lính (đủ tiền + chưa đầy).</summary>
    private bool spawnEnemy(GameObject spawn)
    {
        if (IsAIAtUnitLimit) return false;   // AI thử mỗi nhịp nên không log ở đây

        Slime slimePrefab = (spawn != null) ? spawn.GetComponent<Slime>() : null;
        if (slimePrefab == null)
        {
            Debug.LogError($"{spawn?.name} không có component Slime!");
            return false;
        }

        // MultiPlayer: phe đỏ do NGƯỜI CHƠI thứ hai điều khiển nên có thời gian chờ như phe xanh.
        // Chơi đơn: phe đỏ là AI, ra lính theo nhịp riêng (AI.spawnInterval) nên không dùng chờ.
        if (NetworkRole.IsMultiplayer && !CanChoose(false)) return false;

        int price = slimePrefab.price;
        if (!HasEnoughMoney(false, price)) return false;

        ChangeMoney(false, -price);

        InstantiateRedPrefab(spawn);

        if (NetworkRole.IsMultiplayer) StartCoroutine(ChooseCooldown(false));

        return true;
    }

    /// <summary>
    /// Tạo 1 lính đỏ tại RedSpawnSlime và cho nó đi về phía King xanh. Dùng chung cho cả luồng
    /// spawn của AI lẫn spawn thủ công bằng cheat để 2 luồng luôn cấu hình lính giống nhau.
    /// </summary>
    private GameObject InstantiateRedPrefab(GameObject spawn)
    {
        if (spawn == null)
        {
            Debug.LogError("GameManager: prefab lính đỏ bị null!");
            return null;
        }

        if (RedSpawnSlime == null)
        {
            Debug.LogError("GameManager: chưa gán RedSpawnSlime (điểm ra lính đỏ) trong Inspector!");
            return null;
        }

        Vector3 spawnPos = RedSpawnSlime.position;

        GameObject spawnedObject = Instantiate(spawn, spawnPos, Quaternion.identity);

        Slime spawnedSlime = spawnedObject.GetComponent<Slime>();
        if (spawnedSlime != null)
        {
            spawnedSlime.blueSide = false;
            spawnedSlime.SpawnPosition = spawnPos;
            spawnedSlime.Destination = GetEnemyKingDestination(false, spawnPos);
        }

        // MultiPlayer: phải đăng ký lính với Netcode thì client mới thấy được (chơi đơn: no-op).
        NetworkRole.SpawnIfServer(spawnedObject);

        return spawnedObject;
    }

    // ==========================================
    // ĐÍCH ĐẾN: KING PHE ĐỊCH
    // ==========================================

    private KingSlime cachedBlueKing;
    private KingSlime cachedRedKing;

    /// <summary>
    /// Điểm đến cho lính vừa ra: vị trí King ĐỐI PHƯƠNG. King đứng yên tại chỗ nên chỉ cần lấy vị
    /// trí lúc spawn; lính tự dừng lại đánh khi vào tầm (xem <see cref="Slime.GetClearDistanceAhead"/>).
    ///
    /// Nếu KHÔNG tìm thấy King thì log lỗi rõ ràng rồi cho lính đi về phía trước một quãng - KHÔNG
    /// để Destination = (0,0,0), vì khi đó lính rơi vào hướng mặc định và khựng lại giữa đường.
    /// </summary>
    private Vector3 GetEnemyKingDestination(bool myBlueSide, Vector3 spawnPosition)
    {
        KingSlime enemyKing = FindKing(!myBlueSide);
        if (enemyKing != null) return enemyKing.transform.position;

        Debug.LogError(
            $"GameManager: không tìm thấy KingSlime phe {(!myBlueSide ? "XANH" : "ĐỎ")} trong scene! " +
            "Kiểm tra KingBlue/KingRed đã có trong scene chưa.");

        Vector3 forward = myBlueSide ? Vector3.right : Vector3.left;
        return spawnPosition + forward * 20f;
    }

    /// <summary>
    /// King của 1 phe: ưu tiên field gán trong Inspector, rồi tới bản đã cache, cuối cùng tự quét
    /// scene. King có sẵn trong scene nên tìm 1 lần là đủ; quét lại khi King cũ đã bị huỷ.
    /// </summary>
    private KingSlime FindKing(bool blue)
    {
        KingSlime fromInspector = blue ? BlueKing : RedKing;
        KingSlime cached = blue ? cachedBlueKing : cachedRedKing;

        KingSlime found = (fromInspector != null) ? fromInspector : cached;
        if (found != null) return found;

        foreach (KingSlime king in FindObjectsByType<KingSlime>(FindObjectsSortMode.None))
        {
            if (king == null || king.blueSide != blue) continue;

            if (blue) cachedBlueKing = king;
            else cachedRedKing = king;

            return king;
        }

        return null;
    }

    // ==========================================
    // SPAWN LÍNH ĐỎ THỦ CÔNG (CHEAT / DEBUG)
    // ==========================================

    /// <summary>
    /// Spawn thủ công 1 loại lính đỏ (dùng cho DebugCheatPanel). Mặc định miễn phí nên vẫn spawn
    /// được kể cả khi AI hết tiền. Số lượng lính đỏ vẫn được cộng vào AIHas để model AI đọc đúng
    /// tình hình trận đấu.
    /// </summary>
    public GameObject SpawnRedUnit(RedUnitType type, bool free = true)
    {
        GameObject prefab = GetRedPrefab(type);
        if (prefab == null)
        {
            Debug.LogError($"GameManager: prefab lính đỏ '{type}' chưa được gán trong Inspector!");
            return null;
        }

        Slime slimePrefab = prefab.GetComponent<Slime>();
        if (slimePrefab == null)
        {
            Debug.LogError($"{prefab.name} không có component Slime!");
            return null;
        }

        if (!free)
        {
            int price = slimePrefab.price;
            if (AIMoney < price)
            {
                Debug.Log($"AI không đủ tiền! Cần {price}, đang có {AIMoney}");
                return null;
            }

            AI.Money -= price;
            AIMoney = AI.Money;
        }

        GameObject spawnedObject = InstantiateRedPrefab(prefab);
        IncreaseRedUnitCount(type);
        return spawnedObject;
    }

    /// <summary>Prefab tương ứng với từng loại lính đỏ.</summary>
    public GameObject GetRedPrefab(RedUnitType type)
    {
        switch (type)
        {
            case RedUnitType.Sword: return RedSword;
            case RedUnitType.Archer: return RedArcher;
            case RedUnitType.Slinger: return RedSlinger;
            case RedUnitType.SpearMan: return RedSpearMan;
            case RedUnitType.BaseSlime: return RedBaseSlime;
            case RedUnitType.Catapult: return RedCatapult;
            default: return null;
        }
    }

    private void IncreaseRedUnitCount(RedUnitType type)
    {
        switch (type)
        {
            case RedUnitType.Sword: AI.SwordNumber++; break;
            case RedUnitType.Archer: AI.ArcherNumber++; break;
            case RedUnitType.Slinger: AI.SlingerNumber++; break;
            case RedUnitType.SpearMan: AI.SpearManNumber++; break;
            case RedUnitType.BaseSlime: AI.RedBaseSlimeNumber++; break;
            case RedUnitType.Catapult: AI.CatapultNumber++; break;
        }
    }

    /// <summary>Đếm thêm 1 lính XANH vừa ra sân vào bộ đếm của phe xanh.</summary>
    private void IncreaseBlueUnitCount(int unitType)
    {
        switch ((AIUnitType)unitType)
        {
            case AIUnitType.Sword: player.SwordNumber++; break;
            case AIUnitType.Archer: player.ArcherNumber++; break;
            case AIUnitType.Slinger: player.SlingerNumber++; break;
            case AIUnitType.SpearMan: player.SpearManNumber++; break;
            case AIUnitType.BaseSlime: player.BaseSlimeNumber++; break;
            case AIUnitType.Catapult: player.CatapultNumber++; break;
        }
    }

    // ==========================================
    // LUẬT CHƠI CỦA LEVEL: KỸ NĂNG CHỦ ĐỘNG
    //
    // Kỹ năng chủ động do component AbilityBar (Assets/Scripts/Level/AbilityBar.cs) điều khiển.
    //
    // GameManager chỉ mở API cho component đó gọi, nhờ vậy LUẬT CHƠI và CÁCH SINH LÍNH nằm cùng một
    // chỗ (không để mỗi component tự Instantiate lính theo cách riêng rồi lệch với luồng còn lại).
    // ==========================================

    /// <summary>
    /// Gom các lính ĐANG SỐNG của 1 phe (KHÔNG tính King) vào <paramref name="results"/> - dùng cho kỹ
    /// năng chủ động (thiên thạch cần tìm lính đỏ, hiệu triệu cần tìm lính xanh).
    /// Truyền sẵn list để không cấp phát mảng mới mỗi lần bấm kỹ năng.
    /// </summary>
    public void CollectLiveUnits(bool blueSide, List<Slime> results)
    {
        if (results == null) return;

        results.Clear();

        foreach (Slime slime in FindObjectsByType<Slime>(FindObjectsSortMode.None))
        {
            if (slime == null || slime is KingSlime) continue;
            if (slime.blueSide != blueSide) continue;
            if (GetUnitTypeIndex(slime) < 0) continue;

            results.Add(slime);
        }
    }

    /// <summary>Buffer dùng lại cho <see cref="ApplyAreaDamage"/> - tránh cấp phát danh sách mới mỗi lần thiên thạch nổ.</summary>
    private readonly List<Slime> areaDamageBuffer = new List<Slime>();

    /// <summary>
    /// Gây <paramref name="damage"/> lên MỌI lính thuộc phe <paramref name="targetBlueSide"/> đang đứng trong
    /// bán kính <paramref name="radius"/> quanh <paramref name="center"/> (kỹ năng Thiên thạch - xem
    /// <see cref="AbilityBar"/> và <see cref="MeteorStrike"/>).
    ///
    /// Danh sách lính được ĐỌC LẠI ngay tại lúc gọi chứ không dùng danh sách chụp từ lúc bấm nút: đợt thiên
    /// thạch rơi mất vài phần mười giây mới chạm đất, nên lính đi vào vùng nổ trong lúc đó vẫn ăn đòn, còn
    /// lính chạy ra ngoài thì thoát - nhìn khớp với cảnh tượng trên sân.
    /// </summary>
    /// <param name="targetBlueSide">
    /// Phe ĂN ĐÒN: true = lính xanh, false = lính đỏ. Chơi đơn luôn là false (người chơi phe xanh chỉ có
    /// thể ném thiên thạch vào phe đỏ), còn MultiPlayer thì phe đỏ cũng có kỹ năng nên phải nói rõ.
    /// </param>
    /// <returns>Số lính thực sự bị trừ máu.</returns>
    public int ApplyAreaDamage(bool targetBlueSide, Vector3 center, float radius, int damage)
    {
        if (damage <= 0 || radius <= 0f) return 0;

        CollectLiveUnits(targetBlueSide, areaDamageBuffer);

        float radiusSqr = radius * radius;
        int hitCount = 0;

        for (int i = 0; i < areaDamageBuffer.Count; i++)
        {
            Slime enemy = areaDamageBuffer[i];
            if (enemy == null) continue;
            if ((enemy.transform.position - center).sqrMagnitude > radiusSqr) continue;

            enemy.TakeDamage(damage);
            hitCount++;
        }

        Debug.Log($"Thiên thạch: {damage} sát thương lên {hitCount} lính {(targetBlueSide ? "xanh" : "đỏ")} " +
                  $"quanh ({center.x:0.#}, {center.y:0.#}).");

        return hitCount;
    }

    /// <summary>
    /// Kỹ năng TĂNG VIỆN: gọi ngay <paramref name="count"/> lính của phe <paramref name="blueSide"/>, KHÔNG
    /// trừ tiền (GameManager đã thu tiền của kỹ năng, xem <see cref="TryUseAbility"/>) và KHÔNG chiếm nhịp
    /// chọn lính.
    ///
    /// Tôn trọng trần lính của phe đó nên gọi lúc sân đầy sẽ không ra được lính nào - phía gọi PHẢI kiểm
    /// tra trần trước khi thu tiền (xem <see cref="TryUseAbility"/>).
    /// </summary>
    /// <param name="blueSide">Phe được gọi thêm lính: true = xanh, false = đỏ (MultiPlayer).</param>
    /// <param name="unitType">Loại lính theo <see cref="AIUnitType"/> (phe đỏ dùng đúng loại tương ứng).</param>
    /// <returns>Số lính thực sự ra được.</returns>
    public int SpawnReinforcement(bool blueSide, int unitType, int count)
    {
        if (count <= 0) return 0;

        GameObject prefab = blueSide ? GetBluePrefab(unitType) : GetRedPrefab((RedUnitType)unitType);
        if (prefab == null) return 0;

        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            // Đếm THẲNG trên sân chứ không dùng IsPlayerAtUnitLimit: bộ đếm cache chỉ được làm mới mỗi
            // giây nên trong cùng một lần gọi sẽ ra quá trần (xem RecountUnitsOnField).
            if (CountLiveUnits(blueSide) >= UnitLimitFor(blueSide)) break;

            GameObject unit = SpawnReinforcementUnit(blueSide, prefab);
            if (unit == null) break;

            if (blueSide) IncreaseBlueUnitCount(unitType);
            else IncreaseRedUnitCount((RedUnitType)unitType);

            spawned++;
        }

        return spawned;
    }

    /// <summary>
    /// Ra 1 lính cho kỹ năng TĂNG VIỆN, không trừ tiền và không bắt đầu nhịp chờ chọn lính. Dùng chung
    /// <see cref="SpawnBlueUnit"/> (đã có sẵn luật danh sách trắng của level) cho phe xanh, còn phe đỏ thì
    /// đi thẳng vào <see cref="InstantiateRedPrefab"/> - lính đỏ do AI mua cũng dùng đúng hàm đó nên hai
    /// luồng luôn cấu hình lính giống nhau.
    /// </summary>
    private GameObject SpawnReinforcementUnit(bool blueSide, GameObject prefab)
    {
        if (blueSide) return SpawnBlueUnit(prefab, false);

        GameObject spawned = InstantiateRedPrefab(prefab);
        return (spawned != null) ? spawned : null;
    }

    // ==========================================
    // THỰC THI KỸ NĂNG CHỦ ĐỘNG (chơi đơn + MultiPlayer)
    //
    // AbilityBar chỉ lo phần UI (nút bấm, giá, đếm ngược hồi chiêu). Mọi thứ ĐỔI CỤC DIỆN TRẬN ĐẤU - trừ
    // tiền, gọi lính, gây sát thương, giảm sát thương - nằm ở đây, nhờ vậy chơi đơn và MultiPlayer dùng
    // chung đúng một bộ luật, không lệch nhau theo thời gian.
    //
    // MULTIPLAYER: HOST là bên duy nhất THỰC THI. Máy client gửi yêu cầu qua NetworkGame; host kiểm tra
    // tiền / hồi chiêu / mục tiêu rồi thực thi, sau đó công bố ngược lại cho client (hồi chiêu + hình ảnh +
    // âm thanh). Nhờ vậy sát thương và tiền không bao giờ bị tính hai lần mà cả 2 máy vẫn thấy giống nhau.
    //
    // PHE: kỹ năng luôn được dùng CHO PHE CỦA NGƯỜI BẤM (host = xanh, client = đỏ) và đánh vào phe đối
    // diện, nên mọi hàm dưới đây đều nhận phe của NGƯỜI DÙNG KỸ NĂNG.
    // ==========================================

    /// <summary>Số phe trong trận: 0 = xanh, 1 = đỏ (chỉ số của <see cref="abilityReadyAt"/>).</summary>
    private const int AbilitySideCount = 2;

    /// <summary>Thông số từng kỹ năng, do AbilityBar đăng ký lúc vào trận (xem <see cref="RegisterAbilitySpec"/>).</summary>
    private readonly AbilitySpec[] abilitySpecs = new AbilitySpec[ActiveAbilities.Count];

    /// <summary>Mốc <c>Time.time</c> kỹ năng hồi xong, theo [phe, kỹ năng]. 0 = dùng được ngay.</summary>
    private readonly float[,] abilityReadyAt = new float[AbilitySideCount, ActiveAbilities.Count];

    /// <summary>Buffer dùng lại khi kỹ năng phải quét lính ĐỊCH (thiên thạch nhắm tuyến đầu).</summary>
    private readonly List<Slime> abilityEnemyBuffer = new List<Slime>();

    /// <summary>Buffer dùng lại khi kỹ năng phải quét lính MÌNH (hiệu triệu).</summary>
    private readonly List<Slime> abilityAllyBuffer = new List<Slime>();

    /// <summary>
    /// <see cref="AbilityBar"/> gọi lúc vào trận để đăng ký thông số kỹ năng (tiền, hồi chiêu, sát thương...).
    ///
    /// Vì sao cần bước đăng ký: bộ số vẫn được CHỈNH TRONG INSPECTOR của AbilityBar (chỗ người làm game
    /// quen sửa), nhưng lúc chạy thì GameManager + Netcode mới là nơi thực thi và đồng bộ. Đăng ký một lần
    /// ở Start nên không tốn gì; máy nào cũng đăng ký bộ số của CHÍNH NÓ (cùng một scene nên giống nhau).
    /// </summary>
    public void RegisterAbilitySpec(AbilitySpec spec)
    {
        if (spec == null) return;

        int index = (int)spec.ability;
        if (index < 0 || index >= abilitySpecs.Length)
        {
            Debug.LogWarning($"GameManager: kỹ năng '{spec.ability}' nằm ngoài khoảng hợp lệ, bỏ qua.");
            return;
        }

        abilitySpecs[index] = spec;
    }

    /// <summary>Thông số của 1 kỹ năng; null nếu chưa ai đăng ký (AbilityBar chưa chạy hoặc kỹ năng lạ).</summary>
    public AbilitySpec GetAbilitySpec(ActiveAbilityType ability)
    {
        int index = (int)ability;
        if (index < 0 || index >= abilitySpecs.Length) return null;

        return abilitySpecs[index];
    }

    /// <summary>
    /// Còn bao nhiêu giây nữa kỹ năng mới dùng được cho phe <paramref name="blueSide"/> (0 = dùng được ngay).
    /// UI đọc số này để làm mờ nút và chạy vòng đếm ngược.
    /// </summary>
    public float AbilityCooldownRemaining(bool blueSide, ActiveAbilityType ability)
    {
        int index = (int)ability;
        if (index < 0 || index >= ActiveAbilities.Count) return 0f;

        return Mathf.Max(0f, abilityReadyAt[blueSide ? 0 : 1, index] - Time.time);
    }

    /// <summary>
    /// Người chơi cục bộ vừa bấm 1 kỹ năng (gọi từ <see cref="AbilityBar"/>).
    ///
    /// Chơi đơn và host: thực thi ngay tại đây và trả về kết quả thật.
    /// Client trong MultiPlayer: chỉ GỬI YÊU CẦU lên host rồi trả về true - hồi chiêu và hình ảnh sẽ tới sau
    /// theo gói tin của host (xem <see cref="ApplyRemoteAbilityCast"/>), nên máy client không tự trừ tiền
    /// cũng không tự bắt đầu hồi chiêu.
    /// </summary>
    /// <param name="casterBlueSide">Phe của người bấm nút (host = xanh, client = đỏ).</param>
    /// <param name="ability">Kỹ năng vừa bấm.</param>
    /// <returns>true khi lệnh đã được nhận (client: đã gửi lên host).</returns>
    public bool TryUseAbility(bool casterBlueSide, ActiveAbilityType ability)
    {
        NetworkGame net = NetworkGame.Instance;

        // Chơi đơn (không có NetworkGame) hoặc host: cả hai đều thực thi ngay tại máy này.
        if (net == null || !net.IsSpawned || net.IsServer) return ExecuteAbility(casterBlueSide, ability);

        net.RequestAbility(casterBlueSide, (int)ability);
        return true;
    }

    /// <summary>
    /// HOST (hoặc chơi đơn) thực sự thực thi 1 kỹ năng: kiểm tra tiền + hồi chiêu, áp hiệu ứng, rồi mới trừ
    /// tiền và bắt đầu hồi chiêu.
    ///
    /// Thứ tự kiểm tra cố ý như sau: chưa hồi xong / không đủ tiền thì bỏ qua NGAY, còn "kỹ năng này có tác
    /// dụng gì không" thì kiểm tra bằng chính lúc áp dụng - có tác dụng rồi mới trừ tiền, nhờ vậy người chơi
    /// không bao giờ mất tiền mà không có gì xảy ra (bấm thiên thạch lúc sân trống, tăng viện lúc đủ trần...).
    /// </summary>
    public bool ExecuteAbility(bool casterBlueSide, ActiveAbilityType ability)
    {
        if (NetworkRole.IsMultiplayer && !NetworkRole.IsHost) return false;
        if (matchOver)
        {
            Debug.Log("Trận đã có kết quả, không dùng kỹ năng được nữa.");
            return false;
        }

        AbilitySpec spec = GetAbilitySpec(ability);
        if (spec == null)
        {
            Debug.LogWarning($"GameManager: chưa có thông số cho kỹ năng {ability} (AbilityBar chưa đăng ký).");
            return false;
        }

        int side = casterBlueSide ? 0 : 1;
        int index = (int)ability;

        if (Time.time < abilityReadyAt[side, index])
        {
            Debug.Log($"{spec.DisplayName} đang hồi, còn {abilityReadyAt[side, index] - Time.time:0.#}s.");
            return false;
        }

        int cost = Mathf.Max(0, spec.cost);
        if (MoneyOf(casterBlueSide) < cost)
        {
            Debug.Log($"Không đủ tiền dùng {spec.DisplayName} (cần {cost}, " +
                      $"phe {(casterBlueSide ? "xanh" : "đỏ")} đang có {MoneyOf(casterBlueSide)}).");
            return false;
        }

        if (!ApplyAbilityEffect(casterBlueSide, spec, out Vector3 meteorCenter)) return false;

        if (cost > 0) ChangeMoney(casterBlueSide, -cost);

        float cooldown = Mathf.Max(0f, spec.cooldown);
        abilityReadyAt[side, index] = Time.time + cooldown;

        // Host công bố cho client: vừa để máy kia chạy hình ảnh/âm thanh, vừa để nó bắt đầu hồi chiêu đúng
        // như máy này. Chơi đơn thì NetworkGame không tồn tại nên bước này bỏ qua.
        if (NetworkRole.IsHost)
        {
            NetworkGame.Instance?.PublishAbilityCast(casterBlueSide, index, meteorCenter, cooldown);
        }

        return true;
    }

    /// <summary>
    /// Áp hiệu ứng THẬT của 1 kỹ năng (chỉ host / chơi đơn gọi). Trả về false nếu kỹ năng không có tác dụng
    /// gì - lúc đó KHÔNG trừ tiền và KHÔNG bắt đầu hồi chiêu.
    /// </summary>
    /// <param name="meteorCenter">Điểm rơi của thiên thạch - cần gửi cho client để 2 máy nổ cùng một chỗ.</param>
    private bool ApplyAbilityEffect(bool casterBlueSide, AbilitySpec spec, out Vector3 meteorCenter)
    {
        meteorCenter = Vector3.zero;

        switch (spec.ability)
        {
            case ActiveAbilityType.Meteor: return CastMeteor(casterBlueSide, spec, out meteorCenter);
            case ActiveAbilityType.Reinforce: return CallReinforcement(casterBlueSide, spec);
            case ActiveAbilityType.Rally: return RaiseRally(casterBlueSide, spec);
            default: return false;
        }
    }

    /// <summary>
    /// THIÊN THẠCH: nhắm vào con lính ĐỊCH ĐANG ĐI ĐẦU HÀNG (tuyến đầu phe địch) rồi gây sát thương lên mọi
    /// lính địch trong bán kính.
    ///
    /// Cách chọn mục tiêu: lính XANH đi sang PHẢI còn lính ĐỎ đi sang TRÁI (xem <c>Slime.Start</c>) nên
    /// "đầu hàng" của phe địch chính là con đã tiến sâu nhất về phía thành nhà - con nguy hiểm nhất. Trước
    /// đây kỹ năng nhắm vào chỗ ĐÔNG lính nhất, nhưng như vậy có thể bỏ qua con đang áp sát thành trong khi
    /// cả đám còn ở xa, nên đã đổi thành nhắm tuyến đầu.
    ///
    /// HÌNH ẢNH & THỜI ĐIỂM TRỪ MÁU: kỹ năng giao cho <see cref="MeteorStrike"/> một ĐỢT thiên thạch rơi từ
    /// ngoài màn hình xuống vùng nổ, mỗi viên trừ máu một lần đúng lúc nó chạm đất - lúc đó hàm này đã chạy
    /// xong, tiền và hồi chiêu vẫn tính ngay khi bấm như mọi kỹ năng khác.
    /// </summary>
    /// <returns>false khi không có lính địch nào trên sân - lúc đó kỹ năng không bị trừ tiền.</returns>
    private bool CastMeteor(bool casterBlueSide, AbilitySpec spec, out Vector3 center)
    {
        center = Vector3.zero;

        if (!ResolveMeteorTarget(casterBlueSide, out center))
        {
            Debug.Log("Thiên thạch: trên sân không có lính địch nào.");
            return false;
        }

        float radius = Mathf.Max(0.5f, spec.radius);
        int damage = Mathf.Max(1, spec.damage);

        Debug.Log($"Thiên thạch: phe {(casterBlueSide ? "xanh" : "đỏ")} nhắm vào lính địch tuyến đầu ở " +
                  $"({center.x:0.#}, {center.y:0.#}).");

        PlayMeteorEffects(casterBlueSide, center, radius, damage, applyDamage: true);

        return true;
    }

    /// <summary>
    /// Tìm điểm rơi cho thiên thạch: con lính ĐỊCH đi xa nhất về phía thành nhà (xem <see cref="CastMeteor"/>).
    /// Lính xanh tiến sang phải nên tuyến đầu của phe xanh là x LỚN NHẤT; lính đỏ tiến sang trái nên tuyến
    /// đầu phe đỏ là x NHỎ NHẤT.
    /// </summary>
    private bool ResolveMeteorTarget(bool casterBlueSide, out Vector3 center)
    {
        bool enemyBlueSide = !casterBlueSide;

        CollectLiveUnits(enemyBlueSide, abilityEnemyBuffer);

        center = Vector3.zero;
        if (abilityEnemyBuffer.Count == 0) return false;

        center = abilityEnemyBuffer[0].transform.position;

        for (int i = 0; i < abilityEnemyBuffer.Count; i++)
        {
            Slime candidate = abilityEnemyBuffer[i];
            if (candidate == null) continue;

            Vector3 candidatePosition = candidate.transform.position;
            bool farther = enemyBlueSide ? candidatePosition.x > center.x : candidatePosition.x < center.x;
            if (farther) center = candidatePosition;
        }

        return true;
    }

    /// <summary>
    /// HÌNH ẢNH + ÂM THANH của kỹ năng Thiên thạch. Dùng chung cho host và client: máy nào cũng thấy đúng
    /// đợt thiên thạch ở đúng chỗ, nhưng chỉ máy có <paramref name="applyDamage"/> = true mới trừ máu.
    /// </summary>
    private void PlayMeteorEffects(bool casterBlueSide, Vector3 center, float radius, int damage, bool applyDamage)
    {
        // Chưa gán art thiên thạch thì PlayMeteorStrike trả về false và ta trừ máu ngay như trước - kỹ năng
        // không bao giờ mất tiền mà lại không có gì xảy ra.
        if (!VfxManager.PlayMeteorStrike(center, radius, damage, casterBlueSide, applyDamage))
        {
            VfxManager.PlayAbilityImpact(center);

            if (applyDamage) ApplyAreaDamage(!casterBlueSide, center, radius, damage);
        }

        // Tiếng của nút btn_meteor. Phát ở ĐÂY (sau khi chắc chắn kỹ năng có tác dụng) chứ không phải lúc
        // bấm nút, để bấm lúc đang hồi / không đủ tiền / sân không có lính địch thì không có tiếng.
        AudioManager.PlayMeteor();
    }

    /// <summary>
    /// TĂNG VIỆN: gọi thêm lính của CHÍNH PHE ĐANG BẤM, không tốn thời gian chờ mua lính.
    /// Không ra được lính nào (sân đã đủ trần) thì coi như kỹ năng không có tác dụng -> không mất tiền.
    /// </summary>
    private bool CallReinforcement(bool casterBlueSide, AbilitySpec spec)
    {
        int limit = UnitLimitFor(casterBlueSide);
        int freeSlots = limit - CountLiveUnits(casterBlueSide);

        if (freeSlots <= 0)
        {
            Debug.Log($"Tăng viện: phe {(casterBlueSide ? "xanh" : "đỏ")} đã đủ {limit} lính, chờ lính chết rồi " +
                      "hãy gọi.");
            return false;
        }

        int count = Mathf.Clamp(Mathf.Max(1, spec.unitCount), 1, freeSlots);
        int spawned = SpawnReinforcement(casterBlueSide, (int)spec.reinforceUnit, count);

        if (spawned <= 0)
        {
            Debug.Log("Tăng viện: không gọi được lính nào (kiểm tra prefab lính và điểm ra quân trong scene).");
            return false;
        }

        Debug.Log($"Tăng viện: phe {(casterBlueSide ? "xanh" : "đỏ")} gọi thêm {spawned} lính " +
                  $"{spec.reinforceUnit}.");

        // Tiếng của nút btn_reinfor_blue - chỉ phát khi thực sự gọi được lính (sân đầy thì không có tiếng).
        AudioManager.PlaySummon();

        return true;
    }

    /// <summary>
    /// HIỆU TRIỆU: hồi đầy máu toàn bộ lính ĐANG SỐNG của phe bấm nút và giảm sát thương chúng nhận vào
    /// trong vài giây. Không tính King (King vẫn đứng nguyên như cũ) - kỹ năng này dành cho ĐỘI HÌNH.
    ///
    /// Ở MultiPlayer: host áp buff thật, còn client nhận lệnh rồi áp ĐÚNG các lời gọi này lên bản sao của
    /// lính (máy client không mô phỏng sát thương nên ở đó chỉ có tác dụng hiển thị: nhấp nháy + aura).
    /// </summary>
    private bool RaiseRally(bool casterBlueSide, AbilitySpec spec)
    {
        CollectLiveUnits(casterBlueSide, abilityAllyBuffer);

        if (abilityAllyBuffer.Count == 0)
        {
            Debug.Log($"Hiệu triệu: trên sân không có lính {(casterBlueSide ? "xanh" : "đỏ")} nào.");
            return false;
        }

        ApplyRallyBuff(casterBlueSide, spec);

        return true;
    }

    /// <summary>Hồi đầy máu + giảm sát thương cho toàn bộ lính của 1 phe (dùng chung cho host và client).</summary>
    private void ApplyRallyBuff(bool blueSide, AbilitySpec spec)
    {
        float reduction = Mathf.Clamp01(spec.damageReduction);
        float duration = Mathf.Max(0.5f, spec.duration);

        CollectLiveUnits(blueSide, abilityAllyBuffer);

        int healedCount = 0;

        foreach (Slime ally in abilityAllyBuffer)
        {
            if (ally == null) continue;

            int healthBefore = ally.health;

            // Hồi đầy: xin đúng bằng máu tối đa, Heal tự kẹp trần nên không lo hồi quá.
            ally.Heal(Mathf.Max(1, ally.MaxHealth));
            ally.ApplyDamageReduction(reduction, duration);

            if (ally.health > healthBefore) healedCount++;
        }

        Debug.Log($"Hiệu triệu: {abilityAllyBuffer.Count} lính {(blueSide ? "xanh" : "đỏ")} được giảm sát " +
                  $"thương x{reduction:0.##} trong {duration:0.#}s ({healedCount} lính được hồi máu).");
    }

    /// <summary>
    /// Máy CLIENT nhận lệnh dùng kỹ năng do host công bố (xem <c>NetworkGame.AbilityCastRpc</c>): bắt đầu
    /// hồi chiêu đúng như host rồi chạy phần HÌNH ẢNH + ÂM THANH.
    ///
    /// Không trừ tiền ở đây: số tiền đã do host chốt và tự chảy về qua NetworkVariable.
    /// </summary>
    public void ApplyRemoteAbilityCast(bool casterBlueSide, int abilityIndex, Vector3 meteorCenter,
        float cooldownSeconds)
    {
        if (abilityIndex < 0 || abilityIndex >= ActiveAbilities.Count) return;

        ActiveAbilityType ability = (ActiveAbilityType)abilityIndex;

        abilityReadyAt[casterBlueSide ? 0 : 1, abilityIndex] = Time.time + Mathf.Max(0f, cooldownSeconds);

        AbilitySpec spec = GetAbilitySpec(ability);
        if (spec == null) return;

        switch (ability)
        {
            case ActiveAbilityType.Meteor:
                PlayMeteorEffects(casterBlueSide, meteorCenter, Mathf.Max(0.5f, spec.radius),
                    Mathf.Max(1, spec.damage), applyDamage: false);
                break;

            case ActiveAbilityType.Reinforce:
                // Lính mới do host sinh ra và đã tự đồng bộ sang; ở đây chỉ cần tiếng.
                AudioManager.PlaySummon();
                break;

            case ActiveAbilityType.Rally:
                ApplyRallyBuff(casterBlueSide, spec);
                break;
        }
    }

    private void setMoneyforAI()
    {
        int[] prices = UnitPrices;

        AI.SwordMoney = prices[(int)AIUnitType.Sword];
        AI.ArcherMoney = prices[(int)AIUnitType.Archer];
        AI.SlingerMoney = prices[(int)AIUnitType.Slinger];
        AI.CatapultMoney = prices[(int)AIUnitType.Catapult];
        AI.SpearManMoney = prices[(int)AIUnitType.SpearMan];
        AI.BaseSlimeMoney = prices[(int)AIUnitType.BaseSlime];
        AI.Money = AIMoney;
    }

    // Giá lính đỏ lấy trực tiếp từ component Slime của prefab để AI tính toán tiền.
    private int GetPrice(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("GameManager: prefab lính đỏ chưa được gán!");
            return int.MaxValue;
        }

        Slime slime = prefab.GetComponent<Slime>();
        if (slime == null)
        {
            Debug.LogError($"{prefab.name} không có component Slime!");
            return int.MaxValue;
        }

        return slime.price;
    }

    public void soldierDown(String name)
    {
        switch (name)
        {
            case "BlueSword": player.SwordNumber--; break;
            case "BlueArcher": player.ArcherNumber--; break;
            case "BlueSlinger": player.SlingerNumber--; break;
            case "BlueBaseSlime": player.BaseSlimeNumber--; break;
            case "BlueCatapult": player.CatapultNumber--; break;
            case "BlueSpearMan": player.SpearManNumber--; break;
            case "RedSword": AI.SwordNumber--; break;
            case "RedArcher": AI.ArcherNumber--; break;
            case "RedSlinger": AI.SlingerNumber--; break;
            case "RedBaseSlime": AI.RedBaseSlimeNumber--; break;
            case "RedCatapult": AI.CatapultNumber--; break;
            case "RedSpearMan": AI.SpearManNumber--; break;
        }
    }
}