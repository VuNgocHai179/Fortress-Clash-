using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkUI : MonoBehaviour
{
    public string hostIP = "192.168.1.195";
    public ushort port = 7777;

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

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }
}