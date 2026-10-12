using UnityEngine;

/// <summary>
/// VFX BÁM THEO lính đang được GIẢM SÁT THƯƠNG nhận vào (kỹ năng Hiệu triệu - nút btn_bless trong scene).
///
/// VÌ SAO CẦN: lúc bấm kỹ năng, <see cref="Slime.ApplyDamageReduction"/> cho lính nhấp nháy xanh một nhịp
/// (Slime.PlayFlash) - nhưng nhịp đó chỉ báo được KHOẢNH KHẮC đầu. Suốt 10 giây buff còn lại người chơi
/// không có cách nào nhìn ra con nào đang được giảm sát thương, nên không biết nên dồn quân giữ con nào.
/// Aura này bám theo lính suốt thời gian buff và tự tắt khi hết.
///
/// HÌNH ẢNH VẼ BẰNG CODE, KHÔNG CẦN ASSET: 2 sprite (quầng sáng, tia sáng 4 cánh) được vẽ vào Texture2D
/// lúc chạy nên không phải thêm file ảnh, không phải sửa scene, không phải gán prefab.
/// Material thì dùng lại CHÍNH material của sprite lính (xem <see cref="Build"/>) - nhờ vậy aura render
/// đúng như thân lính trên mọi render pipeline, không phải đi tìm shader.
///
/// HAI LỚP HIỆU ỨNG (đều có chuyển động để mắt bắt được ngay cả khi lính bị che một phần):
///   1. QUẦNG SÁNG vàng phía SAU lính, phồng/xẹp theo nhịp thở;
///   2. TIA SÁNG trắng-vàng bay LÊN trước mặt lính, mờ dần rồi lặp lại, lệch nhịp nhau.
///
/// (Trước đây còn một VÒNG cyan quay ở chân lính nữa; đã BỎ theo yêu cầu vì nhìn rối quanh người lính.)
///
/// CÁCH CHỈNH HÌNH THỨC: các ô [SerializeField] dưới đây là bộ số mặc định. Aura được TẠO LÚC CHẠY nên
/// muốn đổi bằng Inspector thì gắn sẵn component này vào prefab lính rồi sửa ở đó - lúc đó component
/// KHÔNG tự chạy (chưa có ai gọi <see cref="Attach"/>) và <see cref="Attach"/> sẽ dùng luôn bản gắn sẵn.
///
/// ĐA PHE / MULTIPLAYER: aura bám theo chính component <see cref="Slime"/> nên nó chạy được ở CẢ HAI máy.
/// Ở máy client, lính chỉ được host mô phỏng (component Slime bị tắt) nhưng <see cref="ApplyDamageReduction"/>
/// vẫn được gọi từ lệnh đồng bộ của host, nên aura vẫn hiện và vẫn tự tắt đúng lúc hết buff (xem
/// <c>GameManager.ApplyRemoteAbilityCast</c>).
/// </summary>
public class SlimeBuffAura : MonoBehaviour
{
    [Header("Màu (alpha = độ đậm lúc sáng nhất)")]
    [Tooltip("Quầng sáng phía sau lính.")]
    [SerializeField] private Color haloColor = new Color(1f, 0.83f, 0.35f, 0.55f);

    [Tooltip("Tia sáng bay lên. Dùng màu gần trắng để nổi trên cả nền sáng lẫn nền tối.")]
    [SerializeField] private Color sparkleColor = new Color(1f, 0.97f, 0.8f, 0.95f);

    [Header("Nhịp và cỡ")]
    [Tooltip("Tốc độ phồng/xẹp của quầng sáng (radian/giây).")]
    [SerializeField] private float pulseSpeed = 3.1f;

    [Tooltip("Số tia sáng bay lên cùng lúc. 0 = không có tia nào (chỉ còn quầng sáng).")]
    [SerializeField, Range(0, 8)] private int sparkleCount = 4;

    [Tooltip("Số giây để một tia sáng bay từ chân lên hết đầu rồi tắt.")]
    [SerializeField] private float sparkleRiseSeconds = 1.15f;

