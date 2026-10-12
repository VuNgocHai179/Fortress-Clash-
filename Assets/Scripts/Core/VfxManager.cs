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

    /// <summary>Kỹ năng Thiên thạch rơi: phát tại điểm rơi (xem AbilityBar).</summary>
    AbilityImpact = 4,
}

/// <summary>
/// HỆ THỐNG VFX của trận đấu - nơi duy nhất biết cách sinh một hiệu ứng Cartoon FX Remaster (CFXR) ra
/// sân. Các prefab hiệu ứng nằm trong asset <see cref="VfxConfig"/> (Assets/Resources/VfxConfig.asset)
/// nên hệ thống TỰ NẠP lúc chạy - không cần gắn gì vào scene, dùng chung cho cả chơi đơn lẫn
/// MultiPlayer.
///
/// Các sự kiện và hiệu ứng tương ứng:
///   - <see cref="PlaySpawnFlash"/>   : một lính vừa được sinh ra   -> CFXR Flash.
///   - <see cref="PlayWinPoof"/>      : người chơi THẮNG            -> CFXR Magic Poof (giữa màn hình).
///   - <see cref="PlayDeathSmoke"/>   : một lính bị hạ gục          -> CFXR3 Hit Misc F Smoke.
///   - <see cref="PlayLoseLeaves"/>   : người chơi THUA             -> CFXR3 Hit Leaves A (Lit) (giữa màn hình).
///   - <see cref="PlayAbilityImpact"/>: kỹ năng Thiên thạch rơi       -> prefab ở VfxConfig.abilityImpact.
///   - <see cref="PlayMeteorStrike"/> : kỹ năng Thiên thạch           -> đợt thiên thạch rơi từ trên trời
///     xuống (art ở VfxConfig.meteor.sprite), <see cref="MeteorStrike"/>; máu trừ lúc viên đầu chạm đất.
///   - <see cref="PlayBuffAura"/>     : kỹ năng Hiệu triệu (buff)     -> aura VẼ BẰNG CODE bám theo lính,
///     không dùng prefab nên không nằm trong VfxConfig - xem <see cref="SlimeBuffAura"/>.
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
        PlayResultEffect(VfxType.WinPoof, worldPosition);
    }

    /// <summary>Phát CFXR3 Hit Leaves A (Lit) khi người chơi THUA (đặt ở giữa màn hình). Chỉ phát cục bộ.</summary>
    public static void PlayLoseLeaves(Vector3 worldPosition)
    {
        PlayResultEffect(VfxType.LoseLeaves, worldPosition);
    }

    /// <summary>
    /// Hiệu ứng WIN/LOSE: giống <see cref="PlayLocal"/> nhưng hạt chạy bằng THỜI GIAN KHÔNG BỊ ĐÓNG BĂNG.
    ///
    /// Lý do: khi popup kết quả hiện, <c>DlgResult</c> đặt <c>Time.timeScale = 0</c> để đóng băng trận.
    /// Hạt dùng thời gian có scale sẽ đứng im ngay frame đầu (chưa kịp hiện gì) nên phải bật
    /// <c>useUnscaledTime</c> cho đúng 2 hiệu ứng này - xem <see cref="UseUnscaledParticleTime"/>.
    /// </summary>
    private static void PlayResultEffect(VfxType type, Vector3 worldPosition)
    {
        GameObject prefab = GetPrefab(type);
        if (prefab == null) return;

        Spawn(prefab, worldPosition, useUnscaledTime: true);
    }

    /// <summary>
    /// Phát hiệu ứng THIÊN THẠCH RƠI của kỹ năng chủ động tại điểm rơi (xem <c>GameManager</c>).
    ///
    /// Đây chỉ là phần HÌNH ẢNH: phía gọi nói rõ máy này có trừ máu hay không (xem
    /// <paramref name="applyDamage"/>) nên hàm này dùng được cho cả host lẫn client trong MultiPlayer.
    /// </summary>
    /// <param name="casterIsBlue">Phe đã dùng kỹ năng - quyết định hướng rơi và chiều lật ảnh.</param>
    /// <param name="applyDamage">true = trừ máu khi từng viên chạm đất (host / chơi đơn); false = chỉ hình ảnh.</param>
    public static bool PlayMeteorStrike(Vector3 center, float radius, int damage, bool casterIsBlue = true,
        bool applyDamage = true)
    {
        VfxConfig config = GetConfig();
        VfxConfig.MeteorStrikeSettings settings = (config != null) ? config.meteor : null;

        return MeteorStrike.Spawn(settings, center, radius, damage, casterIsBlue, applyDamage);
    }

    /// <summary>
    /// Phát hiệu ứng NỔ của kỹ năng Thiên thạch (prefab CFXR ở <c>VfxConfig.abilityImpact</c>) tại điểm rơi.
    /// Chỉ phát CỤC BỘ ở máy gọi: mỗi máy tự phát khi viên đầu chạm đất (xem <c>MeteorStrike</c>) nên không
    /// cần đồng bộ qua mạng.
    /// </summary>
    public static void PlayAbilityImpact(Vector3 worldPosition)
    {
        PlayLocal(VfxType.AbilityImpact, worldPosition);
    }

    /// <summary>
    /// Bật VFX AURA BÁM THEO cho một lính vừa nhận buff GIẢM SÁT THƯƠNG (kỹ năng Hiệu triệu - xem
    /// <see cref="SlimeBuffAura"/>).
    ///
    /// Khác mọi hàm còn lại ở đây: hiệu ứng này KHÔNG phải prefab dùng-một-lần mà sống bám theo con lính
    /// đúng bằng thời gian buff, nên nó được cài trong component <see cref="SlimeBuffAura"/> chứ không nằm
    /// trong VfxConfig. Hàm này chỉ là cửa vào cho gọn - mọi hiệu ứng đều gọi qua VfxManager.
    /// </summary>
    public static void PlayBuffAura(Slime slime)
    {
        SlimeBuffAura.Attach(slime);
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
            case VfxType.AbilityImpact: return cfg.abilityImpact;
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
    /// <param name="useUnscaledTime">
    /// true = cho hạt chạy bằng thời gian không bị timeScale ảnh hưởng - dùng cho hiệu ứng WIN/LOSE
    /// (lúc đó trận đã đóng băng, xem <see cref="PlayResultEffect"/>).
    /// </param>
    private static void Spawn(GameObject prefab, Vector3 worldPosition, bool useUnscaledTime = false)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab, worldPosition, Quaternion.identity);
        if (instance == null) return;

        if (useUnscaledTime) UseUnscaledParticleTime(instance);

        UnityEngine.Object.Destroy(instance, EstimateLifetime(instance));
    }

    /// <summary>
    /// Cho MỌI ParticleSystem của một hiệu ứng chạy bằng thời gian KHÔNG bị timeScale ảnh hưởng, rồi
    /// phát lại từ đầu: đổi cờ unscaled giữa chừng không ăn với những hạt đã sinh ra nên phải
    /// <c>Stop</c> + <c>Play</c> lại (vừa Instantiate nên chưa mất gì).
    /// </summary>
    private static void UseUnscaledParticleTime(GameObject instance)
    {
        foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particles == null) continue;

            // Phải gán qua struct MainModule thì giá trị mới được ghi ngược vào hệ hạt.
            ParticleSystem.MainModule main = particles.main;
            main.useUnscaledTime = true;

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play();
        }
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
