using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using FortressClash.Core;

namespace FortressClash.UI
{
    /// <summary>
    /// Popup KẾT QUẢ TRẬN ĐẤU (prefab <c>Assets/Prefab/UI/DlgResult.prefab</c>), hiện trong scene Gameplay
    /// khi 1 trong 2 King chết (xem <see cref="GameManager.Winner"/>).
    ///
    /// Popup chỉ có 2 thứ đổi theo kết quả:
    /// - Ảnh banner (<c>current_update</c> trong prefab): THẮNG lấy <see cref="winSprite"/> (you_win),
    ///   THUA lấy <see cref="loseSprite"/> (you_lose).
    /// - Nút hành động chính: THẮNG ghi "Next Level" và đưa người chơi VỀ LẠI MÀN CHỌN LEVEL
    ///   (scene MainMenu + mở sẵn dialog <see cref="LevelSelectDialog"/>) để vào level vừa mở khóa;
    ///   THUA ghi "Replay" và CHƠI LẠI đúng level vừa thua.
    /// </summary>
    public class DlgResult : BaseDialog
    {
        /// <summary>Tên dialog khi đăng ký trong DialogConfig.</summary>
        public const string DialogName = "DlgResult";

        [Header("Ảnh banner kết quả (object 'current_update' trong prefab)")]
        [SerializeField] private Image bannerImage;

        [Tooltip("Ảnh hiện khi người chơi THẮNG (you_win).")]
        [SerializeField] private Sprite winSprite;

        [Tooltip("Ảnh hiện khi người chơi THUA (you_lose).")]
        [SerializeField] private Sprite loseSprite;

        [Header("Nút hành động chính")]
        [SerializeField] private Button actionButton;

        [Tooltip("Text (TMP) là con của actionButton trong prefab.")]
        [SerializeField] private TextMeshProUGUI buttonLabel;

        [Tooltip("Nhãn nút khi THẮNG.")]
        [SerializeField] private string winButtonLabel = "Next Level";

        [Tooltip("Nhãn nút khi THUA.")]
        [SerializeField] private string loseButtonLabel = "Replay";

        [Header("Tên scene dùng cho các nút")]
        [Tooltip("Scene chứa màn chọn level (MainMenu) - nút 'Next Level' quay về đây.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Tooltip("Scene chơi chính - nút 'Replay' load lại chính level đang chơi.")]
        [SerializeField] private string gameplaySceneName = "Gameplay";

        /// <summary>
        /// Hiện popup khi NGƯỜI CHƠI THẮNG level <paramref name="level"/>: banner you_win + nút "Next Level".
        /// </summary>
        /// <param name="newlyUnlockedLevel">Level vừa được mở khóa (0 nếu đã mở trước đó / đã hết level).</param>
        public void ShowWin(int level, int newlyUnlockedLevel)
        {
            Debug.Log($"DlgResult: thắng level {level} (mở khóa: {newlyUnlockedLevel}).");
            AudioManager.PlayWin();
            SetContent(winSprite, winButtonLabel, GoToLevelSelect);
        }

        /// <summary>Hiện popup khi người chơi THUA level <paramref name="level"/>: banner you_lose + nút "Replay".</summary>
        public void ShowLose(int level)
        {
            Debug.Log($"DlgResult: thua level {level}.");
            AudioManager.PlayLose();
            SetContent(loseSprite, loseButtonLabel, ReplayCurrentLevel);
        }

        // ==========================================
        // KẾT QUẢ TRẬN MULTIPLAYER (2 người)
        // ==========================================

        [Header("MultiPlayer (2 người chơi)")]
        [Tooltip("Nhãn nút ở popup kết quả MultiPlayer. Cả thắng lẫn thua đều chỉ có 1 hành động là " +
                 "rời ván và về menu chính (xem LeaveMultiplayer).")]
        [SerializeField] private string multiplayerButtonLabel = "Main Menu";

        /// <summary>
        /// Hiện popup kết quả ở chế độ MultiPlayer: chỉ hiện THẮNG/THUA theo góc nhìn của NGƯỜI CHƠI
        /// CỤC BỘ, KHÔNG đụng tới tiến trình level (đó là chuyện của chế độ chơi đơn vs AI).
        /// </summary>
        public void ShowMultiplayerResult(bool localPlayerWon)
        {
            Debug.Log($"DlgResult: MultiPlayer {(localPlayerWon ? "Win" : "Lose")}.");
            if (localPlayerWon) AudioManager.PlayWin();
            else AudioManager.PlayLose();
            SetContent(localPlayerWon ? winSprite : loseSprite, multiplayerButtonLabel, LeaveMultiplayer);
        }

        /// <summary>
        /// Rời ván MultiPlayer: tắt kết nối mạng (nếu đang có) rồi về menu chính.
        /// KHÔNG tự load lại scene MultiPlayer: scene đó được NetworkManager đồng bộ giữa 2 máy nên
        /// không thể load lẻ ở một bên - muốn chơi lại phải vào lại màn MultiPlayerConnect.
        /// </summary>
        private void LeaveMultiplayer()
        {
            Unity.Netcode.NetworkManager networkManager = Unity.Netcode.NetworkManager.Singleton;

            if (networkManager != null)
            {
                networkManager.Shutdown();

                // NetworkManager tự đặt DontDestroyOnLoad nên chỉ Shutdown thì nó VẪN nằm lại, giữ chỗ
                // Singleton (khiến NetworkManager của lần vào màn tìm trận sau không được nhận) và giữ
                // luôn port 7777. Huỷ hẳn giống nút CANCEL ở màn tìm trận.
                Destroy(networkManager.gameObject);
            }

            if (UIManager.Instance != null) UIManager.Instance.LoadScene(mainMenuSceneName);
        }

        private void SetContent(Sprite banner, string label, UnityAction action)
        {
            if (bannerImage != null && banner != null) bannerImage.sprite = banner;
            if (buttonLabel != null) buttonLabel.SetText(label);

            if (actionButton == null)
            {
                Debug.LogWarning("DlgResult: chưa gán actionButton trong prefab!");
                return;
            }

            // Mỗi lần hiện popup chỉ có ĐÚNG 1 hành động, gán lại từ đầu cho khỏi chồng listener cũ.
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() =>
            {
                Hide();
                action?.Invoke();
            });
        }

        /// <summary>Về lại màn chọn level: load MainMenu rồi mở sẵn dialog chọn level ở scene mới.</summary>
        private void GoToLevelSelect()
        {
            if (UIManager.Instance == null) return;

            UIManager.ShowDialogAfterNextSceneLoad(LevelSelectDialog.DialogName);
            UIManager.Instance.LoadScene(mainMenuSceneName);
        }

        /// <summary>Chơi lại level đang chơi (LevelProgress.SelectedLevel không đổi).</summary>
        private void ReplayCurrentLevel()
        {
            if (UIManager.Instance == null) return;

            UIManager.Instance.LoadScene(gameplaySceneName);
        }
    }
}
