using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Vai trò mạng của MÁY đang chạy game - nơi DUY NHẤT trong code gameplay hỏi
/// "mình là host hay client", để phần còn lại của game không phải rải <c>NetworkManager</c> khắp nơi.
///
/// QUY ƯỚC của scene MultiPlayer: <b>HOST = phe XANH (blue)</b>, <b>CLIENT = phe ĐỎ (red)</b>.
///
/// - Chơi đơn (scene Gameplay): KHÔNG có <c>NetworkManager</c> nên mọi thứ chạy y như trước,
///   netcode không can thiệp gì. Đây là lý do mọi hàm ở đây đều trả về giá trị "chế độ đơn" khi
///   không có <c>NetworkManager</c>, nhờ vậy cùng một bộ script dùng được cho cả 2 scene.
/// - HOST (server): mô phỏng TẤT CẢ - lính, đạn, King, tiền của cả 2 phe, thắng/thua - rồi
///   đồng bộ kết quả cho client. Mọi thay đổi trạng thái chỉ được sinh ra ở đây.
/// - CLIENT: KHÔNG mô phỏng gì. Chỉ nhận transform/số liệu qua mạng để hiển thị và gửi
///   yêu cầu (mua lính, nâng thành) lên server.
/// </summary>
public static class NetworkRole
{
    private static NetworkManager Net => NetworkManager.Singleton;

    /// <summary>Đang chạy một ván multiplayer (đã StartHost hoặc StartClient) hay không.</summary>
    public static bool IsMultiplayer => Net != null && (Net.IsServer || Net.IsClient);

    /// <summary>Máy này là CLIENT thuần (vào game qua StartClient, KHÔNG phải host).</summary>
    public static bool IsClientOnly => Net != null && Net.IsClient && !Net.IsServer;

    /// <summary>Máy này là server/host của ván đang chạy (không tính chế độ chơi đơn).</summary>
    public static bool IsHost => Net != null && Net.IsServer;

    /// <summary>
    /// Máy này có mô phỏng gameplay không: chơi đơn hoặc là host.
    /// Client thuần trả về false - nhờ vậy <see cref="Slime"/> / <see cref="Bullet"/> chỉ cần
    /// tắt chính mình ở client là xong, không phải rắc điều kiện vào từng nhánh Update.
    /// </summary>
    public static bool Simulates => !IsClientOnly;

    /// <summary>
    /// Phe của NGƯỜI CHƠI CỤC BỘ: host (và chế độ chơi đơn) là XANH, client là ĐỎ.
    /// UI đọc số này để biết đang hiển thị tiền của phe nào và nút bấm sẽ mua lính cho phe nào.
    /// </summary>
    public static bool LocalIsBlue => !IsClientOnly;

    /// <summary>
    /// Spawn một vật thể vừa <c>Instantiate</c> lên mạng (chỉ server được gọi).
    ///
    /// Chơi đơn: không làm gì - vật thể vốn đã tồn tại cục bộ.
    /// Client: không làm gì - client không tự sinh lính.
    /// Prefab chưa gắn <see cref="NetworkObject"/>: không làm gì, để game vẫn chạy được như cũ
    /// (tránh vỡ scene khi quên bước gắn component trong Editor).
    /// </summary>
    public static void SpawnIfServer(GameObject go)
    {
        if (go == null) return;
        if (!IsHost) return;                     // chơi đơn hoặc là client: không spawn mạng

        NetworkObject networkObject = go.GetComponent<NetworkObject>();
        if (networkObject == null) return;       // prefab cũ chưa gắn NetworkObject
        if (networkObject.IsSpawned) return;

        networkObject.Spawn(true);
    }
}
