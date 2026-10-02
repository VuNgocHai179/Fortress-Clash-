using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FortressClash.UI;

namespace FortressClash.Core
{
    public class UIManager : Singleton<UIManager>
    {
        [Header("Dialog Configuration")]
        public DialogScriptableObject dialogConfig;
        public Transform dialogParent;

        private Dictionary<string, BaseDialog> instantiatedDialogs = new Dictionary<string, BaseDialog>();
        private Stack<BaseDialog> dialogStack = new Stack<BaseDialog>();

        [Header("Gameplay UI References (Money & Spawn Buttons)")]
        [SerializeField] private TextMeshProUGUI numberMoneyText;
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

        private void Start()
        {
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

            RefreshUpgradeButton(GameManager.Instance != null ? GameManager.Instance.Money : 0);
        }

        private void OnDestroy()
        {
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

            CastleUpgrade castle = (GameManager.Instance != null) ? GameManager.Instance.Castle : null;

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
            // Nút chọn lính chỉ bấm được khi ĐỦ TIỀN, ĐÃ HẾT thời gian chờ (GameManager.chooseDelay)
            // và CHƯA ĐẠT giới hạn số lính trên sân (GameManager.maxUnitsPerSide).
            bool canChoose = (GameManager.Instance == null)
                || (GameManager.Instance.IsChooseReady && !GameManager.Instance.IsPlayerAtUnitLimit);

            if (btnBaseSlime != null) btnBaseSlime.interactable = canChoose && (currentMoney >= priceBaseSlime);
            if (btnSpear != null) btnSpear.interactable = canChoose && (currentMoney >= priceSpearMan);
            if (btnArcher != null) btnArcher.interactable = canChoose && (currentMoney >= priceArcher);
            if (btnSword != null) btnSword.interactable = canChoose && (currentMoney >= priceSword);
            if (btnSlinger != null) btnSlinger.interactable = canChoose && (currentMoney >= priceSlinger);
            if (btnCatapult != null) btnCatapult.interactable = canChoose && (currentMoney >= priceCatapult);
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

        public BaseDialog ShowDialog(string dialogName)
        {
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
                    instantiatedDialogs.Add(dialogName, dialogToShow);
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