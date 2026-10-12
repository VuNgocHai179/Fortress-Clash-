using FortressClash.Core;
using FortressClash.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Quản lý TOÀN BỘ âm thanh của game: nhạc nền + 8 hiệu ứng.
///
/// - Nhạc nền phát lặp vô hạn và KHÔNG ngắt khi đổi scene: object này được <see cref="Singleton{T}"/>
///   đặt DontDestroyOnLoad nên bản nhạc chạy xuyên suốt mọi scene. Bài nhạc chọn theo TÊN SCENE
///   (xem <see cref="ApplyMusicForScene"/>):
///     + scene CHƠI (mặc định "Gameplay" và "MultiPlayer") -> <see cref="gameplayClip"/> (BG_Game);
///     + scene cốt truyện (mặc định "Cutscene")             -> <see cref="cutsceneClip"/> (Fairy_tale_music);
///     + mọi scene còn lại (menu...)                        -> <see cref="bgmClip"/> (BG_slimeBang).
/// - Hiệu ứng phát bằng <see cref="AudioSource.PlayOneShot(AudioClip, float)"/> nên NHIỀU lính
///   cùng đánh/nhảy một lúc vẫn nghe được chồng tiếng, không cắt nhau.
/// - Tiếng <c>buttonclick</c> được gắn TỰ ĐỘNG cho mọi Button nhờ <see cref="ButtonClickSound"/>
///   (xem <see cref="HookButtonSounds"/>), không phải gán tay từng nút.
/// - Mức âm lượng chỉnh trong bảng Settings (xem <c>FortressClash.UI.AudioSettingsPanel</c>) được
///   LƯU vào PlayerPrefs và nạp lại ở lần chơi sau - xem <see cref="LoadSavedVolumes"/> /
///   <see cref="ClearSavedVolumes"/>.
///
/// CÁCH DÙNG (tự nạp - KHÔNG gắn component này vào scene nào):
///   1. Toàn bộ clip + mức âm lượng nằm trong prefab <c>Assets/Resources/AudioManager.prefab</c>.
///      Gán 11 clip ở đó: Bgm (BG_slimeBang), Gameplay (BG_Game), Cutscene (Fairy_tale_music),
///      Attack (attack), Button Click (buttonclick), Lose (losse), Move (move), Upgrade (Upgrade),
///      Win (wiin), Meteor (meteorite), Summon (summon).
///   2. <see cref="Bootstrap"/> chạy ngay sau khi scene đầu tiên load xong, Instantiate prefab đó, và
///      <see cref="Singleton{T}"/> đặt DontDestroyOnLoad -> âm thanh có mặt ở MỌI scene, MỌI lối vào
///      game (chơi từ MainMenu, bấm Play trực tiếp trong Gameplay/MultiPlayer, hay bản clone ParrelSync).
///
/// ĐỪNG gắn component này vào một scene cụ thể. Bản trước đây nằm trong scene "Cutscene" và chỉ có
/// tiếng ở LẦN CHƠI ĐẦU TIÊN: màn chọn cốt truyện chỉ vào Cutscene khi chưa xem hết (xem
/// <c>CutsceneLauncher.HookFirstLaunch</c>), các lần sau game vào thẳng MainMenu nên không có
/// AudioManager nào được sinh ra -> mất sạch tiếng.
/// Không cần gán AudioSource: để trống thì tự tạo trên chính object này.
///
/// Mọi chỗ khác gọi qua API tĩnh cho gọn: <see cref="PlayAttack"/>, <see cref="PlayMove"/>,
/// <see cref="PlayButtonClick"/>, <see cref="PlayUpgrade"/>, <see cref="PlayMeteor"/>,
/// <see cref="PlaySummon"/>, <see cref="PlayMatchResult"/>.
/// Chưa có AudioManager trong scene thì các hàm này "im lặng" (null-check), game không lỗi.
/// </summary>
public class AudioManager : Singleton<AudioManager>
{
    [Header("Nguồn phát (để trống thì tự tạo AudioSource trên object này)")]
    [Tooltip("Nguồn phát NHẠC NỀN (tự set loop = true).")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("Nguồn phát HIỆU ỨNG (dùng PlayOneShot nên chồng được nhiều tiếng).")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Nhạc nền (phát lặp, xuyên suốt mọi scene)")]
    [Tooltip("Nhạc nền CHUNG cho mọi scene (BG_slimeBang).")]
    [SerializeField] private AudioClip bgmClip;

    [Tooltip("Nhạc RIÊNG cho scene cốt truyện (Fairy_tale_music). Scene nào dùng bài này do " +
             "cutsceneSceneName quyết định. Để trống thì scene đó vẫn dùng nhạc nền chung.")]
    [SerializeField] private AudioClip cutsceneClip;

    [Tooltip("Tên scene phát cutsceneClip thay cho nhạc nền chung.")]
    [SerializeField] private string cutsceneSceneName = "Cutscene";

    [Tooltip("Nhạc RIÊNG cho các SCENE CHƠI (BG_Game). Để trống thì scene chơi dùng nhạc nền chung.")]
    [SerializeField] private AudioClip gameplayClip;

    [Tooltip("Tên các scene được coi là ĐANG CHƠI -> phát gameplayClip (BG_Game). " +
             "Sửa được trong Inspector nếu sau này đổi tên scene hoặc thêm màn chơi mới.")]
    [SerializeField] private string[] gameplaySceneNames = { "Gameplay", "MultiPlayer" };

    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.5f;

    [Header("Hiệu ứng âm thanh")]
    [Tooltip("attack.mp3 - khi slime tấn công (mọi loại lính, cả 2 phe).")]
    [SerializeField] private AudioClip attackClip;

    [Tooltip("buttonclick.mp3 - khi chạm vào bất kỳ button nào.")]
    [SerializeField] private AudioClip buttonClickClip;

    [Tooltip("losse.mp3 - khi THUA game.")]
    [SerializeField] private AudioClip loseClip;

    [Tooltip("meteorite.mp3 - khi dùng kỹ năng THIÊN THẠCH (nút btn_meteor - xem AbilityBar.CastMeteor).")]
    [SerializeField] private AudioClip meteorClip;

    [Tooltip("move.mp3 - khi slime di chuyển (mỗi cú nhảy).")]
    [SerializeField] private AudioClip moveClip;

    [Tooltip("summon.mp3 - khi dùng kỹ năng TĂNG VIỆN (nút btn_reinfor_blue - xem " +
             "AbilityBar.CallReinforcement).")]
    [SerializeField] private AudioClip summonClip;

    [Tooltip("Upgrade.mp3 - khi nâng cấp thành thành công.")]
    [SerializeField] private AudioClip upgradeClip;

    [Tooltip("wiin.mp3 - khi THẮNG game.")]
    [SerializeField] private AudioClip winClip;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("Chống dội âm (0 = phát MỌI lần - mặc định)")]
    [Tooltip("Khoảng lặng tối thiểu giữa 2 tiếng TẤN CÔNG (giây). Trên sân đông lính thì tăng lên " +
             "(ví dụ 0.08) để đỡ rè; để 0 thì tiếng nào cũng phát như yêu cầu.")]
    [Min(0f)]
    [SerializeField] private float attackRepeatCooldown = 0f;

