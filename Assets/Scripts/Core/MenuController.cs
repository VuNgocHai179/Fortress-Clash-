using UnityEngine;
using UnityEngine.UI;
using FortressClash.UI;

namespace FortressClash.Core
{
    public class MenuController : MonoBehaviour
    {
        [Header("Buttons References")]
        public Button playButton;
        public Button settingsButton;
        public Button shopButton;
        public Button exitButton;

        private void OnEnable()
        {
            if (playButton != null) playButton.onClick.AddListener(PlayGame);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (shopButton != null) shopButton.onClick.AddListener(OpenShop);
            if (exitButton != null) exitButton.onClick.AddListener(ExitGame);
        }

        private void OnDisable()
        {
            if (playButton != null) playButton.onClick.RemoveAllListeners();
            if (settingsButton != null) settingsButton.onClick.RemoveAllListeners();
            if (shopButton != null) shopButton.onClick.RemoveAllListeners();
            if (exitButton != null) exitButton.onClick.RemoveAllListeners();
        }

        private void PlayGame()
        {
            BaseDialog dialog = UIManager.Instance.ShowDialog("Loading");

            if (dialog != null)
            {
                LoadingScene loading = dialog.GetComponent<LoadingScene>();
                if (loading != null)
                {
                    loading.LoadScene("Gameplay");
                }
            }
        }

        private void OpenSettings()
        {
            UIManager.Instance.ShowDialog("DlgSetting");
        }

        private void OpenShop()
        {
            UIManager.Instance.ShowDialog("Shop");
        }

        private void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}