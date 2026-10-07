using UnityEngine;
using DG.Tweening;

public class Archer : Slime
{
    [Header("Archer Combat & Range")]
    [Tooltip("Tầm bắn (world unit). Đủ xa để Archer đứng SAU 1 lính cận chiến trong hàng đợi vẫn bắn tới " +
             "địch ở tuyến trước: thân lính rộng ~5.25 + khe hở xếp hàng nên cần > 9.4 mới bắn vượt qua " +
             "được 1 đồng minh, và > 15.6 mới vượt qua được 2.")]
    public float archerAttackRange = 13f;
    public float archerAllyCheckDistance = 1.5f;
    public float archerAllyCheckRadius = 0.35f;

    [Header("Projectile")]
    [Tooltip("Prefab viên đạn (Assets/Prefab/Bullet/Arrow.prefab)")]
    public Bullet bulletPrefab;

    [Tooltip("Lệch vị trí spawn đạn so với tâm unit (world unit)")]
    public Vector2 bulletSpawnOffset = new Vector2(1.7f, 0.3f);

    [Tooltip("Nhịp bắn dự phòng (giây).")]
    public float fallbackFireInterval = 1.5f;
    public float minFireInterval = 0.2f;

    /// <summary>
    /// Archer bắn đạn nên là lính TẦM XA: lính cận chiến cùng phe được phép đi xuyên qua để tới
    /// chiến tuyến, và Archer tự né ra sau hàng cận chiến (xem <see cref="Slime.AllyBlocksMe"/>).
    /// </summary>
    protected override bool IsRangedUnit
    {
        get { return true; }
    }

    protected bool isAttacking = false;
    private Animator animator;
    private float timeSinceLastShot = Mathf.Infinity;

    protected override void Start()
    {
        base.Start();
        characterName = "BlueArcher";
        attackRange = archerAttackRange;
        animator = GetComponent<Animator>();

        ApplyCastleUpgrade();   // cộng chỉ số từ nâng cấp thành (phải gọi SAU khi đã gán chỉ số)
    }

    protected override void Update()
    {
        if (isAttacking)
        {
            // Còn địch trong tầm (kể cả địch KHÁC vừa lọt vào tầm) là bắn tiếp ngay, không ngắt đòn:
            // Archer đứng sau hàng đợi vẫn đánh được mà không phải "đợi tới lượt".
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

        if (ArcherHasAllyInFront())
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
    protected virtual bool ArcherHasAllyInFront()
    {
        RaycastHit2D[] hits = ForwardScanAll(transform.position, GetAllyCheckDistance(archerAllyCheckDistance), archerAllyCheckRadius);

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingAlly(hit.collider)) return true;
        }
        return false;
    }

    protected void OnDrawGizmosSelected()
    {
        DrawAttackGizmo(new Color(1f, 0f, 0f, 0.3f));
        DrawForwardScanGizmo(transform.position, GetAllyCheckDistance(archerAllyCheckDistance), archerAllyCheckRadius, Color.yellow);
    }
}