using UnityEngine;

/// <summary>
/// HIỆU ỨNG THIÊN THẠCH RƠI của kỹ năng chủ động (nút btn_meteor - xem <see cref="AbilityBar"/>).
///
/// VÌ SAO CẦN: Thiên thạch là đòn đánh vào chỗ ĐÔNG lính đỏ nhất, nhưng trước đây chỉ có một nhịp VFX ngay
/// tại điểm rơi nên người chơi không thấy được "từ trên trời giáng xuống". Component này cho cả MỘT ĐỢT
/// thiên thạch rơi từ ngoài màn hình xuống vùng nổ, rồi mới trừ máu.
///
/// ART: lấy ở <c>VfxConfig.meteor.sprite</c> (Assets/Textures/meteor.png). Ảnh đó đã vẽ SẴN đuôi lửa chếch
/// lên phải nên ở đây KHÔNG vẽ thêm vệt lửa - chỉ LẬT ảnh (mirrorSprite) cho đuôi hướng về phía nó bay tới
/// rồi xoay theo <c>tailAngleDegrees</c>. Đầu đá cũng LỆCH khỏi tâm ảnh nên phải bù bằng <c>headOffset</c>,
/// nhờ vậy ĐẦU ĐÁ chạm đúng điểm nổ chứ không phải tâm ảnh.
///
/// HƯỚNG RƠI: phe XANH (người chơi) đứng bên TRÁI map nên thiên thạch bay từ trên cao bên TRÁI chéo xuống
/// phải - tức là từ phía BÊN GỌI RA (xem <c>comesFromBlueSide</c>). Nhờ LẬT ảnh mà viên đá vẫn gần như
/// thẳng đứng thay vì bị xoay gần 80 độ.
///
/// SÁT THƯƠNG: MỖI viên gây sát thương một lần, đúng lúc nó chạm đất
/// (<see cref="GameManager.ApplyAreaDamage"/>) nên nhìn khớp - thiên thạch đập xuống rồi lính mới mất máu.
/// Một lính nằm giữa vùng nổ có thể ăn nhiều viên (tối đa bằng số viên), nên đợt này nặng hơn hẳn một cú
/// đánh đơn. Chưa gán art thì <see cref="VfxManager.PlayMeteorStrike"/> trả về false và phía gọi trừ máu
/// ngay như trước - kỹ năng không bao giờ mất tiền mà vô hiệu.
///
/// Cách rải viên: viên 0 rơi ĐÚNG mục tiêu (con lính tuyến đầu của phe địch - xem
/// <c>GameManager.ResolveMeteorTarget</c>), nhóm <c>concentratedCount</c> viên đầu dồn SÁT mục tiêu, phần
/// còn lại rải theo ĐƯỜNG XOẮN ỐC GÓC VÀNG ra tới <c>spreadScale</c> bán kính cho vùng nổ rộng ra. Cố ý
/// KHÔNG dùng random nên lần bấm nào nhìn cũng gọn và lặp lại được.
///
/// PHE: hướng rơi / chiều lật ảnh được tính từ phe ĐÃ DÙNG kỹ năng (<c>casterIsBlue</c>) nên ở MultiPlayer,
/// người chơi phe đỏ cũng thấy thiên thạch bay từ phía mình sang (bản gốc trong <see cref="VfxConfig"/> mô
/// tả hướng của phe XANH, phe đỏ lấy bản lật lại).
///
/// SÁT THƯƠNG Ở MULTIPLAYER: chỉ MÁY CHỦ (host) trừ máu - máy khác chỉ chạy HÌNH ẢNH
/// (<c>applyDamage = false</c>), nhờ vậy đòn đánh không bị tính hai lần mà cả 2 máy vẫn thấy đúng cảnh.
/// </summary>
public class MeteorStrike : MonoBehaviour
{
    /// <summary>Một viên đang rơi: đường bay, mốc thời gian và ảnh của nó.</summary>
    private class FallingMeteor
    {
        public Transform root;
        public SpriteRenderer sprite;
        public Vector3 start;
        public Vector3 landing;
        public float startTime;

