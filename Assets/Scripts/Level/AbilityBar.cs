using System;
using System.Collections.Generic;
using FortressClash.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// DÃY NÚT KỸ NĂNG CHỦ ĐỘNG của người chơi (thiên thạch / tăng viện / hiệu triệu).
///
/// VÌ SAO CẦN: trước đây người chơi chỉ có đúng MỘT hành động - bấm mua lính. Mọi quyết định đều nằm ở
/// chỗ tích tiền, còn diễn biến trận đấu thì không có gì để "ra tay". Kỹ năng chủ động thêm khoảnh khắc
/// người chơi tự tay đổi cục diện (dọn một đợt quân đông, cứu thế trận khi bị áp sát, giữ đội hình trụ
/// lại), và cũng là thứ khiến 2 level khác nhau về CÁCH CHƠI: level mở kỹ năng nào là do
/// <see cref="LevelSettings.abilities"/> quyết định.
///
/// 3 KỸ NĂNG
///   - <see cref="ActiveAbilityType.Meteor"/>    : gây sát thương diện rộng quanh con lính địch ĐI ĐẦU
///     HÀNG (tuyến đầu phe địch - xem GameManager.CastMeteor).
///   - <see cref="ActiveAbilityType.Reinforce"/> : gọi ngay vài lính phe mình, không tốn thời gian chờ mua.
///   - <see cref="ActiveAbilityType.Rally"/>     : hồi đầy máu lính phe mình + giảm sát thương chúng nhận vào.
///
/// COMPONENT NÀY CHỈ LO PHẦN UI. Toàn bộ phần đổi cục diện trận đấu - trừ tiền, gọi lính, gây sát thương,
/// đồng bộ mạng - nằm ở <see cref="GameManager"/> (xem TryUseAbility / ExecuteAbility / ApplyRemoteAbilityCast).
/// Nhờ vậy chơi đơn và MultiPlayer dùng chung đúng một bộ luật:
///   - <see cref="abilityConfigs"/> (tiền / hồi chiêu / sát thương...) được đăng ký sang GameManager lúc
///     vào trận, để cả hai bên đọc CÙNG một bộ số.
///   - Nút chỉ bị làm xám khi ĐANG HỒI CHIÊU hoặc KHÔNG ĐỦ TIỀN; kỹ năng không có mục tiêu để tác động
///     (bấm thiên thạch lúc sân trống...) thì cứ để bấm - GameManager kiểm tra và KHÔNG trừ tiền.
///
/// PHE (MultiPlayer): mỗi máy chỉ bấm được kỹ năng của PHE MÌNH. Host là phe XANH, client là phe ĐỎ
/// (xem <see cref="NetworkRole.LocalIsBlue"/>); kỹ năng luôn đánh vào phe đối diện. Máy client chỉ gửi
/// yêu cầu lên host, nên hồi chiêu hiện trên nút là hồi chiêu THẬT do host chốt.
///
/// ẢNH NÚT cũng theo phe: 2 máy chạy CHUNG một scene nên scene chỉ gắn sẵn được MỘT bộ ảnh (bộ xanh) -
/// máy client đổi sang bộ ảnh đỏ lúc dựng nút (<see cref="ApplyLocalSideArt"/>, art lấy từ
/// <see cref="UIManager.LocalSideAbilitySprite"/>).
///
/// KHÔNG CẦN SỬA SCENE: component tự tạo chính mình khi vào scene chơi (xem <see cref="Bootstrap"/>) và
/// tự dựng dãy nút nếu các ô gán còn trống. Muốn dùng UI tự làm thì gắn component này vào scene, kéo nút
/// + nhãn vào các ô gán (thứ tự ô = thứ tự <see cref="ActiveAbilityType"/>: 0 Meteor, 1 Reinforce,
/// 2 Rally) rồi để các nhãn làm CON của nút - nút của kỹ năng không được mở sẽ bị ẩn cả cụm. Cách tạo UI
/// trong scene nhanh nhất là menu "Fortress Clash / Gameplay / 1 - Tạo UI kỹ năng chủ động" (chạy được cho
/// cả scene Gameplay lẫn MultiPlayer - xem <c>AbilityBarSetupTool</c>).
/// </summary>
public class AbilityBar : MonoBehaviour
{
    /// <summary>Thông số của MỘT kỹ năng do người dùng chỉnh trong Inspector (xem <see cref="AbilitySpec"/>).</summary>
    [Header("Dãy nút kỹ năng (để TRỐNG thì tự dựng UI lúc chạy)")]
    [Tooltip("Transform cha cho dãy nút tự dựng. Để TRỐNG thì tự tìm canvas MainHUD của scene.")]
    [SerializeField] private Transform uiParent;

