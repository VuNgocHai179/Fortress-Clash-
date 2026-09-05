using UnityEngine;

public class Slinger : Archer
{
    [Header("Slinger Skirmisher Settings")]
    public float SlingerAllyCheckDistance = 3.0f;
    public float SlingerAllyCheckRadius = 1.2f;

    private void Reset()
    {
        AttackRange = 4.5f;
        Health = 25;
        Damage = 8;
        Price = 15;
        Velocity = 3.5f;
        SlingerAllyCheckDistance = 3.0f;
        SlingerAllyCheckRadius = 1.2f;
        AllyCheckDistance = 3.0f;
        AllyCheckRadius = 1.2f;
        ArcherAllyCheckDistance = 3.0f;
        ArcherAllyCheckRadius = 1.2f;
    }

    private void Awake()
    {
        // Skirmisher fallback stats if unassigned in inspector
        if (AttackRange <= 0f)
        {
            AttackRange = 4.5f;
        }

        if (Health <= 0)
        {
            Health = 25;
        }

        if (Damage <= 0)
        {
            Damage = 8;
        }

        if (Price <= 0)
        {
            Price = 15;
        }

        if (Velocity <= 0f)
        {
            Velocity = 3.5f;
        }

        if (SlingerAllyCheckDistance <= 0f)
        {
            SlingerAllyCheckDistance = 3.0f;
        }

        if (SlingerAllyCheckRadius <= 0f)
        {
            SlingerAllyCheckRadius = 1.2f;
        }

        // Synchronize skirmisher ally check parameters with base Archer and Soldier
        ArcherAllyCheckDistance = SlingerAllyCheckDistance;
        ArcherAllyCheckRadius = SlingerAllyCheckRadius;
        AllyCheckDistance = SlingerAllyCheckDistance;
        AllyCheckRadius = SlingerAllyCheckRadius;
    }

    private void OnValidate()
    {
        if (SlingerAllyCheckDistance <= 0f)
        {
            SlingerAllyCheckDistance = 3.0f;
        }

        if (SlingerAllyCheckRadius <= 0f)
        {
            SlingerAllyCheckRadius = 1.2f;
        }

        ArcherAllyCheckDistance = SlingerAllyCheckDistance;
        ArcherAllyCheckRadius = SlingerAllyCheckRadius;
        AllyCheckDistance = SlingerAllyCheckDistance;
        AllyCheckRadius = SlingerAllyCheckRadius;
    }

    protected override bool ArcherHasAllyInFront()
    {
        Vector2 currentPosition = transform.position;

        Vector2 direction =
            (Destination - currentPosition).normalized;

        if (direction == Vector2.zero)
        {
            direction = BlueSide
                ? Vector2.right
                : Vector2.left;
        }

        // Query all colliders in the forward vicinity of the Slinger
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            currentPosition,
            SlingerAllyCheckDistance + 1.0f
        );

        Vector2 lateralDir = new Vector2(-direction.y, direction.x);

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject)
                continue;

            if (!hit.CompareTag("ally"))
                continue;

            Vector2 toAlly = (Vector2)hit.transform.position - currentPosition;
            float forwardDistance = Vector2.Dot(direction, toAlly);

            // Ally is ahead within stopping distance
            if (forwardDistance > 0.1f && forwardDistance <= SlingerAllyCheckDistance)
            {
                float lateralDistance = Mathf.Abs(Vector2.Dot(lateralDir, toAlly));
                if (lateralDistance <= SlingerAllyCheckRadius)
                {
                    return true;
                }
            }

            // Also check collider bounds if units partially overlap
            Vector2 toBoundsCenter = (Vector2)hit.bounds.center - currentPosition;
            float boundsForward = Vector2.Dot(direction, toBoundsCenter);
            if (boundsForward > 0.1f && boundsForward <= SlingerAllyCheckDistance)
            {
                float lateralBounds = Mathf.Abs(Vector2.Dot(lateralDir, toBoundsCenter));
                if (lateralBounds <= SlingerAllyCheckRadius)
                {
                    return true;
                }
            }
        }

        return false;
    }

    protected override void OnDrawGizmosSelected()
    {
        Vector2 currentPosition = transform.position;
        Vector2 direction =
            (Destination - currentPosition).normalized;

        if (direction == Vector2.zero)
        {
            direction = BlueSide
                ? Vector2.right
                : Vector2.left;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Vector2 boxCenter = currentPosition + direction * (SlingerAllyCheckDistance * 0.5f);
        Vector2 boxSize = new Vector2(SlingerAllyCheckDistance, SlingerAllyCheckRadius * 2f);

        Gizmos.color = Color.cyan;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(boxCenter, Quaternion.Euler(0, 0, angle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxSize.x, boxSize.y, 0.1f));
        Gizmos.matrix = oldMatrix;

        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }
}