        /// <summary>Viên này đã gây sát thương chưa - mỗi viên chỉ đánh MỘT lần, đúng lúc nó chạm đất.</summary>
        public bool damageApplied;
    }

    /// <summary>Vụ nổ lúc một viên chạm đất: ánh sáng loé + vòng sóng lan ra.</summary>
    private class ImpactBurst
    {
        public Transform glow;
        public Transform ring;
        public SpriteRenderer glowRenderer;
        public SpriteRenderer ringRenderer;
        public float startTime;
        public float baseScale;
    }

    /// <summary>Thời gian một vụ nổ loé lên rồi tắt (giây).</summary>
    private const float ImpactSeconds = 0.3f;

    /// <summary>Cỡ texture của 2 sprite vẽ bằng code cho vụ nổ. Sprite tạo ra rộng ĐÚNG 1 world unit.</summary>
    private const int TextureSize = 96;

    /// <summary>Góc vàng: dùng để rải viên theo đường xoắn ốc cho đều khắp vùng nổ.</summary>
    private const float GoldenAngle = 2.39996323f;

    private static Sprite glowSprite;
    private static Sprite ringSprite;

    private VfxConfig.MeteorStrikeSettings settings;
    private FallingMeteor[] meteors;
    private ImpactBurst[] bursts;

    private Vector3 center;
    private float radius;
    private int damage;

    /// <summary>Phe ĐÃ DÙNG kỹ năng (true = xanh) - quyết định hướng rơi, chiều lật ảnh và phe ăn đòn.</summary>
    private bool casterIsBlue = true;

    /// <summary>Máy này có trừ máu không. Host (và chơi đơn) = true; máy khác chỉ chạy hình ảnh.</summary>
    private bool applyDamage = true;

    /// <summary>Đồng hồ của hiệu ứng (Time.deltaTime) nên tự đứng im khi mở popup tạm dừng.</summary>
    private float elapsed;

    private float totalSeconds;

    /// <summary>
    /// Phát một đợt thiên thạch rơi quanh <paramref name="center"/>. Trả về false nếu chưa gán art thiên thạch
    /// (VfxConfig.meteor.sprite) - lúc đó phía gọi tự trừ máu ngay.
    /// </summary>
    /// <param name="casterIsBlue">Phe dùng kỹ năng - đổi hướng rơi và chiều lật ảnh cho khớp.</param>
    /// <param name="applyDamage">
    /// true = trừ máu khi từng viên chạm đất (host / chơi đơn). false = CHỈ chạy hình ảnh - dùng ở máy
    /// client, nơi sát thương đã do host áp và đồng bộ sang.
    /// </param>
    public static bool Spawn(VfxConfig.MeteorStrikeSettings settings, Vector3 center, float radius, int damage,
        bool casterIsBlue = true, bool applyDamage = true)
    {
        if (settings == null || settings.sprite == null) return false;

        GameObject host = new GameObject("MeteorStrike");
        MeteorStrike strike = host.AddComponent<MeteorStrike>();
        strike.Build(settings, center, Mathf.Max(0.5f, radius), Mathf.Max(1, damage), casterIsBlue, applyDamage);

        return true;
    }