    [Tooltip("Phóng to/thu nhỏ TOÀN BỘ hiệu ứng so với cỡ thân lính. 1 = vừa khít.")]
    [SerializeField] private float sizeScale = 1f;

    /// <summary>Số giây tắt dần sau khi hết buff (không tắt đột ngột).</summary>
    private const float FadeOutSeconds = 0.35f;

    /// <summary>Số giây hiện dần lúc mới gắn (khớp với nhịp nhấp nháy của lính lúc nhận buff).</summary>
    private const float AppearSeconds = 0.22f;

    /// <summary>Cỡ texture vẽ bằng code. Sprite tạo ra có pixelsPerUnit = cỡ này nên 1 sprite = ĐÚNG 1 world unit, phóng to bằng localScale.</summary>
    private const int TextureSize = 128;

    // Cache sprite theo kiểu static: mọi lính dùng chung 2 sprite này, không vẽ lại cho từng con.
    private static Sprite haloTexture;
    private static Sprite sparkleTexture;

    /// <summary>Lính đang được buff - aura đọc <see cref="Slime.HasDamageReduction"/> mỗi frame để biết khi nào tắt.</summary>
    private Slime owner;

    /// <summary>Object cha chứa cả 2 lớp hiệu ứng (con của lính) - huỷ nó là dọn sạch mọi thứ.</summary>
    private Transform vfxRoot;

    private SpriteRenderer halo;
    private SpriteRenderer[] sparkles;

    /// <summary>Pha lệch 0..1 của từng tia để chúng bay lệch nhịp, không chụm lại thành một hàng.</summary>
    private float[] sparklePhases;

    /// <summary>Độ lệch ngang của từng tia so với tâm lính (world unit) - rải tia ra hai bên cho cân.</summary>
    private float[] sparkleSpread;

    private Vector3 haloBaseScale;
    private float sparkleBaseScale;
    private float sparkleStartY;
    private float sparkleEndY;
    private float sparkleDrift;

    /// <summary>Đồng hồ riêng của hiệu ứng, tính bằng Time.deltaTime nên tự đứng im khi mở popup tạm dừng.</summary>
    private float phase;

    /// <summary>Đếm lùi: còn buff thì luôn được nạp đầy, hết buff thì chạy về 0 rồi tự huỷ.</summary>
    private float remaining = FadeOutSeconds;

    /// <summary>
    /// Gắn (hoặc làm mới) aura cho <paramref name="slime"/>. Gọi lặp lại nhiều lần cũng an toàn: aura đã có
    /// thì chỉ được nạp lại thời gian sống, KHÔNG dựng thêm hiệu ứng (nhờ vậy buff gia hạn không sinh ra
    /// một bộ quầng/tia thứ hai chồng lên bộ cũ).
    /// </summary>
    public static void Attach(Slime slime)
    {
        if (slime == null) return;

        SlimeBuffAura aura = slime.GetComponent<SlimeBuffAura>();
        if (aura == null) aura = slime.gameObject.AddComponent<SlimeBuffAura>();

        aura.AttachTo(slime);
    }

    private void AttachTo(Slime slime)
    {
        owner = slime;
        remaining = FadeOutSeconds;

        if (vfxRoot == null) Build();
    }

