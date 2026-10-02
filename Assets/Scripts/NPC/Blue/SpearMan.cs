using UnityEngine;
using DG.Tweening;

public class SpearMan : Slime
{
    [Header("SpearMan Combat & Range")]
    public float spearAttackRange = 6.4f;
    public float spearAllyCheckDistance = 0.8f;
    public float spearAllyCheckRadius = 0.28f;

    protected bool isAttacking = false;
    private Animator animator;

    private void Reset()
    {
        spearAttackRange = 6.4f;
        health = 100;
        damage = 28;
        price = 50;
        velocity = 3.0f;
        allyCheckDistance = 0.8f;
        allyCheckRadius = 0.28f;
    }

    protected override void Start()
    {
        animator = GetComponent<Animator>();

        if (spearAttackRange <= 0f) spearAttackRange = 6.4f;
        if (health <= 0) health = 100;
        if (damage <= 0) damage = 28;
        if (price <= 0) price = 50;

        // ĐỒNG BỘ TẦM ĐÁNH - Bắt buộc phải có dòng này
        attackRange = spearAttackRange;

        if (blueSide && (Destination == Vector3.zero || Destination.x <= transform.position.x))
            Destination = transform.position + Vector3.right * 20f;
        else if (!blueSide && (Destination == Vector3.zero || Destination.x >= transform.position.x))
            Destination = transform.position + Vector3.left * 20f;

        characterName = "BlueSpearMan";
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

        if (SpearManHasAllyInFront())
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
        if (target != null && IsValidTarget(target))
        {
            Slime enemy = target.GetComponent<Slime>();
            if (enemy != null) enemy.TakeDamage(damage);
        }
        else
        {
            StopAttack();
        }
    }

    protected virtual bool SpearManHasAllyInFront()
    {
        Vector2 currentPosition = transform.position;
        Vector2 direction = ((Vector3)Destination - (Vector3)currentPosition).normalized;
        if (direction == Vector2.zero)
        {
            direction = blueSide ? Vector2.right : Vector2.left;
        }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(currentPosition, spearAllyCheckRadius, direction, GetAllyCheckDistance(spearAllyCheckDistance));

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
        Vector2 checkPosition = (Vector2)transform.position + direction * GetAllyCheckDistance(spearAllyCheckDistance);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(checkPosition, spearAllyCheckRadius);
        DrawAttackGizmo(new Color(1f, 0f, 0f, 0.3f));
    }
}