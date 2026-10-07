using UnityEngine;

/// <summary>
/// Loại hiệu ứng VFX. Giá trị số được gửi nguyên qua mạng (xem <see cref="NetworkGame.PublishVfx"/>)
/// nên KHÔNG đổi số của các mục đã có, chỉ thêm mục mới ở cuối.
/// </summary>
public enum VfxType
{
    SpawnFlash = 0,
    WinPoof = 1,
    DeathSmoke = 2,
    LoseLeaves = 3,
}

/// <summary>
/// HỆ THỐNG VFX của trận đấu - nơi duy nhất biết cách sinh một hiệu ứng Cartoon FX Remaster (CFXR) ra
/// sân. Các prefab hiệu ứng nằm trong asset <see cref="VfxConfig"/> (Assets/Resources/VfxConfig.asset)
/// nên hệ thống TỰ NẠP lúc chạy - không cần gắn gì vào scene, dùng chung cho cả chơi đơn lẫn
/// MultiPlayer.
///
/// 4 sự kiện và hiệu ứng tương ứng:
///   - <see cref="PlaySpawnFlash"/> : một lính vừa được sinh ra  -> CFXR Flash.
///   - <see cref="PlayWinPoof"/>    : người chơi THẮNG            -> CFXR Magic Poof (giữa màn hình).
///   - <see cref="PlayDeathSmoke"/> : một lính bị hạ gục          -> CFXR3 Hit Misc F Smoke.
///   - <see cref="PlayLoseLeaves"/> : người chơi THUA             -> CFXR3 Hit Leaves A (Lit) (giữa màn hình).
///
/// ĐỒNG BỘ MẠNG: lính do HOST sinh ra (client tắt component Slime nên không tự chạy hiệu ứng spawn/
/// chết). Vì vậy <see cref="PlaySpawnFlash"/> và <see cref="PlayDeathSmoke"/> phát NGAY Ở MÁY NÀY rồi
/// nhờ host gửi sang client (xem <see cref="PlayNetworked"/> + <c>NetworkGame.PublishVfx</c>) để hai
/// máy cùng thấy. Ngược lại win/lose do MỖI MÁY tự quyết theo kết quả của mình nên chỉ phát cục bộ.
///
/// Chưa có asset VfxConfig (hoặc ô prefab để trống) thì các hàm chỉ "im lặng", game không lỗi.
/// </summary>
public static class VfxManager
{
    /// <summary>Tên asset cấu hình trong thư mục Resources (Assets/Resources/VfxConfig.asset).</summary>
    private const string ConfigResourcePath = "VfxConfig";

    /// <summary>Thời lượng dự phòng (giây) khi prefab không có ParticleSystem nào để đo.</summary>
    private const float FallbackLifetime = 5f;

    private static VfxConfig config;
    private static bool warnedMissingConfig;

    /// <summary>
    /// Phát CFXR Flash khi một lính VỪA ĐƯỢC SINH RA, tại <paramref name="worldPosition"/> (điểm ra
    /// lính). Gọi ở <c>Slime.PlaySpawnPopEffect</c> và <c>Catapult.PlayVehicleSpawnEffect</c> nên phủ
    /// mọi loại lính, cả 2 phe. Có đồng bộ sang client trong MultiPlayer.
    /// </summary>
    public static void PlaySpawnFlash(Vector3 worldPosition)
    {
        PlayNetworked(VfxType.SpawnFlash, worldPosition);
    }

    /// <summary>
    /// Phát CFXR3 Hit Misc F Smoke khi một lính BỊ HẠ GỤC, tại vị trí lính (gọi ở <c>Slime.Die</c>,
    /// trừ King). Có đồng bộ sang client trong MultiPlayer.
    /// </summary>
    public static void PlayDeathSmoke(Vector3 worldPosition)
    {
        PlayNetworked(VfxType.DeathSmoke, worldPosition);
    }

    /// <summary>Phát CFXR Magic Poof khi người chơi THẮNG (đặt ở giữa màn hình). Chỉ phát cục bộ.</summary>
    public static void PlayWinPoof(Vector3 worldPosition)
    {
        PlayLocal(VfxType.WinPoof, worldPosition);
    }

    /// <summary>Phát CFXR3 Hit Leaves A (Lit) khi người chơi THUA (đặt ở giữa màn hình). Chỉ phát cục bộ.</summary>
    public static void PlayLoseLeaves(Vector3 worldPosition)
    {
        PlayLocal(VfxType.LoseLeaves, worldPosition);
    }

