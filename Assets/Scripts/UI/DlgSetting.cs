using UnityEngine;
using UnityEngine.UI;
using FortressClash.Core;

namespace FortressClash.UI
{
    public class DlgSetting : BaseDialog
    {
        [Header("Tab Buttons Configuration")]
        [Tooltip("Các nút tab trên thanh box_btn_select (AUDIO, GAMEPLAY, DICTIONARY...).")]
        public Button[] tabButtons;

        [Header("Tab Content Panels")]
        [Tooltip("Panel nội dung của từng tab, ĐÚNG THỨ TỰ với tabButtons: phần tử i là panel hiện ra " +
                 "khi bấm tabButtons[i]. Để trống/để null phần tử nào thì tab đó không đổi panel.")]
        public GameObject[] contentPanels;

        private float pressedYOffset = -10f;
        private Vector2[] originalPositions;

        [Header("Option Buttons Configuration")]
        public Button applyButton;
        public Button defaultsButton;
        public Button closeButton;

        [Tooltip("Nút 'Reset Data': xoá toàn bộ dữ liệu đã lưu rồi chơi lại cốt truyện từ đầu. " +
                 "Để trống thì bỏ qua.")]
        public Button resetDataButton;

        void Start()
        {
            originalPositions = new Vector2[tabButtons != null ? tabButtons.Length : 0];

            // Đọc vị trí "nghỉ" của từng nút tab đúng 1 lần ở đây, về sau chỉ ghi lại chính giá trị này
            // (cộng pressedYOffset cho tab đang chọn).
            //
            // LƯU Ý QUAN TRỌNG: thanh tab KHÔNG được gắn Horizontal/VerticalLayoutGroup. Component đó
            // "driven" (giữ quyền) cả anchors lẫn anchoredPosition của các nút con, và chỉ đặt vị trí sau
            // khi Canvas chạy layout lần đầu - mà Start() thì chạy TRƯỚC đó, nên mọi nút đều đọc ra (0,0)
            // và lần bấm tab đầu tiên sẽ dồn cả 3 nút vào 1 góc. Vì vậy box_btn_select trong
            // Settings.prefab ghi cứng vị trí từng nút (125 / 375 / 625) và không còn LayoutGroup.
            for (int i = 0; i < originalPositions.Length; i++)
            {
                // Bỏ qua ô trống: mảng tabButtons còn sót slot rỗng (fileID 0) sẽ làm GetComponent
                // ném NullReferenceException và cả dialog không mở được.
                Button tab = tabButtons[i];
                if (tab == null) continue;

                int index = i;
                RectTransform rect = tab.GetComponent<RectTransform>();
                if (rect != null) originalPositions[i] = rect.anchoredPosition;

                tab.onClick.AddListener(() => OnTabSelected(index));
            }

            if (originalPositions.Length > 0)
            {
                OnTabSelected(0);
            }

            if (applyButton != null) applyButton.onClick.AddListener(OnApplyClicked);
            if (defaultsButton != null) defaultsButton.onClick.AddListener(OnDefaultsClicked);
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
            if (resetDataButton != null) resetDataButton.onClick.AddListener(OnResetDataClicked);
        }

        private void OnDestroy()
        {
            if (applyButton != null) applyButton.onClick.RemoveListener(OnApplyClicked);
            if (defaultsButton != null) defaultsButton.onClick.RemoveListener(OnDefaultsClicked);
            if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
            if (resetDataButton != null) resetDataButton.onClick.RemoveListener(OnResetDataClicked);
        }

        private void OnTabSelected(int selectedIndex)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                Button tab = tabButtons[i];
                if (tab == null) continue;

                RectTransform rect = tab.GetComponent<RectTransform>();

                if (i == selectedIndex)
                {
                    // Tab đang chọn bị làm mờ (interactable = false) và tụt xuống 1 chút để nhìn như đang được nhấn.
                    tab.interactable = false;
                    if (rect != null)
                    {
                        rect.anchoredPosition = new Vector2(originalPositions[i].x, originalPositions[i].y + pressedYOffset);
                    }
                }
                else
                {
                    tab.interactable = true;
                    if (rect != null) rect.anchoredPosition = originalPositions[i];
                }
            }

            ShowContent(selectedIndex);
        }

        /// <summary>Chỉ bật panel nội dung của tab đang chọn, tắt các panel còn lại.</summary>
        private void ShowContent(int selectedIndex)
        {
            if (contentPanels == null) return;

            for (int i = 0; i < contentPanels.Length; i++)
            {
                if (contentPanels[i] == null) continue;

                contentPanels[i].SetActive(i == selectedIndex);
            }
        }

        private void OnApplyClicked()
        {
            // TODO
        }

        private void OnDefaultsClicked()
        {
            // TODO
        }

        private void OnCloseClicked()
        {
            UIManager.Instance.HideAllDialogs();
        }

        /// <summary>
        /// Nút "Reset Data": xoá toàn bộ dữ liệu đã lưu (tiến trình mở khoá level + cờ đã xem cốt truyện)
        /// rồi chơi lại cốt truyện từ đầu, tức là quay về đúng trạng thái lần đầu mở game.
        ///
        /// Không cần đóng dialog ở đây: scene Cutscene load kiểu single nên cả scene MainMenu (kèm dialog
        /// này) bị huỷ ngay. Public để có thể nối trực tiếp từ onClick trong Inspector nếu muốn.
        /// </summary>
        public void OnResetDataClicked()
        {
            GameDataReset.ResetAllAndReplayStory();
        }
    }
}