    private void Build(VfxConfig.MeteorStrikeSettings settings, Vector3 center, float radius, int damage,
        bool casterIsBlue, bool applyDamage)
    {
        this.settings = settings;
        this.center = center;
        this.radius = radius;
        this.damage = damage;
        this.casterIsBlue = casterIsBlue;
        this.applyDamage = applyDamage;

        // Object chứa hiệu ứng đứng nguyên ở gốc toạ độ để mọi thứ bên trong đặt thẳng theo toạ độ world.
        transform.position = Vector3.zero;

        Vector2 spriteSize = settings.sprite.bounds.size;                   // world unit theo PPU của ảnh
        float spriteScale = Mathf.Max(0.01f, settings.spriteWidth) / Mathf.Max(0.01f, spriteSize.x);
        Vector2 scaledSize = spriteSize * spriteScale;

        // Bộ số trong VfxConfig mô tả hướng của PHE XANH. Phe đỏ lấy bản LẬT LẠI: vừa đổi phía bay tới, vừa
        // lật ảnh. Nhờ lật ảnh mà viên đá vẫn gần như thẳng đứng, chỉ phải xoay một góc nhỏ; lật ảnh thì toạ
        // độ NGANG của đầu đá cũng đổi dấu theo.
        bool mirrored = !casterIsBlue;
        bool fromBlueSide = settings.comesFromBlueSide ^ mirrored;
        bool mirrorSprite = settings.mirrorSprite ^ mirrored;

        float mirror = mirrorSprite ? -1f : 1f;

        // Đầu đá lệch khỏi tâm ảnh -> bù để ĐẦU ĐÁ nằm đúng trên đường bay (xem headOffset ở VfxConfig).
        Vector3 headOffset = new Vector3(settings.headOffset.x * scaledSize.x * mirror,
            settings.headOffset.y * scaledSize.y, 0f);

        Vector3 travel = TravelDirection(settings, fromBlueSide);
        Quaternion tilt = Quaternion.Euler(0f, 0f, TravelTiltDegrees(settings, travel, mirrorSprite));
        float fallSeconds = Mathf.Max(0.05f, settings.fallSeconds);
        float stagger = Mathf.Max(0f, settings.staggerSeconds);
        float startDistance = StartDistance(center, travel, scaledSize.y, radius);

        int count = Mathf.Max(1, settings.count);
        meteors = new FallingMeteor[count];
        bursts = new ImpactBurst[count];

        for (int i = 0; i < count; i++)
        {
            Vector3 landing = LandingPoint(center, radius, settings, i, count);
            float startTime = i * stagger;

            meteors[i] = CreateMeteor(i, settings, spriteScale, mirror, tilt, headOffset,
                landing - (travel * startDistance), landing, startTime);

            // Vụ nổ của viên này bùng lên đúng lúc nó chạm đất.
            bursts[i] = CreateBurst(i, settings, landing, startTime + fallSeconds);
        }

        totalSeconds = ((count - 1) * stagger) + fallSeconds + ImpactSeconds + 0.05f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float fallSeconds = Mathf.Max(0.05f, settings.fallSeconds);

        for (int i = 0; i < meteors.Length; i++)
        {
            AnimateMeteor(meteors[i], fallSeconds);
            AnimateBurst(bursts[i]);

            // Trừ máu theo TỪNG VIÊN, đúng lúc viên đó chạm đất.
            ApplyLandingDamage(i, fallSeconds);
        }

        if (elapsed >= totalSeconds) Destroy(gameObject);
    }

    /// <summary>
    /// Gây sát thương cho một viên vừa chạm đất. MỖI viên đánh MỘT lần, quanh CHÍNH điểm rơi của nó, với
    /// bán kính của kỹ năng - nên lính nằm giữa vùng nổ có thể ăn nhiều viên và cả đợt nặng hơn hẳn một cú
    /// đánh đơn (đó là điều người chơi mong đợi khi thấy cả một trận mưa thiên thạch).
    /// </summary>
    private void ApplyLandingDamage(int index, float fallSeconds)
    {
        FallingMeteor meteor = meteors[index];
        if (meteor.damageApplied) return;
        if (elapsed < meteor.startTime + fallSeconds) return;

        meteor.damageApplied = true;

        // Hiệu ứng CFXR cũ (nếu có gán vào VfxConfig.abilityImpact) chỉ phát ở VIÊN CHÍNH, không phát 5 lần
        // chồng lên nhau. Phát ở MỌI máy (kể cả máy chỉ chạy hình ảnh) để cả hai bên thấy vụ nổ.
        if (index == 0) VfxManager.PlayAbilityImpact(center);

        // Máy chỉ chạy hình ảnh (client trong MultiPlayer) không trừ máu: sát thương do HOST áp rồi đồng bộ.
        if (!applyDamage) return;

        GameManager gameManager = GameManager.Instance;
        if (gameManager != null) gameManager.ApplyAreaDamage(!casterIsBlue, meteor.landing, radius, damage);
    }

