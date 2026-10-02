using UnityEngine;
using DG.Tweening;
using System;

public class SoldierEnemy : MonoBehaviour
{
    public float Velocity;

    protected float baseSpeed;

    public bool BlueSide;

    public Vector2 Destination;
    public Vector2 SpawnPosition;

    public float AttackRange = 1.5f;

    public int Health;
    public int Damage;
    public int Price;

    public int priceDrop;

    // ==========================================
    // JUMP SETTINGS
    // ==========================================

    [Header("Jump Settings")]
    public float JumpDistance = 0.5f;

    public float JumpHeight = 0.25f;

    // Thời gian thực hiện 1 cú nhảy
    // Càng nhỏ = nhảy càng nhanh
    public float JumpDuration = 0.3f;

    // Thời gian nghỉ giữa 2 cú nhảy
    public float JumpDelay = 0.05f;

    [Header("Ally Blocking")]
    public float AllyCheckDistance = 0.7f;
    public float AllyCheckRadius = 0.25f;


    protected GameObject target;

    protected bool isAttacking;
    protected bool canMove = true;
    protected bool pendingStopAttack;

    // Ally đang đứng trước mặt và chặn đường
    protected bool blockedByAlly;

    // Đang thực hiện cú nhảy
    private bool isJumping;

    private Animator animator;

    private Vector3 originalScale;

    private Sequence jumpSequence;

    [SerializeField]
    private String Name;


    void Start()
    {
        baseSpeed = Velocity;
        animator = GetComponent<Animator>();

        // Lưu scale ban đầu của nhân vật
        originalScale = transform.localScale;
    }


    protected virtual void Update()
{
    // ==========================================
    // ALLY ĐANG CHẶN
    // ==========================================

    if (!isAttacking && IsAllyBlocking())
    {
        blockedByAlly = true;

        Velocity = 0f;

        StopJump();

        return;
    }

    blockedByAlly = false;


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
    // TÌM TARGET
    // ==========================================

    FindTarget();


    // ==========================================
    // CÓ ENEMY
    // ==========================================

    if (target != null)
    {
        StopJump();

        StartAttack();

        return;
    }


    // ==========================================
    // CÓ ALLY
    // ==========================================

    if (blockedByAlly)
    {
        Velocity = 0f;

        StopJump();

        return;
    }


    // ==========================================
    // KHÔNG CÓ AI CHẶN
    // ==========================================

    if (canMove)
    {
        Velocity = baseSpeed;

        JumpToDestination();
    }
}


    // ==========================================
    // KIỂM TRA TARGET
    // ==========================================

    protected virtual bool IsValidTarget(GameObject t)
    {
        if (t == null)
            return false;

        if (!t.activeInHierarchy)
            return false;

        Collider2D col = t.GetComponent<Collider2D>();
        if (col != null)
        {
            Vector2 closestPoint = col.ClosestPoint(transform.position);
            float colliderDistance = Vector2.Distance(
                transform.position,
                closestPoint
            );

            if (colliderDistance <= AttackRange + 0.15f)
            {
                return true;
            }
        }

        float distance = Vector2.Distance(
            transform.position,
            t.transform.position
        );

        return distance <= AttackRange + 0.15f;
    }


    // ==========================================
    // ATTACK
    // ==========================================

    protected void StartAttack()
    {
        if (isAttacking)
            return;

        isAttacking = true;
        canMove = false;
        pendingStopAttack = false;

        Velocity = 0f;

        StopJump();

        animator.SetBool("isAttack", true);
    }


    protected void StopAttack()
    {
        isAttacking = false;
        canMove = true;
        pendingStopAttack = false;

        Velocity = baseSpeed;

        target = null;

        animator.SetBool("isAttack", false);
    }


    // ==========================================
    // NHẢY TỚI DESTINATION
    // ==========================================

