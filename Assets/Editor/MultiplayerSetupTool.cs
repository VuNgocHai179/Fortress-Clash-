using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Công cụ MỘT LẦN để hoàn tất phần "dây nối" mà script không tự làm được: gắn component Netcode
/// vào prefab / scene và đăng ký prefab với NetworkManager.
///
/// Vì sao phải có tool thay vì sửa tay: Unity tự sinh các khoá nội bộ khi import
/// (<c>NetworkObject.GlobalObjectIdHash</c>, guid của prefab trong danh sách mạng...). Gắn bằng tay
/// qua file YAML rất dễ sai và rất khó dò lỗi, còn tool này để Unity tự làm đúng.
///
/// THỨ TỰ DÙNG (mở project bằng Unity):
///   1. "Fortress Clash/MultiPlayer/1 - Gắn netcode vào prefab"
///   2. "Fortress Clash/MultiPlayer/2 - Đăng ký prefab với NetworkManager"
///   3. "Fortress Clash/MultiPlayer/3 - Gắn NetworkGame vào scene MultiPlayer"
/// (hoặc chạy "Chạy tất cả" - làm đủ 3 bước theo đúng thứ tự.)
///
/// Cả 3 bước đều AN TOÀN khi chạy lại nhiều lần: đã có thì bỏ qua, chưa có thì thêm.
/// </summary>
public static class MultiplayerSetupTool
{
    private const string MultiPlayerScenePath = "Assets/Scenes/MultiPlayer.unity";

    /// <summary>Scene chứa NetworkManager + NetworkUI (màn tìm trận) - NetworkManager sống ở đây.</summary>
    private const string MultiPlayerConnectScenePath = "Assets/Scenes/MultiPlayerConnect.unity";

    private const string DefaultPrefabsListName = "DefaultNetworkPrefabs";

    /// <summary>Thư mục chứa prefab lính của CẤU TRÚC CŨ - cần gỡ khỏi danh sách mạng.</summary>
    private const string LegacyPrefabFolder = "Assets/Prefab/MultiPlayer/";

    /// <summary>
    /// Prefab được HOST sinh ra lúc chạy nên phải là network prefab: lính 2 phe + đạn.
    /// (King và thành là vật thể đặt sẵn trong scene ở cả 2 máy nên không nằm trong danh sách này.)
    /// </summary>
    private static readonly string[] NetworkPrefabPaths =
    {
        // lính XANH (host)
        "Assets/Prefab/Blue/Sword.prefab",
        "Assets/Prefab/Blue/Archer.prefab",
        "Assets/Prefab/Blue/Slinger.prefab",
        "Assets/Prefab/Blue/SpearMan.prefab",
        "Assets/Prefab/Blue/BaseSlime.prefab",
        "Assets/Prefab/Blue/Catapult.prefab",

        // lính ĐỎ (client)
        "Assets/Prefab/Red/SwordEnemy.prefab",
        "Assets/Prefab/Red/ArcherEnemy.prefab",
        "Assets/Prefab/Red/SlingShotEnemy.prefab",
        "Assets/Prefab/Red/SpearManEnemy.prefab",
        "Assets/Prefab/Red/RedBaseSlime.prefab",
        "Assets/Prefab/Red/CatapultEnemy.prefab",

        // đạn của lính tầm xa
        "Assets/Prefab/Bullet/Arrow.prefab",
        "Assets/Prefab/Bullet/StoneBig.prefab",
        "Assets/Prefab/Bullet/StoneSmall.prefab",
    };

    // =========================================================
    // MENU
    // =========================================================

