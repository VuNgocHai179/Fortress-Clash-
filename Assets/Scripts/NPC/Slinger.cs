using UnityEngine;

public class Slinger : Archer
{
    [Header("Slinger Skirmisher Settings")]
    public float SlingerAllyCheckDistance = 1.2f;
    public float SlingerAllyCheckRadius = 0.5f;

    private void Reset()
    {
        AttackRange = 4.5f;
        Health = 25;
        Damage = 8;
        Price = 15;
        Velocity = 3.5f;
        AllyCheckDistance = 0.7f;
        AllyCheckRadius = 0.25f;
        ArcherAllyCheckDistance = 1.2f;
        ArcherAllyCheckRadius = 0.5f;
        SlingerAllyCheckDistance = 1.2f;
        SlingerAllyCheckRadius = 0.5f;
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
            SlingerAllyCheckDistance = 1.2f;
        }

        if (SlingerAllyCheckRadius <= 0f)
        {
            SlingerAllyCheckRadius = 0.5f;
        }

        // Synchronize skirmisher ally check parameters with base Archer
        ArcherAllyCheckDistance = SlingerAllyCheckDistance;
        ArcherAllyCheckRadius = SlingerAllyCheckRadius;
    }

    private void OnValidate()
    {
        if (SlingerAllyCheckDistance <= 0f)
        {
            SlingerAllyCheckDistance = 1.2f;
        }

        if (SlingerAllyCheckRadius <= 0f)
        {
            SlingerAllyCheckRadius = 0.5f;
        }

        ArcherAllyCheckDistance = SlingerAllyCheckDistance;
        ArcherAllyCheckRadius = SlingerAllyCheckRadius;
    }

    protected override bool ArcherHasAllyInFront()
    {
        ArcherAllyCheckDistance = SlingerAllyCheckDistance;
        ArcherAllyCheckRadius = SlingerAllyCheckRadius;
        return base.ArcherHasAllyInFront();
    }

    protected override void OnDrawGizmosSelected()
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
            direction * SlingerAllyCheckDistance;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            checkPosition,
            SlingerAllyCheckRadius
        );

        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }
}