    protected virtual void JumpToDestination()
{
    // ==========================================
    // KIỂM TRA ALLY TRƯỚC KHI NHẢY
    // ==========================================

    if (IsAllyBlocking())
    {
        Velocity = 0f;

        StopJump();

        return;
    }


    // ==========================================
    // VELOCITY = 0 THÌ KHÔNG NHẢY
    // ==========================================

    if (Velocity <= 0)
    {
        StopJump();
        return;
    }


    // ==========================================
    // ĐANG NHẢY THÌ KHÔNG TẠO CÚ NHẢY MỚI
    // ==========================================

    if (isJumping)
        return;


    Vector2 currentPosition = transform.position;


    // ==========================================
    // ĐÃ TỚI DESTINATION
    // ==========================================

    if (Vector2.Distance(currentPosition, Destination) < 0.05f)
    {
        transform.position = Destination;
        return;
    }


    // ==========================================
    // HƯỚNG DI CHUYỂN
    // ==========================================

    Vector2 direction =
        (Destination - currentPosition).normalized;


    // ==========================================
    // KHOẢNG CÁCH CÒN LẠI
    // ==========================================

    float remainingDistance =
        Vector2.Distance(currentPosition, Destination);


    float distance = Mathf.Min(
        JumpDistance,
        remainingDistance
    );


    // ==========================================
    // KIỂM TRA ALLY TRÊN ĐƯỜNG NHẢY
    // ==========================================

    RaycastHit2D[] hits = Physics2D.CircleCastAll(
        currentPosition,
        AllyCheckRadius,
        direction,
        distance
    );

    foreach (RaycastHit2D hit in hits)
    {
        if (hit.collider == null)
            continue;

        if (hit.collider.gameObject == gameObject)
            continue;

        if (hit.collider.CompareTag("enemy"))
        {
            Velocity = 0f;

            StopJump();

            return;
        }
    }


    // ==========================================
    // VỊ TRÍ ĐÍCH CỦA CÚ NHẢY
    // ==========================================

    Vector2 jumpDestination =
        currentPosition + direction * distance;


    if (Vector2.Distance(jumpDestination, Destination) < 0.05f)
    {
        jumpDestination = Destination;
    }


    // ==========================================
    // BẮT ĐẦU NHẢY
    // ==========================================

    isJumping = true;


    float duration = JumpDuration;

    if (baseSpeed > 0)
    {
        duration = distance / baseSpeed;

        duration = Mathf.Max(0.05f, duration);
    }


    // ==========================================
    // TẠO SEQUENCE
    // ==========================================

    jumpSequence = DOTween.Sequence();


    // ==========================================
    // NHÚN XUỐNG
    // ==========================================

    jumpSequence.Append(
        transform.DOScale(
            new Vector3(
                originalScale.x * 1.15f,
                originalScale.y * 0.80f,
                originalScale.z
            ),
            0.08f
        )
    );


    // ==========================================
    // NHẢY
    // ==========================================

    jumpSequence.Append(
        transform.DOJump(
            jumpDestination,
            JumpHeight,
            1,
            duration
        )
    );


    // ==========================================
    // DUỖI NGƯỜI
    // ==========================================

    jumpSequence.Join(
        transform.DOScale(
            new Vector3(
                originalScale.x * 0.90f,
                originalScale.y * 1.10f,
                originalScale.z
            ),
            duration * 0.35f
        )
    );


    // ==========================================
    // TRỞ LẠI BÌNH THƯỜNG
    // ==========================================

    jumpSequence.Append(
        transform.DOScale(
            originalScale,
            0.10f
        )
    );


    // ==========================================
    // DELAY
    // ==========================================

    jumpSequence.AppendInterval(JumpDelay);


    // ==========================================
    // KẾT THÚC
    // ==========================================

    jumpSequence.OnComplete(() =>
    {
        transform.localScale = originalScale;

        isJumping = false;

        jumpSequence = null;
    });
}


    // ==========================================
    // DỪNG NHẢY
    // ==========================================

    protected void StopJump()
{
    if (jumpSequence != null)
    {
        jumpSequence.Kill();
        jumpSequence = null;
    }

    isJumping = false;

    // Trả nhân vật về scale ban đầu
    transform.localScale = originalScale;
}


