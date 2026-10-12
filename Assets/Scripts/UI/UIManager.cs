using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using FortressClash.UI;

namespace FortressClash.Core
{
    public class UIManager : Singleton<UIManager>
    {
        /// <summary>Tên dialog "Loading" (thanh tiến trình) trong DialogConfig - dùng khi chuyển scene.</summary>
        public const string LoadingDialogName = "Loading";

        [Header("Dialog Configuration")]
        public DialogScriptableObject dialogConfig;
        public Transform dialogParent;

        private Dictionary<string, BaseDialog> instantiatedDialogs = new Dictionary<string, BaseDialog>();
        private Stack<BaseDialog> dialogStack = new Stack<BaseDialog>();

        /// <summary>
        /// Dialog cần mở ngay khi scene KẾ TIẾP load xong. Không thể truyền trực tiếp cho instance mới
        /// (instance của scene cũ bị huỷ khi sang scene) nên dùng biến static làm "hàng chờ" - xem
        /// <see cref="ShowDialogAfterNextSceneLoad"/>.
        /// </summary>
        private static string pendingDialogAfterSceneLoad;

        [Header("Gameplay UI References (Money & Spawn Buttons)")]
        [SerializeField] private TextMeshProUGUI numberMoneyText;
        [SerializeField] private Button btnPause;
        [SerializeField] private Button btnBaseSlime;
        [SerializeField] private Button btnSpear;
        [SerializeField] private Button btnArcher;
        [SerializeField] private Button btnSword;
        [SerializeField] private Button btnSlinger;
        [SerializeField] private Button btnCatapult;

        [Header("Castle Upgrade UI (nút nâng cấp thành)")]
        [SerializeField] private Button btnUpgrade;
        [Tooltip("Nhãn hiển thị chi phí lần nâng kế tiếp (hiện 'MAX' khi thành đã tối đa).")]
        [SerializeField] private TextMeshProUGUI txtUpgradeCost;

        [Header("Health Bar UI")]
        public Image blueHealthBar;
        public Image redHealthBar;

        [Header("Unit Prices Configuration")]
        public int priceBaseSlime = 10;
        public int priceSpearMan = 50;
        public int priceArcher = 40;
        public int priceSword = 30;
        public int priceSlinger = 20;
        public int priceCatapult = 60;

        [Header("Bộ ảnh nút cho PHE ĐỎ (dùng ở MultiPlayer khi mình là client)")]
        [Tooltip("Ảnh nền nút MUA LÍNH của phe đỏ, theo ĐÚNG thứ tự 6 nút: BaseSlime, Spear, Archer, " +
                 "Sword, Slinger, Catapult. Các ô này lấy từ Assets/Textures/button/button_red.png " +
                 "(mỗi nút đang dùng một ô tương ứng với ô mà phe xanh đang dùng).\n" +
                 "Để trống thì nút giữ nguyên ảnh xanh đã gắn trong scene.")]
        [SerializeField] private Sprite[] redUnitButtonSprites = new Sprite[6];

        [Tooltip("Ảnh nền nút NÂNG CẤP THÀNH của phe đỏ " +
                 "(Assets/Textures/button/btn_upgrade_red.png).\n" +
                 "Để trống thì nút giữ nguyên ảnh xanh đã gắn trong scene.")]
        [SerializeField] private Sprite redUpgradeButtonSprite;

        [Tooltip("Ảnh nút kỹ năng TĂNG VIỆN của phe đỏ " +
                 "(Assets/Textures/button/btn_reinfor_red.png).\n" +
                 "Dùng ở MultiPlayer khi máy này là client - AbilityBar đổi ảnh nút lúc dựng dãy nút.\n" +
                 "Để trống thì nút giữ nguyên ảnh xanh đã gắn trong scene.")]
        [SerializeField] private Sprite redReinforceButtonSprite;

        [Tooltip("Ảnh nút kỹ năng HIỆU TRIỆU của phe đỏ " +
                 "(Assets/Textures/button/btn_bless_red.png).\n" +
                 "Dùng ở MultiPlayer khi máy này là client - AbilityBar đổi ảnh nút lúc dựng dãy nút.\n" +
                 "Để trống thì nút giữ nguyên ảnh xanh đã gắn trong scene.")]
        [SerializeField] private Sprite redBlessButtonSprite;

