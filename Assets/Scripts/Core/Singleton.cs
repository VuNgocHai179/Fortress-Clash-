using UnityEngine;

namespace FortressClash.Core
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// Instance đang sống của singleton. Setter để <c>protected</c> vì lớp con có thể cần TỰ quyết định
        /// vòng đời: UIManager / GameManager là singleton theo TỪNG SCENE nên chúng override Awake() và
        /// tự gán Instance thay vì dùng DontDestroyOnLoad của lớp này.
        /// </summary>
        public static T Instance { get; protected set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                Instance = this as T;
                DontDestroyOnLoad(this.gameObject);
            }
        }
    }
}
