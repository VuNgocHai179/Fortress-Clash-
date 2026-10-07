using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FortressClash.UI
{
    /// <summary>
    /// Tab DICTIONARY của bảng Settings: lật từng trang, mỗi trang là 1 con slime gồm chân dung, tên và
    /// phần mô tả (chỉ số + tác dụng). Điều khiển bằng 2 nút mũi tên <c>btn_prev</c> / <c>btn_next</c>
    /// trong panel <c>content_dictionary</c>.
    ///
    /// Nội dung nằm ngay trong danh sách <see cref="entries"/> (sửa được trong Inspector), không hardcode
    /// ở code. Bấm qua trang cuối thì vòng lại trang đầu.
    /// </summary>
    public class DlgSettingDictionary : MonoBehaviour
    {
        [System.Serializable]
        public class SlimeEntry
        {
            [Tooltip("Tiêu đề trang, in đậm/mở đầu phần mô tả. Ví dụ: ARCHER.")]
            public string title;

            [Tooltip("Chân dung của slime, hiện ở khung ảnh bên trái panel.")]
            public Sprite portrait;

            [TextArea(6, 20)]
            [Tooltip("Phần mô tả: vai trò, chỉ số (giá, máu, sát thương, tầm, tốc độ) và tác dụng.")]
            public string description;
        }

        [Header("Tham chiếu UI")]
        [Tooltip("Image hiện chân dung slime (object 'icon' bên trái panel).")]
        public Image portraitImage;

        [Tooltip("Text hiện tên + mô tả (Text (TMP) trong Scroll View).")]
        public TextMeshProUGUI descriptionText;

        [Tooltip("Nút lùi 1 trang (btn_prev).")]
        public Button prevButton;

        [Tooltip("Nút tới 1 trang (btn_next).")]
        public Button nextButton;

        [Tooltip("Scroll View của panel - để tự cuộn về đầu trang mỗi khi đổi trang. Có thể để trống.")]
        public ScrollRect scrollRect;

        [Header("Định dạng tiêu đề trang")]
        [Tooltip("Markup TMP bọc quanh tiêu đề mỗi trang, {0} là tên slime trong trường 'title'. " +
                 "Màu vàng #FED66D là màu tiêu đề đang dùng ở phần còn lại của bảng Settings. " +
                 "Để trống thì tiêu đề hiện ra dưới dạng chữ trần.")]
        public string titleFormat = "<size=38><color=#FED66D>{0}</color></size>";

        [Header("Trang hiển thị đầu tiên")]
        public int startIndex = 0;

        [Header("Danh sách slime (thứ tự ở đây = thứ tự lật trang)")]
        public SlimeEntry[] entries;

        private int index;

        private void Start()
        {
            index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, EntryCount - 1));

            if (prevButton != null) prevButton.onClick.AddListener(ShowPrev);
            if (nextButton != null) nextButton.onClick.AddListener(ShowNext);

            Refresh();
            UpdateButtonState();
        }

        private void OnDestroy()
        {
            if (prevButton != null) prevButton.onClick.RemoveListener(ShowPrev);
            if (nextButton != null) nextButton.onClick.RemoveListener(ShowNext);
        }

        /// <summary>Số trang đang có.</summary>
        public int EntryCount => (entries != null) ? entries.Length : 0;

        /// <summary>Trang đang xem (0-based).</summary>
        public int CurrentIndex => index;

        public void ShowPrev()
        {
            if (EntryCount == 0) return;

            SetIndex(index - 1);
        }

        public void ShowNext()
        {
            if (EntryCount == 0) return;

            SetIndex(index + 1);
        }

        /// <summary>Nhảy tới 1 trang, vòng vòng ở 2 đầu.</summary>
        public void SetIndex(int newIndex)
        {
            if (EntryCount == 0) return;

            index = ((newIndex % EntryCount) + EntryCount) % EntryCount;

            Refresh();
        }

        /// <summary>Vẽ lại chân dung + chữ theo trang hiện tại.</summary>
        public void Refresh()
        {
            SlimeEntry entry = (EntryCount > 0) ? entries[index] : null;

            if (portraitImage != null)
            {
                portraitImage.sprite = (entry != null) ? entry.portrait : null;
                // Tắt hình khi trang không có chân dung, tránh giữ lại hình của trang trước.
                portraitImage.enabled = portraitImage.sprite != null;
            }

            if (descriptionText != null)
            {
                if (entry == null)
                {
                    descriptionText.text = string.Empty;
                }
                else if (string.IsNullOrEmpty(entry.title))
                {
                    descriptionText.text = entry.description;
                }
                else
                {
                    descriptionText.text = FormatTitle(entry.title) + "\n\n" + entry.description;
                }

                // Đổi nội dung làm đổi luôn chiều cao của Content (ContentSizeFitter) nên phải ép layout
                // cập nhật ngay, nếu không lần cuộn về đầu trang bên dưới sẽ tính theo chiều cao cũ.
                LayoutRebuilder.ForceRebuildLayoutImmediate(descriptionText.rectTransform);
            }

            ScrollToTop();
        }

        /// <summary>
        /// Bọc tiêu đề trang trong markup TMP (<see cref="titleFormat"/>) cho nổi bật. Để trống
        /// titleFormat thì trả về tiêu đề trần.
        /// </summary>
        private string FormatTitle(string title)
        {
            if (string.IsNullOrEmpty(titleFormat)) return title;

            return titleFormat.Replace("{0}", title);
        }

        private void ScrollToTop()
        {
            if (scrollRect == null) return;

            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// Danh sách vòng nên 2 nút luôn bấm được; chỉ khoá khi chưa có trang nào để tránh bấm vào khoảng không.
        /// </summary>
        private void UpdateButtonState()
        {
            bool interactable = EntryCount > 0;

            if (prevButton != null) prevButton.interactable = interactable;
            if (nextButton != null) nextButton.interactable = interactable;
        }
    }
}
