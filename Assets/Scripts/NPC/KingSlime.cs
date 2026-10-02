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
    /// King chính là bản thân "thành" nên mặc định KHÔNG được cộng máu khi nâng cấp thành.
    /// Bật <see cref="CastleUpgrade.applyToKing"/> nếu muốn thành trâu hơn theo level.
    /// </summary>
    protected override bool ReceivesCastleUpgrade
    {
        get
        {
            return GameManager.Instance != null
                && GameManager.Instance.Castle != null
                && GameManager.Instance.Castle.applyToKing;
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
    /// Tiền King nhả ra mỗi lượt. Phe xanh (người chơi) được cộng thưởng theo level thành,
    /// phe đỏ (AI) luôn giữ nguyên <see cref="resourcePerTick"/>.
    /// </summary>
    private int CurrentResourcePerTick
    {
        get
        {
            if (!blueSide) return resourcePerTick;

            CastleUpgrade castle = (GameManager.Instance != null) ? GameManager.Instance.Castle : null;
            float multiplier = (castle != null) ? castle.IncomeMultiplier : 1f;

            return Mathf.RoundToInt(resourcePerTick * multiplier);
        }
    }

    private IEnumerator GenerateResourceRoutine()
    {
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

                    if (blueSide)
                    {
                        GameManager.Instance.Addmoney(income);
                        SpawnFloatingText(income);
                    }
                    else
                    {
                        GameManager.Instance.AI.Money += income;
                        GameManager.Instance.AIMoney = GameManager.Instance.AI.Money;
                    }
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