        /// <summary>
        /// UIManager là UI của RIÊNG scene đang chạy (canvas HUD, thanh máu, nút chọn lính, dialog...),
        /// nên KHÔNG DontDestroyOnLoad: sang scene mới thì UIManager của scene đó tiếp quản và toàn bộ UI
        /// của scene cũ bị huỷ theo scene. Nếu vẫn giữ DontDestroyOnLoad như trước thì UIManager của
        /// Gameplay bị Destroy kéo theo cả HUD (MainHUD là con của canvas này) biến mất.
        /// </summary>
        protected override void Awake()
        {
            if (Instance != null && Instance != this) Destroy(Instance.gameObject);
            Instance = this;

            // Scene vừa load xong có thể đã được "đặt hàng" mở sẵn 1 dialog (ví dụ quay về màn chọn level).
            string pending = pendingDialogAfterSceneLoad;
            pendingDialogAfterSceneLoad = null;

            if (!string.IsNullOrEmpty(pending)) ShowDialog(pending);
        }

        private void Start()
        {
            // Nút tạm dừng trên HUD: mở popup DlgPause (đóng băng trận + menu Resume/Restart/... ).
            if (btnPause != null) btnPause.onClick.AddListener(() => ShowDialog(DlgPause.DialogName));

            if (btnBaseSlime != null) btnBaseSlime.onClick.AddListener(() => GameManager.Instance.BaseSlimeChoosen());
            if (btnSpear != null) btnSpear.onClick.AddListener(() => GameManager.Instance.SpearChoosen());
            if (btnArcher != null) btnArcher.onClick.AddListener(() => GameManager.Instance.ArcherChoosen());
            if (btnSword != null) btnSword.onClick.AddListener(() => GameManager.Instance.SwordChoosen());
            if (btnSlinger != null) btnSlinger.onClick.AddListener(() => GameManager.Instance.SlingshotChoosen());
            if (btnCatapult != null) btnCatapult.onClick.AddListener(() => GameManager.Instance.CatapultChoosen());

            if (btnUpgrade != null)
            {
                btnUpgrade.onClick.AddListener(() =>
                {
                    if (GameManager.Instance != null) GameManager.Instance.UpgradeCastle();
                });
            }

            // MultiPlayer: nếu NGƯỜI CHƠI CỤC BỘ là phe ĐỎ (client) thì HUD phải dùng bộ ảnh nút màu đỏ.
            // Cả 2 máy chạy chung một scene nên không thể đặt sẵn ảnh đỏ trong scene - phải đổi lúc chạy.
            ApplyLocalSideTheme();

            RefreshUpgradeButton(GameManager.Instance != null ? GameManager.Instance.LocalMoney : 0);
        }

        private void OnDestroy()
        {
            if (btnPause != null) btnPause.onClick.RemoveAllListeners();
            if (btnBaseSlime != null) btnBaseSlime.onClick.RemoveAllListeners();
            if (btnSpear != null) btnSpear.onClick.RemoveAllListeners();
            if (btnArcher != null) btnArcher.onClick.RemoveAllListeners();
            if (btnSword != null) btnSword.onClick.RemoveAllListeners();
            if (btnSlinger != null) btnSlinger.onClick.RemoveAllListeners();
            if (btnCatapult != null) btnCatapult.onClick.RemoveAllListeners();
            if (btnUpgrade != null) btnUpgrade.onClick.RemoveAllListeners();
        }

        public void UpdateMoneyDisplay(int currentMoney)
        {
            if (numberMoneyText != null)
            {
                numberMoneyText.SetText(currentMoney.ToString());
            }

            CheckButtonsInteractable(currentMoney);
            RefreshUpgradeButton(currentMoney);
        }

        /// <summary>
        /// Bật/tắt nút "Nâng cấp thành" theo số tiền hiện có và cập nhật nhãn chi phí lần nâng kế tiếp.
        /// </summary>
        private void RefreshUpgradeButton(int currentMoney)
        {
            if (btnUpgrade == null && txtUpgradeCost == null) return;

            CastleUpgrade castle = (GameManager.Instance != null) ? GameManager.Instance.LocalCastle : null;

            if (btnUpgrade != null)
            {
                btnUpgrade.interactable = (castle != null) && castle.CanUpgrade(currentMoney);
            }

            if (txtUpgradeCost != null)
            {
                if (castle == null)
                {
                    txtUpgradeCost.SetText("--");
                }
                else if (castle.IsMaxLevel)
                {
                    txtUpgradeCost.SetText("MAX");
                }
                else
                {
                    txtUpgradeCost.SetText(castle.NextUpgradeCost.ToString());
                }
            }
        }

