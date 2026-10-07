using System.Collections;
using UnityEngine;
using DG.Tweening;

public class KingSlime : Slime
{
    [Header("King Slime Resource Settings")]
    public int resourcePerTick = 5;

    [Header("Random Time Interval (Seconds)")]
    public float minTimeInterval = 8.0f;
    public float maxTimeInterval = 10.0f;

    [Header("Under Attack Settings")]
    [Tooltip("Thời gian King ở trạng thái bị tấn công sau đòn đánh cuối cùng")]
    public float underAttackDuration = 2.0f;

    [Header("Floating Text Effect")]
    public GameObject floatingTextPrefab;

    private int maxHealth;
    private Animator kingAnimator;
    private bool isUnderAttack = false;
    private float attackTimer = 0f;

    private void Reset()
    {
        health = 500;
        damage = 0;
        price = 0;
        velocity = 0f;
    }

    protected override void Awake()
    {
        base.Awake();
        kingAnimator = GetComponent<Animator>();
    }

    /// <summary>
    /// King là vật thể ĐẶT SẴN trong scene ở CẢ HAI máy (không do host sinh ra) nên KHÔNG tắt
    /// component ở client: client vẫn cần chạy animation bị đánh/chết và đọc máu host đồng bộ về.
    /// Chỉ phần SINH TIỀN mới bị chặn ở client - xem <see cref="Start"/>.
    /// </summary>
    protected override bool ReplicatedFromHost
    {
        get { return false; }
    }

    /// <summary>
    /// King (thành) KHÔNG phát hiệu ứng "lính bị hạ gục" khi chết - King đã có hiệu ứng riêng cho
    /// thắng/thua ở GameManager (xem <see cref="VfxManager.PlayWinPoof"/> / <see cref="VfxManager.PlayLoseLeaves"/>).
    /// </summary>
    protected override bool PlaysDeathVfx
    {
        get { return false; }
    }

    /// <summary>
    /// King chính là bản thân "thành" nên mặc định KHÔNG được cộng máu khi nâng cấp thành.
    /// Bật <see cref="CastleUpgrade.applyToKing"/> nếu muốn thành trâu hơn theo level.
    /// </summary>
    protected override bool ReceivesCastleUpgrade
    {
        get
        {
            if (GameManager.Instance == null) return false;

            // King kiểm tra applyToKing của thành ĐÚNG phe mình (xanh = người chơi, đỏ = AI).
            CastleUpgrade castle = GameManager.Instance.CastleFor(blueSide);
            return castle != null && castle.applyToKing;
        }
    }

    protected override void Start()
    {
        if (health <= 0) health = 500;

        // Cộng máu theo level thành (mặc định tắt - xem ReceivesCastleUpgrade).
        ApplyCastleUpgrade();
        maxHealth = health;

        isMoving = false;
        characterName = blueSide ? "BlueKing" : "RedKing";

        StartCoroutine(GenerateResourceRoutine());
        UpdateHealthUI();

        // Client cần biết máu TỐI ĐA của King (để vẽ thanh máu đúng tỉ lệ) nên host công bố ngay
        // khi trận vừa bắt đầu.
        PublishHealthToClients(false);
    }

    protected override void Update()
    {
        base.Update();

        // Xử lý logic đếm ngược thời gian bị tấn công
        if (isUnderAttack)
        {
            attackTimer -= Time.deltaTime;

            // Nếu hết thời gian mà không bị đánh thêm -> quay về bình thường
            if (attackTimer <= 0f)
            {
                isUnderAttack = false;
                if (kingAnimator != null)
                {
                    kingAnimator.SetBool("isUnderAttack", false);
                }
            }
        }
    }

    /// <summary>
    /// King đứng yên tại chỗ (không có Destination) nên không được bị đẩy lùi khi lính địch áp sát:
    /// chỉ lính mới tự lùi ra để giữ khoảng hở với địch.
    /// </summary>
    protected override bool CanBePushedBackFromEnemy
    {
        get { return false; }
    }

    /// <summary>
    /// King (thành) đứng yên suốt trận, đã có animator riêng và còn bị DOPunchScale khi trúng đòn
    /// nên TẮT hiệu ứng "thở" dùng chung của lính (tránh 2 tween cùng giành transform).
    /// </summary>
    protected override bool CanPlayIdleAnimation
    {
        get { return false; }
    }

    /// <summary>
    /// Tiền King nhả ra mỗi lượt, được cộng thưởng theo level thành của ĐÚNG phe mình
    /// (xanh = thành người chơi, đỏ = thành AI).
    /// </summary>
    private int CurrentResourcePerTick
    {
        get
        {
            CastleUpgrade castle = (GameManager.Instance != null) ? GameManager.Instance.CastleFor(blueSide) : null;
            float multiplier = (castle != null) ? castle.IncomeMultiplier : 1f;

            return Mathf.RoundToInt(resourcePerTick * multiplier);
        }
    }