    // ==========================================
    // TÌM TARGET
    // ==========================================

    // ==========================================
// TÌM TARGET TỪ TRÁI SANG PHẢI
// ==========================================

protected virtual void FindTarget()
{
    Collider2D[] hits = Physics2D.OverlapCircleAll(
        transform.position,
        AttackRange
    );

    target = null;
    blockedByAlly = false;

    GameObject closestEnemy = null;
    GameObject closestAlly = null;

    float closestEnemyX = Mathf.Infinity;
    float closestAllyX = Mathf.Infinity;

    foreach (Collider2D col in hits)
    {
        if (col == null)
            continue;

        // Không xét chính mình
        if (col.gameObject == gameObject)
            continue;

        float x = col.transform.position.x;

        // ==========================================
        // ENEMY CỦA SOLDIER ENEMY
        // Tag ally = Enemy
        // ==========================================

        if (col.CompareTag("ally"))
        {
            // Tìm từ TRÁI -> PHẢI
            if (x < closestEnemyX)
            {
                closestEnemyX = x;
                closestEnemy = col.gameObject;
            }
        }

        // ==========================================
        // ALLY CỦA SOLDIER ENEMY
        // Tag enemy = Ally
        // ==========================================

        else if (col.CompareTag("enemy"))
        {
            // Tìm từ TRÁI -> PHẢI
            if (x < closestAllyX)
            {
                closestAllyX = x;
                closestAlly = col.gameObject;
            }
        }
    }

    // ==========================================
    // CÓ ENEMY
    // ==========================================

    if (closestEnemy != null)
    {
        target = closestEnemy;
    }

    // ==========================================
    // CÓ ALLY ĐỨNG TRƯỚC ENEMY
    // ==========================================

    if (closestAlly != null)
    {
        if (closestEnemy == null ||
            closestAllyX < closestEnemyX)
        {
            blockedByAlly = true;

            // Ally đứng trước thì không attack enemy phía sau
            target = null;
        }
    }
}


    // ==========================================
    // GỌI Ở FRAME CUỐI CỦA ANIMATION ATTACK
    // ==========================================

    public void AttackAnimationFinished()
    {
        if (target == null)
        {
            StopAttack();
            return;
        }


        Debug.Log(
            "da danh thanh cong object co ten: "
            + target.name
        );


        Slime enemySoldier =
            target.GetComponent<Slime>();

        Gate gate = target.GetComponent<Gate>();


        // Enemy đã chết / ra khỏi range
        if (pendingStopAttack ||
            target == null ||
            !IsValidTarget(target))
        {
            StopAttack();
            return;
        }


        // Enemy vẫn còn
        // Giữ trạng thái attack
        animator.SetBool("isAttack", true);
    }


    // ==========================================
    // NHẬN DAMAGE
    // ==========================================

    public void getHitted(int damage)
    {
        Health = Mathf.Max(
            0,
            Health - damage
        );


        Debug.Log(
            "mau con lai " + Health
        );


        if (Health == 0)
        {
            if(gameObject.CompareTag("enemy")) GameManager.Instance.Addmoney(priceDrop);

            GameManager.Instance.soldierDown(Name);

            StopJump();

            Destroy(gameObject);
        }
    }

    private bool IsAllyBlocking()
{
    Vector2 currentPosition = transform.position;

    Vector2 moveDirection =
        (Destination - currentPosition).normalized;

    if (moveDirection == Vector2.zero)
        return false;

    RaycastHit2D[] hits = Physics2D.CircleCastAll(
        currentPosition,
        AllyCheckRadius,
        moveDirection,
        AllyCheckDistance
    );

    foreach (RaycastHit2D hit in hits)
    {
        if (hit.collider == null)
            continue;

        // Không xét chính mình
        if (hit.collider.gameObject == gameObject)
            continue;

        if (hit.collider.CompareTag("enemy"))
        {
            return true;
        }
    }

    return false;
}


    // ==========================================
    // GIZMOS
    // ==========================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            AttackRange
        );
    }
}