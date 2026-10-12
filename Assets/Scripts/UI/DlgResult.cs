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
    ///
    /// TRẠNG THÁI WIN/LOSE (xem <see cref="EnterResultState"/>): vào popup là ÂM THANH chỉ còn tiếng
    /// thắng (wiin) hoặc thua (losse) - nhạc nền và mọi tiếng đang chồng đều bị tắt trước - và trận đấu
    /// ĐÓNG BĂNG (<c>Time.timeScale = 0</c>). Thời gian chỉ chạy lại khi rời popup
    /// (<see cref="LeaveResultState"/>), để màn "Loading" lúc đổi scene không bị đứng im.
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
            EnterResultState(true);
            SetContent(winSprite, winButtonLabel, GoToLevelSelect);
        }

        /// <summary>Hiện popup khi người chơi THUA level <paramref name="level"/>: banner you_lose + nút "Replay".</summary>
        public void ShowLose(int level)
        {
            Debug.Log($"DlgResult: thua level {level}.");
            EnterResultState(false);
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
            EnterResultState(localPlayerWon);
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
                // Hide() trả lại Time.timeScale = 1 (xem LeaveResultState) TRƯỚC khi đổi scene: màn
                // "Loading" của UIManager đếm tiến trình bằng Time.deltaTime nên còn đóng băng là nó đứng im.
                Hide();
                action?.Invoke();
            });
        }

        // ==========================================
        // TRẠNG THÁI WIN / LOSE
        // ==========================================

        /// <summary>
        /// TRUE nếu chính popup này đã đặt <see cref="Time.timeScale"/> = 0. Dùng để chỉ trả lại 1 khi
        /// thật sự do mình đóng băng (không ghi đè trạng thái pause của nơi khác).
        /// </summary>
        private bool frozeMatch;

        /// <summary>
        /// Vào TRẠNG THÁI WIN/LOSE: âm thanh chỉ còn đúng tiếng thắng hoặc thua - <see
        /// cref="AudioManager.PlayMatchResult"/> tắt nhạc nền cùng mọi tiếng đang chồng trước khi phát -
        /// và trận đấu ĐÓNG BĂNG hẳn (<see cref="Time.timeScale"/> = 0) để không còn gì diễn ra sau đó.
        ///
        /// Vẫn phải trả lại timeScale = 1 khi rời popup (xem <see cref="LeaveResultState"/>); nếu không
        /// thì màn Loading và cả scene sau đó đều đứng im.
        /// </summary>
        private void EnterResultState(bool playerWon)
        {
            AudioManager.PlayMatchResult(playerWon);

            frozeMatch = true;
            Time.timeScale = 0f;
        }

        /// <summary>Rời trạng thái Win/Lose: chạy lại thời gian bình thường (chỉ làm nếu chính popup này đã đóng băng).</summary>
        private void LeaveResultState()
        {
            if (!frozeMatch) return;

            Time.timeScale = 1f;
            frozeMatch = false;
        }

        /// <summary>Đóng popup thì LUÔN trả lại thời gian chạy, kể cả khi bị ẩn bằng đường khác ngoài nút hành động.</summary>
        public override void Hide()
        {
            LeaveResultState();
            base.Hide();
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
