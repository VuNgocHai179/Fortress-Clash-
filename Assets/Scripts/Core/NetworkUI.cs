using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FortressClash.Core;

/// <summary>
/// Màn TÌM TRẬN / nối mạng LAN (scene <c>MultiPlayerConnect</c>).
///
/// Có 2 lối vào:
///   - TỰ ĐỘNG: nút <c>btn_find_a_match</c> ở MainMenu đặt cờ <see cref="AutoMatchOnStart"/> rồi
///     load scene này; vào scene là <see cref="AutoMatch"/> tự chạy: quét host trong LAN, thấy thì
///     vào làm client, không thấy thì tự mở phòng và chờ người khác.
///   - THỦ CÔNG: nút "Host" và "Find hosts" trong scene (giữ lại để test tay).
/// </summary>
public class NetworkUI : Singleton<NetworkUI>
{
    // =========================================================
    // GIAO THỨC TÌM TRẬN TRONG LAN (UDP broadcast)
    // =========================================================

    /// <summary>Gói client gửi broadcast để hỏi "có phòng nào không?".</summary>
    private const string DiscoverMessage = "FORTRESSCLASH_DISCOVER";

    /// <summary>Gói host trả lời để nói "tao đây".</summary>
    private const string HostResponseMessage = "FORTRESSCLASH_HOST";

    /// <summary>Scene màn hình chính - nút huỷ tìm trận quay về đây.</summary>
    private const string MainMenuSceneName = "MainMenu";

    /// <summary>
    /// Được đặt TRUE từ MainMenu (nút <c>btn_find_a_match</c>) TRƯỚC khi load scene này.
    /// Là biến static vì cờ được set ở scene này nhưng đọc ở scene kia - instance của scene cũ
    /// không truyền sang được.
    /// </summary>
    public static bool AutoMatchOnStart;

    public string hostIP = "192.168.1.195";
    public ushort port = 7777;

    [Header("LAN Discovery")]
    [SerializeField] private ushort discoveryPort = 47777;

    private List<string> lanHosts = new List<string>();

    [SerializeField]
    private GameObject IpHost;

    [SerializeField]
    private Transform HostList;

    [Header("Tìm trận tự động (nút 'Find a match' ở MainMenu)")]
    [Tooltip("Panel phủ hiện trong lúc tìm trận và lúc chờ người chơi vào phòng.")]
    [SerializeField] private GameObject searchingPanel;

    [Tooltip("Dòng chữ trạng thái trong panel tìm trận (object 'StatusText' trong scene).")]
    [SerializeField] private TMP_Text searchingText;

    [Tooltip("Nút huỷ: dừng tìm trận / đóng phòng rồi quay về MainMenu.")]
    [SerializeField] private Button btnCancelSearch;

    [Tooltip("Số giây quét host trong LAN. Hết thời gian mà không ai trả lời thì máy này TỰ MỞ PHÒNG.")]
    [Min(0f)]
    [SerializeField] private float autoMatchSearchTimeout = 3f;

    [Tooltip("Con quay trong panel tìm trận (object 'Spinner' trong scene). Chỉ quay khi THỰC SỰ đang " +
             "tìm / đang chờ người chơi; lúc báo lỗi thì tắt đi cho khỏi quay vô ích.")]
    [SerializeField] private GameObject spinner;

    private UdpClient discoveryServer;
    private Coroutine discoveryServerCoroutine;

    /// <summary>TRUE khi panel tìm trận đang mở vì luồng tự động (đang quét, đang vào phòng hoặc đang chờ người).</summary>
    private bool autoMatching;

    /// <summary>
    /// NetworkUI là component của RIÊNG scene MultiPlayerConnect (panel tìm trận, danh sách host, nút huỷ
    /// đều nằm trong scene đó) nên KHÔNG DontDestroyOnLoad - giống UIManager / GameManager.
    ///
    /// Vì sao bắt buộc: để lớp cha <see cref="Singleton{T}"/> giữ instance qua DontDestroyOnLoad thì sau
    /// lần đầu vào màn tìm trận, instance cũ (đã mất sạch tham chiếu UI vì scene bị huỷ) vẫn sống.
    /// Lần vào sau, Awake của instance MỚI thấy Instance != null nên tự huỷ chính nó -> Start() không chạy
    /// -> không quét LAN, không mở phòng: bấm "Find a match" lần hai chẳng có gì xảy ra.
    /// </summary>
    protected override void Awake()
    {
        if (Instance != null && Instance != this) Destroy(Instance.gameObject);

        Instance = this;
    }