    [MenuItem("Fortress Clash/MultiPlayer/1 - Gắn netcode vào prefab", priority = 0)]
    public static void Step1_AddNetcodeToPrefabs()
    {
        int changed = 0;

        foreach (string path in NetworkPrefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogError($"[MultiPlayer] Không mở được prefab: {path}");
                continue;
            }

            bool dirty = false;

            // NetworkObject phải có TRƯỚC NetworkTransform (NetworkTransform là NetworkBehaviour).
            if (root.GetComponent<NetworkObject>() == null)
            {
                root.AddComponent<NetworkObject>();
                dirty = true;
                Debug.Log($"[MultiPlayer] {path}: thêm NetworkObject.");
            }

            if (root.GetComponent<NetworkTransform>() == null)
            {
                NetworkTransform transform = root.AddComponent<NetworkTransform>();

                // Đồng bộ cả SCALE: hoạt ảnh nhảy/nhấp nhô/co giãn của lính chạy bằng DOTween trên
                // host (client không mô phỏng lại), nếu không đồng bộ scale thì client chỉ thấy lính
                // trượt tới chứ không thấy nhảy. Mặc định của NGO đã bật, ghi rõ ở đây cho khỏi mất.
                transform.SyncPositionX = true;
                transform.SyncPositionY = true;
                transform.SyncPositionZ = true;
                transform.SyncRotAngleX = true;
                transform.SyncRotAngleY = true;
                transform.SyncRotAngleZ = true;
                transform.SyncScaleX = true;
                transform.SyncScaleY = true;
                transform.SyncScaleZ = true;

                dirty = true;
                Debug.Log($"[MultiPlayer] {path}: thêm NetworkTransform (đồng bộ cả scale).");
            }

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed++;
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MultiPlayer] Bước 1 xong: {changed}/{NetworkPrefabPaths.Length} prefab được cập nhật.");
    }

    [MenuItem("Fortress Clash/MultiPlayer/2 - Đăng ký prefab với NetworkManager", priority = 1)]
    public static void Step2_RegisterNetworkPrefabs()
    {
        NetworkPrefabsList list = FindDefaultPrefabsList();
        if (list == null) return;

        // Gỡ lính của CẤU TRÚC CŨ (Assets/Prefab/MultiPlayer/...) - chúng không còn được dùng.
        List<NetworkPrefab> stale = new List<NetworkPrefab>();
        foreach (NetworkPrefab entry in list.PrefabList)
        {
            if (entry == null || entry.Prefab == null)
            {
                stale.Add(entry);   // entry rỗng/mất prefab: dọn luôn
                continue;
            }

            string entryPath = AssetDatabase.GetAssetPath(entry.Prefab);
            if (!string.IsNullOrEmpty(entryPath) && entryPath.StartsWith(LegacyPrefabFolder))
            {
                stale.Add(entry);
            }
        }

        foreach (NetworkPrefab entry in stale)
        {
            list.Remove(entry);
        }

        int added = 0;
        int missingNetworkObject = 0;

        foreach (string path in NetworkPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[MultiPlayer] Không tìm thấy prefab: {path}");
                continue;
            }

            if (prefab.GetComponent<NetworkObject>() == null)
            {
                missingNetworkObject++;
                Debug.LogWarning($"[MultiPlayer] {path} chưa có NetworkObject - hãy chạy bước 1 trước.");
                continue;
            }

            if (list.Contains(prefab)) continue;

            list.Add(new NetworkPrefab { Prefab = prefab });
            added++;
        }

        EditorUtility.SetDirty(list);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MultiPlayer] Bước 2 xong: thêm {added} prefab, gỡ {stale.Count} entry cũ " +
                  $"khỏi '{AssetDatabase.GetAssetPath(list)}'." +
                  (missingNetworkObject > 0 ? $" CÓ {missingNetworkObject} prefab thiếu NetworkObject!" : ""));
    }

    [MenuItem("Fortress Clash/MultiPlayer/3 - Gắn NetworkGame vào scene MultiPlayer", priority = 2)]
    public static void Step3_SetupScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(MultiPlayerScenePath, OpenSceneMode.Single);

        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("[MultiPlayer] Không tìm thấy GameManager trong scene MultiPlayer!");
            return;
        }

        GameObject host = gameManager.gameObject;
        bool dirty = false;

        // GameManager và NetworkGame phải nằm CÙNG một GameObject có NetworkObject, vì NetworkGame
        // là NetworkBehaviour và là nơi nhận RPC từ client.
        if (host.GetComponent<NetworkObject>() == null)
        {
            host.AddComponent<NetworkObject>();
            dirty = true;
            Debug.Log($"[MultiPlayer] '{host.name}': thêm NetworkObject.");
        }

        if (host.GetComponent<NetworkGame>() == null)
        {
            host.AddComponent<NetworkGame>();
            dirty = true;
            Debug.Log($"[MultiPlayer] '{host.name}': thêm NetworkGame.");
        }

        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MultiPlayer] Bước 3 xong: scene MultiPlayer đã lưu.");
        }
        else
        {
            Debug.Log("[MultiPlayer] Bước 3: scene MultiPlayer đã sẵn sàng từ trước, không cần sửa.");
        }
    }

    [MenuItem("Fortress Clash/MultiPlayer/4 - Sinh GlobalObjectIdHash cho prefab", priority = 3)]
    public static void Step4_RegenerateGlobalObjectIdHash()
    {
        // NGO tra prefab theo ĐÚNG con số GlobalObjectIdHash khi client nhận CreateObjectMessage.
        // Con số đó KHÔNG tự sinh lúc chạy game: NetworkObject.OnValidate (chỉ có ở Editor) tính nó từ
        // GlobalObjectId của asset rồi lưu xuống file. Prefab nào bị gắn NetworkObject bằng tay - sửa
        // thẳng file YAML thay vì qua Unity - sẽ nằm lại với hash = 0.
        //
        // Hậu quả: hash = 0 ở nhiều prefab thì client không phân biệt được chúng -> báo
        // "NetworkPrefab could not be found" và lính chỉ hiện ở máy host. Trong Editor lỗi này có thể
        // tạm không thấy (OnValidate tính lại hash trong bộ nhớ mỗi lần asset được nạp), nhưng bản
        // build không có OnValidate nên hỏng hẳn.
        List<string> missing = new List<string>();

        foreach (string path in NetworkPrefabPaths)
        {
            if (ReadGlobalObjectIdHash(path) == 0) missing.Add(path);
        }

        if (missing.Count == 0)
        {
            Debug.Log("[MultiPlayer] Bước 4: mọi prefab đã có GlobalObjectIdHash trong file, không cần làm gì.");
            return;
        }

        // Ép Unity nạp lại rồi ghi lại asset: OnValidate tính hash và giá trị đó được lưu xuống file.
        AssetDatabase.ForceReserializeAssets(missing);
        AssetDatabase.Refresh();

        int fixedCount = 0;

        foreach (string path in missing)
        {
            if (ReadGlobalObjectIdHash(path) != 0)
            {
                fixedCount++;
            }
            else
            {
                Debug.LogWarning($"[MultiPlayer] {path} vẫn còn GlobalObjectIdHash = 0 sau khi ghi lại. " +
                                 "Hãy mở prefab đó trong Prefab Mode một lần rồi Ctrl+S, sau đó chạy lại bước 4.");
            }
        }

        Debug.Log($"[MultiPlayer] Bước 4 xong: {fixedCount}/{missing.Count} prefab đã được sinh hash. " +
                  (fixedCount < missing.Count ? "Còn prefab chưa xong - xem cảnh báo ở trên." : ""));
    }

    [MenuItem("Fortress Clash/MultiPlayer/5 - Nối NetworkManager với danh sách prefab", priority = 4)]
    public static void Step5_WireNetworkManagerPrefabList()
    {
        // NetworkManager CHỈ resolve được prefab nằm trong danh sách mà CHÍNH NÓ tham chiếu. Bước 2 chỉ
        // đăng ký prefab vào asset DefaultNetworkPrefabs; nếu NetworkManager không trỏ vào asset đó thì
        // host vẫn Instantiate + Spawn được lính (nên vẫn thấy trên máy host) nhưng client nhận
        // CreateObjectMessage sẽ báo "NetworkPrefab could not be found" và không thấy lính nào.
        NetworkPrefabsList list = FindDefaultPrefabsList();
        if (list == null) return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(MultiPlayerConnectScenePath, OpenSceneMode.Single);

        NetworkManager networkManager = Object.FindFirstObjectByType<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError($"[MultiPlayer] Không tìm thấy NetworkManager trong scene {MultiPlayerConnectScenePath}!");
            return;
        }

        if (networkManager.NetworkConfig == null)
        {
            Debug.LogError("[MultiPlayer] NetworkManager chưa có NetworkConfig!");
            return;
        }

        List<NetworkPrefabsList> lists = networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists;
        if (lists == null)
        {
            lists = new List<NetworkPrefabsList>();
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists = lists;
        }

        bool dirty = false;

        // Gỡ các danh sách trỏ vào cấu trúc CŨ (Assets/Prefab/MultiPlayer/...): prefab trong đó không còn
        // được sinh ra ở đâu nữa nên chỉ làm rối cấu hình và dễ khiến tưởng "đã đăng ký rồi".
        for (int i = lists.Count - 1; i >= 0; i--)
        {
            NetworkPrefabsList entry = lists[i];
            if (entry == null)
            {
                lists.RemoveAt(i);
                dirty = true;
                continue;
            }

            string entryPath = AssetDatabase.GetAssetPath(entry);
            if (!string.IsNullOrEmpty(entryPath) && entryPath.StartsWith(LegacyPrefabFolder))
            {
                Debug.Log($"[MultiPlayer] Gỡ danh sách prefab cũ '{entryPath}' khỏi NetworkManager.");
                lists.RemoveAt(i);
                dirty = true;
            }
        }

        if (!lists.Contains(list))
        {
            lists.Add(list);
            dirty = true;
        }

        if (!dirty)
        {
            Debug.Log($"[MultiPlayer] Bước 5: NetworkManager đã trỏ đúng vào '{AssetDatabase.GetAssetPath(list)}' từ trước.");
            return;
        }

        EditorUtility.SetDirty(networkManager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[MultiPlayer] Bước 5 xong: NetworkManager ('{networkManager.name}') đã trỏ vào " +
                  $"'{AssetDatabase.GetAssetPath(list)}'.");
    }

    [MenuItem("Fortress Clash/MultiPlayer/Chạy tất cả (1 → 5)", priority = 20)]
    public static void RunAll()
    {
        Step1_AddNetcodeToPrefabs();
        Step2_RegisterNetworkPrefabs();
        Step3_SetupScene();
        Step4_RegenerateGlobalObjectIdHash();
        Step5_WireNetworkManagerPrefabList();
        Verify();
    }

    [MenuItem("Fortress Clash/MultiPlayer/Kiểm tra cấu hình", priority = 21)]
    public static void Verify()
    {
        int problems = 0;

        foreach (string path in NetworkPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[MultiPlayer][Kiểm tra] Thiếu prefab: {path}");
                problems++;
                continue;
            }

            if (prefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError($"[MultiPlayer][Kiểm tra] {path} thiếu NetworkObject.");
                problems++;
            }

            if (prefab.GetComponent<NetworkTransform>() == null)
            {
                Debug.LogError($"[MultiPlayer][Kiểm tra] {path} thiếu NetworkTransform.");
                problems++;
            }
        }

        NetworkPrefabsList list = FindDefaultPrefabsList();
        if (list == null)
        {
            problems++;
        }
        else
        {
            foreach (string path in NetworkPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && !list.Contains(prefab))
                {
                    Debug.LogError($"[MultiPlayer][Kiểm tra] {path} chưa có trong {AssetDatabase.GetAssetPath(list)}.");
                    problems++;
                }
            }
        }

        // GlobalObjectIdHash phải có THẬT trong file prefab (xem bước 4): hash 0 làm client không
        // resolve được prefab, triệu chứng là lính chỉ hiện ở máy host.
        foreach (string path in NetworkPrefabPaths)
        {
            if (ReadGlobalObjectIdHash(path) != 0) continue;

            Debug.LogError($"[MultiPlayer][Kiểm tra] {path} có GlobalObjectIdHash = 0 trong file - chạy bước 4.");
            problems++;
        }

        problems += CountNetworkManagerPrefabListProblems(list);

        Debug.Log(problems == 0
            ? "[MultiPlayer][Kiểm tra] OK: prefab + danh sách mạng + NetworkManager đã đủ. " +
              "Nhớ mở scene MultiPlayer kiểm tra GameObject 'GameManager' có NetworkObject + NetworkGame."
            : $"[MultiPlayer][Kiểm tra] Còn {problems} vấn đề ở trên - chạy lại các bước 1 → 5.");
    }

    // =========================================================
    // TIỆN ÍCH
    // =========================================================

    /// <summary>
    /// Đếm số vấn đề ở phần "NetworkManager có tham chiếu danh sách prefab hay không".
    ///
    /// Cố ý đọc thẳng FILE scene thay vì mở scene ra kiểm tra: NetworkManager nằm trong scene nên không
    /// hỏi được nó qua API khi scene chưa mở, mà Verify thì không nên đổi scene đang mở của người dùng.
    /// </summary>
    private static int CountNetworkManagerPrefabListProblems(NetworkPrefabsList list)
    {
        if (list == null) return 0;

        if (!File.Exists(MultiPlayerConnectScenePath))
        {
            Debug.LogWarning($"[MultiPlayer][Kiểm tra] Không đọc được {MultiPlayerConnectScenePath}.");
            return 0;
        }

        string listGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(list));
        if (string.IsNullOrEmpty(listGuid)) return 0;

        if (File.ReadAllText(MultiPlayerConnectScenePath).Contains("guid: " + listGuid)) return 0;

        Debug.LogError($"[MultiPlayer][Kiểm tra] NetworkManager trong {MultiPlayerConnectScenePath} CHƯA tham chiếu " +
                       $"'{AssetDatabase.GetAssetPath(list)}'. Host vẫn sinh được lính nhưng client sẽ báo " +
                       "'NetworkPrefab could not be found' và không thấy lính nào - chạy bước 5.");
        return 1;
    }

    /// <summary>
    /// Đọc GlobalObjectIdHash ĐANG NẰM TRONG FILE của 1 prefab; trả về -1 nếu không đọc được.
    ///
    /// Cố ý đọc text chứ không dùng <c>NetworkObject.PrefabIdHash</c>: trong Editor, OnValidate tính lại
    /// hash ngay khi asset được nạp nên đọc qua API luôn thấy giá trị mới, kể cả khi file vẫn còn số 0 -
    /// như vậy không phân biệt được "đã lưu xuống file" với "mới chỉ có trong bộ nhớ".
    /// </summary>
    private static long ReadGlobalObjectIdHash(string prefabPath)
    {
        if (!File.Exists(prefabPath)) return -1;

        foreach (string line in File.ReadAllLines(prefabPath))
        {
            string trimmed = line.Trim();

            // Khớp đúng dòng của NetworkObject, bỏ qua InScenePlacedSourceGlobalObjectIdHash.
            if (!trimmed.StartsWith("GlobalObjectIdHash:")) continue;

            string value = trimmed.Substring("GlobalObjectIdHash:".Length).Trim();
            if (long.TryParse(value, out long hash)) return hash;
        }

        return -1;
    }

    /// <summary>
    /// Tìm asset NetworkPrefabsList mặc định (DefaultNetworkPrefabs.asset) đang được NetworkManager dùng.
    /// </summary>
    private static NetworkPrefabsList FindDefaultPrefabsList()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:NetworkPrefabsList"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == DefaultPrefabsListName)
            {
                return AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
            }
        }

        // Không có file tên đúng -> dùng tạm asset đầu tiên tìm được để không chặn công việc.
        string[] all = AssetDatabase.FindAssets("t:NetworkPrefabsList");
        if (all.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(all[0]);
            Debug.LogWarning($"[MultiPlayer] Không thấy '{DefaultPrefabsListName}.asset', dùng tạm: {path}");
            return AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
        }

        Debug.LogError($"[MultiPlayer] Không tìm thấy NetworkPrefabsList nào. " +
                       $"Tạo asset qua Create > Netcode > Network Prefabs List và đặt tên '{DefaultPrefabsListName}'.");
        return null;
    }
}
