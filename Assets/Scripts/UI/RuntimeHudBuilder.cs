using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Các hàm dựng UI LÚC CHẠY cho những thành phần gameplay tự tạo (dãy nút kỹ năng - AbilityBar).
///
/// Vì sao cần nhóm này: các tính năng đó phải chạy được NGAY mà không phải sửa scene, nhưng đồng thời
/// vẫn cho phép người dùng tự làm UI trong Editor rồi kéo vào các ô gán của component. Vậy nên
/// component kia chỉ dựng UI bằng nhóm hàm này KHI các ô gán còn trống.
///
/// Mọi hàm ở đây CHỈ tạo GameObject/component, không đọc gameplay - nhờ vậy sửa hình thức nút kỹ năng
/// không phải đụng tới luật chơi.
///
/// QUY TẮC QUAN TRỌNG: một object chỉ được có MỘT Graphic (Image hoặc TextMeshProUGUI). Muốn vừa nền vừa
/// chữ thì tạo 2 object bằng <see cref="CreateRect"/> rồi gắn mỗi object một Graphic - gắn cái thứ hai lên
/// cùng object thì Unity từ chối, trả về null, và code phía sau sẽ chết bằng NullReferenceException.
/// </summary>
public static class RuntimeHudBuilder
{
    /// <summary>Neo "giữa mép TRÊN" - dùng cho băng cảnh báo.</summary>
    public static readonly Vector2 TopCenterAnchor = new Vector2(0.5f, 1f);

    /// <summary>Neo "giữa mép DƯỚI" - dùng cho dãy nút kỹ năng.</summary>
    public static readonly Vector2 BottomCenterAnchor = new Vector2(0.5f, 0f);

    /// <summary>Tên canvas HUD của scene chơi - ưu tiên gắn UI mới vào đây để dùng chung CanvasScaler.</summary>
    public const string HudCanvasName = "MainHUD";

    private static bool warnedMissingFont;

    /// <summary>
    /// Tạo một GameObject CHỈ có RectTransform (chưa có hình/chữ) để gắn tiếp Image/Button/Text.
    /// <paramref name="anchor"/> áp cho cả anchorMin và anchorMax nên <paramref name="anchoredPosition"/>
    /// là khoảng lệch so với điểm neo đó - ví dụ neo giữa-mép-trên + y âm = đặt dưới mép trên màn hình.
    /// </summary>
    public static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 size)
    {
        GameObject instance = new GameObject(name, typeof(RectTransform));

        RectTransform rect = (RectTransform)instance.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        return rect;
    }

    /// <summary>
    /// Gắn Image lên một RectTransform đã có. <paramref name="raycastTarget"/> mặc định TẮT để nền băng
    /// cảnh báo không nuốt cú bấm của người chơi; nút bấm phải BẬT thì mới nhận được cú bấm.
    /// </summary>
    public static Image AddImage(RectTransform owner, Color color, Sprite sprite = null, bool raycastTarget = false)
    {
        if (!CanAddGraphic(owner, nameof(Image))) return null;

        Image image = owner.gameObject.AddComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.raycastTarget = raycastTarget;

        return image;
    }

    /// <summary>
    /// Gắn Button lên một RectTransform đã có, dùng <paramref name="target"/> làm hình nền (Button đổi màu
    /// hình này khi hover/bấm/nút bị khoá).
    /// </summary>
    public static Button AddButton(RectTransform owner, Image target)
    {
        Button button = owner.gameObject.AddComponent<Button>();
        button.targetGraphic = target;

        return button;
    }

    /// <summary>
    /// Gắn chữ lên một RectTransform đã có. Dùng font mặc định của TextMeshPro (TMP_Settings) nên không
    /// cần kéo font vào từng chỗ; chưa cấu hình font mặc định thì cảnh báo MỘT LẦN rồi chữ để trống,
    /// game vẫn chạy.
    /// </summary>
    public static TextMeshProUGUI AddLabel(RectTransform owner, string text, float fontSize, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        if (!CanAddGraphic(owner, nameof(TextMeshProUGUI))) return null;

        TextMeshProUGUI label = owner.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null)
        {
            label.font = font;
        }
        else if (!warnedMissingFont)
        {
            warnedMissingFont = true;
            Debug.LogWarning("RuntimeHudBuilder: chưa cấu hình font mặc định của TextMeshPro (TMP_Settings) " +
                             "nên các nhãn UI tự dựng sẽ không hiện chữ. " +
                             "Sửa ở Window > TextMeshPro > Settings > Default Font Asset.");
        }

        return label;
    }

    /// <summary>
    /// Một GameObject chỉ được có MỘT Graphic (Image HOẶC TextMeshProUGUI).
    ///
    /// Vì sao cần kiểm tra trước: Unity TỪ CHỐI gắn Graphic thứ hai và trả về null, rồi code phía sau chết
    /// với NullReferenceException khó đoán (lỗi thật đã gặp: băng cảnh báo đợt quân gắn cả Image lẫn
    /// TextMeshProUGUI lên cùng một object nên cả `Start()` chết theo). Cần cả nền lẫn chữ thì phải tạo 2
    /// object bằng <see cref="CreateRect"/> rồi gắn mỗi object một Graphic.
    /// </summary>
    private static bool CanAddGraphic(RectTransform owner, string what)
    {
        if (owner == null)
        {
            Debug.LogError($"RuntimeHudBuilder: không gắn được {what} vì object chứa nó là null.");
            return false;
        }

        if (owner.GetComponent<Graphic>() != null)
        {
            Debug.LogError($"RuntimeHudBuilder: không gắn được {what} lên '{owner.name}' vì object đó đã có " +
                           "một Graphic (Image/TextMeshProUGUI) rồi - mỗi object chỉ được có MỘT. " +
                           "Cần cả nền lẫn chữ thì tạo thêm một RectTransform con cho phần tử thứ hai.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Canvas HUD của scene chơi: ưu tiên canvas tên <see cref="HudCanvasName"/> (MainHUD) vì nó đã có
    /// CanvasScaler 1920x1080 nên UI tự dựng sẽ đúng tỉ lệ như HUD đang có. Không tìm thấy thì lấy canvas
    /// overlay gốc đầu tiên, không có canvas nào thì trả về null (phía gọi tự bỏ qua việc dựng UI).
    /// </summary>
    public static Canvas FindHudCanvas()
    {
        Canvas fallback = null;

        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas == null) continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (!canvas.isRootCanvas) continue;

            if (canvas.gameObject.name == HudCanvasName) return canvas;
            if (fallback == null) fallback = canvas;
        }

        return fallback;
    }
}
