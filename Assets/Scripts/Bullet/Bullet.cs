using UnityEngine;

/// <summary>
/// Viên đạn bắn ra của các unit tầm xa (Arrow, StoneBig, StoneSmall).
/// Bay thẳng hoặc theo vòng cung, gây damage cho đơn vị địch ĐẦU TIÊN chạm phải.
/// Gate/Wall không nhận damage (đạn bay xuyên qua), King (KingSlime) là đơn vị nên nhận damage bình thường.
/// </summary>
public class Bullet : MonoBehaviour
{
    public enum FlightMode
    {
        Straight,   // Bay thẳng tới điểm mục tiêu
        Arc         // Bay theo vòng cung (dùng cho Catapult nã đá)
    }

    [Header("Bay")]
    public FlightMode flightMode = FlightMode.Straight;

    [Tooltip("Tốc độ bay (world unit / giây)")]
    public float speed = 14f;

    [Tooltip("Độ cao vòng cung - chỉ dùng khi flightMode = Arc")]
    public float arcHeight = 3f;

    [Tooltip("Tự huỷ sau bao nhiêu giây nếu không trúng ai")]
    public float lifetime = 4f;

    [Header("Hướng sprite")]
    [Tooltip("Xoay sprite theo hướng bay")]
    public bool rotateToDirection = false;

    [Tooltip("Góc bù của sprite so với hướng bay (độ). Arrow cần 45")]
    public float rotationOffset = 0f;

    [Tooltip("Tự xoay (độ/giây), dùng cho hòn đá lăn trên không")]
    public float spinSpeed = 0f;

    [Header("Va chạm")]
    [Tooltip("Bán kính kiểm tra va chạm của viên đạn")]
    public float hitRadius = 0.5f;

    private int damage;
    private bool fromBlueSide;
    private GameObject shooter;

    private Vector2 startPosition;
    private Vector2 targetPosition;
    private Vector2 direction;
    private float flightDuration;
    private float elapsed;
    private bool launched;

    /// <summary>
    /// Bắn viên đạn từ <paramref name="from"/> tới vị trí <paramref name="to"/>.
    /// </summary>
    /// <param name="damage">Damage gây ra cho đơn vị địch đầu tiên trúng.</param>
    /// <param name="blueSide">True nếu đạn của phe xanh (chỉ gây damage cho phe đỏ).</param>
    /// <param name="shooter">Unit bắn ra viên đạn (để không tự bắn trúng mình).</param>
    public void Launch(Vector2 from, Vector2 to, int damage, bool blueSide, GameObject shooter)
    {
        startPosition = from;
        targetPosition = to;
        this.damage = damage;
        fromBlueSide = blueSide;
        this.shooter = shooter;

        elapsed = 0f;
        transform.position = from;

        Vector2 toTarget = to - from;
        float distance = toTarget.magnitude;

        direction = distance > 0.001f
            ? toTarget / distance
            : (blueSide ? Vector2.right : Vector2.left);

        // Thời gian bay tới đích, cũng dùng để chia quãng đường cho chế độ vòng cung
        flightDuration = Mathf.Max(distance / Mathf.Max(speed, 0.01f), 0.05f);

        launched = true;

        if (rotateToDirection)
        {
            FaceDirection(direction);
        }
    }

    private void Update()
    {
        if (!launched) return;

        float deltaTime = Time.deltaTime;
        elapsed += deltaTime;

        Vector2 previousPosition = transform.position;
        Vector2 currentPosition;
        Vector2 step;

        if (flightMode == FlightMode.Arc)
        {
            float progress = Mathf.Clamp01(elapsed / flightDuration);

            Vector2 flatPosition = Vector2.Lerp(startPosition, targetPosition, progress);
            float height = arcHeight * 4f * progress * (1f - progress);

            currentPosition = flatPosition + Vector2.up * height;
            transform.position = currentPosition;
            step = currentPosition - previousPosition;
        }
        else
        {
            step = direction * speed * deltaTime;
            currentPosition = previousPosition + step;
            transform.position = currentPosition;
        }

        if (rotateToDirection && step.sqrMagnitude > 0.000001f)
        {
            FaceDirection(step.normalized);
        }
        else if (spinSpeed != 0f)
        {
            transform.Rotate(0f, 0f, spinSpeed * deltaTime);
        }

        if (CheckHit(previousPosition, currentPosition)) return;

        bool reachedArcTarget = flightMode == FlightMode.Arc && elapsed >= flightDuration;

        if (elapsed >= lifetime || reachedArcTarget)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Kiểm tra quãng đường vừa bay qua, gây damage cho đơn vị địch đầu tiên trúng phải.
    /// </summary>
    private bool CheckHit(Vector2 previousPosition, Vector2 currentPosition)
    {
        Vector2 step = currentPosition - previousPosition;
        float distance = step.magnitude;

        if (distance > 0.0001f)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(previousPosition, hitRadius, step / distance, distance);

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null) continue;

                if (TryDamage(hit.collider)) return true;
            }

            return false;
        }

        // Quãng đường quá ngắn (đạn gần như đứng yên) thì quét vùng cho chắc
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(currentPosition, hitRadius);

        foreach (Collider2D col in overlaps)
        {
            if (col == null) continue;

            if (TryDamage(col)) return true;
        }

        return false;
    }

    /// <summary>
    /// Gây damage nếu collider thuộc phe địch. Trả về true nếu đạn đã trúng địch.
    /// </summary>
    private bool TryDamage(Collider2D col)
    {
        GameObject hitObject = col.gameObject;

        // Không bắn trúng chính người bắn
        if (shooter != null &&
            (hitObject == shooter || hitObject.transform.IsChildOf(shooter.transform)))
        {
            return false;
        }

        Slime slime = hitObject.GetComponentInParent<Slime>();
        if (slime != null)
        {
            if (slime.blueSide == fromBlueSide) return false;   // Đồng minh -> bay xuyên qua

            slime.TakeDamage(damage);
            Destroy(gameObject);
            return true;
        }

        SoldierEnemy soldier = hitObject.GetComponentInParent<SoldierEnemy>();
        if (soldier != null)
        {
            if (soldier.BlueSide == fromBlueSide) return false;  // Đồng minh -> bay xuyên qua

            soldier.getHitted(damage);
            Destroy(gameObject);
            return true;
        }

        // Gate / Wall / vật thể khác: bỏ qua, không gây damage
        return false;
    }

    private void FaceDirection(Vector2 flyDirection)
    {
        float angle = Mathf.Atan2(flyDirection.y, flyDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }
}
