using UnityEngine;

/// <summary>
/// 6 loại lính của phe AI (đỏ).
/// THỨ TỰ NÀY LÀ HỢP ĐỒNG CHUNG: dataset.csv, model ONNX, <see cref="AIPolicy"/> và
/// <see cref="AI"/> đều dùng đúng thứ tự này. Đổi thứ tự là phải sinh lại dataset + train lại.
/// </summary>
public enum AIUnitType
{
    Sword = 0,
    Archer = 1,
    Slinger = 2,
    SpearMan = 3,
    BaseSlime = 4,
    Catapult = 5
}

/// <summary>Một thế trận mà AI nhìn thấy để ra quyết định. Dùng chung cho game và cho việc sinh dataset.</summary>
public struct AIState
{
    /// <summary>Số lính HIỆN CÓ của từng loại, theo thứ tự <see cref="AIUnitType"/>.</summary>
    public int[] playerCounts;
    public int[] ownCounts;

    /// <summary>Đơn giá từng loại lính (theo thứ tự <see cref="AIUnitType"/>).</summary>
    public int[] prices;

    /// <summary>Tiền AI đang có.</summary>
    public int money;

    public AIState(int[] playerCounts, int[] ownCounts, int[] prices, int money)
    {
        this.playerCounts = playerCounts;
        this.ownCounts = ownCounts;
        this.prices = prices;
        this.money = money;
    }
}

/// <summary>
/// BỘ NÃO LUẬT của AI (rule-based). Đây là "nguồn chân lý" về cách AI nên chơi:
///
///  - Chạy trực tiếp trong game khi <see cref="AI"/> ở chế độ RuleBased (mặc định).
///  - Là THẦY để sinh dataset.csv (xem Assets/Editor/AIDatasetGenerator.cs) rồi train model ONNX.
///
/// Nguyên tắc chơi, theo tinh thần Age of War:
///   1. Không bao giờ để tiền nằm không: đủ tiền mua là mua (trừ khi chưa đủ loại rẻ nhất).
///   2. Khắc chế đội hình địch: địch nhiều melee -> mua tầm xa; địch nhiều tầm xa -> mua melee nhanh
///      hoặc Catapult để bắn xa hơn.
///   3. Giữ đội hình cân bằng melee/tầm xa (xem <see cref="DesiredMeleeShare"/>), không dồn hết
///      vào một loại.
///   4. Đầu trận ưu tiên lính rẻ để nhanh có quân; về sau ưu tiên lính mạnh.
///   5. Khi tiền đã dư dả thì nghiêng hẳn về lính đắt tiền nhất để không phí tiền.
///
/// Hàm <see cref="Decide"/> là hàm THUẦN TÚY (pure): cùng thế trận luôn cho cùng quyết định,
/// nhờ vậy dataset sinh ra có nhãn nhất quán và model học được.
/// </summary>
public static class AIPolicy
{
    /// <summary>Số loại lính.</summary>
    public const int UnitCount = 6;

    /// <summary>Không mua gì cả.</summary>
    public const int Waiting = -1;

    /// <summary>Tỉ lệ melee mong muốn trong đội hình (0.6 = 60% melee, 40% tầm xa).</summary>
    private const float DesiredMeleeShare = 0.6f;

    /// <summary>Số lính tối đa còn tính là "đầu trận" (ưu tiên lính rẻ).</summary>
    private const float EarlyGameArmySize = 4f;

    /// <summary>
    /// Điểm nền theo sức mạnh/giá của từng loại lính (thứ tự <see cref="AIUnitType"/>).
    /// Cố ý để RẤT NHỎ: đây chỉ là tiêu chí phá hòa, không được để nó lấn át phần khắc chế.
    /// </summary>
    private static readonly float[] BaseValue =
    {
        0.6f,   // Sword
        0.7f,   // Archer
        0.3f,   // Slinger
        0.9f,   // SpearMan
        0.2f,   // BaseSlime
        1.0f    // Catapult
    };

    /// <summary>
    /// Khắc chế: COUNTER[u, 0] = điểm khi ĐỊCH nhiều melee, COUNTER[u, 1] = điểm khi ĐỊCH nhiều tầm xa.
    /// Cột 0: địch là melee thì tầm xa (Archer/Catapult) và giáo dài (SpearMan) rất tốt.
    /// Cột 1: địch là tầm xa thì melee lao vào nhanh (Sword) tốt nhất;
    ///        Catapult chỉ 0.45 vì tuy bắn xa hơn nhưng chậm và máu giấy, dễ bị áp sát.
    /// </summary>
    private static readonly float[,] Counter =
    {
        //  địch melee, địch tầm xa
        {   0.25f,      1.00f },   // Sword    - lao vào nhanh, sát thương cao, rẻ
        {   0.95f,      0.30f },   // Archer   - bắn tỉa melee đang bận cận chiến
        {   0.50f,      0.20f },   // Slinger  - tầm ngắn, chỉ hơn BaseSlime
        {   0.90f,      0.35f },   // SpearMan - giáo dài khắc Sword, trâu
        {   0.20f,      0.65f },   // BaseSlime- lấy thịt đè người, chỉ tốt khi địch mỏng
        {   0.85f,      0.45f },   // Catapult - tầm 9, nhưng chậm và dễ vỡ
    };

