using System;
using FortressClash.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Công cụ đưa UI KỸ NĂNG CHỦ ĐỘNG vào SCENE dưới dạng object THẬT, sửa được trong Hierarchy.
///
/// VÌ SAO CẦN TOOL NÀY: <see cref="AbilityBar"/> trước đây tự dựng UI LÚC CHẠY nên trong Hierarchy không
/// có gì để sửa - muốn đổi vị trí / màu / art đều phải sửa code. Tool này tạo sẵn các object đó trong
/// scene rồi nối hết tham chiếu của component; sau đó nó KHÔNG tự dựng UI nữa vì đã có ô gán (xem
/// AbilityBar.BuildSlots: đã gán thì dùng ô gán).
///
/// CÁCH DÙNG: mở scene CHƠI cần tạo UI bằng Unity (Gameplay hoặc MultiPlayer) rồi bấm menu
/// <b>Fortress Clash / Gameplay / 1 - Tạo UI kỹ năng chủ động</b>. Tool chỉ cần scene có canvas HUD tên
/// "MainHUD" nên dùng được cho MỌI scene chơi - tên menu giữ nguyên như trước cho khỏi đổi thói quen, chữ
/// "Gameplay" ở đây nghĩa là "màn chơi", không phải tên scene.
///
/// BẤM LẠI NHIỀU LẦN CŨNG AN TOÀN: object nào đã có thì DÙNG LẠI NGUYÊN TRẠNG - không đụng tới vị trí / kích
/// thước / màu bạn đã chỉnh tay, chỉ nối lại tham chiếu cho component. Chỉ object CHƯA CÓ mới được tạo
/// thêm và trang trí.
///
/// Art được lấy TỰ ĐỘNG theo tên file trong Assets/Textures/button/:
///   - btn_meteor.png        -&gt; nút kỹ năng THIÊN THẠCH
///   - btn_reinfor_blue.png  -&gt; nút kỹ năng TĂNG VIỆN
///   - btn_bless_blue.png    -&gt; nút kỹ năng HIỆU TRIỆU
///
/// Đó là bộ ảnh của phe XANH, gắn thẳng vào nút trong scene. Bộ ảnh phe ĐỎ (btn_reinfor_red.png,
/// btn_bless_red.png) KHÔNG gắn vào nút mà nối vào 2 ô của UIManager để LÚC CHẠY máy client tự đổi
/// (xem <see cref="WireRedAbilityArt"/>) - vì 2 máy chạy chung một scene nên scene không thể mang sẵn ảnh
/// đỏ cho cả hai bên.
///
/// KÍCH THƯỚC tính theo đơn vị canvas của HUD (canvas scale theo 1920x1080), KHÔNG phải pixel ảnh - nên art
/// 1300px vẫn hiện ra đúng cỡ. Nút cao <see cref="ButtonHeight"/>, chiều rộng suy từ tỉ lệ ảnh. Chỉnh
/// trực tiếp Width/Height trong Hierarchy cũng được, và lần bấm menu sau sẽ KHÔNG ghi đè.
///
/// LƯU Ý LÚC CHẠY: trong scene Gameplay, nút kỹ năng chỉ hiện ở level có MỞ kỹ năng đó - ở level 1 chúng tự
/// ẩn, đúng thiết kế chứ không phải lỗi (hiện tại: level 2 mở Thiên thạch + Tăng viện, level 3 mở cả 3, xem
/// LevelSettings.abilities trong GameManager của scene). Ở scene MultiPlayer thì trận 2 người KHÔNG có hồ
/// sơ level nên cả 3 kỹ năng đều mở (xem GameManager.multiplayerAbilities), và mỗi máy chỉ bấm được kỹ năng
/// của PHE MÌNH: host là phe XANH, client là phe ĐỎ (nút cũng hiện ảnh của phe mình - xem
/// <see cref="WireRedAbilityArt"/>).
///
/// Đây là file trong Assets/Editor nên KHÔNG vào bản build phát hành.
/// </summary>
public static class AbilityBarSetupTool
{
    private const string MenuPath = "Fortress Clash/Gameplay/1 - Tạo UI kỹ năng chủ động";

    /// <summary>Thư mục chứa art của các nút.</summary>
    private const string ArtFolder = "Assets/Textures/button/";

    private const string AbilityRowName = "box_ability";
    private const string AbilityBarObjectName = "AbilityBar";

