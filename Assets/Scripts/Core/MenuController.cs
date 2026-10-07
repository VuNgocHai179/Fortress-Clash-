using UnityEngine;
using UnityEngine.SceneManagement;
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
        public Button findMatchButton;
        public Button exitButton;

        [Header("Tìm trận MultiPlayer")]
        [Tooltip("Scene lo phần nối mạng LAN (NetworkManager + NetworkUI nằm ở đây).")]
        [SerializeField] private string multiplayerConnectSceneName = "MultiPlayerConnect";

        private void OnEnable()
        {
            if (playButton != null) playButton.onClick.AddListener(PlayGame);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (shopButton != null) shopButton.onClick.AddListener(OpenShop);
            if (findMatchButton != null) findMatchButton.onClick.AddListener(FindMatch);
            if (exitButton != null) exitButton.onClick.AddListener(ExitGame);
        }

        private void OnDisable()
        {
            if (playButton != null) playButton.onClick.RemoveAllListeners();
            if (settingsButton != null) settingsButton.onClick.RemoveAllListeners();
            if (shopButton != null) shopButton.onClick.RemoveAllListeners();
            if (findMatchButton != null) findMatchButton.onClick.RemoveAllListeners();
            if (exitButton != null) exitButton.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// Nút Play KHÔNG vào thẳng Gameplay nữa mà mở màn CHỌN LEVEL (dialog LevelScene). Level nào còn
        /// khóa thì không bấm được, nên chỉ vào được level đã thắng tới đó.
        /// </summary>
        private void PlayGame()
        {
            UIManager.Instance.ShowDialog(LevelSelectDialog.DialogName);
        }

        /// <summary>
        /// Nút "Find a match": xin vào một ván 2 người.
        ///
        /// Việc tìm trận nằm ở scene <c>MultiPlayerConnect</c> vì NetworkManager sống ở đó. Nút bấm chỉ
        /// ĐẶT CỜ rồi đi tới scene ấy - NetworkUI bên đó thấy cờ sẽ tự quét LAN (thấy host thì vào làm
        /// client, không thấy thì mở phòng chờ người khác).
        /// </summary>
        private void FindMatch()
        {
            NetworkUI.AutoMatchOnStart = true;

            if (UIManager.Instance != null)
            {
                UIManager.Instance.LoadScene(multiplayerConnectSceneName);
            }
            else
            {
                // Thiếu UIManager (scene test): vào thẳng cho khỏi kẹt.
                SceneManager.LoadScene(multiplayerConnectSceneName);
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