    [Tooltip("Nút của từng kỹ năng, theo ĐÚNG thứ tự: 0 = Meteor, 1 = Reinforce, 2 = Rally.")]
    [SerializeField] private Button[] abilityButtons = new Button[ActiveAbilities.Count];

    [Tooltip("Nhãn TÊN kỹ năng của từng nút (cùng thứ tự với Ability Buttons).")]
    [SerializeField] private TextMeshProUGUI[] abilityNameLabels = new TextMeshProUGUI[ActiveAbilities.Count];

    [Tooltip("Nhãn GIÁ TIỀN của từng nút (cùng thứ tự).")]
    [SerializeField] private TextMeshProUGUI[] abilityCostLabels = new TextMeshProUGUI[ActiveAbilities.Count];

    [Tooltip("Nhãn ĐẾM NGƯỢC thời gian hồi của từng nút (cùng thứ tự).")]
    [SerializeField] private TextMeshProUGUI[] abilityCooldownLabels = new TextMeshProUGUI[ActiveAbilities.Count];

    [Tooltip("Ảnh phủ mờ chạy dần theo thời gian hồi (kiểu Filled / Radial 360). Cùng thứ tự.")]
    [SerializeField] private Image[] abilityCooldownFills = new Image[ActiveAbilities.Count];