    /// <summary>
    /// Tên object nút của từng kỹ năng, theo ĐÚNG thứ tự enum ActiveAbilityType (Meteor, Reinforce, Rally).
    /// Đây là HỢP ĐỒNG với scene: tool TÌM object theo tên này để nối vào <see cref="AbilityBar"/>, nên đổi
    /// tên nút trong Hierarchy thì phải đổi ở đây - không thì lần bấm menu sau tool tưởng nút chưa có, tạo
    /// thêm một nút mới và nút cũ thành thừa (không còn được nối vào AbilityBar).
    /// </summary>
    private static readonly string[] AbilityButtonNames = { "btn_meteor", "btn_reinfor_blue", "btn_bless" };

    /// <summary>File art của từng nút; null = chưa có art nên dùng nút tạm (xem <see cref="AbilityButtonNames"/>).</summary>
    private static readonly string[] AbilityButtonArtFiles = { "btn_meteor.png", "btn_reinfor_blue.png", "btn_bless_blue.png" };

    /// <summary>
    /// File art nút kỹ năng của PHE ĐỎ, theo ĐÚNG thứ tự enum (<see cref="ActiveAbilityType"/>); null =
    /// kỹ năng không có bản đỏ nên giữ nguyên ảnh xanh. Art này KHÔNG gắn vào nút mà nối vào UIManager -
    /// xem <see cref="WireRedAbilityArt"/>.
    /// </summary>
    private static readonly string[] RedAbilityButtonArtFiles = { null, "btn_reinfor_red.png", "btn_bless_red.png" };

    /// <summary>Tên kỹ năng - chỉ dùng cho nút CHƯA CÓ ART (nút có art thường đã có chữ trong ảnh).</summary>
    private static readonly string[] AbilityTitles = { "Thiên thạch", "Tăng viện", "Hiệu triệu" };

    /// <summary>Giá tiền mặc định của từng kỹ năng - chỉ để hiện sẵn trong Editor, lúc chạy code ghi đè.</summary>
    private static readonly int[] AbilityCosts = { 80, 60, 70 };

    // Kích thước theo đơn vị canvas của HUD (canvas đang scale theo 1920x1080).
    private const float ButtonHeight = 118f;
    private const float ButtonSpacing = 14f;

    /// <summary>Hàng nút nằm sát đáy màn hình (HUD sẵn có dùng nửa trên nên đáy còn trống).</summary>
    private static readonly Vector2 AbilityRowPosition = new Vector2(0f, 40f);

    private static readonly Color PlaceholderColor = new Color(0.16f, 0.2f, 0.28f, 0.95f);
    private static readonly Color CooldownFillColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color CostTextColor = new Color(1f, 0.84f, 0.38f, 1f);
    private static readonly Color LabelColor = Color.white;

    /// <summary>Số object tạo mới trong lần chạy menu hiện tại (chỉ để ghi log cho dễ kiểm tra).</summary>
    private static int createdCount;

