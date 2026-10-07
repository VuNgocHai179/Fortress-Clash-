using UnityEngine;
using UnityEngine.UI;
using FortressClash.Core;

namespace FortressClash.UI
{
    /// <summary>
    /// Popup TẠM DỪNG (prefab <c>Assets/Prefab/UI/DlgPause.prefab</c>), mở từ nút <c>btn_pause</c>
    /// trên HUD của scene Gameplay (xem <see cref="UIManager"/>).
    ///
    /// - Mở popup thì ĐÓNG BĂNG trận đấu (<see cref="Time.timeScale"/> = 0), đóng lại thì chạy tiếp.
    ///   Trạng thái đóng băng LUÔN được trả về bình thường trước khi đổi scene để dialog "Loading"
    ///   (đếm tiến trình theo Time.deltaTime) không bị đứng im.
    /// - Resume / nút X: đóng popup và chơi tiếp.
    /// - Restart: chơi lại level đang chơi. Main Menu: về màn hình chính.
    /// - Settings: mở dialog cài đặt (<see cref="DlgSetting"/>) đè lên trên.
    /// - Cheat: mở bảng cheat con (cộng tiền, max thành, thắng luôn, thua luôn) - chỉ dùng khi test.
    /// </summary>
    public class DlgPause : BaseDialog
    {
        /// <summary>Tên dialog khi đăng ký trong DialogConfig.</summary>
        public const string DialogName = "DlgPause";

        [Header("Nút của menu tạm dừng")]
        [SerializeField] private Button btnClose;
        [SerializeField] private Button btnResume;
        [SerializeField] private Button btnRestart;
        [SerializeField] private Button btnSetting;
        [SerializeField] private Button btnMainMenu;
        [SerializeField] private Button btnCheat;

        [Header("Bảng cheat con (mặc định ẩn)")]
        [Tooltip("Object bật/tắt theo nút Cheat - tương ứng GameObject 'Content_cheat' trong prefab.")]
        [SerializeField] private GameObject contentCheat;
        [SerializeField] private Button btnCheatClose;
        [SerializeField] private Button btnMoney;
        [SerializeField] private Button btnUpgrade;
        [SerializeField] private Button btnWinGame;
        [SerializeField] private Button btnLoseGame;