    // ==========================================
    // NHỊP RƠI / NHỊP NỔ
    // ==========================================

    private void AnimateMeteor(FallingMeteor meteor, float fallSeconds)
    {
        float t = (elapsed - meteor.startTime) / fallSeconds;

        // Chưa tới lượt hoặc đã chạm đất: ẩn viên đá (lúc chạm đất thì vụ nổ đã loé lên thay chỗ nó).
        if (t < 0f || t >= 1f)
        {
            if (meteor.sprite.enabled) meteor.sprite.enabled = false;
            return;
        }

        float accelerate = Mathf.Pow(t, 1.35f);        // rơi nhanh dần chứ không đều đều

        meteor.root.localPosition = Vector3.Lerp(meteor.start, meteor.landing, accelerate);
        meteor.sprite.enabled = true;

        // Hiện dần ở đoạn đầu hành trình (viên đá xuất hiện phía ngoài màn hình nên không bị "bật" đột ngột).
        meteor.sprite.color = WithAlpha(Color.white, Mathf.Clamp01(t / 0.1f));
    }

    private void AnimateBurst(ImpactBurst burst)
    {
        float t = (elapsed - burst.startTime) / ImpactSeconds;

        if (t < 0f || t >= 1f)
        {
            if (burst.glowRenderer.enabled) burst.glowRenderer.enabled = false;
            if (burst.ringRenderer.enabled) burst.ringRenderer.enabled = false;
            return;
        }

        float expand = Mathf.Sqrt(t);                  // loé ra thật nhanh rồi chậm dần (kiểu sóng xung kích)
        float fade = 1f - t;

        burst.glowRenderer.enabled = true;
        burst.ringRenderer.enabled = true;

        burst.glow.localScale = Vector3.one * burst.baseScale * Mathf.Lerp(0.5f, 2.1f, expand);
        burst.glowRenderer.color = WithAlpha(settings.impactColor, settings.impactColor.a * fade * fade);

        burst.ring.localScale = Vector3.one * burst.baseScale * Mathf.Lerp(0.3f, 1.9f, expand);
        burst.ringRenderer.color = WithAlpha(settings.impactColor, settings.impactColor.a * fade);
    }

    // ==========================================
    // DỰNG CÁC MẢNH HIỆU ỨNG
    // ==========================================

    private FallingMeteor CreateMeteor(int index, VfxConfig.MeteorStrikeSettings settings, float spriteScale,
        float mirror, Quaternion tilt, Vector3 headOffset, Vector3 start, Vector3 landing, float startTime)
    {
        GameObject rootObject = new GameObject($"meteor_{index}");
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = start;

        GameObject spriteObject = new GameObject("sprite");
        spriteObject.transform.SetParent(rootObject.transform, false);
        spriteObject.transform.localRotation = tilt;

        // Dịch ảnh ngược lại đúng khoảng lệch của đầu đá, ĐÃ XOAY theo góc nghiêng của viên đá: nhờ vậy ĐẦU
        // ĐÁ nằm đúng vị trí của root (thiếu bước xoay này thì đầu đá lệch khỏi điểm rơi khoảng 0,3-0,4 unit).
        spriteObject.transform.localPosition = -(tilt * headOffset);

        // mirror = -1: lật ảnh theo chiều ngang (x âm) để đuôi lửa hướng về phía viên đá bay tới.
        spriteObject.transform.localScale = new Vector3(spriteScale * mirror, spriteScale, 1f);

        SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = settings.sprite;
        spriteRenderer.sortingOrder = settings.sortingOrder;
        spriteRenderer.enabled = false;

        return new FallingMeteor
        {
            root = rootObject.transform,
            sprite = spriteRenderer,
            start = start,
            landing = landing,
            startTime = startTime
        };
    }

