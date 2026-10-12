using Unity.Netcode;
using UnityEngine;

/// <summary>
/// LỚP ĐỒNG BỘ của scene MultiPlayer - cầu nối giữa gameplay (chơi đơn, <see cref="GameManager"/>)
/// và Netcode. Nó KHÔNG chứa luật chơi: mọi quyết định vẫn nằm ở <see cref="GameManager"/>,
/// component này chỉ lo 2 việc:
///
///   1. ĐỒNG BỘ TRẠNG THÁI từ host sang client: tiền của 2 phe (NetworkVariable), máu King,
///      cấp nâng thành và kết quả trận (Rpc).
///   2. CHUYỂN YÊU CẦU của client lên host: mua lính, nâng thành. Host mới là bên kiểm tra
///      tiền / giới hạn lính / thời gian chờ rồi thực sự sinh lính.
///
/// Component nằm CÙNG GameObject với <see cref="GameManager"/> (GameObject này được gắn thêm
/// NetworkObject). Chơi đơn thì component không tồn tại nên mọi thứ chạy y như trước.
/// </summary>
public class NetworkGame : NetworkBehaviour
{
    /// <summary>Instance đang sống của lớp đồng bộ; null khi chơi đơn.</summary>
    public static NetworkGame Instance { get; private set; }

    /// <summary>Tiền phe XANH (host). Host ghi, cả 2 máy đọc.</summary>
    private NetworkVariable<int> blueMoney = new NetworkVariable<int>(0);

    /// <summary>Tiền phe ĐỎ (client). Host ghi, cả 2 máy đọc.</summary>
    private NetworkVariable<int> redMoney = new NetworkVariable<int>(0);

    /// <summary>Máy này có quyền ghi trạng thái mạng không (chỉ host).</summary>
    public bool IsAuthority => IsSpawned && IsServer;

    public int BlueMoney => blueMoney.Value;
    public int RedMoney => redMoney.Value;

    // =========================================================
    // VÒNG ĐỜI
    // =========================================================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Instance = this;

        blueMoney.OnValueChanged += OnBlueMoneyChanged;
        redMoney.OnValueChanged += OnRedMoneyChanged;

        // Giá trị khởi tạo đi kèm gói spawn nên OnValueChanged KHÔNG bắn cho lần đầu tiên:
        // tự áp một lần ở đây để client có ngay số tiền ban đầu.
        MirrorMoney();