        [Header("Cấu hình")]
        [Tooltip("Scene màn hình chính - nút 'Main Menu' load scene này.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Tooltip("Scene chơi chính - nút 'Restart' load lại chính level đang chơi.")]
        [SerializeField] private string gameplaySceneName = "Gameplay";

        [Tooltip("Tên dialog cài đặt mở từ nút 'Settings' (đăng ký trong DialogConfig).")]
        [SerializeField] private string settingDialogName = "DlgSetting";

        [Tooltip("Số tiền cộng cho người chơi ở nút '+ 10000 money'.")]
        [SerializeField] private int cheatMoneyAmount = 10000;

        /// <summary>
        /// TRUE nếu chính popup này đã đặt <see cref="Time.timeScale"/> = 0. Dùng để chỉ trả lại 1
        /// khi thật sự do mình đóng băng (tránh ghi đè trạng thái pause của nơi khác).
        /// </summary>
        private bool pausedGame;

        private void Start()
        {
            if (btnClose != null) btnClose.onClick.AddListener(Resume);
            if (btnResume != null) btnResume.onClick.AddListener(Resume);
            if (btnRestart != null) btnRestart.onClick.AddListener(RestartLevel);
            if (btnSetting != null) btnSetting.onClick.AddListener(OpenSettings);
            if (btnMainMenu != null) btnMainMenu.onClick.AddListener(GoToMainMenu);
            if (btnCheat != null) btnCheat.onClick.AddListener(ToggleCheat);
            if (btnCheatClose != null) btnCheatClose.onClick.AddListener(HideCheat);
            if (btnMoney != null) btnMoney.onClick.AddListener(CheatAddMoney);
            if (btnUpgrade != null) btnUpgrade.onClick.AddListener(CheatMaxCastle);
            if (btnWinGame != null) btnWinGame.onClick.AddListener(CheatWinGame);
            if (btnLoseGame != null) btnLoseGame.onClick.AddListener(CheatLoseGame);

            if (contentCheat != null) contentCheat.SetActive(false);
        }

        private void OnDestroy()
        {
            if (btnClose != null) btnClose.onClick.RemoveAllListeners();
            if (btnResume != null) btnResume.onClick.RemoveAllListeners();
            if (btnRestart != null) btnRestart.onClick.RemoveAllListeners();
            if (btnSetting != null) btnSetting.onClick.RemoveAllListeners();
            if (btnMainMenu != null) btnMainMenu.onClick.RemoveAllListeners();
            if (btnCheat != null) btnCheat.onClick.RemoveAllListeners();
            if (btnCheatClose != null) btnCheatClose.onClick.RemoveAllListeners();
            if (btnMoney != null) btnMoney.onClick.RemoveAllListeners();
            if (btnUpgrade != null) btnUpgrade.onClick.RemoveAllListeners();
            if (btnWinGame != null) btnWinGame.onClick.RemoveAllListeners();
            if (btnLoseGame != null) btnLoseGame.onClick.RemoveAllListeners();
        }

        /// <summary>Mở popup: đóng băng trận đấu và luôn đóng bảng cheat con.</summary>
        public override void Show()
        {
            base.Show();

            if (contentCheat != null) contentCheat.SetActive(false);

            pausedGame = true;
            Time.timeScale = 0f;
        }

        /// <summary>Đóng popup: trả trạng thái chạy tiếp (cũng để màn Loading không đứng khi đổi scene).</summary>
        public override void Hide()
        {
            base.Hide();

            if (pausedGame)
            {
                Time.timeScale = 1f;
                pausedGame = false;
            }
        }

        // ==========================================
        // NÚT CỦA MENU TẠM DỪNG
        // ==========================================

        /// <summary>Nút Resume và nút X: đóng popup, chơi tiếp.</summary>
        private void Resume()
        {
            Hide();
        }

        /// <summary>Chơi lại level đang chơi (LevelProgress.SelectedLevel không đổi).</summary>
        private void RestartLevel()
        {
            Hide();   // trả timeScale về 1 trước khi load scene

            if (UIManager.Instance != null) UIManager.Instance.LoadScene(gameplaySceneName);
        }

        /// <summary>Về màn hình chính.</summary>
        private void GoToMainMenu()
        {
            Hide();

            if (UIManager.Instance != null) UIManager.Instance.LoadScene(mainMenuSceneName);
        }

        /// <summary>Mở dialog cài đặt đè lên popup (trận đấu vẫn đang tạm dừng).</summary>
        private void OpenSettings()
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowDialog(settingDialogName);
        }

        // ==========================================
        // BẢNG CHEAT (CHỈ ĐỂ TEST)
        // ==========================================

        private void ToggleCheat()
        {
            if (contentCheat == null) return;

            contentCheat.SetActive(!contentCheat.activeSelf);
        }

        private void HideCheat()
        {
            if (contentCheat != null) contentCheat.SetActive(false);
        }

        private void CheatAddMoney()
        {
            if (GameManager.Instance != null) GameManager.Instance.Addmoney(cheatMoneyAmount);
        }

        /// <summary>
        /// Nâng thành người chơi lên cấp tối đa. Vì đây là cheat nên nếu thiếu tiền thì cộng đủ
        /// phần còn thiếu rồi nâng cho tới max (không phụ thuộc việc bấm nút cộng tiền trước).
        /// </summary>
        private void CheatMaxCastle()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null) return;

            CastleUpgrade castle = gameManager.Castle;
            if (castle == null)
            {
                Debug.LogWarning("DlgPause: không tìm thấy CastleUpgrade của người chơi!");
                return;
            }

            // Cộng đủ số tiền còn thiếu cho các lần nâng còn lại (bỏ qua level thiếu cấu hình giá).
            int missing = 0;
            for (int level = castle.CurrentLevel + 1; level <= castle.maxLevel; level++)
            {
                int cost = castle.GetUpgradeCost(level);
                if (cost == int.MaxValue) break;

                missing += cost;
            }

            if (gameManager.Money < missing) gameManager.Addmoney(missing - gameManager.Money);

            while (!castle.IsMaxLevel && gameManager.UpgradeCastle()) { }
        }

        /// <summary>Thắng luôn: kết thúc trận như khi hạ được King đỏ (mở khóa level kế tiếp).</summary>
        private void CheatWinGame()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null) return;

            Hide();   // chạy tiếp để coroutine hiện popup kết quả đếm được thời gian
            gameManager.Winner("blue");
        }

        /// <summary>
        /// Thua luôn: kết thúc trận như khi King xanh (phe người chơi) bị hạ - hiện popup kết quả
        /// với banner you_lose (xem <see cref="DlgResult.ShowLose"/>).
        /// </summary>
        private void CheatLoseGame()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null) return;

            Hide();   // chạy tiếp để coroutine hiện popup kết quả đếm được thời gian
            gameManager.Winner("red");
        }
    }
}