    /// <summary>
    /// King nhả tiền theo nhịp ngẫu nhiên. Chỉ HOST (hoặc chế độ chơi đơn) mới sinh tiền;
    /// ở client vòng lặp thoát ngay vì tiền do host chốt rồi đồng bộ sang.
    /// </summary>
    private IEnumerator GenerateResourceRoutine()
    {
        if (!NetworkRole.Simulates) yield break;

        while (health > 0)
        {
            float waitTime = Random.Range(minTimeInterval, maxTimeInterval);
            yield return new WaitForSeconds(waitTime);

            if (health <= 0) break;

            // CHỈ CỘNG TIỀN NẾU KING KHÔNG BỊ TẤN CÔNG
            if (!isUnderAttack)
            {
                if (GameManager.Instance != null)
                {
                    int income = CurrentResourcePerTick;

                    // Đi qua AddKingIncome (thay vì ghi thẳng vào AI.Money) để phe ĐỎ cũng được host
                    // công bố sang client y như phe XANH - xem chú thích ở GameManager.AddKingIncome.
                    GameManager.Instance.AddKingIncome(blueSide, income);

                    // Hiệu ứng "+N" bay lên giữ nguyên chỉ ở phe XANH như trước, để không đổi hình ảnh
                    // của chế độ chơi đơn (nơi King đỏ thuộc về AI).
                    if (blueSide) SpawnFloatingText(income);
                }
            }
            else
            {
                Debug.Log($"{gameObject.name} đang bị tấn công, bỏ qua lượt cộng tiền này!");
            }
        }
    }

    private void SpawnFloatingText(int amount)
    {
        if (floatingTextPrefab != null)
        {
            Vector3 spawnWorldPos = transform.position + Vector3.up * 1.5f;

            GameObject textObj = Instantiate(floatingTextPrefab);
            FloatingText ft = textObj.GetComponent<FloatingText>();
            if (ft != null)
            {
                ft.Init(amount, spawnWorldPos);
            }
        }
    }

    public override void TakeDamage(int dmg)
    {
        base.TakeDamage(dmg);
        transform.DOComplete();
        transform.DOPunchScale(new Vector3(0.1f, -0.1f, 0f), 0.15f, 1, 0.5f);

        UpdateHealthUI();

        // KÍCH HOẠT TRẠNG THÁI BỊ TẤN CÔNG
        isUnderAttack = true;
        attackTimer = underAttackDuration; // Reset lại thời gian đếm ngược

        if (kingAnimator != null)
        {
            kingAnimator.SetBool("isUnderAttack", true);
        }

        // MultiPlayer: host công bố máu mới + hiệu ứng trúng đòn cho client (chơi đơn: no-op).
        PublishHealthToClients(true);
    }

    /// <summary>
    /// Đặt máu cho King theo ĐỘ KHÓ của level đang chơi (GameManager gọi lúc bắt đầu trận).
    /// Ghi cả máu hiện tại lẫn máu tối đa nên gọi trước hay sau <see cref="Start"/> đều đúng,
    /// sau đó cập nhật lại thanh máu trên UI.
    /// </summary>
    public void SetLevelHealth(int healthValue)
    {
        if (healthValue <= 0) return;

        health = healthValue;
        maxHealth = healthValue;
        UpdateHealthUI();

        // MultiPlayer: máu tối đa đổi theo level -> công bố lại cho client (chơi đơn: no-op).
        PublishHealthToClients(false);
    }

    /// <summary>
    /// HOST công bố máu King cho client (chơi đơn / client: không làm gì).
    /// </summary>
    private void PublishHealthToClients(bool playHit)
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.PublishKingHealth(blueSide, health, maxHealth, playHit);
    }

    /// <summary>
    /// Áp máu do HOST đồng bộ sang (client). Client CHỈ HIỂN THỊ: không tự trừ máu, không tự quyết
    /// định thắng thua - mọi thứ đã được host chốt rồi.
    /// </summary>
    /// <param name="playHit">Chạy hiệu ứng trúng đòn (nảy + nhấp nháy animator) như vừa bị đánh.</param>
    public void ApplySyncedHealth(int healthValue, int maxHealthValue, bool playHit)
    {
        bool wasAlive = health > 0;

        health = healthValue;
        maxHealth = maxHealthValue;

        if (playHit && health > 0)
        {
            transform.DOComplete();
            transform.DOPunchScale(new Vector3(0.1f, -0.1f, 0f), 0.15f, 1, 0.5f);

            isUnderAttack = true;
            attackTimer = underAttackDuration;
            if (kingAnimator != null) kingAnimator.SetBool("isUnderAttack", true);
        }

        UpdateHealthUI();

        // Máu về 0 -> chạy animation chết. Die() gọi Winner() nhưng ở client Winner() tự bỏ qua
        // (kết quả trận do host quyết định và gửi sang qua NetworkGame).
        if (wasAlive && health <= 0) Die();
    }

    private void UpdateHealthUI()
    {
        if (FortressClash.Core.UIManager.Instance != null)
        {
            FortressClash.Core.UIManager.Instance.UpdateKingHealth(blueSide, health, maxHealth);
        }
    }

    protected override void Die()
    {
        if (GameManager.Instance != null)
        {
            if (blueSide)
            {
                GameManager.Instance.Winner("red");
            }
            else
            {
                GameManager.Instance.Winner("blue");
            }
        }

        Sequence dieSeq = DOTween.Sequence();
        dieSeq.Append(transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
        dieSeq.OnComplete(() => {
            base.Die();
        });
    }
}