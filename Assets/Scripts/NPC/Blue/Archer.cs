using UnityEngine;
using DG.Tweening;

public class Archer : Slime
{
    [Header("Archer Combat & Range")]
    public float archerAttackRange = 8.2f;
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
            if (target == null || !IsValidTarget(target))
            {
                StopAttack();
            }
            else
            {
                timeSinceLastShot += Time.deltaTime;
                if (timeSinceLastShot >= fallbackFireInterval)
                {
                    FireBullet();
                }
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
    }

    protected virtual bool ArcherHasAllyInFront()
    {
        Vector2 currentPosition = transform.position;
        Vector2 direction = ((Vector3)Destination - (Vector3)currentPosition).normalized;
        if (direction == Vector2.zero)
        {
            direction = blueSide ? Vector2.right : Vector2.left;
        }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(currentPosition, archerAllyCheckRadius, direction, GetAllyCheckDistance(archerAllyCheckDistance));

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingAlly(hit.collider)) return true;
        }
        return false;
    }

    protected void OnDrawGizmosSelected()
    {
        DrawAttackGizmo(new Color(1f, 0f, 0f, 0.3f));

        Vector2 direction = ((Vector3)Destination - transform.position).normalized;
        if (direction == Vector2.zero)
        {
            direction = blueSide ? Vector2.right : Vector2.left;
        }

        Vector2 checkPosition = (Vector2)transform.position + direction * GetAllyCheckDistance(archerAllyCheckDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(checkPosition, archerAllyCheckRadius);
    }
}