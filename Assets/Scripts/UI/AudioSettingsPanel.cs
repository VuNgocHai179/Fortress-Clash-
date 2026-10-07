using UnityEngine;
using UnityEngine.UI;

namespace FortressClash.UI
{
    /// <summary>
    /// Tab AUDIO của bảng Settings (panel <c>content_change_sound</c> trong
    /// <c>Assets/Prefab/UI/Settings.prefab</c>): nối 2 thanh trượt vào <see cref="AudioManager"/>.
    ///
    /// - Hàng "SOUND EFFECT" (box_1): kéo slider -> <see cref="AudioManager.SetSfxVolume"/>.
    /// - Hàng "MUSIC VOLUME" (box_2): kéo slider -> <see cref="AudioManager.SetMusicVolume"/>.
    ///   (AudioManager tự lưu mức mới xuống PlayerPrefs nên lần chơi sau vẫn giữ nguyên.)
    /// - ICON cạnh mỗi slider: tự đổi sang <c>iconOff</c> khi âm lượng bị kéo về 0 (tắt tiếng) và tự
    ///   quay lại <c>iconOn</c> khi lớn hơn 0. Chỉ hiển thị, KHÔNG bấm được (theo yêu cầu).
    ///
    /// Mở lại dialog thì <see cref="OnEnable"/> đọc mức âm lượng hiện có từ AudioManager để vẽ lại
    /// slider + icon cho khớp, nên không cần đồng bộ tay ở đâu khác.
    /// </summary>
    public class AudioSettingsPanel : MonoBehaviour
    {
        [Header("Hàng SOUND EFFECT (box_1)")]
        [Tooltip("Slider điều khiển âm lượng hiệu ứng.")]
        [SerializeField] private Slider sfxSlider;

        [Tooltip("Icon hiện trạng thái tiếng của hiệu ứng.")]
        [SerializeField] private Image sfxIcon;

        [Tooltip("Icon khi CÓ tiếng (âm lượng > 0).")]
        [SerializeField] private Sprite sfxIconOn;

        [Tooltip("Icon khi TẮT tiếng (âm lượng = 0).")]
        [SerializeField] private Sprite sfxIconOff;

        [Header("Hàng MUSIC VOLUME (box_2)")]
        [Tooltip("Slider điều khiển âm lượng nhạc nền.")]
        [SerializeField] private Slider musicSlider;

        [Tooltip("Icon hiện trạng thái tiếng của nhạc nền.")]
        [SerializeField] private Image musicIcon;

        [Tooltip("Icon khi CÓ nhạc (âm lượng > 0).")]
        [SerializeField] private Sprite musicIconOn;

        [Tooltip("Icon khi TẮT nhạc (âm lượng = 0).")]
        [SerializeField] private Sprite musicIconOff;

        /// <summary>Mức tối đa vẫn coi là "tắt tiếng" (slider có thể trả về số hơi lớn hơn 0).</summary>
        private const float MutedThreshold = 0.0001f;

        private void OnEnable()
        {
            // Vẽ lại slider theo âm lượng HIỆN CÓ trước khi gắn listener: SetValueWithoutNotify không bắn
            // sự kiện nên tránh được vòng lặp "gán giá trị -> gọi SetVolume -> gán lại giá trị".
            SyncFromAudio();

            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        }

        private void OnDisable()
        {
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        }

        /// <summary>Đọc mức âm lượng hiện tại từ AudioManager rồi vẽ lại cả slider lẫn icon.</summary>
        public void SyncFromAudio()
        {
            AudioManager audio = AudioManager.Instance;

            if (audio == null)
            {
                Debug.LogWarning("AudioSettingsPanel: chưa có AudioManager trong scene nên slider không đọc/ghi được âm lượng!");
            }

            float sfx = (audio != null) ? audio.SfxVolume : 1f;
            float music = (audio != null) ? audio.MusicVolume : 0.5f;

            SetSliderValue(sfxSlider, sfx);
            SetSliderValue(musicSlider, music);
            RefreshIcon(sfxIcon, sfx, sfxIconOn, sfxIconOff);
            RefreshIcon(musicIcon, music, musicIconOn, musicIconOff);
        }

        private void OnSfxChanged(float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(value);

            RefreshIcon(sfxIcon, value, sfxIconOn, sfxIconOff);
        }

        private void OnMusicChanged(float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(value);

            RefreshIcon(musicIcon, value, musicIconOn, musicIconOff);
        }

        /// <summary>Đặt giá trị slider KHÔNG phát sự kiện onValueChanged.</summary>
        private static void SetSliderValue(Slider slider, float value)
        {
            if (slider == null) return;

            slider.SetValueWithoutNotify(Mathf.Clamp(value, slider.minValue, slider.maxValue));
        }

        /// <summary>Kéo về 0 (tắt tiếng) thì hiện icon off, còn lại hiện icon on.</summary>
        private static void RefreshIcon(Image icon, float value, Sprite onSprite, Sprite offSprite)
        {
            if (icon == null) return;

            Sprite target = (value <= MutedThreshold) ? offSprite : onSprite;
            if (target != null) icon.sprite = target;
        }
    }
}
