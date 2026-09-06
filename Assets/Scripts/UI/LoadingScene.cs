using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using FortressClash.Core;

namespace FortressClash.UI
{
    public class LoadingScene : BaseDialog
    {
        [Header("Loading UI")]
        public Slider loadingSlider;
        public TextMeshProUGUI progressText;

        [Header("Settings")]
        public float minLoadingTime = 2.5f;

        public void LoadScene(string sceneName)
        {
            StartCoroutine(LoadSceneAsync(sceneName));
        }

        private IEnumerator LoadSceneAsync(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;

            float elapsedTime = 0f;
            while (!operation.isDone)
            {
                elapsedTime += Time.deltaTime;

                float realProgress = Mathf.Clamp01(operation.progress / 0.9f);
                float timeProgress = Mathf.Clamp01(elapsedTime / minLoadingTime);
                float displayProgress = Mathf.Min(realProgress, timeProgress);

                if (loadingSlider != null) loadingSlider.value = displayProgress;
                if (progressText != null) progressText.text = Mathf.RoundToInt(displayProgress * 100) + "%";

                if (operation.progress >= 0.9f && elapsedTime >= minLoadingTime)
                {
                    operation.allowSceneActivation = true;
                }

                yield return null;
            }

            UIManager.Instance.HideAllDialogs();
        }
    }
}