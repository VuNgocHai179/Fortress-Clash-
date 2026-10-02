using System;
using System.Collections;
using FortressClash.Core;
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

    [SerializeField] private int moneyPersecond = 10;
    [SerializeField] private float chooseDelay = 5f;
    private bool canChoose = true;

    public PlayerHas player;
    public AIHas AI;

    [Header("Castle Upgrade (nâng cấp thành)")]
    [Tooltip("Component quản lý nâng cấp thành. Để trống sẽ tự tìm trên cùng GameObject với GameManager.")]
    [SerializeField] private CastleUpgrade castleUpgrade;

    /// <summary>Hệ thống nâng cấp thành của phe người chơi (tự tìm nếu chưa gán trong Inspector).</summary>
    public CastleUpgrade Castle
    {
        get
        {
            if (castleUpgrade == null) castleUpgrade = GetComponent<CastleUpgrade>();
            return castleUpgrade;
        }
    }

    /// <summary>Tiền mỗi giây của NGƯỜI CHƠI, đã cộng thưởng từ nâng cấp thành.</summary>
    public int MoneyPerSecond
    {
        get
        {
            float multiplier = (Castle != null) ? Castle.IncomeMultiplier : 1f;
            return Mathf.RoundToInt(moneyPersecond * multiplier);
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

    /// <summary>AI đã đạt giới hạn số lính chưa.</summary>
    public bool IsAIAtUnitLimit => CountAll(GetOwnUnitCounts()) >= maxUnitsPerSide;

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
    /// </summary>
    private int GetAICatchUpBonus()
    {
        if (!aiCatchUpEnabled) return 0;

        int missing = CountAll(GetPlayerUnitCounts()) - CountAll(GetOwnUnitCounts());
        if (missing <= 0) return 0;

        return Mathf.Min(aiCatchUpMaxBonus, Mathf.RoundToInt(missing * aiCatchUpMoneyPerMissingUnit));
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

    void Start()
    {
        UpdateMoneyUI();
        setMoneyforAI();
        StartCoroutine(AddMoneyPerSecond());
    }

    private IEnumerator AddMoneyPerSecond()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            // Đếm lại quân thật trên sân trước khi tính tiền, để mọi thứ (giới hạn lính, tiền
            // catch-up, input cho model AI) luôn khớp với thực tế.
            RecountUnitsOnField();

            // Phe người chơi được cộng thưởng theo level thành; AI giữ nguyên thu nhập gốc
            // (cộng thêm tiền catch-up nếu đang ít lính hơn người chơi).
            Money += MoneyPerSecond;
            AI.Money += moneyPersecond + GetAICatchUpBonus();
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
            UIManager.Instance.UpdateMoneyDisplay(Money);
        }
    }

    private bool isEnoughMoney(int money)
    {
        if (money <= Money) return true;
        Debug.Log("Không đủ tiền!");
        return false;
    }

    private void spentMoney(int money)
    {
        Money -= money;
        UpdateMoneyUI();
    }

    private bool CanChoose()
    {
        if (!canChoose)
        {
            Debug.Log("Đang chờ, chưa thể chọn lính!");
            return false;
        }
        return true;
    }

    /// <summary>Người chơi đã hết thời gian chờ chọn lính chưa (UI dùng để bật/tắt nút).</summary>
    public bool IsChooseReady => canChoose;

    private IEnumerator ChooseCooldown()
    {
        canChoose = false;
        UpdateMoneyUI();          // làm xám các nút chọn lính ngay khi vừa ra lính

        yield return new WaitForSeconds(chooseDelay);

        canChoose = true;
        UpdateMoneyUI();          // sáng lại nút khi hết thời gian chờ
    }

    // ==========================================
    // BLUE CHOOSEN
    //
    // Chỉ cộng số lượng + bắt đầu cooldown KHI THỰC SỰ ra được lính. Trước đây các hàm này cộng
    // số lượng và chạy lại cooldown kể cả khi bấm lúc đang chờ / không đủ tiền, làm số lượng lính
    // lệch với thực tế trên sân và cooldown bị reset liên tục nên không bao giờ hết.
    // ==========================================
    public virtual void SwordChoosen()
    {
        if (!spawnSoldier(Sword)) return;

        player.SwordNumber++;
        StartCoroutine(ChooseCooldown());
    }

    public virtual void ArcherChoosen()
    {
        if (!spawnSoldier(Archer)) return;

        player.ArcherNumber++;
        StartCoroutine(ChooseCooldown());
    }

    public virtual void SpearChoosen()
    {
        if (!spawnSoldier(SpearMan)) return;

        player.SpearManNumber++;
        StartCoroutine(ChooseCooldown());
    }

    public virtual void SlingshotChoosen()
    {
        if (!spawnSoldier(Slinger)) return;

        player.SlingerNumber++;
        StartCoroutine(ChooseCooldown());
    }

    public virtual void BaseSlimeChoosen()
    {
        if (!spawnSoldier(BaseSlime)) return;

        player.BaseSlimeNumber++;
        StartCoroutine(ChooseCooldown());
    }

    public virtual void CatapultChoosen()
    {
        if (!spawnSoldier(Catapult)) return;

        player.CatapultNumber++;
        StartCoroutine(ChooseCooldown());
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
        Money += money;
        UpdateMoneyUI();
    }

    /// <summary>
    /// Nút "Nâng cấp thành" trên UI gọi hàm này. Xem <see cref="CastleUpgrade"/> để biết chi tiết
    /// chi phí và phần thưởng từng level.
    /// </summary>
    public bool UpgradeCastle()
    {
        if (Castle == null)
        {
            Debug.LogError("GameManager: chưa gắn component CastleUpgrade vào GameObject này!");
            return false;
        }

        return Castle.TryUpgrade();
    }

    public void Winner(string winner)
    {
        if (winner == "blue") Debug.Log("Blue win");
        else if (winner == "red") Debug.Log("Red win");
    }

    /// <summary>Ra 1 lính xanh. Trả về true nếu thực sự ra được lính (đủ tiền + hết cooldown + chưa đầy).</summary>
    private bool spawnSoldier(GameObject spawn)
    {
        if (IsPlayerAtUnitLimit)
        {
            Debug.Log($"Đã đủ {maxUnitsPerSide} lính trên sân, phải chờ lính chết mới ra thêm được!");
            return false;
        }

        if (!CanChoose()) return false;

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
        if (!isEnoughMoney(price)) return false;

        spentMoney(price);

        Vector3 spawnPos = SpawnSlime.position;

        GameObject spawnedObject = Instantiate(spawn, spawnPos, Quaternion.identity);

        Slime spawnedSlime = spawnedObject.GetComponent<Slime>();
        if (spawnedSlime != null)
        {
            spawnedSlime.blueSide = true;
            spawnedSlime.SpawnPosition = spawnPos;
            spawnedSlime.Destination = GetEnemyKingDestination(true, spawnPos);
        }

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

        int price = slimePrefab.price;
        if (AI.Money < price)
        {
            Debug.Log($"AI không đủ tiền! Cần {price}, đang có {AI.Money}");
            return false;
        }

        AI.Money -= price;
        AIMoney = AI.Money;

        InstantiateRedPrefab(spawn);
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