    /// <summary>Trọng số GỐC của phần khắc chế (tiêu chí quan trọng nhất). Bị nhân bởi độ khắc chế của level.</summary>
    private const float CounterWeight = 3.0f;

    /// <summary>Trọng số của phần giữ đội hình cân bằng melee / tầm xa.</summary>
    private const float RoleWeight = 1.2f;

    /// <summary>Trọng số của phần ưu tiên lính rẻ đầu trận.</summary>
    private const float CheapBiasWeight = 1.6f;

    /// <summary>
    /// Mức phạt khi đã có nhiều lính cùng loại (lợi ích biên giảm dần) — theo TỪNG loại.
    /// BaseSlime bị phạt nặng nhất vì đây là lính rẻ nhất (10 tiền) nên nếu phạt nhẹ là AI spam
    /// slime cả trận, nhìn rất máy móc.
    /// </summary>
    private static readonly float[] RedundancyPenalty =
    {
        0.55f,   // Sword
        0.55f,   // Archer
        0.80f,   // Slinger
        0.45f,   // SpearMan
        1.20f,   // BaseSlime
        0.45f    // Catapult
    };

    /// <summary>
    /// Trọng số phạt theo TỈ LỆ một loại trong đội hình. Đây là "chốt chống spam": đội mà dồn gần hết
    /// vào một loại thì loại đó bị trừ điểm rất mạnh, ép AI mua loại khác cho giống người chơi.
    /// </summary>
    private const float DiversityWeight = 2.5f;

    /// <summary>
    /// Số lính TỐI ĐA mỗi loại trong đội. Đây là "chốt cứng" chống spam: dù một loại là thứ duy nhất
    /// mua được, AI cũng không dồn quá mức này vào nó mà CHỜ tích tiền mua loại khác — đúng cách một
    /// người chơi thật giữ đội hình đa dạng. Nhờ vậy AI thôi spam slime.
    /// </summary>
    private const int MaxUnitsPerType = 3;

    /// <summary>Lính này có phải tuyến trước (melee) không.</summary>
    public static bool IsMelee(int unit)
    {
        return unit == (int)AIUnitType.Sword
            || unit == (int)AIUnitType.SpearMan
            || unit == (int)AIUnitType.BaseSlime;
    }

    /// <summary>Giá lớn hơn mức này coi như cấu hình lỗi (ví dụ prefab chưa gán) và bị bỏ qua.</summary>
    private const int MaxSanePrice = 100000;

    /// <summary>
    /// Quyết định mua gì với độ khắc chế mặc định (1 = khắc chế tối đa). Giữ để tương thích với
    /// <c>AIDatasetGenerator</c>; trong game nên gọi bản có độ khắc chế theo level.
    /// </summary>
    public static int Decide(AIState state)
    {
        return Decide(state, 1f);
    }