        private void CheckButtonsInteractable(int currentMoney)
        {
            // Nút chọn lính chỉ bấm được khi ĐỦ TIỀN, ĐÃ HẾT thời gian chờ (GameManager.chooseDelay),
            // CHƯA ĐẠT giới hạn số lính trên sân của level (GameManager.LocalUnitLimit) và LEVEL CÓ CHO
            // PHÉP dùng loại lính đó (LevelSettings.playerUnitWhitelist - xem Assets/Scripts/Level/LevelRules.cs).
            // Ở MultiPlayer, "người chơi" là phe cục bộ (host = xanh, client = đỏ) nên dùng
            // IsLocalAtUnitLimit / LocalMoney thay vì các giá trị cứng của phe xanh.
            GameManager gameManager = GameManager.Instance;

            bool canChoose = (gameManager == null)
                || (gameManager.IsChooseReady && !gameManager.IsLocalAtUnitLimit);

            if (btnBaseSlime != null) btnBaseSlime.interactable = CanBuy(gameManager, canChoose, currentMoney, priceBaseSlime, AIUnitType.BaseSlime);
            if (btnSpear != null) btnSpear.interactable = CanBuy(gameManager, canChoose, currentMoney, priceSpearMan, AIUnitType.SpearMan);
            if (btnArcher != null) btnArcher.interactable = CanBuy(gameManager, canChoose, currentMoney, priceArcher, AIUnitType.Archer);
            if (btnSword != null) btnSword.interactable = CanBuy(gameManager, canChoose, currentMoney, priceSword, AIUnitType.Sword);
            if (btnSlinger != null) btnSlinger.interactable = CanBuy(gameManager, canChoose, currentMoney, priceSlinger, AIUnitType.Slinger);
            if (btnCatapult != null) btnCatapult.interactable = CanBuy(gameManager, canChoose, currentMoney, priceCatapult, AIUnitType.Catapult);
        }

        /// <summary>
        /// Một nút mua lính có bấm được không: hết thời gian chờ + đủ tiền + LUẬT CHƠI CỦA LEVEL cho phép
        /// dùng loại lính đó (<see cref="GameManager.IsPlayerUnitAllowed"/>). Nút bị khoá vì luật của
        /// level thì chỉ hiện xám chứ không bấm được - giống nút đang chờ/hết tiền.
        /// </summary>
        private static bool CanBuy(GameManager gameManager, bool canChoose, int currentMoney, int price, AIUnitType unit)
        {
            if (!canChoose || currentMoney < price) return false;

            return gameManager == null || gameManager.IsPlayerUnitAllowed((int)unit);
        }

        // ==========================================
        // ẢNH NÚT THEO PHE (MultiPlayer)
        // ==========================================

        /// <summary>
        /// Đổi bộ ảnh nút theo PHE CỦA NGƯỜI CHƠI CỤC BỘ: host (và chế độ chơi đơn) giữ ảnh xanh đã có
        /// sẵn trong scene, client (phe đỏ) đổi sang ảnh đỏ - xem <see cref="redUnitButtonSprites"/>.
        ///
        /// Vì 2 máy chạy CHUNG một scene MultiPlayer nên không thể đặt sẵn ảnh đỏ trong scene: phải đổi
        /// lúc chạy thì mỗi bên mới thấy đúng màu phe mình.
        /// </summary>
        private void ApplyLocalSideTheme()
        {
            // Chơi đơn / host = phe xanh -> dùng đúng ảnh đã gắn trong scene, không đổi gì.
            bool localIsBlue = (GameManager.Instance == null) || GameManager.Instance.LocalPlayerIsBlue;
            if (localIsBlue) return;

            Button[] unitButtons = { btnBaseSlime, btnSpear, btnArcher, btnSword, btnSlinger, btnCatapult };

            for (int i = 0; i < unitButtons.Length; i++)
            {
                if (redUnitButtonSprites == null || i >= redUnitButtonSprites.Length) continue;

                SetButtonSprite(unitButtons[i], redUnitButtonSprites[i]);
            }

            SetButtonSprite(btnUpgrade, redUpgradeButtonSprite);
        }

        /// <summary>
        /// Đổi ảnh nền của 1 nút. CHỈ đụng tới ảnh nền của chính nút (icon giá tiền + chữ là con của nút
        /// nên giữ nguyên).
        /// </summary>
        private static void SetButtonSprite(Button button, Sprite sprite)
        {
            if (button == null || sprite == null) return;

            // targetGraphic của Button chính là Image nền đang nhìn thấy; nếu vì lý do nào đó chưa gán
            // thì lấy Image nằm ngay trên GameObject của nút.
            Image background = button.targetGraphic as Image;
            if (background == null) background = button.GetComponent<Image>();

            if (background != null) background.sprite = sprite;
        }

