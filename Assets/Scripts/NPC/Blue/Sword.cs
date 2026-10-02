using UnityEngine;
using DG.Tweening;

public class Sword : Slime
{
    [Header("Sword Combat & Range")]
    public float swordAttackRange = 3.1f;
    public float swordAllyCheckDistance = 0.7f;
    public float swordAllyCheckRadius = 0.25f;

    protected bool isAttacking = false;
    private Animator animator;

    protected override void Start()
    {
        if (swordAttackRange <= 0f) swordAttackRange = 3.1f;
        if (health <= 0) health = 65;
        if (damage <= 0) damage = 16;
        if (price <= 0) price = 30;

        // ĐỒNG BỘ TẦM ĐÁNH CHO CLASS CHA
        attackRange = swordAttackRange;

        animator = GetComponent<Animator>();

        if (blueSide && (Destination == Vector3.zero || Destination.x <= transform.position.x))
            Destination = transform.position + Vector3.right * 20f;
        else if (!blueSide && (Destination == Vector3.zero || Destination.x >= transform.position.x))
            Destination = transform.position + Vector3.left * 20f;

        characterName = "BlueSword";

        base.Start();

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
            return;
        }

        FindTarget();

        if (target != null)
        {
            // Để cú nhảy đang dở đáp xuống đất rồi mới đứng lại đánh
            // (Pause/Kill giữa chừng sẽ làm lính treo lơ lửng trên không trung).
            isMoving = false;

            StartAttack();
            return;
        }

        if (SwordHasAllyInFront())
        {
            StopMovingForAlly();
            return;
        }

        ContinueMovingIfIdle();
    }

    // Tầm đánh & kiểm tra mục tiêu dùng chung logic đo trên collider ở class cha Slime
    // (đo mép-với-mép nên 2 phe không phải đè lên nhau mới đánh được).

    protected virtual void StartAttack()
    {
        if (isAttacking) return;
        isAttacking = true;

        // Không Kill tween ở đây: để cú nhảy đang dở bay hết quỹ đạo rồi đáp xuống đất,
        // nếu Kill giữa chừng lính sẽ bị treo lơ lửng trên không trung.
        isMoving = false;

        if (animator != null) animator.SetBool("isAttack", true);
    }

    protected virtual void StopAttack()
    {
        isAttacking = false;
        target = null;

        if (animator != null) animator.SetBool("isAttack", false);

        StartMoving();
    }

    // NHỚ TẠO ANIMATION EVENT GỌI HÀM NÀY ĐỂ GÂY SÁT THƯƠNG
    public virtual void AttackAnimationFinished()
    {
        if (target != null && IsValidTarget(target))
        {
            Slime enemySlime = target.GetComponent<Slime>();
            if (enemySlime != null) enemySlime.TakeDamage(damage);
        }
        else
        {
            StopAttack();
        }
    }

    // KIỂM TRA ĐỒNG ĐỘI (Đã dùng CircleCastAll để không đẩy nhau)
    protected virtual bool SwordHasAllyInFront()
    {
        Vector2 currentPosition = transform.position;
        Vector2 direction = ((Vector3)Destination - (Vector3)currentPosition).normalized;

        if (direction == Vector2.zero)
        {
            direction = blueSide ? Vector2.right : Vector2.left;
        }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(currentPosition, swordAllyCheckRadius, direction, GetAllyCheckDistance(swordAllyCheckDistance));
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

        Vector2 checkPosition = (Vector2)transform.position + direction * GetAllyCheckDistance(swordAllyCheckDistance);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(checkPosition, swordAllyCheckRadius);

        DrawAttackGizmo(new Color(1f, 0f, 0f, 0.3f));
    }
}