    private void Start()
    {
        AdoptSceneNetworkManager();

        // Không có NetworkManager thì mọi thứ bên dưới (quét LAN, mở phòng) đều vô nghĩa; chặn ở đây
        // để lỗi hiện ra rõ ràng thay vì NullReferenceException ở giữa coroutine tìm trận.
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NETWORK] Không có NetworkManager để tìm trận.");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        if (btnCancelSearch != null) btnCancelSearch.onClick.AddListener(CancelAutoMatch);

        ShowSearchingPanel(false);

        if (AutoMatchOnStart)
        {
            // Cờ chỉ dùng MỘT lần cho lần vào scene này; xoá ngay để lần sau vào bằng đường
            // thủ công thì không tự tìm trận nữa.
            AutoMatchOnStart = false;
            StartCoroutine(AutoMatch());
        }
    }

    /// <summary>Mở phòng với IP/port khai báo trên component (nút "Host" của scene gọi hàm này).</summary>
    public void StartHost()
    {
        StartHostInternal();
    }

    /// <summary>
    /// Mở phòng: lắng nghe trên mọi card mạng rồi <c>StartHost</c>, kèm bật dịch vụ trả lời quét LAN.
    /// Trả về FALSE nếu không mở được (thường là port đã bị chiếm) để màn tìm trận báo lỗi rõ ràng
    /// thay vì đứng im chờ mãi.
    /// </summary>
    private bool StartHostInternal()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NETWORK] Mất NetworkManager - không mở được phòng.");
            return false;
        }

        UnityTransport transport =
            NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetConnectionData(
            hostIP,
            port,
            "0.0.0.0"
        );

        Debug.Log("[NETWORK] Starting Host...");

        if (!NetworkManager.Singleton.StartHost())
        {
            Debug.LogError(
                $"[NETWORK] Không mở được phòng tại {hostIP}:{port}."
            );

            return false;
        }

        if (!StartLANDiscoveryServer())
        {
            // Phòng có mở nhưng KHÔNG quảng bá được (không bind được cổng quét LAN) thì người chơi khác
            // quét kiểu gì cũng không thấy -> coi như mở phòng thất bại để màn tìm trận báo rõ cho người
            // chơi bấm huỷ thử lại, thay vì đứng chờ một người sẽ không bao giờ vào được.
            Debug.LogError(
                $"[NETWORK] Không quảng bá được phòng qua cổng quét LAN {discoveryPort}."
            );

            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Bật dịch vụ trả lời quét LAN. Trả về FALSE nếu không bind được cổng quét LAN (thường là do một
    /// instance khác trên CÙNG máy đang giữ cổng) - lúc đó không ai tìm thấy phòng này được.
    /// </summary>
    private bool StartLANDiscoveryServer()
    {
        if (discoveryServerCoroutine != null) return true;

        if (!OpenDiscoverySocket()) return false;

        Debug.Log(
            $"[LAN] Host Discovery đang chạy tại port {discoveryPort}"
        );

        discoveryServerCoroutine =
            StartCoroutine(LANDiscoveryServer());

        return true;
    }

    private bool OpenDiscoverySocket()
    {
        // Cổng chỉ bind được một lần: dọn socket cũ (nếu còn sót) trước khi mở cái mới.
        CloseDiscoverySocket();

        try
        {
            discoveryServer = new UdpClient(discoveryPort);
        }
        catch (Exception exception)
        {
            // Thường là do một tiến trình khác (ván game đang chạy) đã giữ port quét LAN.
            Debug.LogWarning($"[LAN] Không mở được cổng quét LAN {discoveryPort}: {exception.Message}");
            discoveryServer = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Dừng coroutine trả lời quét LAN (nếu đang chạy) rồi NHẢ cổng quét LAN.
    /// </summary>
    private void StopLANDiscoveryServer()
    {
        if (discoveryServerCoroutine != null)
        {
            StopCoroutine(discoveryServerCoroutine);
            discoveryServerCoroutine = null;
        }

        CloseDiscoverySocket();
    }

    /// <summary>
    /// Đóng socket quét LAN để nhả <see cref="discoveryPort"/> NGAY.
    ///
    /// Vì sao cần: socket không được đóng thì cổng vẫn bị giữ cho tới khi GC dọn (không đoán được lúc nào,
    /// có khi tới lúc thoát game). Lần mở phòng kế tiếp <c>new UdpClient(discoveryPort)</c> sẽ lỗi
    /// "Only one usage of each socket address" -> phòng mở ra nhưng KHÔNG ai quét thấy.
    /// </summary>
    private void CloseDiscoverySocket()
    {
        if (discoveryServer == null) return;

        try
        {
            discoveryServer.Close();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[LAN] Đóng cổng quét LAN {discoveryPort} lỗi: {exception.Message}");
        }
        finally
        {
            discoveryServer = null;
        }
    }

    private IEnumerator LANDiscoveryServer()
    {
        while (
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsHost
        )
        {
            if (discoveryServer != null && discoveryServer.Available > 0)
            {
                IPEndPoint remoteEndPoint = null;

                byte[] data =
                    discoveryServer.Receive(
                        ref remoteEndPoint
                    );

                string message =
                    Encoding.UTF8.GetString(data);

                if (message == DiscoverMessage)
                {
                    byte[] response =
                        Encoding.UTF8.GetBytes(
                            HostResponseMessage
                        );

                    discoveryServer.Send(
                        response,
                        response.Length,
                        remoteEndPoint
                    );

                    Debug.Log(
                        $"[LAN] Đã trả lời Client {remoteEndPoint.Address}"
                    );
                }
            }

            yield return null;
        }

        // Hết làm host (vào trận / huỷ / thoát ra menu): nhả cổng ngay, đừng đợi GC dọn socket.
        discoveryServerCoroutine = null;
        CloseDiscoverySocket();
    }

    public void StartClient()
    {
        ConnectTo(hostIP);
    }

    /// <summary>Vào phòng của <paramref name="ip"/> với port đã khai báo. Trả về FALSE nếu không khởi động được.</summary>
    private bool ConnectTo(string ip)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NETWORK] Mất NetworkManager - không vào được phòng.");
            return false;
        }

        UnityTransport transport =
            NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetConnectionData(
            ip,
            port
        );

        Debug.Log(
            $"[NETWORK] Connecting to {ip}:{port}"
        );

        if (!NetworkManager.Singleton.StartClient())
        {
            Debug.LogError($"[NETWORK] Không khởi động được Client tới {ip}:{port}.");
            return false;
        }

        return true;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log(
            $"[NETWORK] Connected: {clientId}"
        );

        // CHỈ HOST được load Game Scene
        if (NetworkManager.Singleton.IsHost)
        {
            // Đợi Client kết nối rồi mới chuyển scene
            if (NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
            {
                LoadGameScene();
            }
        }
    }

    private void LoadGameScene()
    {
        Debug.Log("[NETWORK] Loading MultiPlayer...");

        NetworkManager.Singleton.SceneManager.LoadScene(
            "MultiPlayer",
            LoadSceneMode.Single
        );
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log(
            $"[NETWORK] Client disconnected: {clientId}"
        );

        // CLIENT vào không được (host đóng / sai IP): báo ngay trên panel tìm trận để người chơi
        // biết đường huỷ, thay vì cứ nhìn chữ "đang vào...".
        if (autoMatching && NetworkRole.IsClientOnly)
        {
            SetSearchingText("Couldn't join the battle. Press Cancel to try again.", false);
        }
    }

    // =========================================================
    // TÌM TRẬN TỰ ĐỘNG
    // =========================================================

    /// <summary>
    /// Luồng của nút <c>btn_find_a_match</c>: quét host trong LAN trong <see cref="autoMatchSearchTimeout"/>
    /// giây.
    ///   - Thấy host  -> vào làm CLIENT (phe ĐỎ) của phòng đó.
    ///   - Không thấy -> TỰ MỞ PHÒNG làm HOST (phe XANH) rồi chờ người khác vào.
    /// Cả 2 nhánh đều để panel "Đang tìm trận..." lại kèm nút huỷ cho tới khi sang scene đấu.
    /// </summary>
    private IEnumerator AutoMatch()
    {
        autoMatching = true;
        ShowSearchingPanel(true);
        SetSearchingText("Looking for an opponent...");

        string foundHost = null;

        yield return DiscoverHosts(autoMatchSearchTimeout, ip =>
        {
            if (foundHost == null) foundHost = ip;
        });

        if (foundHost != null)
        {
            // Chữ hiện cho người chơi nói kiểu game; log vẫn ghi rõ IP để còn chẩn đoán.
            Debug.Log($"[MATCHMAKING] Tìm thấy phòng tại {foundHost} - vào làm client.");
            SetSearchingText("Opponent found! Joining the battle...");

            if (!ConnectTo(foundHost))
            {
                SetSearchingText("Couldn't join the battle. Press Cancel to try again.", false);
            }
        }
        else
        {
            SetSearchingText("No opponent yet - waiting for a challenger...");

            if (!StartHostInternal())
            {
                SetSearchingText("Couldn't create the battle. Press Cancel to try again.", false);
            }
        }

        // Panel (kèm nút huỷ) cố tình để mở cho tới khi scene đấu được load - host chờ người vào,
        // client chờ scene đồng bộ từ host.
    }

    /// <summary>Nút huỷ: dừng mọi việc đang làm dở (quét LAN / phòng vừa mở) rồi về màn hình chính.</summary>
    public void CancelAutoMatch()
    {
        Debug.Log("[MATCHMAKING] Người chơi huỷ tìm trận.");

        // Dừng dịch vụ quét LAN TRƯỚC StopAllCoroutines: StopAllCoroutines chỉ ngắt coroutine chứ không
        // đóng socket, nên cổng quét LAN vẫn bị giữ tới khi GC dọn -> bấm "Find a match" lần kế tiếp sẽ
        // mở phòng lỗi bind và không ai tìm thấy phòng nữa.
        StopLANDiscoveryServer();

        StopAllCoroutines();
        autoMatching = false;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            networkManager.Shutdown();

            // NetworkManager tự đặt DontDestroyOnLoad nên phải huỷ hẳn, nếu không lần sau vào lại
            // màn tìm trận sẽ có 2 NetworkManager và Singleton vẫn trỏ vào cái đã chết.
            Destroy(networkManager.gameObject);
        }

        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void ShowSearchingPanel(bool visible)
    {
        if (searchingPanel != null) searchingPanel.SetActive(visible);
    }

    private void SetSearchingText(string message, bool searching = true)
    {
        Debug.Log($"[MATCHMAKING] {message}");

        if (searchingText != null) searchingText.SetText(message);

        // Con quay đi kèm dòng chữ: báo lỗi (không còn tìm/chờ gì nữa) thì tắt cho khỏi quay vô ích.
        if (spinner != null) spinner.SetActive(searching);
    }

    /// <summary>
    /// Phát gói hỏi phòng qua UDP broadcast rồi gọi <paramref name="onHostFound"/> cho MỖI host trả lời
    /// trong vòng <paramref name="timeout"/> giây. Dùng chung cho cả nút "Find hosts" (liệt kê UI) và
    /// luồng tự động tìm trận (chỉ lấy host đầu tiên).
    /// </summary>
    private IEnumerator DiscoverHosts(float timeout, Action<string> onHostFound)
    {
        using (UdpClient client = new UdpClient())
        {
            client.EnableBroadcast = true;

            IPEndPoint broadcastEndPoint =
                new IPEndPoint(
                    IPAddress.Broadcast,
                    discoveryPort
                );

            byte[] data = Encoding.UTF8.GetBytes(DiscoverMessage);

            try
            {
                client.Send(
                    data,
                    data.Length,
                    broadcastEndPoint
                );
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[LAN] Không gửi được gói quét LAN: {exception.Message}");
                yield break;
            }

            float startTime = Time.time;

            while (Time.time - startTime < timeout)
            {
                if (client.Available > 0)
                {
                    IPEndPoint remoteEndPoint = null;

                    byte[] response =
                        client.Receive(ref remoteEndPoint);

                    string message =
                        Encoding.UTF8.GetString(response);

                    if (message == HostResponseMessage && remoteEndPoint != null)
                    {
                        onHostFound?.Invoke(remoteEndPoint.Address.ToString());
                    }
                }

                yield return null;
            }
        }
    }

    private IEnumerator FindLANHostsCoroutine()
    {
        lanHosts.Clear();

        // Xóa các IP Host cũ trên UI
        ClearHostList();

        Debug.Log("[LAN] Đang tìm các Host trong mạng LAN...");

        yield return DiscoverHosts(3f, foundHostIP =>
        {
            if (lanHosts.Contains(foundHostIP)) return;

            lanHosts.Add(foundHostIP);

            Debug.Log($"[LAN] Tìm thấy Host: {foundHostIP}");

            SpawnHostUI(foundHostIP);
        });

        Debug.Log(
            $"[LAN] Tìm thấy {lanHosts.Count} Host."
        );
    }

    private void SpawnHostUI(string ip)
    {
        if (IpHost == null)
        {
            Debug.LogError("[LAN] IpHost chưa được gán!");
            return;
        }

        if (HostList == null)
        {
            Debug.LogError("[LAN] HostList chưa được gán!");
            return;
        }

        GameObject hostObject =
            Instantiate(IpHost, HostList);

        hostObject.name = "IpHost_" + ip;

        IpHost ipHost =
            hostObject.GetComponent<IpHost>();

        if (ipHost != null)
        {
            ipHost.Setup(ip);
        }
        else
        {
            Debug.LogError(
                "[LAN] Prefab IpHost không có component IpHost!"
            );
        }
    }

    private void ClearHostList()
    {
        if (HostList == null)
            return;

        for (int i = HostList.childCount - 1; i >= 0; i--)
        {
            Destroy(HostList.GetChild(i).gameObject);
        }
    }

    public void FindLANHosts()
    {
        StartCoroutine(FindLANHostsCoroutine());
    }

    /// <summary>
    /// Ép <c>NetworkManager</c> CỦA SCENE NÀY làm <see cref="NetworkManager.Singleton"/> và dọn các
    /// NetworkManager còn sót của ván trước.
    ///
    /// Vì sao cần: NetworkManager tự gọi DontDestroyOnLoad nên sau khi rời một ván, nó (đã Shutdown)
    /// vẫn nằm lại và giữ chỗ Singleton. Lúc đó NetworkManager của scene này không được đặt làm
    /// Singleton (OnEnable chỉ đặt khi Singleton còn null) nên nút bấm sẽ nói chuyện với cái đã chết.
    ///
    /// LƯU Ý (đã từng sai ở đây): NetworkManager bị DontDestroyOnLoad kéo sang scene "DontDestroyOnLoad",
    /// còn NetworkUI thì nằm trong scene MultiPlayerConnect - nên KHÔNG thể so sánh <c>gameObject.scene</c>
    /// để biết cái nào là của scene này (cách đó huỷ nhầm chính NetworkManager của scene, để lại
    /// Singleton = null -> NullReferenceException khi mở phòng). Thay vào đó: giữ cái mà NGO đang giữ
    /// (nếu trống thì lấy cái đầu tiên - đó là cái của scene vừa load) và huỷ mọi cái khác.
    /// </summary>
    private void AdoptSceneNetworkManager()
    {
        NetworkManager[] managers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);

        if (managers.Length == 0)
        {
            Debug.LogError("[NETWORK] Scene tìm trận không có NetworkManager nào.");
            return;
        }

        // "== null" của Unity cũng đúng cho object đã bị huỷ, nên chỗ này an toàn cả khi cái cũ đã chết.
        NetworkManager target = NetworkManager.Singleton;

        if (target == null) target = managers[0];

        foreach (NetworkManager candidate in managers)
        {
            if (candidate == target) continue;

            // Ván trước còn sót lại: huỷ hẳn, không thì nó vẫn giữ port 7777 khiến mở phòng mới lỗi.
            // OnDestroy của nó chỉ xoá Singleton nếu Singleton đang là chính nó, nên không đụng gì
            // tới cái ta sắp nhận bên dưới.
            Destroy(candidate.gameObject);
        }

        if (NetworkManager.Singleton != target)
        {
            Debug.LogWarning("[NETWORK] Phát hiện NetworkManager cũ còn sót - nhận lại Singleton cho scene tìm trận.");
            target.SetSingleton();
        }
    }

    private void OnDestroy()
    {
        // Nhả cổng quét LAN: scene tìm trận bị huỷ (vào trận, huỷ tìm trận, quay về menu...) mà socket
        // chưa đóng thì cổng vẫn bị giữ và lần mở phòng sau sẽ thất bại.
        CloseDiscoverySocket();

        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }
}