        /// <summary>
        /// Ảnh nút KỸ NĂNG CHỦ ĐỘNG theo PHE CỦA NGƯỜI CHƠI CỤC BỘ - để <see cref="AbilityBar"/> đổi ảnh
        /// nút lúc dựng dãy nút (AbilityBar không tự biết art nào của phe nào, còn phần "ảnh nút theo phe"
        /// là việc của UIManager - xem <see cref="ApplyLocalSideTheme"/> cho bộ nút mua lính).
        ///
        /// Trả về <c>null</c> khi là phe XANH (chơi đơn / host): lúc đó nút đã mang sẵn ảnh xanh gắn trong
        /// scene nên không phải đổi gì. Máy client (phe đỏ) nhận ảnh đỏ; chỉ 2 kỹ năng có bản đỏ
        /// (Tăng viện, Hiệu triệu) - Thiên thạch không có nên vẫn giữ ảnh xanh.
        /// </summary>
        public Sprite LocalSideAbilitySprite(ActiveAbilityType ability)
        {
            bool localIsBlue = (GameManager.Instance == null) || GameManager.Instance.LocalPlayerIsBlue;
            if (localIsBlue) return null;

            switch (ability)
            {
                case ActiveAbilityType.Reinforce: return redReinforceButtonSprite;
                case ActiveAbilityType.Rally: return redBlessButtonSprite;
                default: return null;
            }
        }

        // Hàm cập nhật thanh máu cho King
        public void UpdateKingHealth(bool isBlueSide, float currentHealth, float maxHealth)
        {
            float fillAmount = Mathf.Clamp01(currentHealth / maxHealth);

            if (isBlueSide && blueHealthBar != null)
            {
                blueHealthBar.fillAmount = fillAmount;
            }
            else if (!isBlueSide && redHealthBar != null)
            {
                redHealthBar.fillAmount = fillAmount;
            }
        }

        // ==========================================
        // CHUYỂN SCENE
        // ==========================================

        /// <summary>
        /// Load 1 scene kèm dialog "Loading" (thanh tiến trình). Mọi nút chuyển scene (màn chọn level,
        /// popup kết quả, menu chính) đều đi qua đây để luồng load luôn giống nhau.
        /// </summary>
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("UIManager.LoadScene: tên scene rỗng!");
                return;
            }

            BaseDialog dialog = ShowDialog(LoadingDialogName);
            LoadingScene loading = (dialog != null) ? dialog.GetComponent<LoadingScene>() : null;

            if (loading == null)
            {
                // Chưa cấu hình dialog Loading (ví dụ scene test) thì load thẳng cho khỏi kẹt.
                Debug.LogWarning($"UIManager: không có dialog '{LoadingDialogName}' trong DialogConfig, load thẳng scene '{sceneName}'.");
                SceneManager.LoadScene(sceneName);
                return;
            }

            loading.LoadScene(sceneName);
        }

        /// <summary>
        /// Đặt hàng: scene kế tiếp load xong thì mở dialog này. Dùng cho luồng "thắng xong quay về
        /// màn chọn level" vì UIManager của scene cũ bị huỷ khi đổi scene.
        /// </summary>
        public static void ShowDialogAfterNextSceneLoad(string dialogName)
        {
            if (string.IsNullOrEmpty(dialogName)) return;

            pendingDialogAfterSceneLoad = dialogName;
        }

        // ==========================================
        // DIALOG
        // ==========================================

        public BaseDialog ShowDialog(string dialogName)
        {
            if (dialogConfig == null)
            {
                Debug.LogError($"UIManager: chưa gán DialogConfig nên không mở được dialog '{dialogName}'!");
                return null;
            }

            BaseDialog dialogToShow = null;

            if (instantiatedDialogs.ContainsKey(dialogName))
            {
                dialogToShow = instantiatedDialogs[dialogName];
            }
            else
            {
                BaseDialog prefab = dialogConfig.GetDialogPrefab(dialogName);
                if (prefab != null)
                {
                    dialogToShow = Instantiate(prefab, dialogParent);
                    dialogToShow.name = dialogName;
                    instantiatedDialogs.Add(dialogName, dialogToShow);

                    // Dialog sinh lúc chạy nên không nằm trong scene lúc AudioManager quét: gắn tiếng
                    // "buttonclick" cho button của nó ngay tại đây.
                    AudioManager.HookButtonSounds(dialogToShow.gameObject);
                }
                else
                {
                    Debug.LogError($"UIManager: DialogConfig chưa có dialog '{dialogName}'!");
                }
            }

            if (dialogToShow != null)
            {
                dialogStack.Push(dialogToShow);
                dialogToShow.Show();
                dialogToShow.transform.SetAsLastSibling();
            }

            return dialogToShow;
        }

        public void PopDialog()
        {
            if (dialogStack.Count > 0)
            {
                BaseDialog topDialog = dialogStack.Pop();
                topDialog.Hide();
            }
        }

        public void HideDialog(string dialogName)
        {
            if (instantiatedDialogs.ContainsKey(dialogName))
            {
                instantiatedDialogs[dialogName].Hide();
            }
        }

        public void HideAllDialogs()
        {
            while (dialogStack.Count > 0)
            {
                BaseDialog dialog = dialogStack.Pop();
                dialog.Hide();
            }
        }
    }
}