    /// <summary>
    /// Quyết định mua gì. Trả về <see cref="Waiting"/> hoặc chỉ số trong <see cref="AIUnitType"/>.
    ///
    /// <paramref name="counterAwareness"/> nằm trong [0,1]: mức AI đọc bài đối thủ.
    ///   0 = bỏ qua đội hình địch, chỉ giữ đội hình cân bằng (vẫn chọn lính hợp lý, chỉ là không khôn).
    ///   1 = khắc chế tối đa. Đây là nút chỉnh độ khó mà KHÔNG làm AI chọn lính vô lý.
    /// </summary>
    public static int Decide(AIState state, float counterAwareness)
    {
        int[] price = state.prices;
        if (price == null || price.Length != UnitCount) return Waiting;
        if (state.playerCounts == null || state.ownCounts == null) return Waiting;

        // Chỉ xét những loại có giá hợp lệ, tránh tràn số khi prefab bị thiếu (GetPrice trả int.MaxValue).
        int cheapest = int.MaxValue;
        int mostExpensive = 0;
        for (int u = 0; u < UnitCount; u++)
        {
            if (!IsSanePrice(price[u])) continue;

            if (price[u] < cheapest) cheapest = price[u];
            if (price[u] > mostExpensive) mostExpensive = price[u];
        }

        // (1) Chưa đủ tiền mua loại rẻ nhất thì đành chờ.
        if (cheapest == int.MaxValue || state.money < cheapest || mostExpensive <= 0) return Waiting;

        // (2) Thế trận của địch: nghiêng melee hay tầm xa?
        float enemyMelee = CountMelee(state.playerCounts);
        float enemyRanged = CountRanged(state.playerCounts);
        float enemyTotal = enemyMelee + enemyRanged;

        float enemyMeleeWeight = 0.5f;
        float enemyRangedWeight = 0.5f;
        if (enemyTotal > 0f)
        {
            enemyMeleeWeight = enemyMelee / enemyTotal;
            enemyRangedWeight = enemyRanged / enemyTotal;
        }

        // (3) Vai trò đội mình: đang thiếu melee hay thiếu tầm xa?
        // Chưa có lính nào thì coi như share = 0 để AI mở màn bằng tuyến trước (melee).
        float myMelee = CountMelee(state.ownCounts);
        float myRanged = CountRanged(state.ownCounts);
        float myTotal = myMelee + myRanged;

        float myMeleeShare = (myTotal > 0f) ? (myMelee / myTotal) : 0f;
        float meleeDeficit = DesiredMeleeShare - myMeleeShare;   // > 0 => đang thiếu melee

        // (4) Đầu trận thích lính rẻ, về sau thích lính mạnh.
        float cheapBias = Mathf.Clamp01(1f - (myTotal / EarlyGameArmySize));

        // (5) Tiền dư quá nhiều thì phải tiêu vào lính đắt cho đỡ phí.
        bool moneyIsPilingUp = state.money >= 2 * mostExpensive;

        // Độ khắc chế của level nhân vào phần "đọc bài đối thủ" (nút chỉnh độ khó).
        float counterWeight = CounterWeight * Mathf.Clamp01(counterAwareness);

        float bestScore = float.NegativeInfinity;
        int best = Waiting;

        for (int u = 0; u < UnitCount; u++)
        {
            if (!IsSanePrice(price[u])) continue;          // loại lính cấu hình lỗi
            if (state.money < price[u]) continue;          // không đủ tiền loại này
            if (state.ownCounts[u] >= MaxUnitsPerType) continue;   // đã đủ nhiều loại này -> thôi

            float priceRatio = (float)price[u] / mostExpensive;

            float score = BaseValue[u];

            // Khắc chế đội hình địch (tiêu chí quan trọng nhất, co lại theo độ khó của level).
            score += counterWeight * ((Counter[u, 0] * enemyMeleeWeight) + (Counter[u, 1] * enemyRangedWeight));

            // Cân bằng melee / tầm xa.
            score += RoleWeight * (IsMelee(u) ? meleeDeficit : -meleeDeficit);

            // Chống spam: phạt theo số lượng đã có (lợi ích biên giảm dần, nặng với lính rẻ dễ spam).
            score -= RedundancyPenalty[u] * state.ownCounts[u];

            // Chống spam: phạt theo TỈ LỆ loại này trong đội hình. Đội càng dồn vào 1 loại thì loại đó
            // càng mất điểm, ép AI mua loại khác -> đội hình đa dạng, giống người chơi.
            float projectedShare = (state.ownCounts[u] + 1f) / (myTotal + 1f);
            score -= DiversityWeight * projectedShare;

            // Đầu trận đẩy lính rẻ lên.
            score += CheapBiasWeight * cheapBias * (1f - priceRatio);

            // Tiền dư thì đẩy lính đắt lên.
            if (moneyIsPilingUp) score += 1.0f * priceRatio;

            if (score > bestScore)
            {
                bestScore = score;
                best = u;
            }
        }

        return best;
    }

    /// <summary>Giá hợp lệ để đưa vào tính toán (dương và không quá lớn).</summary>
    public static bool IsSanePrice(int price)
    {
        return price > 0 && price <= MaxSanePrice;
    }

    /// <summary>Tổng số lính melee đang có (tuyến trước).</summary>
    public static float CountMelee(int[] counts)
    {
        if (counts == null) return 0f;
        return counts[(int)AIUnitType.Sword]
             + counts[(int)AIUnitType.SpearMan]
             + counts[(int)AIUnitType.BaseSlime];
    }

    /// <summary>Tổng số lính tầm xa đang có (tuyến sau).</summary>
    public static float CountRanged(int[] counts)
    {
        if (counts == null) return 0f;
        return counts[(int)AIUnitType.Archer]
             + counts[(int)AIUnitType.Slinger]
             + counts[(int)AIUnitType.Catapult];
    }
}
