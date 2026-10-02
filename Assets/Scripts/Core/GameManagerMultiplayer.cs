using System.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class GameManagerMultiplayer : NetworkBehaviour
{
    // =========================================================
    // NETWORK
    // =========================================================

    private NetworkManager networkManager;


    // =========================================================
    // UI
    // =========================================================

    [Header("Choosing UI")]
    [SerializeField] private GameObject BlueChoosing;
    [SerializeField] private GameObject RedChoosing;
    [SerializeField] private TextMeshProUGUI numberMoney;


    // =========================================================
    // GATE
    // =========================================================

    [Header("Gates")]
    [SerializeField] private GameObject BlueGate;
    [SerializeField] private GameObject RedGate;


    // =========================================================
    // BLUE PREFABS
    // =========================================================

    [Header("Blue Prefabs")]
    [SerializeField] private GameObject Sword;
    [SerializeField] private GameObject Archer;
    [SerializeField] private GameObject Slinger;
    [SerializeField] private GameObject SpearMan;
    [SerializeField] private GameObject Soldier;
    [SerializeField] private GameObject Catapult;


    // =========================================================
    // RED PREFABS
    // =========================================================

    [Header("Red Prefabs")]
    [SerializeField] private GameObject RedSword;
    [SerializeField] private GameObject RedArcher;
    [SerializeField] private GameObject RedSlinger;
    [SerializeField] private GameObject RedSpearMan;
    [SerializeField] private GameObject RedSoldier;
    [SerializeField] private GameObject RedCatapult;


    // =========================================================
    // MONEY
    // =========================================================

    [Header("Money")]
    [SerializeField] private int Money = 1000;
    [SerializeField] private int ClientMoney = 1000;
    [SerializeField] private int moneyPersecond = 10;


    // =========================================================
    // CHOOSE COOLDOWN
    // =========================================================

    [Header("Choose Cooldown")]
    [SerializeField] private float chooseDelay = 5f;

    private bool canChoose = true;


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        networkManager = NetworkManager.Singleton;
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (networkManager == null)
        {
            Debug.LogError(
                "[MULTIPLAYER] NetworkManager.Singleton không tồn tại!"
            );

            return;
        }

        Debug.Log(
            "[MULTIPLAYER]\n" +
            $"ClientId = {networkManager.LocalClientId}\n" +
            $"IsHost = {networkManager.IsHost}\n" +
            $"IsClient = {networkManager.IsClient}\n" +
            $"IsServer = {networkManager.IsServer}"
        );


        // =====================================================
        // BLUE = HOST
        // RED = CLIENT
        // =====================================================

        if (networkManager.IsHost)
        {
            if (BlueChoosing != null)
                BlueChoosing.SetActive(true);

            if (RedChoosing != null)
                RedChoosing.SetActive(false);
        }
        else if (
            networkManager.IsClient &&
            !networkManager.IsHost
        )
        {
            if (BlueChoosing != null)
                BlueChoosing.SetActive(false);

            if (RedChoosing != null)
                RedChoosing.SetActive(true);
        }


        // =====================================================
        // MONEY UI
        // =====================================================

        UpdateMoneyUI();


        // =====================================================
        // SERVER QUẢN LÝ TIỀN
        // =====================================================
        StartCoroutine(AddMoneyPerSecond());
    }


    // =========================================================
    // ON NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log(
            "[MULTIPLAYER] GameManagerMultiplayer OnNetworkSpawn\n" +
            $"IsServer = {IsServer}\n" +
            $"IsClient = {IsClient}\n" +
            $"IsHost = {IsHost}\n" +
            $"IsSpawned = {IsSpawned}\n" +
            $"LocalClientId = {NetworkManager.Singleton.LocalClientId}"
        );


        // =====================================================
        // BLUE = HOST
        // RED = CLIENT
        // =====================================================

        if (IsHost)
        {
            if (BlueChoosing != null)
                BlueChoosing.SetActive(true);

            if (RedChoosing != null)
                RedChoosing.SetActive(false);
        }
        else if (IsClient)
        {
            if (BlueChoosing != null)
                BlueChoosing.SetActive(false);

            if (RedChoosing != null)
                RedChoosing.SetActive(true);
        }
    }


    // =========================================================
    // MONEY PER SECOND
    // =========================================================

    private IEnumerator AddMoneyPerSecond()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            Money += moneyPersecond;
            ClientMoney += moneyPersecond;

            UpdateMoneyUI();
        }
    }


    // =========================================================
    // MONEY UI
    // =========================================================

    private void UpdateMoneyUI()
    {
        if (numberMoney == null)
            return;

        if (IsHost)
        {
            numberMoney.SetText(
                Money.ToString()
            );
        }
        else if (IsClient)
        {
            numberMoney.SetText(
                ClientMoney.ToString()
            );
        }
    }

    [ClientRpc]
    private void UpdateClientMoneyUIClientRpc(int money)
    {
        ClientMoney = money;
        UpdateMoneyUI();
    }


    // =========================================================
    // BLUE
    // BLUE = HOST
    // =========================================================

    public void SwordChoosen()
    {
        if (!IsHost)
            return;

        SpawnBlueSoldier(0);
    }


    public void ArcherChoosen()
    {
        if (!IsHost)
            return;

        SpawnBlueSoldier(1);
    }


    public void SpearChoosen()
    {
        if (!IsHost)
            return;

        SpawnBlueSoldier(2);
    }


    public void SlingshotChoosen()
    {
        if (!IsHost)
            return;

        SpawnBlueSoldier(3);
    }


    public void SoldierChoosen()
    {
        if (!IsHost)
            return;

        SpawnBlueSoldier(4);
    }


    public void CatapultChoosen()
    {
        if (!IsHost)
            return;

        SpawnBlueSoldier(5);
    }


    // =========================================================
    // BLUE SPAWN
    // =========================================================

    private void SpawnBlueSoldier(int soldierType)
    {
        // =====================================================
        // SERVER CHECK
        // =====================================================

        if (!IsServer)
        {
            Debug.LogError(
                "[MULTIPLAYER] Blue spawn phải chạy trên Server!"
            );

            return;
        }


        // =====================================================
        // COOLDOWN
        // =====================================================

        if (!CanChoose())
            return;


        // =====================================================
        // PREFAB
        // =====================================================

        GameObject prefab = GetBluePrefab(soldierType);

        if (prefab == null)
        {
            Debug.LogError(
                $"[MULTIPLAYER] Blue prefab không tồn tại: {soldierType}"
            );

            return;
        }


        Slime soldierPrefab =
            prefab.GetComponent<Slime>();

        if (soldierPrefab == null)
        {
            Debug.LogError(
                $"{prefab.name} không có Soldier!"
            );

            return;
        }


        // =====================================================
        // NETWORK OBJECT
        // =====================================================

        NetworkObject prefabNetworkObject =
            prefab.GetComponent<NetworkObject>();

        if (prefabNetworkObject == null)
        {
            Debug.LogError(
                $"{prefab.name} không có NetworkObject!"
            );

            return;
        }


        // =====================================================
        // GATE
        // =====================================================

        Gate gate =
            BlueGate.GetComponent<Gate>();

        if (gate == null)
        {
            Debug.LogError(
                "BlueGate không có Gate!"
            );

            return;
        }


        if (!gate.CheckAroundHasSoldier())
            return;


        // =====================================================
        // MONEY
        // =====================================================

        int price = soldierPrefab.price;

        if (Money < price)
        {
            Debug.Log(
                "[MULTIPLAYER] BLUE không đủ tiền!"
            );

            return;
        }

        Money -= price;

        UpdateMoneyUI();


        // =====================================================
        // INSTANTIATE
        // =====================================================

        GameObject spawnedObject =
            Instantiate(
                prefab,
                BlueGate.transform.position,
                Quaternion.identity
            );


        Slime soldier =
            spawnedObject.GetComponent<Slime>();

        if (soldier == null)
        {
            Destroy(spawnedObject);

            Debug.LogError(
                $"{prefab.name} không có Soldier!"
            );

            return;
        }


        soldier.Destination =
            RedGate.transform.position;

        soldier.SpawnPosition =
            BlueGate.transform.position;


        // =====================================================
        // NETWORK SPAWN
        // =====================================================

        NetworkObject networkObject =
            spawnedObject.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Destroy(spawnedObject);

            Debug.LogError(
                $"{prefab.name} không có NetworkObject!"
            );

            return;
        }


        networkObject.Spawn();

        StartCoroutine(
            ChooseCooldown()
        );


        Debug.Log(
            $"[MULTIPLAYER] BLUE spawn {prefab.name}"
        );
    }


    // =========================================================
    // RED
    // RED = CLIENT
    // =========================================================

    public void RedSwordChoosenMultiplayer()
    {
        if (!IsClient)
            return;

        SpawnRedSoldierServerRpc(0);
    }


    public void RedArcherChoosenMultiplayer()
    {
        if (!IsClient)
            return;

        SpawnRedSoldierServerRpc(1);
    }


    public void RedSlingerChoosenMultiplayer()
    {
        if (!IsClient)
            return;

        SpawnRedSoldierServerRpc(2);
    }


    public void RedSpearManChoosenMultiplayer()
    {
        if (!IsClient)
            return;

        SpawnRedSoldierServerRpc(3);
    }


    public void RedSoldierChoosenMultiplayer()
    {
        if (!IsClient)
            return;

        SpawnRedSoldierServerRpc(4);
    }


    public void RedCatapultChoosenMultiplayer()
    {
        if (!IsClient)
            return;

        SpawnRedSoldierServerRpc(5);
    }


    // =========================================================
    // RED SERVER RPC (Sử dụng chuẩn Rpc mới của Unity 6)
    // =========================================================

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SpawnRedSoldierServerRpc(int soldierType)
    {
        // =====================================================
        // DEBUG
        // =====================================================

        Debug.Log(
            "[MULTIPLAYER] ============================\n" +
            "[MULTIPLAYER] RED SPAWN RPC\n" +
            $"IsServer = {IsServer}\n" +
            $"IsClient = {IsClient}\n" +
            $"IsHost = {IsHost}\n" +
            $"IsSpawned = {IsSpawned}\n" +
            "[MULTIPLAYER] ============================"
        );


        // =====================================================
        // SERVER CHECK
        // =====================================================

        if (!IsServer)
        {
            Debug.LogError(
                "[MULTIPLAYER] RED RPC KHÔNG CHẠY TRÊN SERVER!"
            );

            return;
        }


        // =====================================================
        // PREFAB
        // =====================================================

        GameObject prefab =
            GetRedPrefab(soldierType);

        if (prefab == null)
        {
            Debug.LogError(
                $"[MULTIPLAYER] Red prefab không tồn tại: {soldierType}"
            );

            return;
        }


        SoldierEnemy soldierPrefab =
            prefab.GetComponent<SoldierEnemy>();

        if (soldierPrefab == null)
        {
            Debug.LogError(
                $"{prefab.name} không có SoldierEnemy!"
            );

            return;
        }


        // =====================================================
        // NETWORK OBJECT
        // =====================================================

        NetworkObject prefabNetworkObject =
            prefab.GetComponent<NetworkObject>();

        if (prefabNetworkObject == null)
        {
            Debug.LogError(
                $"{prefab.name} không có NetworkObject!"
            );

            return;
        }


        // =====================================================
        // GATE
        // =====================================================

        Gate gate =
            RedGate.GetComponent<Gate>();

        if (gate == null)
        {
            Debug.LogError(
                "RedGate không có Gate!"
            );

            return;
        }


        if (!gate.CheckAroundHasSoldier())
        {
            Debug.Log(
                "[MULTIPLAYER] RED Gate đang bị chặn!"
            );

            return;
        }


        // =====================================================
        // MONEY
        // =====================================================

        int price =
            soldierPrefab.Price;

        if (ClientMoney < price)
        {
            Debug.Log(
                "[MULTIPLAYER] RED không đủ tiền!"
            );

            return;
        }

        ClientMoney -= price;

        UpdateClientMoneyUIClientRpc(ClientMoney);


        // =====================================================
        // INSTANTIATE
        // =====================================================

        GameObject spawnedObject =
            Instantiate(
                prefab,
                RedGate.transform.position,
                Quaternion.identity
            );


        SoldierEnemy soldier =
            spawnedObject.GetComponent<SoldierEnemy>();

        if (soldier == null)
        {
            Destroy(spawnedObject);

            Debug.LogError(
                $"{prefab.name} không có SoldierEnemy!"
            );

            return;
        }


        soldier.Destination =
            BlueGate.transform.position;

        soldier.SpawnPosition =
            RedGate.transform.position;


        // =====================================================
        // NETWORK OBJECT
        // =====================================================

        NetworkObject networkObject =
            spawnedObject.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Destroy(spawnedObject);

            Debug.LogError(
                $"{prefab.name} không có NetworkObject!"
            );

            return;
        }


        // =====================================================
        // SERVER SPAWN
        // =====================================================

        networkObject.Spawn();


        Debug.Log(
            $"[MULTIPLAYER] RED spawn thành công: " +
            $"{prefab.name}"
        );
    }


    // =========================================================
    // GET BLUE PREFAB
    // =========================================================

    private GameObject GetBluePrefab(int type)
    {
        switch (type)
        {
            case 0:
                return Sword;

            case 1:
                return Archer;

            case 2:
                return SpearMan;

            case 3:
                return Slinger;

            case 4:
                return Soldier;

            case 5:
                return Catapult;

            default:
                return null;
        }
    }


    // =========================================================
    // GET RED PREFAB
    // =========================================================

    private GameObject GetRedPrefab(int type)
    {
        switch (type)
        {
            case 0:
                return RedSword;

            case 1:
                return RedArcher;

            case 2:
                return RedSpearMan;

            case 3:
                return RedSlinger;

            case 4:
                return RedSoldier;

            case 5:
                return RedCatapult;

            default:
                return null;
        }
    }


    // =========================================================
    // CHOOSE COOLDOWN
    // =========================================================

    private bool CanChoose()
    {
        if (!canChoose)
        {
            Debug.Log(
                "[MULTIPLAYER] Đang cooldown!"
            );

            return false;
        }

        return true;
    }


    private IEnumerator ChooseCooldown()
    {
        canChoose = false;

        yield return new WaitForSeconds(
            chooseDelay
        );

        canChoose = true;
    }
}