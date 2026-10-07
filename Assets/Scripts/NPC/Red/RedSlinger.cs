using UnityEngine;

/// <summary>
/// Súng cao su phe đỏ (do AI điều khiển).
/// Kế thừa <see cref="Slinger"/> của phe xanh và chỉ lật phe + tên hiển thị.
/// Lưu ý: bulletSpawnOffset trong prefab đỏ đã bị lật dấu X vì sprite đỏ quay sang trái.
/// </summary>
public class RedSlinger : Slinger
{
    protected override void Start()
    {
        // Phải set phe TRƯỚC base.Start() để hướng mặc định (Destination) và
        // hướng kiểm tra đồng minh đi sang trái.
        blueSide = false;

        base.Start();

        // GameManager.soldierDown() đếm quân theo characterName.
        characterName = "RedSlinger";
    }
}
