using UnityEngine;

/// <summary>
/// Slime cơ bản phe đỏ (do AI điều khiển).
/// Kế thừa <see cref="BaseSlime"/> của phe xanh và chỉ lật phe + tên hiển thị.
/// </summary>
public class RedBaseSlime : BaseSlime
{
    protected override void Start()
    {
        // Phải set phe TRƯỚC base.Start() để hướng mặc định (Destination) và
        // hướng kiểm tra đồng minh đi sang trái.
        blueSide = false;

        base.Start();

        // GameManager.soldierDown() đếm quân theo characterName.
        characterName = "RedBaseSlime";
    }
}
