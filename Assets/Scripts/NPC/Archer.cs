using UnityEngine;

public class Archer : Soldier
{
    [Header("Archer Ally Blocking")]
    public float ArcherAllyCheckDistance = 1.5f;
    public float ArcherAllyCheckRadius = 0.35f;

    protected override void Update()
    {
        // ==========================================
        // ĐANG ATTACK
        // ==========================================

        if (isAttacking)
        {
            if (target == null || !IsValidTarget(target))
            {
                pendingStopAttack = true;
            }

            return;
        }


        // ==========================================
        // TÌM ENEMY TRONG TẦM
        // ==========================================

        FindTarget();


        // ==========================================
        // CÓ ENEMY TRONG TẦM
        // ==========================================

        if (target != null)
        {
            // Có enemy thì bắn ngay.
            // Ally đứng trước KHÔNG ảnh hưởng.

            Velocity = 0f;

            StopJump();

            StartAttack();

            return;
        }


        // ==========================================
        // KHÔNG CÓ ENEMY
        // KIỂM TRA ALLY PHÍA TRƯỚC
        // ==========================================

        if (ArcherHasAllyInFront())
        {
            // Có Ally phía trước -> DỪNG HẲN

            Velocity = 0f;

            StopJump();

            return;
        }


        // ==========================================
        // KHÔNG CÓ ALLY -> TIẾP TỤC ĐI
        // ==========================================

        if (canMove)
        {
            Velocity = baseSpeed;

            JumpToDestination();
        }
    }


    // ==========================================
    // TÌM ENEMY
    // ==========================================

    protected override void FindTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            AttackRange
        );

        target = null;

        Vector2 moveDirection =
            (Destination - (Vector2)transform.position).normalized;

        if (moveDirection == Vector2.zero)
        {
            moveDirection = BlueSide
                ? Vector2.right
                : Vector2.left;
        }

        float closestEnemyDistance = Mathf.Infinity;

        foreach (Collider2D col in hits)
        {
            if (col == null)
                continue;

            if (col.gameObject == gameObject)
                continue;

            if (!col.CompareTag("enemy"))
                continue;


            Vector2 directionToEnemy =
                ((Vector2)col.transform.position -
                (Vector2)transform.position).normalized;


            // Enemy phải ở phía trước
            float dot = Vector2.Dot(
                moveDirection,
                directionToEnemy
            );

            if (dot <= 0)
                continue;


            float distance = Vector2.Distance(
                transform.position,
                col.transform.position
            );


            if (distance < closestEnemyDistance)
            {
                closestEnemyDistance = distance;
                target = col.gameObject;
            }
        }
    }


    // ==========================================
    // KIỂM TRA ALLY PHÍA TRƯỚC
    // ==========================================

    protected virtual bool ArcherHasAllyInFront()
    {
        Vector2 currentPosition = transform.position;


        // Xác định hướng tiến tới
        Vector2 direction =
            (Destination - currentPosition).normalized;


        // Nếu Destination không xác định được
        // thì dùng hướng của phe
        if (direction == Vector2.zero)
        {
            direction = BlueSide
                ? Vector2.right
                : Vector2.left;
        }


        // Vị trí kiểm tra nằm phía trước Archer
        Vector2 checkPosition =
            currentPosition +
            direction * ArcherAllyCheckDistance;


        // Tìm collider xung quanh vị trí phía trước
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            checkPosition,
            ArcherAllyCheckRadius
        );


        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;


            if (hit.gameObject == gameObject)
                continue;


            if (hit.CompareTag("ally"))
            {
                return true;
            }
        }


        return false;
    }


    // ==========================================
    // DEBUG
    // ==========================================

    protected virtual void OnDrawGizmosSelected()
    {
        Vector2 direction =
            (Destination - (Vector2)transform.position).normalized;

        if (direction == Vector2.zero)
        {
            direction = BlueSide
                ? Vector2.right
                : Vector2.left;
        }


        Vector2 checkPosition =
            (Vector2)transform.position +
            direction * ArcherAllyCheckDistance;


        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            checkPosition,
            ArcherAllyCheckRadius
        );
    }
}