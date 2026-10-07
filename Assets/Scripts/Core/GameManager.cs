using System;
using System.Collections;
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

/// <summary>
/// Độ khó của MỘT level. Mỗi level là một trận mới nên các chỉ số này được áp lại từ đầu trận
/// (xem <see cref="GameManager.Winner"/> / ApplyLevelDifficulty trong GameManager).
///
/// Bố cục gợi ý (xem ApplyLevelDifficulty + AI.ReadDifficulty):
///   - Level 1 (người mới) : AI ra quân thưa, ít tiền, ít lính, gần như không khắc chế.
///   - Level 2 (thường)    : AI ra quân vừa, đủ tiền, có khắc chế vừa phải.
///   - Level 3 (khó)       : AI ra quân dày, nhiều tiền, khắc chế tối đa, King trâu hơn.
/// </summary>
[Serializable]
public class LevelSettings
{
    [Tooltip("Level áp dụng (1 = level đầu tiên).")]
    public int level = 1;

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

    [Tooltip("Số giây giữa 2 lần AI ra lính ở level này. Lớn hơn = AI ra quân thưa hơn, người chơi dễ thở hơn.")]
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
}

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

    [Tooltip("Tên dialog hiện kết quả thắng/thua (đăng ký trong DialogConfig).")]
    [SerializeField] private string resultDialogName = DlgResult.DialogName;

    [Tooltip("Chờ bao lâu sau khi King chết rồi mới hiện popup kết quả (cho animation chết chạy xong).")]
    [Min(0f)]
    [SerializeField] private float resultPopupDelay = 0.8f;

    /// <summary>Trận này đã có King chết chưa - chặn xử lý thắng/thua 2 lần cho cùng 1 trận.</summary>
    private bool matchOver;

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

    /// <summary>Tiền mỗi giây của AI, đã cộng thưởng nâng cấp thành phe đỏ và hệ số thu nhập của level.</summary>
    public int AIMoneyPerSecond
    {
        get
        {
            float castleMultiplier = (RedCastle != null) ? RedCastle.IncomeMultiplier : 1f;
            float levelMultiplier = (ActiveLevelSettings != null) ? Mathf.Max(0f, ActiveLevelSettings.aiIncomeMultiplier) : 1f;

            return Mathf.RoundToInt(moneyPersecond * castleMultiplier * levelMultiplier);
        }
    }

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

    /// <summary>Người chơi đã đạt giới hạn số lính chưa (UI dùng để làm xám nút chọn lính).</summary>
    public bool IsPlayerAtUnitLimit => CountAll(GetPlayerUnitCounts()) >= maxUnitsPerSide;

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
    /// </summary>
    public bool IsLocalAtUnitLimit => CountLiveUnits(LocalPlayerIsBlue) >= maxUnitsPerSide;

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

    /// <summary>Ra 1 lính xanh. Trả về true nếu thực sự ra được lính (đủ tiền + hết cooldown + chưa đầy).</summary>
    private bool spawnSoldier(GameObject spawn)
    {
        if (IsPlayerAtUnitLimit)
        {
            Debug.Log($"Đã đủ {maxUnitsPerSide} lính trên sân, phải chờ lính chết mới ra thêm được!");
            return false;
        }

        if (!CanChoose(true)) return false;

        Slime slimePrefab = spawn.GetComponent<Slime>();
        if (slimePrefab == null)
        {
            Debug.LogError($"{spawn.name} không có component Slime!");
            return false;
        }

        if (SpawnSlime == null)
        {
            Debug.LogError("GameManager: chưa gán SpawnSlime (điểm ra lính xanh) trong Inspector!");
            return false;
        }

        int price = slimePrefab.price;
        if (!HasEnoughMoney(true, price)) return false;

        ChangeMoney(true, -price);

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

        StartCoroutine(ChooseCooldown(true));   // bắt đầu thời gian chờ ngay khi vừa ra lính

        return true;
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