    [Tooltip("Khoảng lặng tối thiểu giữa 2 tiếng DI CHUYỂN (giây). Xem ghi chú ở attackRepeatCooldown.")]
    [Min(0f)]
    [SerializeField] private float moveRepeatCooldown = 0f;

    private float lastAttackTime = float.NegativeInfinity;
    private float lastMoveTime = float.NegativeInfinity;

    // Khoá PlayerPrefs lưu mức âm lượng người chơi chỉnh trong Settings. Đặt tên theo quy ước
    // "fortressclash.<nhóm>.<tên>.v1" giống CutsceneLauncher để sau này đổi định dạng còn phân biệt được.
    private const string MusicVolumeKey = "fortressclash.audio.musicVolume.v1";
    private const string SfxVolumeKey = "fortressclash.audio.sfxVolume.v1";

    /// <summary>Âm lượng nhạc nền hiện tại (0..1).</summary>
    public float MusicVolume => musicVolume;

    /// <summary>Âm lượng hiệu ứng hiện tại (0..1).</summary>
    public float SfxVolume => sfxVolume;

    /// <summary>Tên prefab trong thư mục Resources (Assets/Resources/AudioManager.prefab).</summary>
    private const string PrefabResourcePath = "AudioManager";

    /// <summary>
    /// TỰ SINH AudioManager ngay sau khi scene đầu tiên load xong, nhờ vậy âm thanh có mặt ở MỌI scene
    /// và MỌI lối vào game (chơi từ MainMenu, bấm Play trực tiếp trong Gameplay/MultiPlayer, hay bản
    /// clone ParrelSync) - xem ghi chú ở đầu class.
    ///
    /// Chọn AfterSceneLoad (không phải BeforeSceneLoad) vì lúc Instantiate thì scene đã load xong:
    /// button của scene đầu tiên được gắn tiếng click ngay trong OnEnable, và AudioSource đã có
    /// AudioListener của scene để phát nhạc (tránh warning "no audio listeners in the scene").
    ///
    /// Có sẵn AudioManager rồi thì thôi; lỡ sinh trùng thì <see cref="Singleton{T}"/> tự huỷ bản sau.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;   // đã có sẵn (scene nào đó gắn tay, hoặc đang chạy test)

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"AudioManager: không tìm thấy Assets/Resources/{PrefabResourcePath}.prefab " +
                             "nên game sẽ KHÔNG có âm thanh.");
            return;
        }

        Instantiate(prefab);
    }

    protected override void Awake()
    {
        // base.Awake() gán Instance + DontDestroyOnLoad; nếu đã có AudioManager khác thì object NÀY
        // bị huỷ (Destroy chỉ có hiệu lực cuối frame) nên phải kiểm tra lại trước khi làm gì thêm.
        base.Awake();
        if (Instance != this) return;

        LoadSavedVolumes();   // mức âm lượng đã chỉnh lần trước phải có TRƯỚC khi tạo nguồn phát
        EnsureSources();
        ApplyMusicForScene(SceneManager.GetActiveScene());
    }

    private void OnEnable()
    {
        if (Instance != this) return;   // bản trùng đang bị huỷ => đừng đăng ký gì

        // Gắn tiếng click cho button của scene hiện tại và của MỌI scene load sau này.
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HookButtonsInScene(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    // ==========================================
    // NHẠC NỀN
    // ==========================================

    /// <summary>Tạo AudioSource nếu Inspector chưa gán, rồi cấu hình cho đúng vai trò.</summary>
    private void EnsureSources()
    {
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;             // nhạc nền lặp vô hạn
        musicSource.spatialBlend = 0f;       // 2D - không giảm âm theo khoảng cách camera
        musicSource.volume = musicVolume;

        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = sfxVolume;
    }

    /// <summary>
    /// Chọn bài nhạc cho scene đang chạy, theo TÊN SCENE với thứ tự ưu tiên:
    ///   1. scene CHƠI (<see cref="gameplaySceneNames"/>, mặc định Gameplay + MultiPlayer) -> <see cref="gameplayClip"/> (BG_Game);
    ///   2. scene cốt truyện (<see cref="cutsceneSceneName"/>) -> <see cref="cutsceneClip"/> (Fairy_tale_music);
    ///   3. mọi scene còn lại (MainMenu...) -> <see cref="bgmClip"/> (BG_slimeBang).
    /// Ô clip nào để trống thì tự rơi xuống bài nhạc nền chung, không im lặng mất nhạc.
    ///
    /// CÙNG bài thì để nhạc chạy liền mạch (không ngắt quãng mỗi lần đổi scene); KHÁC bài thì đổi clip
    /// rồi phát lại từ đầu - nhờ vậy vào/ra scene Cutscene, hay vào màn chơi, là đổi được bài.
    /// </summary>
    private void ApplyMusicForScene(Scene scene)
    {
        if (musicSource == null) return;

        AudioClip clip = ChooseMusicClip(scene);

        if (clip == null)
        {
            Debug.LogWarning("AudioManager: chưa gán bgmClip (BG_slimeBang) trong Inspector!");
            return;
        }

        if (musicSource.clip == clip)
        {
            if (!musicSource.isPlaying) musicSource.Play();
            return;
        }

        musicSource.clip = clip;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    /// <summary>Bài nhạc của 1 scene (xem thứ tự ưu tiên ở <see cref="ApplyMusicForScene"/>).</summary>
    private AudioClip ChooseMusicClip(Scene scene)
    {
        if (scene.IsValid())
        {
            if (IsGameplayScene(scene.name) && gameplayClip != null) return gameplayClip;

            if (!string.IsNullOrEmpty(cutsceneSceneName)
                && scene.name == cutsceneSceneName
                && cutsceneClip != null) return cutsceneClip;
        }

        return bgmClip;
    }

    /// <summary>Tên scene này có nằm trong danh sách SCENE CHƠI không (so khớp không phân biệt hoa/thường).</summary>
    private bool IsGameplayScene(string sceneName)
    {
        if (gameplaySceneNames == null || string.IsNullOrEmpty(sceneName)) return false;

        for (int i = 0; i < gameplaySceneNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(gameplaySceneNames[i])
                && string.Equals(gameplaySceneNames[i], sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Nạp mức âm lượng đã lưu ở lần chơi trước (nếu có), đè lên giá trị mặc định trong Inspector.</summary>
    private void LoadSavedVolumes()
    {
        if (PlayerPrefs.HasKey(MusicVolumeKey))
        {
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, musicVolume));
        }

        if (PlayerPrefs.HasKey(SfxVolumeKey))
        {
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume));
        }
    }

    /// <summary>Đổi âm lượng nhạc nền (0..1) - màn Settings gọi khi kéo slider. Mức mới được lưu lại.</summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null) musicSource.volume = musicVolume;

        SaveVolume(MusicVolumeKey, musicVolume);
    }

    /// <summary>Đổi âm lượng hiệu ứng (0..1) - màn Settings gọi khi kéo slider. Mức mới được lưu lại.</summary>
    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null) sfxSource.volume = sfxVolume;

        SaveVolume(SfxVolumeKey, sfxVolume);
    }

    /// <summary>
    /// Ghi mức âm lượng xuống PlayerPrefs. KHÔNG gọi <see cref="PlayerPrefs.Save"/> ở đây vì slider bắn
    /// sự kiện liên tục trong lúc kéo - Unity tự lưu khi thoát game (OnApplicationQuit).
    /// </summary>
    private static void SaveVolume(string key, float volume)
    {
        PlayerPrefs.SetFloat(key, volume);
    }

    /// <summary>
    /// Xoá mức âm lượng đã lưu để lần chạy sau quay về đúng giá trị mặc định ghi trên AudioManager
    /// trong Inspector. <see cref="GameDataReset.ClearAll"/> gọi hàm này khi người chơi bấm "Reset Data".
    /// </summary>
    public static void ClearSavedVolumes()
    {
        PlayerPrefs.DeleteKey(MusicVolumeKey);
        PlayerPrefs.DeleteKey(SfxVolumeKey);
        PlayerPrefs.Save();
    }

    // ==========================================
    // HIỆU ỨNG ÂM THANH (API tĩnh cho nơi gọi)
    // ==========================================

    /// <summary>Tiếng slime tấn công (gọi ở StartAttack của mọi loại lính).</summary>
    public static void PlayAttack()
    {
        if (Instance != null) Instance.PlayAttackInternal();
    }

    /// <summary>Tiếng slime di chuyển (gọi khi bắt đầu 1 cú nhảy).</summary>
    public static void PlayMove()
    {
        if (Instance != null) Instance.PlayMoveInternal();
    }

    /// <summary>Tiếng chạm button (gắn tự động qua <see cref="ButtonClickSound"/>).</summary>
    public static void PlayButtonClick()
    {
        if (Instance != null) Instance.PlaySfx(Instance.buttonClickClip);
    }

    /// <summary>Tiếng nâng cấp thành (gọi khi nâng thành thành công).</summary>
    public static void PlayUpgrade()
    {
        if (Instance != null) Instance.PlaySfx(Instance.upgradeClip);
    }

    /// <summary>Tiếng dùng kỹ năng THIÊN THẠCH (nút btn_meteor - xem <c>AbilityBar.CastMeteor</c>).</summary>
    public static void PlayMeteor()
    {
        if (Instance != null) Instance.PlaySfx(Instance.meteorClip);
    }

    /// <summary>Tiếng dùng kỹ năng TĂNG VIỆN (nút btn_reinfor_blue - xem <c>AbilityBar.CallReinforcement</c>).</summary>
    public static void PlaySummon()
    {
        if (Instance != null) Instance.PlaySfx(Instance.summonClip);
    }

    /// <summary>
    /// ÂM THANH LÚC KẾT THÚC TRẬN: tắt nhạc nền và MỌI hiệu ứng đang dở rồi chỉ phát đúng tiếng thắng
    /// (wiin.mp3) hoặc thua (losse.mp3) - nhờ vậy ở trạng thái Win/Lose không còn tiếng nào khác lẫn vào.
    /// Gọi từ <c>DlgResult</c> (nơi cũng đặt <c>Time.timeScale = 0</c> để đóng băng trận).
    /// </summary>
    /// <param name="playerWon">true = người chơi cục bộ THẮNG (phát wiin), false = THUA (phát losse).</param>
    public static void PlayMatchResult(bool playerWon)
    {
        if (Instance != null) Instance.PlayMatchResultInternal(playerWon);
    }

    private void PlayMatchResultInternal(bool playerWon)
    {
        // Cắt nhạc nền + các tiếng đang chồng (lính đánh, tiếng nút...) TRƯỚC khi phát tiếng kết quả:
        // Stop() trên nguồn hiệu ứng dừng luôn cả các PlayOneShot đang chạy.
        if (musicSource != null) musicSource.Stop();
        if (sfxSource != null) sfxSource.Stop();

        PlaySfx(playerWon ? winClip : loseClip);
    }

    private void PlayAttackInternal()
    {
        if (!CanPlay(ref lastAttackTime, attackRepeatCooldown)) return;
        PlaySfx(attackClip);
    }

    private void PlayMoveInternal()
    {
        if (!CanPlay(ref lastMoveTime, moveRepeatCooldown)) return;
        PlaySfx(moveClip);
    }

    /// <summary>Phát 1 clip hiệu ứng (chồng tiếng được). Bỏ qua nếu clip hoặc nguồn phát trống.</summary>
    public void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;

        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>
    /// Còn được phát không (đã qua khoảng lặng tối thiểu chưa). Luôn cập nhật mốc thời gian khi phát.
    /// Dùng <c>Time.unscaledTime</c> để không phụ thuộc timeScale.
    /// </summary>
    private static bool CanPlay(ref float lastTime, float cooldown)
    {
        float now = Time.unscaledTime;

        if (cooldown > 0f && now - lastTime < cooldown) return false;

        lastTime = now;
        return true;
    }

    // ==========================================
    // GẮN TIẾNG CLICK CHO MỌI BUTTON
    // ==========================================

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Đổi scene: gắn tiếng click cho button của scene mới + đổi nhạc nền nếu scene đó có bài riêng.
        HookButtonsInScene(scene);
        ApplyMusicForScene(scene);
    }

    private void HookButtonsInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            AttachButtonSounds(root);
        }
    }

    /// <summary>
    /// Gắn <see cref="ButtonClickSound"/> cho MỌI Button nằm trong <paramref name="root"/>
    /// (kể cả object đang tắt). Dialog sinh lúc chạy thì <see cref="UIManager.ShowDialog"/> gọi hàm này.
    /// </summary>
    public static void HookButtonSounds(GameObject root)
    {
        if (Instance != null) Instance.AttachButtonSounds(root);
    }

    private void AttachButtonSounds(GameObject root)
    {
        if (root == null) return;

        // includeInactive = true: button trong panel/dialog đang tắt vẫn phải được gắn sẵn.
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.GetComponent<ButtonClickSound>() == null)
            {
                button.gameObject.AddComponent<ButtonClickSound>();
            }
        }
    }
}
