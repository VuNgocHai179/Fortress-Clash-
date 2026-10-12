using UnityEngine;
using DG.Tweening;

public class Slinger : Slime
{
    [Header("Slinger Combat & Range")]
    [Tooltip("Tầm bắn (world unit). Đủ xa để Slinger đứng SAU 1 lính cận chiến trong hàng đợi vẫn bắn tới " +
             "địch ở tuyến trước: cần > 9.3 mới vượt qua được 1 đồng minh (thân rộng ~5.25 + khe hở xếp " +
             "hàng), > 15.5 mới vượt qua được 2. Slinger là lính xa có tầm THẤP NHẤT.")]
    public float slingerAttackRange = 10f;
    public float slingerAllyCheckDistance = 1.2f;
    public float slingerAllyCheckRadius = 0.5f;

    [Header("Projectile")]
    public Bullet bulletPrefab;
    public Vector2 bulletSpawnOffset = new Vector2(1.5f, 0.3f);
    public float fallbackFireInterval = 1.5f;
    public float minFireInterval = 0.2f;

    /// <summary>
    /// Slinger bắn đạn nên là lính TẦM XA: lính cận chiến cùng phe được phép đi xuyên qua để tới
    /// chiến tuyến, và Slinger tự né ra sau hàng cận chiến (xem <see cref="Slime.AllyBlocksMe"/>).
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
        slingerAttackRange = 10f;
        health = 30;
        damage = 9;
        price = 20;
        velocity = 3.5f;
        allyCheckDistance = 0.7f;
        allyCheckRadius = 0.25f;
        slingerAllyCheckDistance = 1.2f;
        slingerAllyCheckRadius = 0.5f;
    }

    protected override void Start()
    {
        if (slingerAttackRange <= 0f) slingerAttackRange = 10f;
        if (health <= 0) health = 30;
        if (damage <= 0) damage = 9;
        if (price <= 0) price = 20;
        if (velocity <= 0f) velocity = 3.5f;

        base.Start();
        characterName = "BlueSlinger";
        attackRange = slingerAttackRange;
        animator = GetComponent<Animator>();

        ApplyCastleUpgrade();   // cộng chỉ số từ nâng cấp thành (phải gọi SAU khi đã gán chỉ số)
    }

    protected override void Update()
    {
        if (isAttacking)
        {
            // Còn địch trong tầm (kể cả địch KHÁC vừa lọt vào tầm) là bắn tiếp ngay, không ngắt đòn:
            // Slinger đứng sau hàng đợi vẫn đánh được mà không phải "đợi tới lượt".
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
            // Để cú nhảy đang dở đáp xuống đất rồi mới đứng lại bắn
            // (Pause/Kill giữa chừng sẽ làm lính treo lơ lửng trên không trung).
            isMoving = false;
            StartAttack();
            return;
        }

        if (SlingerHasAllyInFront())
        {
            StopMovingForAlly();
            return;
        }

        ContinueMovingIfIdle();
    }

    // Tìm mục tiêu & kiểm tra tầm đánh dùng chung logic đo trên collider ở class cha Slime,
    // để mọi loại lính đều dừng đúng tầm đánh mà không đè lên nhau.

    protected virtual void StartAttack()
    {
        if (isAttacking) return;
        isAttacking = true;

        AudioManager.PlayAttack();   // tiếng tấn công (mọi loại lính, cả 2 phe)

        // Không Kill tween ở đây: để cú nhảy đang dở bay hết quỹ đạo rồi đáp xuống đất,
        // nếu Kill giữa chừng lính sẽ bị treo lơ lửng trên không trung.
        isMoving = false;

        timeSinceLastShot = 0f;
        if (animator != null) animator.SetBool("isAttack", true);
    }

    protected virtual void StopAttack()
    {
        isAttacking = false;
        target = null;

        if (animator != null) animator.SetBool("isAttack", false);
        StartMoving();
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

    /// <summary>Quét bằng KHUNG cao bằng thân (xem <see cref="Slime.ForwardScanAll"/>).</summary>
    protected virtual bool SlingerHasAllyInFront()
    {
        RaycastHit2D[] hits = ForwardScanAll(transform.position, GetAllyCheckDistance(slingerAllyCheckDistance), slingerAllyCheckRadius);

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingAlly(hit.collider)) return true;
        }
        return false;
    }

    protected void OnDrawGizmosSelected()
    {
        DrawForwardScanGizmo(transform.position, GetAllyCheckDistance(slingerAllyCheckDistance), slingerAllyCheckRadius, Color.cyan);
        DrawAttackGizmo(new Color(0f, 1f, 1f, 0.3f));
    }
}