    /// <summary>
    /// Phát hiệu ứng Ở MÁY NÀY rồi (nếu là host) gửi sang client để máy kia cũng thấy - dùng cho các
    /// hiệu ứng gắn với vòng đời lính (spawn/chết) vốn chỉ được mô phỏng ở host.
    /// </summary>
    public static void PlayNetworked(VfxType type, Vector3 worldPosition)
    {
        PlayLocal(type, worldPosition);

        // Chơi đơn: NetworkGame không tồn tại -> bỏ qua. MultiPlayer: chỉ host gửi (client không sinh lính).
        NetworkGame.Instance?.PublishVfx((int)type, worldPosition);
    }

    /// <summary>
    /// Phát hiệu ứng CHỈ Ở MÁY NÀY. Dùng cho win/lose (mỗi máy tự phát theo kết quả của mình) và cho
    /// RPC nhận từ host (tránh gửi ngược lại gây lặp hiệu ứng).
    /// </summary>
    public static void PlayLocal(VfxType type, Vector3 worldPosition)
    {
        GameObject prefab = GetPrefab(type);
        if (prefab == null) return;

        Spawn(prefab, worldPosition);
    }

    /// <summary>Phát hiệu ứng theo chỉ số loại (dùng cho RPC vì enum đi qua mạng dưới dạng int).</summary>
    public static void PlayById(int vfxType, Vector3 worldPosition)
    {
        PlayLocal((VfxType)vfxType, worldPosition);
    }

    /// <summary>
    /// Vị trí GIỮA MÀN HÌNH trên mặt phẳng chơi (z = 0) - chỗ đặt hiệu ứng win/lose. Tính từ
    /// <c>Camera.main</c> (camera ngang của màn chơi) nên đúng cả khi người chơi đã pan/zoom.
    /// Không có camera thì trả về gốc toạ độ để không lỗi.
    /// </summary>
    public static Vector3 ScreenCenterWorldPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;

        // Với camera 2D (orthographic) đặt lùi khỏi mặt phẳng chơi một quãng |z|, "khoảng cách" đúng
        // để điểm chiếu rơi xuống z = 0 chính là |z| của camera.
        float depth = Mathf.Abs(cam.transform.position.z);
        return cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
    }

    // ==========================================
    // NỘI BỘ
    // ==========================================

    private static GameObject GetPrefab(VfxType type)
    {
        VfxConfig cfg = GetConfig();
        if (cfg == null) return null;

        switch (type)
        {
            case VfxType.SpawnFlash: return cfg.spawnFlash;
            case VfxType.WinPoof: return cfg.winPoof;
            case VfxType.DeathSmoke: return cfg.deathSmoke;
            case VfxType.LoseLeaves: return cfg.loseLeaves;
            default: return null;
        }
    }

    /// <summary>
    /// Nạp asset cấu hình từ Resources MỘT LẦN rồi cache. Chưa có asset thì cảnh báo đúng một lần và
    /// mọi hiệu ứng "im lặng" cho tới khi asset xuất hiện.
    /// </summary>
    private static VfxConfig GetConfig()
    {
        if (config != null) return config;

        config = Resources.Load<VfxConfig>(ConfigResourcePath);
        if (config == null && !warnedMissingConfig)
        {
            warnedMissingConfig = true;
            Debug.LogWarning(
                $"VfxManager: không tìm thấy '{ConfigResourcePath}' trong Resources " +
                "(Assets/Resources/VfxConfig.asset) - các hiệu ứng VFX sẽ không hiện.");
        }

        return config;
    }

    /// <summary>
    /// Sinh một prefab hiệu ứng tại vị trí cho trước (không gắn vào vật thể nào để không bị co giãn
    /// theo). Prefab CFXR tự huỷ khi chạy xong (CFXR_Effect.clearBehavior = Destroy); thêm lưới an
    /// toàn theo thời lượng hạt để prefab KHÁC (không phải CFXR) cũng không bị kẹt lại trên sân.
    /// </summary>
    private static void Spawn(GameObject prefab, Vector3 worldPosition)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab, worldPosition, Quaternion.identity);
        if (instance == null) return;

        UnityEngine.Object.Destroy(instance, EstimateLifetime(instance));
    }

    /// <summary>
    /// Ước lượng thời gian sống của một hiệu ứng = vòng chạy lâu nhất + hạn dài nhất của hạt, cộng
    /// thêm một nhịp nhỏ cho chắc. Không có ParticleSystem nào thì dùng mức dự phòng.
    /// </summary>
    private static float EstimateLifetime(GameObject instance)
    {
        float longest = 0f;

        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null) continue;

            float total = ps.main.duration + ps.main.startLifetime.constantMax;
            if (total > longest) longest = total;
        }

        return (longest > 0f) ? (longest + 0.5f) : FallbackLifetime;
    }
}