    [Header("Hình thức dãy nút (chỉ dùng khi phải tự dựng)")]
    [SerializeField] private Vector2 slotSize = new Vector2(196f, 104f);
    [SerializeField] private float slotSpacing = 14f;
    [Tooltip("Vị trí dãy nút so với mép DƯỚI giữa màn hình. Đặt ở đáy vì HUD sẵn có dùng nửa trên màn hình.")]
    [SerializeField] private Vector2 rowAnchoredPosition = new Vector2(0f, 28f);
    [SerializeField] private float nameFontSize = 27f;
    [SerializeField] private float infoFontSize = 22f;
    [SerializeField] private Color slotBackgroundColor = new Color(0.09f, 0.15f, 0.23f, 0.92f);
    [SerializeField] private Color slotTextColor = new Color(0.95f, 0.97f, 1f, 1f);
    [SerializeField] private Color costTextColor = new Color(1f, 0.84f, 0.38f, 1f);
    [SerializeField] private Color cooldownFillColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("Thông số từng kỹ năng")]
    [Tooltip("Để TRỐNG thì dùng bộ số mặc định trong code (Thiên thạch 80 tiền / 25s, " +
             "Tăng viện 60 / 30s, Hiệu triệu 70 / 35s).")]
    [SerializeField] private AbilitySpec[] abilityConfigs = BuildDefaultConfigs();

    [Tooltip("Bao lâu quét lại một lần xem nút nào bấm được (đủ tiền, hết hồi chiêu). " +
             "Không cần quét mỗi frame vì tiền chỉ đổi theo nhịp vài phần mười giây.")]
    [Min(0.05f)] [SerializeField] private float stateRefreshInterval = 0.2f;

    /// <summary>Một ô kỹ năng trên HUD: các thành phần UI + thông số của kỹ năng đó.</summary>
    private class Slot
    {
        public ActiveAbilityType ability;
        public AbilitySpec config;
        public Button button;
        public TextMeshProUGUI costLabel;
        public TextMeshProUGUI cooldownLabel;
        public Image cooldownFill;
    }

    private readonly List<Slot> slots = new List<Slot>();

    /// <summary>Các kỹ năng được mở ở trận đang chơi, đã xếp theo thứ tự enum (để thứ tự nút luôn cố định).</summary>
    private readonly List<ActiveAbilityType> enabledAbilities = new List<ActiveAbilityType>();

    /// <summary>Thông số tra nhanh theo <see cref="ActiveAbilityType"/>.</summary>
    private readonly AbilitySpec[] configByType = new AbilitySpec[ActiveAbilities.Count];

    private float stateRefreshTimer;

    /// <summary>
    /// Tự tạo component khi vào scene chơi, y như DebugCheatPanel - nhờ vậy kỹ năng chạy được ngay mà
    /// không phải sửa scene Gameplay. Scene đã có AbilityBar (người dùng tự gắn để dùng UI riêng) thì thôi.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreate();
    }

    private static void TryCreate()
    {
        if (GameManager.Instance == null) return; // scene không phải màn chơi
        if (FindFirstObjectByType<AbilityBar>() != null) return;

        new GameObject("AbilityBar").AddComponent<AbilityBar>();
    }

    private void Start()
    {
        GameManager gameManager = GameManager.Instance;

        // Không phải màn chơi (không có GameManager) thì không có kỹ năng. Nút kỹ năng do người dùng đặt sẵn
        // trong scene KHÔNG phải con của component này nên phải ẩn tay trước khi tự huỷ - nếu không chúng
        // nằm lại trên HUD mà bấm vào chẳng có gì xảy ra.
        if (gameManager == null)
        {
            HideAllAssignedButtons();
            Destroy(gameObject);
            return;
        }

        BuildConfigLookup();

        // Đăng ký bộ số sang GameManager: từ đây GameManager là nơi THỰC THI kỹ năng (trừ tiền, gọi lính,
        // gây sát thương) và cũng là nơi đồng bộ sang máy kia, nên nó phải có đủ thông số.
        for (int i = 0; i < ActiveAbilities.Count; i++) gameManager.RegisterAbilitySpec(configByType[i]);

        CollectEnabledAbilities(gameManager);

        // Trận này không mở kỹ năng nào (ví dụ level 1) -> ẩn nút rồi tự huỷ cho sạch scene.
        if (enabledAbilities.Count == 0)
        {
            HideAllAssignedButtons();
            Destroy(gameObject);
            return;
        }

        // Dựng UI có bọc lỗi: một lỗi khi tạo nút (ví dụ gắn 2 Graphic lên cùng một object) KHÔNG được làm
        // chết cả component - nút nào dựng xong trước đó vẫn dùng được, và log phải nói rõ vấn đề.
        try
        {
            BuildSlots();
        }
        catch (Exception e)
        {
            Debug.LogError($"AbilityBar: dựng dãy nút kỹ năng thất bại ({e.Message}).");
        }

        RefreshSlotStates();

        Debug.Log($"AbilityBar: phe {(gameManager.LocalPlayerIsBlue ? "xanh" : "đỏ")} mở " +
                  $"{enabledAbilities.Count} kỹ năng - {DescribeEnabled()}");
    }

    private void Update()
    {
        if (slots.Count == 0) return;

        UpdateCooldownVisuals();

        // Quét trạng thái bấm được theo nhịp thưa (xem stateRefreshInterval) - tiền chỉ đổi theo nhịp vài
        // phần mười giây; còn phần đếm ngược thời gian hồi thì phải mượt nên chạy mỗi frame.
        stateRefreshTimer -= Time.deltaTime;
        if (stateRefreshTimer <= 0f)
        {
            stateRefreshTimer = Mathf.Max(0.05f, stateRefreshInterval);
            RefreshSlotStates();
        }
    }

    // ==========================================
    // CHUẨN BỊ
    // ==========================================

    /// <summary>Gom các kỹ năng đang được mở ở trận này, theo thứ tự enum để thứ tự nút luôn cố định.</summary>
    private void CollectEnabledAbilities(GameManager gameManager)
    {
        enabledAbilities.Clear();

        for (int i = 0; i < ActiveAbilities.Count; i++)
        {
            ActiveAbilityType ability = (ActiveAbilityType)i;
            if (gameManager.IsAbilityEnabled(ability)) enabledAbilities.Add(ability);
        }
    }

    /// <summary>
    /// Dựng bảng tra thông số theo kỹ năng. Ô nào trong Inspector còn thiếu thì lấy từ bộ mặc định trong
    /// code, nhờ vậy xoá bớt phần tử trong Ability Configs cũng không làm kỹ năng mất thông số.
    /// </summary>
    private void BuildConfigLookup()
    {
        AbilitySpec[] defaults = BuildDefaultConfigs();

        for (int i = 0; i < ActiveAbilities.Count; i++) configByType[i] = null;

        if (abilityConfigs != null)
        {
            foreach (AbilitySpec config in abilityConfigs)
            {
                if (config == null) continue;

                int index = (int)config.ability;
                if (index < 0 || index >= ActiveAbilities.Count) continue;

                configByType[index] = config;
            }
        }

        foreach (AbilitySpec fallback in defaults)
        {
            int index = (int)fallback.ability;
            if (configByType[index] == null) configByType[index] = fallback;
        }
    }

    /// <summary>
    /// Bộ thông số mặc định. Cố ý để ở code chứ không chỉ ở Inspector: component này thường được TỰ TẠO
    /// lúc chạy nên nếu chỉ dựa vào Inspector thì game sẽ chạy với thông số rỗng.
    /// </summary>
    private static AbilitySpec[] BuildDefaultConfigs()
    {
        return new[]
        {
            new AbilitySpec
            {
                ability = ActiveAbilityType.Meteor,
                displayName = "Thiên thạch",
                cost = 80,
                cooldown = 25f,
                radius = 15f,
                damage = 300
            },
            new AbilitySpec
            {
                ability = ActiveAbilityType.Reinforce,
                displayName = "Tăng viện",
                cost = 60,
                cooldown = 30f,
                unitCount = 2,
                reinforceUnit = AIUnitType.Sword
            },
            new AbilitySpec
            {
                ability = ActiveAbilityType.Rally,
                displayName = "Hiệu triệu",
                cost = 70,
                cooldown = 35f,
                damageReduction = 0.5f,
                duration = 10f
            }
        };
    }

    /// <summary>Dựng dãy nút: dùng UI gán trong Inspector nếu có, không thì tự dựng bằng code.</summary>
    private void BuildSlots()
    {
        slots.Clear();

        if (HasInspectorButton()) BuildSlotsFromInspector();
        else BuildSlotsUi();

        ApplyLocalSideArt();
    }

    /// <summary>
    /// Đổi ảnh nền nút kỹ năng theo PHE CỦA NGƯỜI CHƠI CỤC BỘ. Hai máy chơi MultiPlayer chạy CHUNG một
    /// scene nên không thể đặt sẵn ảnh đỏ trong scene: scene giữ bộ ảnh XANH, còn máy client (phe đỏ) lấy
    /// ảnh đỏ từ <see cref="UIManager.LocalSideAbilitySprite"/> ngay lúc dựng dãy nút. Chơi đơn và host
    /// (phe xanh) không phải đổi gì.
    ///
    /// Cố ý làm SAU khi dãy nút đã dựng xong: nút lấy ảnh nào là do scene/Inspector quyết định, đây chỉ
    /// là bước ghi đè ảnh cho phe đỏ. Chỉ đụng tới ảnh NỀN của chính nút - nhãn giá tiền / thời gian hồi
    /// là con của nút nên giữ nguyên.
    /// </summary>
    private void ApplyLocalSideArt()
    {
        UIManager ui = UIManager.Instance;
        if (ui == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot.button == null) continue;

            // null = phe xanh, hoặc kỹ năng không có bản đỏ (Thiên thạch) -> giữ ảnh đang gắn trong scene.
            Sprite art = ui.LocalSideAbilitySprite(slot.ability);
            if (art == null) continue;

            // targetGraphic của Button chính là Image nền đang nhìn thấy; chưa gán thì lấy Image nằm ngay
            // trên GameObject của nút (giống UIManager.SetButtonSprite).
            Image background = slot.button.targetGraphic as Image;
            if (background == null) background = slot.button.GetComponent<Image>();

            if (background != null) background.sprite = art;
        }
    }

    /// <summary>Có ô Button nào được gán trong Inspector chưa (tức là người dùng muốn dùng UI tự làm).</summary>
    private bool HasInspectorButton()
    {
        for (int i = 0; i < ActiveAbilities.Count; i++)
        {
            if (Pick(abilityButtons, i) != null) return true;
        }

        return false;
    }

    /// <summary>
    /// Ẩn MỌI nút đã gán trong Inspector. Gọi khi trận không mở kỹ năng nào (hoặc không phải màn chơi):
    /// nút do người dùng đặt trong scene không phải con của component này nên nếu không ẩn thì nó nằm lại
    /// trên HUD, bấm vào không có gì xảy ra và trông như lỗi.
    /// </summary>
    private void HideAllAssignedButtons()
    {
        for (int i = 0; i < ActiveAbilities.Count; i++)
        {
            Button button = Pick(abilityButtons, i);
            if (button != null) button.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Dùng UI gán trong Inspector. Nút của kỹ năng KHÔNG được mở sẽ bị ẩn cả cụm GameObject (nên các
    /// nhãn phải là con của nút), còn vị trí thì giữ nguyên như người dùng đã sắp trong scene.
    /// </summary>
    private void BuildSlotsFromInspector()
    {
        for (int i = 0; i < ActiveAbilities.Count; i++)
        {
            ActiveAbilityType ability = (ActiveAbilityType)i;
            Button button = Pick(abilityButtons, i);
            bool isEnabled = enabledAbilities.Contains(ability);

            if (button != null) button.gameObject.SetActive(isEnabled);

            if (!isEnabled) continue;

            if (button == null)
            {
                Debug.LogWarning($"AbilityBar: trận này mở kỹ năng {ability} nhưng ô Ability Buttons số {i} " +
                                 "chưa được gán nên kỹ năng này không bấm được.");
                continue;
            }

            AddSlot(ability, button, Pick(abilityCostLabels, i), Pick(abilityCooldownLabels, i),
                Pick(abilityCooldownFills, i));
        }

        // Nhãn tên của nút đã gán sẵn thì chỉ cần ghi tên kỹ năng vào.
        for (int i = 0; i < ActiveAbilities.Count; i++)
        {
            TextMeshProUGUI nameLabel = Pick(abilityNameLabels, i);
            if (nameLabel == null) continue;

            nameLabel.SetText(configByType[i].DisplayName);
        }
    }

    /// <summary>Tự dựng dãy nút dưới canvas HUD của scene, mỗi kỹ năng được mở một nút.</summary>
    private void BuildSlotsUi()
    {
        Transform parent = uiParent;

        if (parent == null)
        {
            Canvas canvas = RuntimeHudBuilder.FindHudCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("AbilityBar: không tìm thấy Canvas nào trong scene nên không dựng được dãy nút " +
                                 "kỹ năng. Gán tay vào ô Ui Parent, hoặc thêm một Canvas vào scene.");
                return;
            }

            parent = canvas.transform;
        }

        float step = slotSize.x + slotSpacing;
        float firstX = -step * (enabledAbilities.Count - 1) * 0.5f;

        for (int i = 0; i < enabledAbilities.Count; i++)
        {
            ActiveAbilityType ability = enabledAbilities[i];
            AbilitySpec config = configByType[(int)ability];

            Vector2 position = rowAnchoredPosition + new Vector2(firstX + (step * i), 0f);

            RectTransform slotRect = RuntimeHudBuilder.CreateRect(parent, $"ability_{ability}",
                RuntimeHudBuilder.BottomCenterAnchor, RuntimeHudBuilder.BottomCenterAnchor, position, slotSize);

            Image background = RuntimeHudBuilder.AddImage(slotRect, slotBackgroundColor, null, true);
            Button button = RuntimeHudBuilder.AddButton(slotRect, background);

            // Vòng phủ mờ chạy dần = phần thời gian hồi còn lại. Là CON của nút nên nằm trên nền và dưới
            // các nhãn (nhãn tạo sau sẽ vẽ đè lên).
            RectTransform fillRect = RuntimeHudBuilder.CreateRect(slotRect, "cooldown_fill",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, slotSize);

            Image cooldownFill = RuntimeHudBuilder.AddImage(fillRect, cooldownFillColor);
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.fillMethod = Image.FillMethod.Radial360;
            cooldownFill.fillOrigin = (int)Image.Origin360.Top;
            cooldownFill.fillAmount = 0f;

            RectTransform nameRect = RuntimeHudBuilder.CreateRect(slotRect, "text_name",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(slotSize.x, 34f));

            RuntimeHudBuilder.AddLabel(nameRect, config.DisplayName, nameFontSize, slotTextColor);

            RectTransform costRect = RuntimeHudBuilder.CreateRect(slotRect, "text_cost",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(slotSize.x, 30f));

            TextMeshProUGUI costLabel = RuntimeHudBuilder.AddLabel(costRect, config.cost.ToString(), infoFontSize, costTextColor);

            RectTransform cooldownRect = RuntimeHudBuilder.CreateRect(slotRect, "text_cooldown",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, slotSize);

            TextMeshProUGUI cooldownLabel = RuntimeHudBuilder.AddLabel(cooldownRect, "", nameFontSize, slotTextColor);

            AddSlot(ability, button, costLabel, cooldownLabel, cooldownFill);
        }
    }

    /// <summary>Gắn 1 ô kỹ năng vào danh sách và nối sự kiện bấm nút.</summary>
    private void AddSlot(ActiveAbilityType ability, Button button, TextMeshProUGUI costLabel,
        TextMeshProUGUI cooldownLabel, Image cooldownFill)
    {
        Slot slot = new Slot
        {
            ability = ability,
            config = configByType[(int)ability],
            button = button,
            costLabel = costLabel,
            cooldownLabel = cooldownLabel,
            cooldownFill = cooldownFill
        };

        button.onClick.AddListener(() => TryUse(slot));
        slots.Add(slot);
    }

    // ==========================================
    // DÙNG KỸ NĂNG
    // ==========================================

    /// <summary>
    /// Bấm 1 kỹ năng. Component này chỉ KIỂM TRA SƠ (trận đã xong? đang hồi? đủ tiền?) rồi giao cho
    /// <see cref="GameManager.TryUseAbility"/> - nơi biết phe của người bấm và biết đường đi (chơi đơn /
    /// host thực thi / client gửi yêu cầu lên host).
    ///
    /// Chốt an toàn "không mất tiền khi kỹ năng không có tác dụng" nằm TRONG GameManager (kiểm tra mục tiêu
    /// rồi mới trừ tiền), nên ở đây không cần đoán.
    /// </summary>
    private void TryUse(Slot slot)
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null) return;

        if (gameManager.IsMatchOver)
        {
            Debug.Log("Trận đã có kết quả, không dùng kỹ năng được nữa.");
            return;
        }

        bool localSide = gameManager.LocalPlayerIsBlue;

        if (gameManager.AbilityCooldownRemaining(localSide, slot.ability) > 0f)
        {
            Debug.Log($"{slot.config.DisplayName} đang hồi, còn " +
                      $"{gameManager.AbilityCooldownRemaining(localSide, slot.ability):0.#}s.");
            return;
        }

        int cost = Mathf.Max(0, slot.config.cost);
        if (gameManager.LocalMoney < cost)
        {
            Debug.Log($"Không đủ tiền dùng {slot.config.DisplayName} (cần {cost}, " +
                      $"đang có {gameManager.LocalMoney}).");
            return;
        }

        // Máy client: lệnh đã được gửi lên host, hồi chiêu và hình ảnh sẽ tới theo gói tin của host.
        if (!gameManager.TryUseAbility(localSide, slot.ability))
        {
            Debug.Log($"{slot.config.DisplayName}: không dùng được (xem log lý do ngay trên).");
            return;
        }

        RefreshSlotStates();
    }

    // ==========================================
    // HIỂN THỊ TRẠNG THÁI NÚT
    // ==========================================

    /// <summary>
    /// Làm xám/sáng nút và ghi nhãn giá tiền. Nút bấm được khi: trận CHƯA KẾT THÚC, kỹ năng ĐÃ HỒI XONG và
    /// ĐỦ TIỀN mua.
    ///
    /// CỐ Ý KHÔNG xét "có mục tiêu để tác động hay chưa": bấm thiên thạch lúc trên sân chưa có lính địch,
    /// hay hiệu triệu lúc chưa có lính mình, thì kỹ năng không có tác dụng - nhưng GameManager đã lo phần
    /// đó (không trừ tiền, không vào hồi chiêu, chỉ ghi lý do ra Console). Làm xám nút theo mục tiêu thì
    /// đầu trận nút sáng/tối thất thường theo diễn biến trên sân, khó đoán hơn là cứ để bấm.
    ///
    /// Hồi chiêu đọc từ <see cref="GameManager"/> chứ không giữ trong component: ở MultiPlayer hồi chiêu do
    /// HOST chốt và gửi sang, nên nếu tự đếm ở đây thì máy client sẽ hồi chiêu oan những lần host từ chối.
    /// </summary>
    private void RefreshSlotStates()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null) return;

        bool matchOver = gameManager.IsMatchOver;
        bool localSide = gameManager.LocalPlayerIsBlue;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot.button == null) continue;

            int cost = Mathf.Max(0, slot.config.cost);
            bool affordable = gameManager.LocalMoney >= cost;
            bool cooling = gameManager.AbilityCooldownRemaining(localSide, slot.ability) > 0f;

            slot.button.interactable = affordable && !matchOver && !cooling;

            if (slot.costLabel != null)
            {
                slot.costLabel.SetText(affordable ? cost.ToString() : $"({cost})");
            }
        }
    }

    /// <summary>Cập nhật nhãn đếm ngược và vòng phủ mờ - chạy mỗi frame nên phải thật nhẹ.</summary>
    private void UpdateCooldownVisuals()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null) return;

        bool localSide = gameManager.LocalPlayerIsBlue;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];

            float remaining = gameManager.AbilityCooldownRemaining(localSide, slot.ability);
            bool cooling = remaining > 0f;

            if (slot.cooldownLabel != null)
            {
                // Ghi lại chuỗi mỗi frame là đủ nhẹ, nhưng tránh cập nhật khi chuỗi không đổi để khỏi
                // bắt TextMeshPro dựng lại lưới chữ.
                string text = cooling ? Mathf.CeilToInt(remaining).ToString() : "";
                if (slot.cooldownLabel.text != text) slot.cooldownLabel.SetText(text);
            }

            if (slot.cooldownFill != null)
            {
                float total = Mathf.Max(0.01f, slot.config.cooldown);
                slot.cooldownFill.fillAmount = cooling ? Mathf.Clamp01(remaining / total) : 0f;
            }
        }
    }

    /// <summary>Lấy phần tử thứ <paramref name="index"/> của một mảng, chịu được mảng null/thiếu phần tử.</summary>
    private static T Pick<T>(T[] array, int index) where T : class
    {
        if (array == null || index < 0 || index >= array.Length) return null;

        return array[index];
    }

    /// <summary>Tên các kỹ năng đang mở để ghi ra Console lúc vào trận.</summary>
    private string DescribeEnabled()
    {
        string text = "";

        for (int i = 0; i < enabledAbilities.Count; i++)
        {
            if (text.Length > 0) text += ", ";
            text += configByType[(int)enabledAbilities[i]].DisplayName;
        }

        return text;
    }
}
