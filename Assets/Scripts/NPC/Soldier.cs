using UnityEngine;

public class Soldier : MonoBehaviour
{
    public float Velocity;

    private float baseSpeed;

    public bool BlueSide;

    public Vector2 Destination;
    public Vector2 SpawnPosition;

    public float AttackRange = 1.5f;

    public int Health;

    public int Damage;

    public int Price;

    private GameObject target;

    private bool isAttacking;
    private bool canMove = true;
    private bool pendingStopAttack;

    // Ally đang đứng trước mặt và chặn đường
    private bool blockedByAlly;

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

        // Có enemy trong tầm
        if (target != null)
        {
            StartAttack();
            return;
        }

        // Có ally phía trước
        if (blockedByAlly)
        {
            // Đứng yên
            Velocity = 0f;
            return;
        }

        // Không có enemy hoặc ally
        if (canMove)
        {
            Velocity = baseSpeed;
            MoveToDestination();
        }
    }


    private bool IsValidTarget(GameObject t)
    {
        if (t == null)
            return false;

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
    blockedByAlly = false;

    // Hướng soldier đang di chuyển
    Vector2 moveDirection = (Destination - (Vector2)transform.position).normalized;

    // Nếu đã gần Destination thì không cần xét hướng
    if (moveDirection == Vector2.zero)
    {
        moveDirection = Vector2.right;
    }

    foreach (Collider2D col in hits)
    {
        if (col.gameObject == gameObject)
            continue;

        Vector2 directionToObject =
            ((Vector2)col.transform.position - (Vector2)transform.position).normalized;

        // Dot > 0 nghĩa là object nằm phía trước
        float dot = Vector2.Dot(moveDirection, directionToObject);

        // Chỉ xét object nằm phía trước
        if (dot <= 0)
            continue;


        // =========================
        // ENEMY
        // =========================
        if (col.CompareTag("enemy"))
        {
            target = col.gameObject;

            // Enemy ưu tiên
            return;
        }


        // =========================
        // ALLY
        // =========================
        if (col.CompareTag("ally"))
        {
            blockedByAlly = true;
        }
    }
}


    // ==========================================
    // GỌI Ở FRAME CUỐI CỦA ANIMATION ATTACK
    // ==========================================
    public void AttackAnimationFinished()
    {
        Debug.Log("da danh thanh cong object co ten:" + target.name);
        target.GetComponent<Soldier>().getHitted(Damage);
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

    public void getHitted(int damage)
    {
        Health = Mathf.Max(0, Health - damage);
        Debug.Log("mau con lai" + Health);
        if(Health == 0)
        {
            Destroy(gameObject);
        }
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