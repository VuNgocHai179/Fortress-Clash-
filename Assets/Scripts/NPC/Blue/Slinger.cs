using UnityEngine;
using DG.Tweening;

public class Slinger : Slime
{
    [Header("Slinger Combat & Range")]
    public float slingerAttackRange = 6f;
    public float slingerAllyCheckDistance = 1.2f;
    public float slingerAllyCheckRadius = 0.5f;

    [Header("Projectile")]
    public Bullet bulletPrefab;
    public Vector2 bulletSpawnOffset = new Vector2(1.5f, 0.3f);
    public float fallbackFireInterval = 1.5f;
    public float minFireInterval = 0.2f;

    protected bool isAttacking = false;
    private Animator animator;
    private float timeSinceLastShot = Mathf.Infinity;

    private void Reset()
    {
        slingerAttackRange = 6f;
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
        if (slingerAttackRange <= 0f) slingerAttackRange = 6f;
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

    protected virtual bool SlingerHasAllyInFront()
    {
        Vector2 currentPosition = transform.position;
        Vector2 direction = ((Vector3)Destination - (Vector3)currentPosition).normalized;
        if (direction == Vector2.zero)
        {
            direction = blueSide ? Vector2.right : Vector2.left;
        }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(currentPosition, slingerAllyCheckRadius, direction, GetAllyCheckDistance(slingerAllyCheckDistance));
        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingAlly(hit.collider)) return true;
        }
        return false;
    }

    protected void OnDrawGizmosSelected()
    {
        Vector2 direction = ((Vector3)Destination - transform.position).normalized;
        if (direction == Vector2.zero)
        {
            direction = blueSide ? Vector2.right : Vector2.left;
        }
        Vector2 checkPosition = (Vector2)transform.position + direction * GetAllyCheckDistance(slingerAllyCheckDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(checkPosition, slingerAllyCheckRadius);
        DrawAttackGizmo(new Color(0f, 1f, 1f, 0.3f));
    }
}