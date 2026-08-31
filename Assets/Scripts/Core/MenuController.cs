using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace FortressClash.Core
{
    public class MenuController : Singleton<MenuController>
    {
        [Header("UI Panels")]
        public GameObject mainMenuPanel;
        public GameObject loadingPanel;

        [Header("Loading UI")]
        public Slider loadingSlider;
        public TextMeshProUGUI progressText;

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
            mainMenuPanel.SetActive(false);
            loadingPanel.SetActive(true);

            StartCoroutine(LoadSceneAsync("Gameplay"));
        }

        private void OpenSettings()
        {
            Debug.Log("Open Settings panel...");
        }

        private void OpenShop()
        {
            Debug.Log("Open Shop panel...");
        }

        private void ExitGame()
        {
            Debug.Log("Exit Game...");
            Application.Quit();
        }

        private IEnumerator LoadSceneAsync(string sceneName)
        {

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

            // Prevent Unity from automatically transitioning to the new scene
            operation.allowSceneActivation = false;

            while (!operation.isDone)
            {
                // Unity's load progress stops at 0.9. Normalize it to a 0-1 scale.
                float progress = Mathf.Clamp01(operation.progress / 0.9f);

                // Update the loading slider's fill value
                if (loadingSlider != null)
                {
                    loadingSlider.value = progress;
                }

                // Update the loading percentage text
                if (progressText != null)
                {
                    progressText.text = Mathf.RoundToInt(progress * 100) + "%";
                }

                // Check if the scene has finished loading into memory (progress reaches 0.9)
                if (operation.progress >= 0.9f)
                {
                    // Optional: Wait for 1 second to let the player see the 100% completion
                    yield return new WaitForSeconds(1f);

                    // Allow the scene to activate and complete the transition
                    operation.allowSceneActivation = true;
                }

                yield return null;
            }
        }
    }
}