    [MenuItem(MenuPath, false, 100)]
    public static void CreateAbilityUi()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Fortress Clash",
                "Đang ở chế độ Play nên không sửa được scene.\nHãy dừng chơi (Stop) rồi bấm lại menu.", "OK");
            return;
        }

        Canvas canvas = RuntimeHudBuilder.FindHudCanvas();

        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Fortress Clash",
                "Không tìm thấy canvas HUD (object tên MainHUD) trong scene đang mở.\n\n" +
                "Hãy mở scene chơi (Gameplay hoặc MultiPlayer) rồi bấm lại menu.", "OK");
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Tạo UI kỹ năng chủ động");
        createdCount = 0;

        RectTransform abilityRow = FindOrCreateRect(canvas.transform, AbilityRowName,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), AbilityRowPosition, out bool rowIsNew);

        BuildAbilityButtons(abilityRow, rowIsNew);
        AbilityBar abilityBar = WireAbilityBar(abilityRow);
        WireRedAbilityArt();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

        Selection.activeGameObject = abilityBar.gameObject;

        Debug.Log($"[FortressClash] UI kỹ năng chủ động đã sẵn sàng trong scene " +
                  $"'{canvas.gameObject.scene.name}' (tạo mới {createdCount} object).\n" +
                  $"  - 3 nút kỹ năng trong '{AbilityRowName}' (thứ tự: {string.Join(", ", AbilityButtonNames)})\n" +
                  $"  - Chỉnh giá / thời gian hồi / sát thương ở object '{AbilityBarObjectName}'\n" +
                  $"  - Ảnh phe đỏ ({RedAbilityButtonArtFiles[1]}, {RedAbilityButtonArtFiles[2]}) đã nối " +
                  "vào UIManager - máy client tự đổi lúc chạy\n" +
                  "Nhớ Ctrl+S để lưu scene.");

        EditorUtility.DisplayDialog("Fortress Clash",
            "Đã tạo xong UI kỹ năng chủ động.\n\n" +
            $"- 3 nút kỹ năng trong '{AbilityRowName}'\n" +
            $"- Chỉnh thông số kỹ năng ở '{AbilityBarObjectName}'\n\n" +
            "Nhớ Ctrl+S để lưu scene.", "OK");
    }

    // ==========================================
    // 3 NÚT KỸ NĂNG
    // ==========================================

    /// <summary>Tạo 3 nút theo đúng thứ tự enum, xếp giữa hàng theo tỉ lệ art của từng nút.</summary>
    private static void BuildAbilityButtons(Transform row, bool rowIsNew)
    {
        int count = AbilityButtonNames.Length;

        Sprite[] arts = new Sprite[count];
        float[] widths = new float[count];
        float totalWidth = ButtonSpacing * (count - 1);

        for (int i = 0; i < count; i++)
        {
            arts[i] = LoadArt(AbilityButtonArtFiles[i]);
            widths[i] = ButtonWidth(arts[i]);
            totalWidth += widths[i];
        }

        if (rowIsNew) SetSize(row as RectTransform, new Vector2(totalWidth, ButtonHeight));

        float cursor = -totalWidth * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float centerX = cursor + (widths[i] * 0.5f);
            cursor += widths[i] + ButtonSpacing;

            RectTransform buttonRect = FindOrCreateRect(row, AbilityButtonNames[i],
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(centerX, 0f), out bool buttonIsNew);

            if (!buttonIsNew) continue;   // nút đã có: giữ nguyên mọi thứ người dùng đã chỉnh

            SetSize(buttonRect, new Vector2(widths[i], ButtonHeight));
            ConfigureButton(buttonRect, arts[i], i);
        }
    }

    /// <summary>
    /// Trang trí 1 nút: nền (art hoặc màu tạm), nút bấm, lớp phủ thời gian hồi và các nhãn chữ.
    ///
    /// MỖI OBJECT CHỈ ĐƯỢC CÓ MỘT Graphic (Image hoặc TextMeshProUGUI) - nền, lớp phủ và từng nhãn phải nằm
    /// trên object riêng. Gắn 2 Graphic lên cùng một object thì Unity từ chối, trả về null, rồi code phía
    /// sau chết bằng NullReferenceException (lỗi này đã từng xảy ra với băng cảnh báo).
    /// </summary>
    private static void ConfigureButton(RectTransform buttonRect, Sprite art, int abilityIndex)
    {
        Image background = GetOrAdd<Image>(buttonRect.gameObject);
        background.sprite = art;
        background.color = (art != null) ? Color.white : PlaceholderColor;
        background.preserveAspect = true;
        background.raycastTarget = true;

        Button button = GetOrAdd<Button>(buttonRect.gameObject);
        button.targetGraphic = background;

        // Lớp phủ mờ chạy dần = thời gian hồi còn lại (AbilityBar ghi fillAmount mỗi frame).
        RectTransform fillRect = FindOrCreateRect(buttonRect, "cooldown_fill",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, out _);

        SetSize(fillRect, buttonRect.sizeDelta);

        Image fill = GetOrAdd<Image>(fillRect.gameObject);
        fill.sprite = null;
        fill.color = CooldownFillColor;
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillAmount = 0f;

        // Nhãn đếm ngược (giữa nút) - AbilityBar ghi số vào lúc chạy.
        RectTransform cooldownRect = FindOrCreateRect(buttonRect, "text_cooldown",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, out _);

        SetSize(cooldownRect, buttonRect.sizeDelta);
        GetOrAddLabel(cooldownRect, 34f, LabelColor);

        // Nhãn giá tiền sát mép dưới - AbilityBar ghi lại theo tiền thật lúc chạy.
        RectTransform costRect = FindOrCreateRect(buttonRect, "text_cost",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), out _);

        SetSize(costRect, new Vector2(buttonRect.sizeDelta.x, 30f));
        GetOrAddLabel(costRect, 24f, CostTextColor).SetText(AbilityCosts[abilityIndex].ToString());

        // Nút có art thì thường đã có chữ trong ảnh nên không cần nhãn tên; nút tạm thì cần để biết là kỹ
        // năng nào.
        if (art != null) return;

        RectTransform nameRect = FindOrCreateRect(buttonRect, "text_name",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), out _);

        SetSize(nameRect, new Vector2(buttonRect.sizeDelta.x, 34f));
        GetOrAddLabel(nameRect, 26f, LabelColor).SetText(AbilityTitles[abilityIndex]);
    }

    // ==========================================
    // NỐI THAM CHIẾU CHO COMPONENT
    // ==========================================

    /// <summary>
    /// Tạo (hoặc dùng lại) object gắn <see cref="AbilityBar"/> rồi nối đủ 5 dãy tham chiếu của nó. Phải đi
    /// qua SerializedObject vì các ô đó là private [SerializeField].
    /// </summary>
    private static AbilityBar WireAbilityBar(Transform row)
    {
        GameObject host = FindOrCreateObject(AbilityBarObjectName);
        AbilityBar bar = GetOrAdd<AbilityBar>(host);

        SerializedObject serialized = new SerializedObject(bar);

        AssignArray(serialized, "abilityButtons", i => ComponentIn<Button>(row, AbilityButtonNames[i]));
        AssignArray(serialized, "abilityCostLabels", i => ComponentIn<TextMeshProUGUI>(row, AbilityButtonNames[i], "text_cost"));
        AssignArray(serialized, "abilityCooldownLabels", i => ComponentIn<TextMeshProUGUI>(row, AbilityButtonNames[i], "text_cooldown"));
        AssignArray(serialized, "abilityCooldownFills", i => ComponentIn<Image>(row, AbilityButtonNames[i], "cooldown_fill"));

        // Nhãn TÊN chỉ tồn tại ở nút chưa có art (xem ConfigureButton) - ô nào không có thì để trống.
        AssignArray(serialized, "abilityNameLabels", i => ComponentIn<TextMeshProUGUI>(row, AbilityButtonNames[i], "text_name"));

        SetObjectField(serialized, "uiParent", row);

        // Dùng bản CÓ ghi Undo (không phải WithoutUndo) để Ctrl+Z hoàn tác được cả phần nối tham chiếu, không
        // chỉ phần tạo object.
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(bar);

        return bar;
    }

    /// <summary>
    /// Nối 2 ô ảnh nút kỹ năng của PHE ĐỎ trên <see cref="UIManager"/> (Tăng viện, Hiệu triệu). Nút trong
    /// scene chỉ mang ảnh XANH; máy client (phe đỏ) lấy ảnh từ 2 ô này lúc chạy - xem
    /// <c>AbilityBar.ApplyLocalSideArt</c>. UIManager không nằm trong hàng nút nên phải tìm riêng.
    /// </summary>
    private static void WireRedAbilityArt()
    {
        UIManager ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();

        if (ui == null)
        {
            Debug.LogWarning("[FortressClash] Scene không có UIManager nên không nối được ảnh nút kỹ năng " +
                             "của phe đỏ - ở MultiPlayer máy client sẽ giữ ảnh xanh.");
            return;
        }

        SerializedObject serialized = new SerializedObject(ui);

        SetObjectField(serialized, "redReinforceButtonSprite",
            LoadArt(RedAbilityButtonArtFiles[(int)ActiveAbilityType.Reinforce]));
        SetObjectField(serialized, "redBlessButtonSprite",
            LoadArt(RedAbilityButtonArtFiles[(int)ActiveAbilityType.Rally]));

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);
    }

    // ==========================================
    // TIỆN ÍCH
    // ==========================================

    /// <summary>
    /// Nạp sprite theo tên file. File ở chế độ Sprite Mode = Multiple (sprite sheet) thì lấy SUB-SPRITE ĐẦU
    /// TIÊN, vì LoadAssetAtPath chỉ trả về Sprite khi file ở chế độ Single.
    /// </summary>
    private static Sprite LoadArt(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;   // nút tạm, không có art riêng

        string path = ArtFolder + fileName;

        Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (single != null) return single;

        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Sprite sheetSprite)
            {
                Debug.Log($"[FortressClash] '{fileName}' là sprite sheet nên dùng ô '{sheetSprite.name}'. " +
                          "Muốn dùng ô khác thì đổi Sprite Mode của file về Single, hoặc sửa ô Source Image tay.");
                return sheetSprite;
            }
        }

        Debug.LogWarning($"[FortressClash] Không nạp được sprite từ '{path}' - nút/băng sẽ dùng màu tạm. " +
                         "Kiểm tra file có tồn tại và Texture Type = Sprite (2D and UI) không.");
        return null;
    }

    /// <summary>Chiều rộng nút suy từ tỉ lệ ảnh (không có art thì lấy vuông cho đủ chỗ bấm).</summary>
    private static float ButtonWidth(Sprite art)
    {
        if (art == null || art.rect.height <= 0f) return ButtonHeight;

        return ButtonHeight * (art.rect.width / art.rect.height);
    }

    /// <summary>
    /// Tìm object con theo tên; CHƯA CÓ thì tạo mới (kèm Undo) với neo/pivot/vị trí cho trước.
    /// <paramref name="wasCreated"/> cho biết object là mới (để phía gọi chỉ trang trí object mới, không ghi
    /// đè lên những gì người dùng đã chỉnh tay).
    /// </summary>
    private static RectTransform FindOrCreateRect(Transform parent, string name, Vector2 anchor, Vector2 pivot,
        Vector2 anchoredPosition, out bool wasCreated)
    {
        Transform existing = parent.Find(name);

        if (existing != null)
        {
            wasCreated = false;
            return (RectTransform)existing;
        }

        GameObject instance = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(instance, $"Tạo {name}");

        RectTransform rect = (RectTransform)instance.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(ButtonHeight, ButtonHeight);

        createdCount++;
        wasCreated = true;

        return rect;
    }

    /// <summary>Tìm object gốc (không nằm trong canvas) theo tên, chưa có thì tạo mới.</summary>
    private static GameObject FindOrCreateObject(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null) return existing;

        GameObject instance = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(instance, $"Tạo {name}");

        createdCount++;
        return instance;
    }

    /// <summary>Lấy component có sẵn hoặc gắn thêm (có đăng ký Undo để Ctrl+Z hoàn tác được).</summary>
    private static T GetOrAdd<T>(GameObject host) where T : Component
    {
        T existing = host.GetComponent<T>();
        if (existing != null) return existing;

        return Undo.AddComponent<T>(host);
    }

    /// <summary>
    /// Lấy hoặc gắn nhãn chữ với font mặc định của TextMeshPro. Không dùng RuntimeHudBuilder.AddLabel vì nhãn
    /// tạo trong Editor phải đi qua Undo mới Ctrl+Z được.
    /// </summary>
    private static TextMeshProUGUI GetOrAddLabel(RectTransform owner, float fontSize, Color color)
    {
        TextMeshProUGUI label = GetOrAdd<TextMeshProUGUI>(owner.gameObject);

        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        if (font != null)
        {
            label.font = font;
        }
        else
        {
            Debug.LogWarning("[FortressClash] Chưa cấu hình font mặc định của TextMeshPro (TMP_Settings) nên " +
                             "các nhãn chữ sẽ trống. Sửa ở Window > TextMeshPro > Settings > Default Font Asset.");
        }

        return label;
    }

    private static void SetSize(RectTransform rect, Vector2 size)
    {
        if (rect == null) return;

        rect.sizeDelta = size;
    }

    /// <summary>Lấy component ở "<c>hàng/tên-nút/tên-con</c>", chịu được object còn thiếu.</summary>
    private static T ComponentIn<T>(Transform row, string buttonName, string childName = null) where T : Component
    {
        if (row == null) return null;

        Transform button = row.Find(buttonName);
        if (button == null) return null;
        if (string.IsNullOrEmpty(childName)) return button.GetComponent<T>();

        Transform child = button.Find(childName);
        return (child != null) ? child.GetComponent<T>() : null;
    }

    /// <summary>Gán cả một dãy tham chiếu (3 phần tử, theo thứ tự kỹ năng) - ô nào không có thì để trống.</summary>
    private static void AssignArray<T>(SerializedObject serialized, string fieldName, Func<int, T> getter)
        where T : UnityEngine.Object
    {
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            Debug.LogError($"[FortressClash] Không tìm thấy ô '{fieldName}' trên AbilityBar - tên ô có thể đã " +
                           "đổi, hãy sửa lại trong AbilityBarSetupTool cho khớp.");
            return;
        }

        property.arraySize = AbilityButtonNames.Length;

        for (int i = 0; i < AbilityButtonNames.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = getter(i);
        }
    }

    /// <summary>Gán một ô tham chiếu đơn.</summary>
    private static void SetObjectField(SerializedObject serialized, string fieldName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            Debug.LogError($"[FortressClash] Không tìm thấy ô '{fieldName}' trên " +
                           $"{serialized.targetObject.GetType().Name} - tên ô có thể đã đổi.");
            return;
        }

        property.objectReferenceValue = value;
    }
}
