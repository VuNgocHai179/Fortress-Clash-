using UnityEngine;
using DG.Tweening;

public class BaseSlime : Slime
{
    protected bool isAttacking = false;
    private Animator animator;

    protected override void Start()
    {
        base.Start();
        characterName = blueSide ? "BlueBaseSlime" : "RedBaseSlime";
        price = 10;
        health = 45;
        damage = 10;
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
            return;
        }

        FindTarget();

        if (target != null)
        {
            // Để cú nhảy đang dở đáp xuống đất rồi mới đứng lại đánh
            // (Pause giữa chừng sẽ làm lính treo lơ lửng trên không trung).
            isMoving = false;

            StartAttack();
            return;
        }

        base.Update();
    }

    // Tìm mục tiêu & kiểm tra tầm đánh dùng chung logic đo trên collider ở class cha Slime,
    // để mọi loại lính đều dừng đúng tầm đánh mà không đè lên nhau.

    protected virtual void StartAttack()
    {
        if (isAttacking) return;
        isAttacking = true;

        AudioManager.PlayAttack();   // tiếng tấn công (mọi loại lính, cả 2 phe)

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
}