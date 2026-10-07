using UnityEngine;

/// <summary>
/// Lính giáo phe đỏ (do AI điều khiển).
/// Kế thừa <see cref="SpearMan"/> của phe xanh và chỉ lật phe + tên hiển thị,
/// nhờ vậy dùng chung toàn bộ logic di chuyển, tìm mục tiêu và tấn công.
/// </summary>
public class RedSpearMan : SpearMan
{
    protected override void Start()
    {
        // Phải set phe TRƯỚC base.Start() để hướng mặc định (Destination) và
        // hướng kiểm tra đồng minh đi sang trái.
        blueSide = false;

        base.Start();

        // GameManager.soldierDown() đếm quân theo characterName.
        characterName = "RedSpearMan";
    }
}
