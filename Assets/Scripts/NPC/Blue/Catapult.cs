using UnityEngine;
using DG.Tweening;

public class Catapult : Slime
{
    [Header("Catapult Vehicle Settings")]
    [Tooltip("Tầm bắn (world unit). Xa nhất trong 3 lính tầm xa: đủ để Catapult đứng sau 2 lính cận chiến " +
             "trong hàng đợi vẫn nã đá tới địch ở tuyến trước (cần > 18.2 để vượt qua 2 đồng minh, " +
             "> 11.9 để vượt qua 1).")]
    public float catapultAttackRange = 20f;
    public float catapultAllyCheckDistance = 1.8f;
    public float catapultAllyCheckRadius = 0.4f;
    public float moveSpeed = 3.5f;

    [Header("Projectile")]
    public Bullet bulletPrefab;
    public Vector2 bulletSpawnOffset = new Vector2(0f, 4.2f);
    public float fallbackFireInterval = 1.5f;
    public float minFireInterval = 0.2f;

    /// <summary>
    /// Catapult nã đá nên là lính TẦM XA: lính cận chiến cùng phe được phép đi xuyên qua để tới
    /// chiến tuyến, và xe tự né ra sau hàng cận chiến (xem <see cref="Slime.AllyBlocksMe"/>).
    /// </summary>
    protected override bool IsRangedUnit
    {
        get { return true; }
    }

    protected bool isAttacking = false;
    private Animator animator;
    private float timeSinceLastShot = Mathf.Infinity;

