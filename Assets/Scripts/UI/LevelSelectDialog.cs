using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FortressClash.Core;

namespace FortressClash.UI
{
    /// <summary>
    /// Màn CHỌN LEVEL (prefab <c>Assets/Prefab/UI/LevelScene.prefab</c>) - cầu nối giữa MainMenu và Gameplay.
    ///
    /// - Mỗi nút level được KHÓA/MỞ theo <see cref="LevelProgress"/>: nút bị khóa thì
    ///   <c>Button.interactable = false</c> (không bấm được, bị làm xám bởi ColorTint) và đổi sang
    ///   sprite ổ khóa (<c>level2_lock.png</c>, <c>level3_lock.png</c>).
    /// - Bấm vào level đã mở: ghi level đó vào <see cref="LevelProgress.SelectedLevel"/> rồi load scene Gameplay.
    /// - Nút <c>btn_back</c> đóng dialog để quay lại menu chính.
    /// </summary>
    public class LevelSelectDialog : BaseDialog
    {
        /// <summary>Tên dialog khi đăng ký trong DialogConfig (MenuController + popup kết quả dùng tên này).</summary>
        public const string DialogName = "LevelScene";

        /// <summary>Một nút level trên màn chọn level.</summary>
        [Serializable]
        public class LevelSlot
        {
            [Tooltip("Số thứ tự level (1, 2, 3...) - cũng chính là level ghi vào LevelProgress.SelectedLevel.")]
            public int level = 1;

            [Tooltip("Nút bấm của level. Bị khóa thì interactable = false.")]
            public Button button;

            [Tooltip("Image hiển thị ảnh của level - đổi sprite theo trạng thái khóa/mở.")]
            public Image thumbnail;

            [Tooltip("Sprite khi level đã mở khóa (ví dụ level2.png).")]
            public Sprite unlockedSprite;

            [Tooltip("Sprite khi level còn khóa (ví dụ level2_lock.png). Để trống thì giữ nguyên sprite hiện tại.")]
            public Sprite lockedSprite;
        }

        [Header("Danh sách level (theo thứ tự nút trong box_level)")]
        [SerializeField] private LevelSlot[] levels;

        [Header("Nút quay lại menu chính")]
        [SerializeField] private Button backButton;

        [Header("Scene sẽ load khi chọn level")]
        [SerializeField] private string gameplaySceneName = "Gameplay";

        private readonly List<Action> registeredHandlers = new List<Action>();

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(Hide);

            if (levels == null) return;

            foreach (LevelSlot slot in levels)
            {
                if (slot == null || slot.button == null) continue;

                int level = slot.level;
                Action handler = () => PlayLevel(level);
                slot.button.onClick.AddListener(() => handler());
                registeredHandlers.Add(handler);
            }
        }

        private void OnDestroy()
        {
            if (backButton != null) backButton.onClick.RemoveListener(Hide);

            if (levels == null) return;

            foreach (LevelSlot slot in levels)
            {
                if (slot != null && slot.button != null) slot.button.onClick.RemoveAllListeners();
            }

            registeredHandlers.Clear();
        }

        /// <summary>Cập nhật lại khóa/mở khóa mỗi lần dialog được mở.</summary>
        public override void Show()
        {
            base.Show();
            Refresh();
        }

        /// <summary>
        /// Đồng bộ trạng thái từng nút với tiến trình đã lưu: level chưa mở thì nút không bấm được và
        /// hiển thị sprite ổ khóa.
        /// </summary>
        public void Refresh()
        {
            if (levels == null) return;

            foreach (LevelSlot slot in levels)
            {
                if (slot == null) continue;

                bool unlocked = LevelProgress.IsUnlocked(slot.level);

                if (slot.button != null) slot.button.interactable = unlocked;

                if (slot.thumbnail != null)
                {
                    Sprite sprite = unlocked ? slot.unlockedSprite : slot.lockedSprite;

                    // Sprite nào không được gán thì giữ nguyên ảnh đang có trong prefab.
                    if (sprite != null) slot.thumbnail.sprite = sprite;
                }
            }
        }

        /// <summary>Bấm 1 level: còn khóa thì bỏ qua, mở rồi thì vào thẳng scene chơi.</summary>
        private void PlayLevel(int level)
        {
            if (!LevelProgress.IsUnlocked(level))
            {
                Debug.Log($"Level {level} còn khóa - phải thắng level {level - 1} trước!");
                return;
            }

            LevelProgress.SelectedLevel = level;

            if (UIManager.Instance != null)
            {
                UIManager.Instance.LoadScene(gameplaySceneName);
            }
            else
            {
                SceneManager.LoadScene(gameplaySceneName);
            }
        }
    }
}