    private void Update()
    {
        // Component đặt sẵn trong prefab mà chưa ai gọi Attach thì nằm im, không tự huỷ (xem doc của class).
        if (owner == null || vfxRoot == null) return;

        if (owner.HasDamageReduction)
        {
            remaining = FadeOutSeconds;    // còn buff (kể cả vừa được gia hạn) => luôn ở mức hiện rõ
        }
        else
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                Release();
                return;
            }
        }

        phase += Time.deltaTime;

        // visibility: 1 lúc đang buff, giảm dần về 0 sau khi hết buff (nhân thêm đoạn hiện dần lúc mới gắn).
        float visibility = Mathf.Clamp01(remaining / FadeOutSeconds) * Mathf.Clamp01(phase / AppearSeconds);

        AnimateHalo(visibility);
        AnimateSparkles(visibility);
    }

    /// <summary>Huỷ hiệu ứng: tắt component trước rồi mới huỷ object, tránh Update chạy thêm một nhịp sau khi đã dọn.</summary>
    private void Release()
    {
        enabled = false;

        if (vfxRoot != null) Destroy(vfxRoot.gameObject);
        vfxRoot = null;
        owner = null;

        Destroy(this);
    }

    // ==========================================
    // DỰNG HIỆU ỨNG
    // ==========================================

    /// <summary>
    /// Dựng 2 lớp hiệu ứng và đặt cỡ/vị trí theo ĐÚNG khung hình của con lính này (mỗi loại lính to nhỏ
    /// khác nhau nên phải đo từ sprite của nó chứ không dùng số cứng).
    /// </summary>
    private void Build()
    {
        SpriteRenderer[] bodyRenderers = CollectBodyRenderers();

        // Khung hình thân lính (world). Không tìm được sprite nào thì lấy tạm 1x1 quanh chỗ đứng để hiệu
        // ứng vẫn hiện ra thay vì lỗi.
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, new Vector3(1f, 1f, 0f));
        int sortingLayerId = 0;
        int minOrder = 0;
        int maxOrder = 0;
        Material material = null;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            SpriteRenderer body = bodyRenderers[i];
            if (body == null) continue;

            if (!hasBounds)
            {
                bounds = body.bounds;
                sortingLayerId = body.sortingLayerID;
                minOrder = body.sortingOrder;
                maxOrder = body.sortingOrder;
                material = body.sharedMaterial;   // dùng lại material của chính lính: chắc chắn render đúng
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(body.bounds);
            minOrder = Mathf.Min(minOrder, body.sortingOrder);
            maxOrder = Mathf.Max(maxOrder, body.sortingOrder);
        }

        float unit = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, 0.2f)) * Mathf.Max(0.1f, sizeScale);

        GameObject rootObject = new GameObject("buff_aura");
        rootObject.transform.SetParent(transform, false);
        vfxRoot = rootObject.transform;

        // QUẦNG SÁNG: nằm SAU lính (order nhỏ hơn) và hơi dẹt ngang cho ra dáng hào quang.
        halo = CreateLayer("halo", GetHaloSprite(), haloColor, sortingLayerId, minOrder - 1, material);
        halo.transform.localPosition = ToLocalPoint(bounds.center);
        haloBaseScale = UniformScale(LocalScaleFor(unit * 1.75f), 1.18f);
        halo.transform.localScale = haloBaseScale;

        BuildSparkles(bounds, unit, sortingLayerId, maxOrder + 1, material);
    }

    /// <summary>TIA SÁNG bay lên: nằm TRƯỚC lính (order lớn hơn) để không bị thân che mất.</summary>
    private void BuildSparkles(Bounds bounds, float unit, int sortingLayerId, int order, Material material)
    {
        int count = Mathf.Max(0, sparkleCount);

        sparkles = new SpriteRenderer[count];
        sparklePhases = new float[count];
        sparkleSpread = new float[count];

        sparkleBaseScale = LocalScaleFor(unit * 0.45f);
        sparkleStartY = ToLocalPoint(new Vector3(0f, bounds.min.y + unit * 0.15f, 0f)).y;
        sparkleEndY = ToLocalPoint(new Vector3(0f, bounds.max.y + unit * 0.30f, 0f)).y;
        sparkleDrift = LocalScaleFor(unit * 0.18f);

        for (int i = 0; i < count; i++)
        {
            sparkles[i] = CreateLayer($"sparkle_{i}", GetSparkleSprite(), sparkleColor, sortingLayerId, order, material);
            sparklePhases[i] = (count > 1) ? (i / (float)count) : 0f;

            // Rải tia ra hai bên tâm: 0, +1, -1, +2, -2... (chia đôi để không lệch hẳn về một phía).
            float step = ((i % 2 == 0) ? 1f : -1f) * Mathf.Ceil(i * 0.5f);
            sparkleSpread[i] = LocalScaleFor(unit * 0.22f) * step;
        }
    }

    private SpriteRenderer CreateLayer(string name, Sprite sprite, Color color, int sortingLayerId, int order, Material material)
    {
        GameObject layerObject = new GameObject(name);
        layerObject.transform.SetParent(vfxRoot, false);

        // Đánh dấu để Slime.CacheDamageFlashRenderers BỎ QUA lớp này: aura tự đổi màu/alpha mỗi frame, nếu
        // bị đưa vào danh sách nhấp nháy khi trúng đòn thì màu aura bị ghi đè theo nhịp đỏ -> giật hình.
        layerObject.AddComponent<SlimeBuffAuraRenderer>();

        SpriteRenderer renderer = layerObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = order;

        // Không có material của lính (lính dựng bằng code chẳng hạn) thì để mặc định của SpriteRenderer.
        if (material != null) renderer.sharedMaterial = material;

        return renderer;
    }

    /// <summary>Các SpriteRenderer của THÂN lính, bỏ qua lớp của chính aura (tránh aura tự đo chính nó).</summary>
    private SpriteRenderer[] CollectBodyRenderers()
    {
        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);

        int kept = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (IsBodyRenderer(all[i])) kept++;
        }

        SpriteRenderer[] body = new SpriteRenderer[kept];
        int index = 0;

        for (int i = 0; i < all.Length; i++)
        {
            if (!IsBodyRenderer(all[i])) continue;

            body[index] = all[i];
            index++;
        }

        return body;
    }

    private static bool IsBodyRenderer(SpriteRenderer renderer)
    {
        return renderer != null && renderer.GetComponent<SlimeBuffAuraRenderer>() == null;
    }

    // ==========================================
    // NHỊP CHẠY
    // ==========================================

    private void AnimateHalo(float visibility)
    {
        if (halo == null) return;

        float pulse = Mathf.Sin(phase * pulseSpeed) * 0.5f + 0.5f;   // 0..1

        halo.color = WithAlpha(haloColor, haloColor.a * (0.7f + 0.3f * pulse) * visibility);
        halo.transform.localScale = haloBaseScale * (1f + 0.12f * pulse);
    }

    private void AnimateSparkles(float visibility)
    {
        if (sparkles == null) return;

        float rise = Mathf.Max(0.05f, sparkleRiseSeconds);

        for (int i = 0; i < sparkles.Length; i++)
        {
            SpriteRenderer sparkle = sparkles[i];
            if (sparkle == null) continue;

            // t chạy 0 -> 1 rồi lặp lại; mỗi tia lệch pha nên chúng nối đuôi nhau bay lên.
            float t = Mathf.Repeat(phase / rise + sparklePhases[i], 1f);

            Vector3 position = sparkle.transform.localPosition;
            position.x = sparkleSpread[i] + Mathf.Sin(t * Mathf.PI * 2f + i) * sparkleDrift;
            position.y = Mathf.Lerp(sparkleStartY, sparkleEndY, t);
            sparkle.transform.localPosition = position;

            // Mờ ở hai đầu hành trình, rõ nhất ở giữa - tia hiện ra rồi tan chứ không bật/tắt cứng.
            sparkle.color = WithAlpha(sparkleColor, Mathf.Sin(t * Mathf.PI) * sparkleColor.a * visibility);

            float scale = sparkleBaseScale * (0.75f + 0.5f * t);
            sparkle.transform.localScale = new Vector3(scale, scale, 1f);
            sparkle.transform.localRotation = Quaternion.Euler(0f, 0f, t * 200f);
        }
    }

    // ==========================================
    // TIỆN ÍCH TOẠ ĐỘ / CỠ
    // ==========================================

    /// <summary>Điểm world -> toạ độ local của lính (các lớp hiệu ứng đều là con của lính).</summary>
    private Vector3 ToLocalPoint(Vector3 worldPoint)
    {
        return transform.InverseTransformPoint(worldPoint);
    }

    /// <summary>Đổi một cỡ world sang localScale, bù lại cỡ của chính lính (có prefab đặt scale khác 1).</summary>
    private float LocalScaleFor(float worldSize)
    {
        Vector3 scale = transform.lossyScale;
        float reference = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));

        return worldSize / ((reference > 0.0001f) ? reference : 1f);
    }

    private static Vector3 UniformScale(float scale, float verticalMultiplier)
    {
        return new Vector3(scale, scale * verticalMultiplier, 1f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    // ==========================================
    // SPRITE VẼ BẰNG CODE
    // ==========================================

    private static Sprite GetHaloSprite()
    {
        if (haloTexture == null) haloTexture = BuildSprite(BuildHaloPixels());
        return haloTexture;
    }

    private static Sprite GetSparkleSprite()
    {
        if (sparkleTexture == null) sparkleTexture = BuildSprite(BuildSparklePixels());
        return sparkleTexture;
    }

    /// <summary>Quầng sáng mềm: đặc ở tâm rồi mờ dần ra rìa (không có viền cứng nên nhìn không bị "dán đĩa").</summary>
    private static Color[] BuildHaloPixels()
    {
        Color[] pixels = NewPixels();

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float r = Radius(x, y);

                float alpha = Mathf.Clamp01(1f - r);
                alpha = alpha * alpha * (0.35f + 0.65f * alpha);

                pixels[Index(x, y)] = new Color(1f, 1f, 1f, alpha);
            }
        }

        return pixels;
    }

    /// <summary>Tia sáng 4 cánh (hình astroid |x|^p + |y|^p = 1) cộng thêm lõi sáng ở tâm.</summary>
    private static Color[] BuildSparklePixels()
    {
        Color[] pixels = NewPixels();

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float nx = Normalized(x);
                float ny = Normalized(y);
                float r = Mathf.Sqrt(nx * nx + ny * ny);

                float star = Mathf.Pow(Mathf.Abs(nx), 0.45f) + Mathf.Pow(Mathf.Abs(ny), 0.45f);
                float alpha = Mathf.Clamp01((1.05f - star) * 1.6f);
                alpha = Mathf.Clamp01(alpha + Mathf.Clamp01(1f - r * 2.6f) * 0.55f);

                pixels[Index(x, y)] = new Color(1f, 1f, 1f, alpha);
            }
        }

        return pixels;
    }

    private static Color[] NewPixels()
    {
        return new Color[TextureSize * TextureSize];
    }

    /// <summary>Toạ độ pixel -> -1..1 (tâm texture là 0,0).</summary>
    private static float Normalized(int coordinate)
    {
        return ((coordinate + 0.5f) / TextureSize) * 2f - 1f;
    }

    private static float Radius(int x, int y)
    {
        float nx = Normalized(x);
        float ny = Normalized(y);

        return Mathf.Sqrt(nx * nx + ny * ny);
    }

    private static int Index(int x, int y)
    {
        return y * TextureSize + x;
    }

    /// <summary>Đóng gói pixel thành Sprite 1x1 world unit. Texture để HideAndDontSave vì chỉ sống trong lúc chạy.</summary>
    private static Sprite BuildSprite(Color[] pixels)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.name = "BuffAuraTexture";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply(false, false);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f), TextureSize);
        sprite.name = "BuffAuraSprite";
        sprite.hideFlags = HideFlags.HideAndDontSave;

        return sprite;
    }
}

/// <summary>
/// Đánh dấu mọi SpriteRenderer THUỘC AURA BUFF (xem <see cref="SlimeBuffAura"/>).
///
/// VÌ SAO CẦN: <see cref="Slime"/> gom toàn bộ SpriteRenderer con để nhấp nháy màu khi trúng đòn
/// (Slime.CacheDamageFlashRenderers). Aura thì tự ghi màu/alpha mỗi frame, nên nếu bị gom vào đó thì mỗi
/// cú đánh sẽ đè màu đỏ lên aura, còn aura lại ghi đè ngược lại - hiệu ứng giật và sai màu. Marker này để
/// Slime nhận ra và bỏ qua các lớp của aura.
///
/// Không có logic gì bên trong, chỉ để nhận diện.
/// </summary>
public class SlimeBuffAuraRenderer : MonoBehaviour
{
}
