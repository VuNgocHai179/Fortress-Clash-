using UnityEngine;

public class Soldier : MonoBehaviour
{
    public float Velocity;

    private float baseSpeed;

    public bool BlueSide;

    public Vector2 Destination;
    public Vector2 SpawnPosition;

    public float AttackRange = 1.5f;

    private GameObject target;

    private bool isAttacking;
    private bool canMove = true;
    private bool pendingStopAttack;

    private Animator animator;


    void Start()
    {
        baseSpeed = Velocity;
        animator = GetComponent<Animator>();
    }


    void Update()
    {
        // =========================
        // ĐANG ATTACK
        // =========================
        if (isAttacking)
        {
            // Enemy đã chết hoặc ra khỏi tầm
            if (target == null || !IsValidTarget(target))
            {
                pendingStopAttack = true;
            }

            // Không di chuyển khi đang attack
            return;
        }


        // =========================
        // KHÔNG ATTACK
        // =========================

        FindTarget();

        if (target != null)
        {
            StartAttack();
        }
        else if (canMove)
        {
            MoveToDestination();
        }
    }


    private bool IsValidTarget(GameObject t)
    {
        if (t == null)
            return false;

        // Kiểm tra object có thực sự tồn tại hay không
        if (!t.activeInHierarchy)
            return false;

        float distance = Vector2.Distance(
            transform.position,
            t.transform.position
        );

        return distance <= AttackRange + 0.15f;
    }


    private void StartAttack()
    {
        if (isAttacking)
            return;

        isAttacking = true;
        canMove = false;
        pendingStopAttack = false;

        Velocity = 0f;

        animator.SetBool("isAttack", true);
    }


    private void StopAttack()
    {
        isAttacking = false;
        canMove = true;
        pendingStopAttack = false;

        Velocity = baseSpeed;

        target = null;

        animator.SetBool("isAttack", false);
    }


    private void MoveToDestination()
    {
        transform.position = Vector2.MoveTowards(
            transform.position,
            Destination,
            Velocity * Time.deltaTime
        );
    }


    private void FindTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            AttackRange
        );

        target = null;

        foreach (Collider2D col in hits)
        {
            if (col.gameObject == gameObject)
                continue;

            if (col.CompareTag("enemy"))
            {
                target = col.gameObject;
                break;
            }
        }
    }


    // ==========================================
    // GỌI Ở FRAME CUỐI CỦA ANIMATION ATTACK
    // ==========================================
    public void AttackAnimationFinished()
    {
        // Enemy đã chết / ra khỏi range
        if (pendingStopAttack ||
            target == null ||
            !IsValidTarget(target))
        {
            StopAttack();
            return;
        }

        // Enemy vẫn còn
        // Giữ trạng thái attack để đánh tiếp
        animator.SetBool("isAttack", true);
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            AttackRange
        );
    }
}