    private ImpactBurst CreateBurst(int index, VfxConfig.MeteorStrikeSettings settings, Vector3 landing, float startTime)
    {
        GameObject rootObject = new GameObject($"impact_{index}");
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = landing;

        // Ánh sáng loé và vòng sóng đều vẽ bằng code, cỡ tính theo bán kính kỹ năng để vùng nổ to thì vụ nổ
        // cũng to theo.
        SpriteRenderer glow = CreateBurstLayer(rootObject.transform, "glow", GetGlowSprite(), settings.sortingOrder);
        SpriteRenderer ring = CreateBurstLayer(rootObject.transform, "ring", GetRingSprite(), settings.sortingOrder);

        return new ImpactBurst
        {
            glow = glow.transform,
            ring = ring.transform,
            glowRenderer = glow,
            ringRenderer = ring,
            startTime = startTime,
            baseScale = radius * settings.impactScale
        };
    }

    private static SpriteRenderer CreateBurstLayer(Transform parent, string name, Sprite sprite, int sortingOrder)
    {
        GameObject layerObject = new GameObject(name);
        layerObject.transform.SetParent(parent, false);

        SpriteRenderer renderer = layerObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = false;

        return renderer;
    }

    // ==========================================
    // HÌNH HỌC
    // ==========================================

    /// <summary>
    /// Hướng bay của viên thiên thạch: chếch xuống và đi TỪ PHÍA BÊN GỌI RA. Phe XANH đứng bên TRÁI map -
    /// lính xanh đi sang phải, xem <c>Slime.Start</c> - nên mặc định bay từ trên cao bên trái chéo xuống
    /// PHẢI. Phía gọi đã tính sẵn <paramref name="fromBlueSide"/> (phe đỏ = bản lật lại của phe xanh).
    /// </summary>
    private static Vector3 TravelDirection(VfxConfig.MeteorStrikeSettings settings, bool fromBlueSide)
    {
        float radians = Mathf.Clamp(settings.fallAngleDegrees, 15f, 90f) * Mathf.Deg2Rad;
        float horizontal = Mathf.Cos(radians);

        if (!fromBlueSide) horizontal = -horizontal;

        return new Vector3(horizontal, -Mathf.Sin(radians), 0f).normalized;
    }

    /// <summary>
    /// Góc xoay cần áp cho ảnh: đuôi lửa phải hướng về phía viên đá bay TỚI, tức ngược hướng bay. Ảnh gốc có
    /// đuôi chếch lên phải <c>tailAngleDegrees</c> độ; nếu ảnh được LẬT ngang thì hướng đuôi trong ảnh đổi
    /// thành 180 - số đó. Góc xoay = hướng đuôi cần - hướng đuôi đang có.
    /// </summary>
    private static float TravelTiltDegrees(VfxConfig.MeteorStrikeSettings settings, Vector3 travel, bool mirrorSprite)
    {
        float tailInArt = mirrorSprite ? (180f - settings.tailAngleDegrees) : settings.tailAngleDegrees;
        float tailNeeded = (Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg) + 180f;

        return tailNeeded - tailInArt;
    }

    /// <summary>
    /// Điểm xuất phát cách điểm rơi bao xa: đủ để viên đá bắt đầu ở NGOÀI màn hình phía trên (nhìn như từ
    /// trên trời rơi xuống) - không có camera thì lấy một khoảng an toàn theo bán kính kỹ năng.
    /// </summary>
    private static float StartDistance(Vector3 center, Vector3 travel, float spriteHeight, float radius)
    {
        Camera camera = Camera.main;

        float startY = (camera != null && camera.orthographic)
            ? camera.transform.position.y + camera.orthographicSize + spriteHeight * 0.6f
            : center.y + (radius * 2f) + spriteHeight;

        float vertical = Mathf.Max(startY - center.y, radius);

        return vertical / Mathf.Max(0.2f, Mathf.Abs(travel.y));
    }