        Debug.Log($"[MULTIPLAYER] NetworkGame sẵn sàng. IsServer={IsServer} IsClient={IsClient} IsHost={IsHost}");
    }

    public override void OnNetworkDespawn()
    {
        blueMoney.OnValueChanged -= OnBlueMoneyChanged;
        redMoney.OnValueChanged -= OnRedMoneyChanged;

        if (Instance == this) Instance = null;

        base.OnNetworkDespawn();
    }

    private void OnBlueMoneyChanged(int previous, int current) => MirrorMoney();
    private void OnRedMoneyChanged(int previous, int current) => MirrorMoney();

    /// <summary>Đẩy số tiền đã đồng bộ vào GameManager (để UI và luật chơi đọc như thường lệ).</summary>
    private void MirrorMoney()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        gm.SetSyncedMoney(true, blueMoney.Value);
        gm.SetSyncedMoney(false, redMoney.Value);
    }

    // =========================================================
    // API CHO GameManager GỌI (host/offline)
    // =========================================================

    /// <summary>Host công bố số tiền mới của 1 phe. Client không gọi hàm này.</summary>
    public void PublishMoney(bool blueSide, int value)
    {
        if (!IsAuthority) return;

        if (blueSide) blueMoney.Value = Mathf.Max(0, value);
        else redMoney.Value = Mathf.Max(0, value);
    }

    /// <summary>Host công bố máu King cho client (để client vẽ thanh máu + chạy animation trúng đòn).</summary>
    public void PublishKingHealth(bool blueSide, int health, int maxHealth, bool playHit)
    {
        if (!IsAuthority) return;

        SyncKingHealthRpc(blueSide, health, maxHealth, playHit);
    }

    /// <summary>Host công bố cấp nâng thành của 1 phe cho client (để client vẽ hình thành cho đúng).</summary>
    public void PublishCastleLevel(bool blueSide, int level)
    {
        if (!IsAuthority) return;

        SyncCastleLevelRpc(blueSide, level);
    }

    /// <summary>Host công bố kết quả trận cho client (host tự hiện popup của mình).</summary>
    public void PublishMatchResult(bool blueWon)
    {
        if (!IsAuthority) return;

        ShowResultRpc(blueWon);
    }

    /// <summary>
    /// Host công bố một hiệu ứng VFX gắn với vòng đời lính (sinh ra / bị hạ gục) cho client, để máy
    /// client cũng thấy hiệu ứng như host. Host đã tự phát cục bộ trước khi gọi hàm này.
    ///
    /// Vì sao phải đồng bộ riêng: lính do HOST mô phỏng, client tắt component <see cref="Slime"/> nên
    /// các hàm phát hiệu ứng trong đó không chạy ở client - xem <see cref="VfxManager.PlayNetworked"/>.
    /// Chơi đơn / client gọi thì tự bỏ qua (chỉ host gửi).
    /// </summary>
    /// <param name="vfxType">Chỉ số <see cref="VfxType"/>.</param>
    /// <param name="position">Vị trí thế giới đặt hiệu ứng.</param>
    public void PublishVfx(int vfxType, Vector3 position)
    {
        if (!IsAuthority) return;

        PlayVfxRpc(vfxType, position);
    }

    // =========================================================
    // API CHO UI GỌI (máy người chơi)
    // =========================================================

    /// <summary>
    /// Người chơi cục bộ vừa bấm mua 1 loại lính. Host thì mua luôn, client thì gửi yêu cầu lên host.
    /// </summary>
    public void RequestSpawn(bool blueSide, int unitType)
    {
        if (!IsSpawned)
        {
            // Chưa spawn xong (chơi đơn hoặc chưa vào mạng): rơi về đường chơi đơn của GameManager.
            GameManager.Instance?.HandleLocalSpawn(blueSide, unitType);
            return;
        }

        if (IsServer)
        {
            GameManager.Instance?.HandleLocalSpawn(blueSide, unitType);
        }
        else
        {
            RequestSpawnRpc(blueSide, unitType);
        }
    }

    /// <summary>Người chơi cục bộ vừa bấm nâng thành của phe mình.</summary>
    public void RequestUpgrade(bool blueSide)
    {
        if (!IsSpawned)
        {
            GameManager.Instance?.HandleLocalUpgrade(blueSide);
            return;
        }

        if (IsServer)
        {
            GameManager.Instance?.HandleLocalUpgrade(blueSide);
        }
        else
        {
            RequestUpgradeRpc(blueSide);
        }
    }

    /// <summary>
    /// Người chơi cục bộ vừa bấm 1 KỸ NĂNG CHỦ ĐỘNG. Host thì thực thi luôn, client thì gửi yêu cầu lên
    /// host - giống hệt cách mua lính và nâng thành đi qua đây, nhờ vậy tiền và sát thương chỉ được tính
    /// MỘT LẦN ở máy chủ.
    ///
    /// Chỉ số kỹ năng đi qua mạng chứ không phải cả bộ thông số: 2 máy chạy cùng một scene nên bộ số chỉnh
    /// trong Inspector của AbilityBar là giống nhau, host tự tra bộ số của mình (xem
    /// <c>GameManager.GetAbilitySpec</c>).
    /// </summary>
    public void RequestAbility(bool casterBlueSide, int abilityIndex)
    {
        if (!IsSpawned)
        {
            // Chưa vào mạng (chơi đơn / chưa spawn xong): chạy thẳng đường chơi đơn.
            GameManager.Instance?.ExecuteAbility(casterBlueSide, (ActiveAbilityType)abilityIndex);
            return;
        }

        if (IsServer)
        {
            GameManager.Instance?.ExecuteAbility(casterBlueSide, (ActiveAbilityType)abilityIndex);
        }
        else
        {
            RequestAbilityRpc(casterBlueSide, abilityIndex);
        }
    }

    /// <summary>
    /// Host công bố một kỹ năng VỪA ĐƯỢC DÙNG cho client: máy client bắt đầu hồi chiêu đúng như host rồi
    /// chạy hình ảnh/âm thanh (xem <c>GameManager.ApplyRemoteAbilityCast</c>).
    ///
    /// Vì sao phải gửi cả hồi chiêu: nếu client tự tính hồi chiêu theo lúc nó bấm thì máy nào cũng đúng,
    /// nhưng khi host TỪ CHỐI (hết tiền, sân đầy, không có mục tiêu) client sẽ hồi chiêu oan. Gửi từ host thì
    /// chỉ những lần kỹ năng thực sự có tác dụng mới tính.
    /// </summary>
    /// <param name="meteorCenter">Điểm rơi thiên thạch (host chọn mục tiêu) - chỉ có ý nghĩa với kỹ năng Thiên thạch.</param>
    public void PublishAbilityCast(bool casterBlueSide, int abilityIndex, Vector3 meteorCenter, float cooldownSeconds)
    {
        if (!IsAuthority) return;

        AbilityCastRpc(casterBlueSide, abilityIndex, meteorCenter, cooldownSeconds);
    }

    // =========================================================
    // RPC
    // =========================================================

    /// <summary>Client xin host mua 1 lính. Host mới là bên kiểm tra tiền/giới hạn rồi sinh lính.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSpawnRpc(bool blueSide, int unitType)
    {
        GameManager.Instance?.HandleLocalSpawn(blueSide, unitType);
    }

    /// <summary>Client xin host nâng thành của phe mình.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestUpgradeRpc(bool blueSide)
    {
        GameManager.Instance?.HandleLocalUpgrade(blueSide);
    }

    /// <summary>
    /// Client xin host dùng 1 kỹ năng cho phe mình. Host mới là bên kiểm tra tiền / hồi chiêu / mục tiêu rồi
    /// thực thi và công bố kết quả ngược lại (xem <see cref="PublishAbilityCast"/>).
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestAbilityRpc(bool casterBlueSide, int abilityIndex)
    {
        GameManager.Instance?.ExecuteAbility(casterBlueSide, (ActiveAbilityType)abilityIndex);
    }

    /// <summary>Host báo MỌI CLIENT một kỹ năng vừa được dùng (hồi chiêu + hình ảnh + âm thanh).</summary>
    [Rpc(SendTo.NotServer)]
    private void AbilityCastRpc(bool casterBlueSide, int abilityIndex, Vector3 meteorCenter, float cooldownSeconds)
    {
        GameManager.Instance?.ApplyRemoteAbilityCast(casterBlueSide, abilityIndex, meteorCenter, cooldownSeconds);
    }

    /// <summary>Host đẩy máu King sang MỌI CLIENT (host tự biết máu của mình rồi).</summary>
    [Rpc(SendTo.NotServer)]
    private void SyncKingHealthRpc(bool blueSide, int health, int maxHealth, bool playHit)
    {
        GameManager.Instance?.ApplySyncedKingHealth(blueSide, health, maxHealth, playHit);
    }

    /// <summary>Host đẩy cấp nâng thành sang MỌI CLIENT.</summary>
    [Rpc(SendTo.NotServer)]
    private void SyncCastleLevelRpc(bool blueSide, int level)
    {
        GameManager.Instance?.ApplySyncedCastleLevel(blueSide, level);
    }

    /// <summary>Host báo MỌI CLIENT kết quả trận (host tự hiện popup của mình).</summary>
    [Rpc(SendTo.NotServer)]
    private void ShowResultRpc(bool blueWon)
    {
        GameManager.Instance?.HandleRemoteMatchResult(blueWon);
    }

    /// <summary>Host báo MỌI CLIENT phát một hiệu ứng VFX (spawn/chết) tại vị trí cho trước.</summary>
    [Rpc(SendTo.NotServer)]
    private void PlayVfxRpc(int vfxType, Vector3 position)
    {
        VfxManager.PlayById(vfxType, position);
    }
}
