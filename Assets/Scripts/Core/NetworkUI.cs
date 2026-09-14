using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FortressClash.Core;

public class NetworkUI : Singleton<NetworkUI>
{
    public string hostIP = "192.168.1.195";
    public ushort port = 7777;

    [Header("LAN Discovery")]
    [SerializeField] private ushort discoveryPort = 47777;

    private List<string> lanHosts = new List<string>();

    [SerializeField]
    private GameObject IpHost;

    [SerializeField]
    private Transform HostList;
    
    private UdpClient discoveryServer;
    private Coroutine discoveryServerCoroutine;

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public void StartHost()
    {
        UnityTransport transport =
            NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetConnectionData(
            hostIP,
            port,
            "0.0.0.0"
        );

        Debug.Log("[NETWORK] Starting Host...");

        NetworkManager.Singleton.StartHost();

        StartLANDiscoveryServer();
    }

    private void StartLANDiscoveryServer()
    {
        discoveryServerCoroutine =
            StartCoroutine(LANDiscoveryServer());
    }

    private IEnumerator LANDiscoveryServer()
    {
        discoveryServer =
            new UdpClient(discoveryPort);

        Debug.Log(
            $"[LAN] Host Discovery đang chạy tại port {discoveryPort}"
        );

        while (
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsHost
        )
        {
            if (discoveryServer.Available > 0)
            {
                IPEndPoint remoteEndPoint = null;

                byte[] data =
                    discoveryServer.Receive(
                        ref remoteEndPoint
                    );

                string message =
                    Encoding.UTF8.GetString(data);

                if (message == "FORTRESSCLASH_DISCOVER")
                {
                    byte[] response =
                        Encoding.UTF8.GetBytes(
                            "FORTRESSCLASH_HOST"
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
    }

    public void StartClient()
    {
        UnityTransport transport =
            NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetConnectionData(
            hostIP,
            port
        );

        Debug.Log(
            $"[NETWORK] Connecting to {hostIP}:{port}"
        );

        NetworkManager.Singleton.StartClient();
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
    }

    private IEnumerator FindLANHostsCoroutine()
    {
        lanHosts.Clear();

        // Xóa các IP Host cũ trên UI
        ClearHostList();

        Debug.Log("[LAN] Đang tìm các Host trong mạng LAN...");

        using (UdpClient client = new UdpClient())
        {
            client.EnableBroadcast = true;

            IPEndPoint broadcastEndPoint =
                new IPEndPoint(
                    IPAddress.Broadcast,
                    discoveryPort
                );

            byte[] data = Encoding.UTF8.GetBytes(
                "FORTRESSCLASH_DISCOVER"
            );

            client.Send(
                data,
                data.Length,
                broadcastEndPoint
            );

            float timeout = 3f;
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

                    if (message == "FORTRESSCLASH_HOST")
                    {
                        string foundHostIP =
                            remoteEndPoint.Address.ToString();

                        if (!lanHosts.Contains(foundHostIP))
                        {
                            lanHosts.Add(foundHostIP);

                            Debug.Log(
                                $"[LAN] Tìm thấy Host: {foundHostIP}"
                            );

                            // Spawn UI IP Host
                            SpawnHostUI(foundHostIP);
                        }
                    }
                }

                yield return null;
            }
        }

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

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }
}