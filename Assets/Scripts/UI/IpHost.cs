using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class IpHost : MonoBehaviour
{
    private TMP_Text ipText;

    [SerializeField] private Button button;

    [SerializeField] private ushort port = 7777;

    public void Setup(string ip)
    {
        // Tìm TextMeshPro trên object con tên IPHost
        Transform ipHostObject = transform.Find("IPHost");

        if (ipHostObject == null)
        {
            Debug.LogError(
                $"[LAN] Không tìm thấy object IPHost trong {gameObject.name}"
            );

            return;
        }

        ipText = ipHostObject.GetComponent<TMP_Text>();

        if (ipText == null)
        {
            Debug.LogError(
                "[LAN] Object IPHost không có component TextMeshPro!"
            );

            return;
        }

        ipText.text = ip;

        Debug.Log(
            $"[LAN] Đã hiển thị IP: {ip}"
        );

        // Đăng ký sự kiện cho Button
        if (button != null)
        {
            button.onClick.RemoveListener(ConnectToHost);
            button.onClick.AddListener(ConnectToHost);
        }
        else
        {
            Debug.LogError(
                $"[LAN] Button chưa được gán trong {gameObject.name}"
            );
        }
    }

    private void ConnectToHost()
    {
        if (ipText == null)
        {
            Debug.LogError("[LAN] ipText chưa được Setup!");
            return;
        }

        string hostIP = ipText.text.Trim();

        if (string.IsNullOrEmpty(hostIP))
        {
            Debug.LogError("[LAN] IP Host đang trống!");
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("[LAN] Không tìm thấy NetworkManager!");
            return;
        }

        // Kiểm tra đã kết nối chưa
        if (networkManager.IsClient ||
            networkManager.IsHost ||
            networkManager.IsServer)
        {
            Debug.LogWarning(
                "[LAN] NetworkManager đã được sử dụng."
            );

            return;
        }

        UnityTransport transport =
            networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError(
                "[LAN] Không tìm thấy UnityTransport!"
            );

            return;
        }

        // Đặt IP và port của Host
        transport.SetConnectionData(
            hostIP,
            port
        );

        Debug.Log(
            $"[LAN] Đang kết nối tới Host: {hostIP}:{port}"
        );

        // Kết nối tới Host
        bool success = networkManager.StartClient();

        if (!success)
        {
            Debug.LogError(
                $"[LAN] Không thể StartClient tới {hostIP}:{port}"
            );
        }
        else
        {
            Debug.Log(
                $"[LAN] StartClient thành công: {hostIP}:{port}"
            );
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(ConnectToHost);
        }
    }
}