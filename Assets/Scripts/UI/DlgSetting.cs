using UnityEngine;
using UnityEngine.UI;
using FortressClash.Core;

namespace FortressClash.UI
{
    public class DlgSetting : BaseDialog
    {
        [Header("Tab Buttons Configuration")]
        public Button[] tabButtons;
        private float pressedYOffset = -10f;
        private Vector2[] originalPositions;

        [Header("Option Buttons Configuration")]
        public Button applyButton;
        public Button defaultsButton;
        public Button closeButton;

        void Start()
        {
            originalPositions = new Vector2[tabButtons.Length];

            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                RectTransform rect = tabButtons[i].GetComponent<RectTransform>();
                originalPositions[i] = rect.anchoredPosition;

                tabButtons[i].onClick.AddListener(() => OnTabSelected(index));
            }

            if (tabButtons.Length > 0)
            {
                OnTabSelected(0);
            }

            if (applyButton != null) applyButton.onClick.AddListener(OnApplyClicked);
            if (defaultsButton != null) defaultsButton.onClick.AddListener(OnDefaultsClicked);
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnDestroy()
        {
            if (applyButton != null) applyButton.onClick.RemoveListener(OnApplyClicked);
            if (defaultsButton != null) defaultsButton.onClick.RemoveListener(OnDefaultsClicked);
            if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnTabSelected(int selectedIndex)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                RectTransform rect = tabButtons[i].GetComponent<RectTransform>();

                if (i == selectedIndex)
                {
                    tabButtons[i].interactable = false;
                    rect.anchoredPosition = new Vector2(originalPositions[i].x, originalPositions[i].y + pressedYOffset);
                }
                else
                {
                    tabButtons[i].interactable = true;
                    rect.anchoredPosition = originalPositions[i];
                }
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
    }
}