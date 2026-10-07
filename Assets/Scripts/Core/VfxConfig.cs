using UnityEngine;

/// <summary>
/// Cấu hình HỆ THỐNG VFX của trận đấu: 4 prefab hiệu ứng (Cartoon FX Remaster) dùng cho 4 sự kiện
/// trong game - xem <see cref="VfxManager"/> để biết chỗ phát.
///
/// Asset này nằm ở <c>Assets/Resources/VfxConfig.asset</c> nên <see cref="VfxManager"/> tự nạp lúc
/// chạy (Resources.Load) - KHÔNG cần gắn gì vào scene, cả chơi đơn lẫn MultiPlayer đều có hiệu ứng.
/// Muốn đổi prefab thì chọn asset này trong Project và sửa ở Inspector.
/// </summary>
[CreateAssetMenu(fileName = "VfxConfig", menuName = "Fortress Clash/VFX Config")]
public class VfxConfig : ScriptableObject
{
    [Tooltip("CFXR Flash - hiệu ứng khi một lính VỪA ĐƯỢC SINH RA (mọi loại lính, cả 2 phe).")]
    public GameObject spawnFlash;

    [Tooltip("CFXR Magic Poof - hiệu ứng khi NGƯỜI CHƠI THẮNG, phát ở giữa màn hình lúc hiện popup kết quả.")]
    public GameObject winPoof;

    [Tooltip("CFXR3 Hit Misc F Smoke - hiệu ứng khi một lính BỊ HẠ GỤC (mọi loại lính, cả 2 phe; không tính King).")]
    public GameObject deathSmoke;

    [Tooltip("CFXR3 Hit Leaves A (Lit) - hiệu ứng khi NGƯỜI CHƠI THUA, phát ở giữa màn hình lúc hiện popup kết quả.")]
    public GameObject loseLeaves;
}