    private void Reset()
    {
        catapultAttackRange = 20f;
        health = 70;
        damage = 30;
        price = 60;
        velocity = 3.5f;
        allyCheckDistance = 0.9f;
        allyCheckRadius = 0.3f;
    }

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
    }

    protected override void Start()
    {
        if (catapultAttackRange <= 0f) catapultAttackRange = 20f;
        if (health <= 0) health = 70;
        if (damage <= 0) damage = 30;
        if (price <= 0) price = 60;

        if (blueSide && (Destination == Vector3.zero || Destination.x <= transform.position.x))
            Destination = transform.position + Vector3.right * 20f;
        else if (!blueSide && (Destination == Vector3.zero || Destination.x >= transform.position.x))
            Destination = transform.position + Vector3.left * 20f;

        characterName = "BlueCatapult";
        attackRange = catapultAttackRange;

        AlignToGround();   // đứng lên mặt collider nền (Grass) trước khi chạy hiệu ứng spawn
        PlayVehicleSpawnEffect();

        ApplyCastleUpgrade();   // cộng chỉ số từ nâng cấp thành (phải gọi SAU khi đã gán chỉ số)
    }

    protected override void Update()
    {
        if (isAttacking)
        {
            // Còn địch trong tầm (kể cả địch KHÁC vừa lọt vào tầm) là nã tiếp ngay, không ngắt đòn:
            // xe bắn đá đứng sau hàng đợi vẫn đánh được mà không phải "đợi tới lượt".
            if (!EnsureTargetStillValid())
            {
                StopAttack();
                return;
            }

            timeSinceLastShot += Time.deltaTime;
            if (timeSinceLastShot >= fallbackFireInterval)
            {
                FireBullet();
            }
            return;
        }

        FindTarget();

        if (target != null)
        {
            // Cho xe đang chạy dở dừng lại đúng chỗ (không Kill giữa chừng) rồi mới nã đá.
            isMoving = false;
            StartAttack();
            return;
        }

        if (CatapultHasAllyInFront())
        {
            StopMovingForAlly();
            return;
        }

        if (!isMoving)
        {
            StartMovingAsVehicle();
        }
    }

    protected override void StartMoving()
    {
        StartMovingAsVehicle();
    }

    private void StartMovingAsVehicle()
    {
        SnapToGround();   // luôn chạy trên mặt nền

        float distanceToDest = Mathf.Abs(transform.position.x - Destination.x);
        if (distanceToDest <= arriveDistance) return;

        float direction = blueSide ? 1f : -1f;

        // Giới hạn quãng chạy theo chỗ trống phía trước để xe không chạy đè lên đồng minh.
        float step = Mathf.Min(5f, GetClearDistanceAhead());
        if (step <= 0.1f)
        {
            isMoving = false;
            return;
        }

        isMoving = true;

        AudioManager.PlayMove();   // tiếng di chuyển của xe nã đá (chạy bằng StartMovingAsVehicle)

        float targetX = transform.position.x + (step * direction);
        if (blueSide && targetX > Destination.x) targetX = Destination.x;
        if (!blueSide && targetX < Destination.x) targetX = Destination.x;

        float travelDist = Mathf.Abs(targetX - transform.position.x);
        float duration = travelDist / moveSpeed;

        Sequence vehicleSeq = DOTween.Sequence();
        vehicleSeq.Append(transform.DOMoveX(targetX, duration).SetEase(Ease.Linear));

        Sequence bobbingSeq = DOTween.Sequence();
        bobbingSeq.Append(transform.DOMoveY(transform.position.y + 0.08f, 0.15f).SetEase(Ease.OutSine));
        bobbingSeq.Append(transform.DOMoveY(transform.position.y, 0.15f).SetEase(Ease.InSine));

        vehicleSeq.Join(bobbingSeq);
        vehicleSeq.OnComplete(() => {
            SnapToGround();

            float currentDist = Mathf.Abs(transform.position.x - Destination.x);
            if (currentDist > arriveDistance && isMoving)
            {
                StartMovingAsVehicle();
            }
            else
            {
                isMoving = false;
            }
        });

        moveTween = vehicleSeq;
    }

    private void PlayVehicleSpawnEffect()
    {
        // Hiệu ứng VFX lúc xe vừa được sinh ra (CFXR Flash) - giống mọi lính khác, xem VfxManager.
        VfxManager.PlaySpawnFlash(transform.position);

        isSpawning = true;
        isMoving = true;
        transform.localScale = Vector3.zero;

        Sequence spawnSeq = DOTween.Sequence();
        spawnSeq.Append(transform.DOScale(originalScale, 0.35f).SetEase(Ease.OutBack));
        spawnSeq.OnComplete(() => {
            isSpawning = false;
            SnapToGround();
            isMoving = false;   // để Update tự quyết định chạy tiếp hay nã đá
        });

        moveTween = spawnSeq;
    }

    // Tìm mục tiêu & kiểm tra tầm đánh dùng chung logic đo trên collider ở class cha Slime,
    // để mọi loại lính đều dừng đúng tầm đánh mà không đè lên nhau.
    // Riêng đường chạy của xe vẫn giới hạn bằng GetClearDistanceAhead() ở StartMovingAsVehicle().

    protected virtual void StartAttack()
    {
        if (isAttacking) return;
        isAttacking = true;

        AudioManager.PlayAttack();   // tiếng tấn công (mọi loại lính, cả 2 phe)

        // Không Kill tween ở đây: để xe đang chạy dở dừng lại đúng vị trí,
        // nếu Kill giữa chừng xe có thể bị lệch khỏi mặt đất.
        isMoving = false;

        timeSinceLastShot = 0f;
        if (animator != null) animator.SetBool("isAttack", true);
    }

    protected virtual void StopAttack()
    {
        isAttacking = false;
        target = null;

        if (animator != null) animator.SetBool("isAttack", false);
        StartMovingAsVehicle();
    }

    public virtual void AttackAnimationFinished()
    {
        // CLIENT chỉ để NHÌN: animation đánh ở máy client do UnitReplicaAnimation bật, đạn thì HOST bắn.
        if (!CanApplyAttackEffects) return;

        FireBullet();
    }

    private void FireBullet()
    {
        if (!isAttacking || bulletPrefab == null) return;
        if (target == null || !IsValidTarget(target)) return;
        if (timeSinceLastShot < minFireInterval) return;

        timeSinceLastShot = 0f;

        Vector2 spawnPosition = (Vector2)transform.position + bulletSpawnOffset;
        Bullet bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);
        bullet.Launch(spawnPosition, target.transform.position, damage, blueSide, gameObject);

        // MultiPlayer: đạn cũng phải do HOST sinh ra rồi đồng bộ sang client (chơi đơn: no-op).
        NetworkRole.SpawnIfServer(bullet.gameObject);
    }

    /// <summary>
    /// Có đồng minh đang chắn ngay trước xe không. Quét bằng KHUNG cao bằng thân (xem
    /// <see cref="Slime.ForwardScanAll"/>) - xe cao 9.1 nên nếu quét tròn ở tâm thì tia bay qua đầu
    /// các lính thấp (slime/Slinger/Archer chỉ cao 2.8-3.5) và xe tưởng trước mặt trống.
    /// </summary>
    protected virtual bool CatapultHasAllyInFront()
    {
        RaycastHit2D[] hits = ForwardScanAll(transform.position, GetAllyCheckDistance(catapultAllyCheckDistance), catapultAllyCheckRadius);

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingAlly(hit.collider)) return true;
        }
        return false;
    }

    protected void OnDrawGizmosSelected()
    {
        DrawForwardScanGizmo(transform.position, GetAllyCheckDistance(catapultAllyCheckDistance), catapultAllyCheckRadius, new Color(1f, 0.5f, 0f));
        DrawAttackGizmo(new Color(1f, 0f, 0f, 0.3f));
    }
}