    /// <summary>
    /// Điểm rơi của từng viên: ƯU TIÊN DỒN VÀO MỤC TIÊU (tuyến đầu phe địch) rồi mới rải rộng ra.
    /// Viên 0 rơi đúng mục tiêu; các viên trong nhóm <c>concentratedCount</c> rơi trong bán kính
    /// <c>concentratedSpread</c>; phần còn lại rải xa dần ra tới <c>spreadScale</c> bán kính.
    /// Các viên vẫn lệch nhau theo đường xoắn ốc góc vàng nên không chồng khít lên nhau.
    /// </summary>
    private static Vector3 LandingPoint(Vector3 center, float radius, VfxConfig.MeteorStrikeSettings settings,
        int index, int count)
    {
        if (index <= 0) return center;

        int concentrated = Mathf.Clamp(settings.concentratedCount, 1, count);
        float inner = Mathf.Clamp01(settings.concentratedSpread) * radius;
        float outer = Mathf.Max(inner, settings.spreadScale * radius);
        float angle = index * GoldenAngle;

        float distance;

        if (index < concentrated)
        {
            // Nhóm dồn: rơi quanh mục tiêu, chỉ lệch chút để mắt vẫn tách được từng viên.
            distance = inner * (0.35f + (0.65f * index / concentrated));
        }
        else
        {
            // Nhóm rải: xa dần ra tới ĐÚNG mép vùng nổ cho vùng rộng ra (t chạy từ 1/remaining tới 1).
            int remaining = Mathf.Max(1, count - concentrated);
            float t = (index - concentrated + 1) / (float)remaining;

            distance = Mathf.Lerp(inner, outer, 0.35f + (0.65f * t));
        }

        return center + new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    // ==========================================
    // SPRITE VẼ BẰNG CODE (dùng cho vụ nổ)
    // ==========================================

    private static Sprite GetGlowSprite()
    {
        if (glowSprite == null) glowSprite = BuildGlowSprite();
        return glowSprite;
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite == null) ringSprite = BuildRingSprite();
        return ringSprite;
    }

    /// <summary>Ánh sáng loé: đặc ở tâm, mờ dần ra rìa, thêm lõi sáng cho cảm giác nóng.</summary>
    private static Sprite BuildGlowSprite()
    {
        Color[] pixels = new Color[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float r = Radius(x, y);

                float alpha = Mathf.Clamp01(1f - r);
                alpha = alpha * alpha;
                alpha = Mathf.Clamp01(alpha + Mathf.Clamp01(1f - r * 2.2f) * 0.7f);

                pixels[(y * TextureSize) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        return FinishSprite(pixels);
    }

    /// <summary>Vòng sóng xung kích: vành khuyên mảnh, hai mép mờ để không có viền cứng.</summary>
    private static Sprite BuildRingSprite()
    {
        const float inner = 0.78f;
        const float outer = 0.98f;
        const float softness = 0.05f;

        Color[] pixels = new Color[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float r = Radius(x, y);

                float alpha = Mathf.Clamp01(Mathf.Min((r - inner) / softness, (outer - r) / softness));

                pixels[(y * TextureSize) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        return FinishSprite(pixels);
    }

    private static float Radius(int x, int y)
    {
        float nx = (((x + 0.5f) / TextureSize) * 2f) - 1f;
        float ny = (((y + 0.5f) / TextureSize) * 2f) - 1f;

        return Mathf.Sqrt((nx * nx) + (ny * ny));
    }

    /// <summary>Đóng gói pixel thành Sprite rộng đúng 1 world unit (để phóng to bằng localScale).</summary>
    private static Sprite FinishSprite(Color[] pixels)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.name = "MeteorStrikeTexture";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply(false, false);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f), TextureSize);
        sprite.name = "MeteorStrikeSprite";
        sprite.hideFlags = HideFlags.HideAndDontSave;

        return sprite;
    }
}
