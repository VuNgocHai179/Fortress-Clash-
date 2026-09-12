using System.Collections;
using FortressClash.Core;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManagerMultiplayer : GameManager
{
    private NetworkManager networkManager;

    protected override void Awake()
    {
        base.Awake();

        networkManager = NetworkManager.Singleton;
    }

    private void Start()
    {
        Debug.Log(
            $"[MULTIPLAYER] " +
            $"ClientId = {networkManager.LocalClientId}, " +
            $"IsHost = {networkManager.IsHost}, " +
            $"IsClient = {networkManager.IsClient}, " +
            $"IsServer = {networkManager.IsServer}"
        );
    }

    // =========================================================
    // BLUE PLAYER
    // =========================================================

    public override void SwordChoosen()
    {
        if (!networkManager.IsClient)
            return;

        SpawnBlueSoldierServerRpc(0);
    }

    public override void ArcherChoosen()
    {
        if (!networkManager.IsClient)
            return;

        SpawnBlueSoldierServerRpc(1);
    }

    public override void SpearChoosen()
    {
        if (!networkManager.IsClient)
            return;

        SpawnBlueSoldierServerRpc(2);
    }

    public override void SlingshotChoosen()
    {
        if (!networkManager.IsClient)
            return;

        SpawnBlueSoldierServerRpc(3);
    }

    public override void SoldierChoosen()
    {
        if (!networkManager.IsClient)
            return;

        SpawnBlueSoldierServerRpc(4);
    }

    public override void CatapultChoosen()
    {
        if (!networkManager.IsClient)
            return;

        SpawnBlueSoldierServerRpc(5);
    }

    // =========================================================
    // SERVER RPC - BLUE
    // =========================================================

    [ServerRpc(RequireOwnership = false)]
    private void SpawnBlueSoldierServerRpc(
        int soldierType,
        ServerRpcParams rpcParams = default)
    {
        GameObject prefab = GetBluePrefab(soldierType);

        if (prefab == null)
        {
            Debug.LogError(
                $"[MULTIPLAYER] Blue prefab không tồn tại: {soldierType}"
            );

            return;
        }

        Soldier soldierPrefab =
            prefab.GetComponent<Soldier>();

        if (soldierPrefab == null)
        {
            Debug.LogError(
                $"[MULTIPLAYER] {prefab.name} không có Soldier component!"
            );

            return;
        }

        int price = soldierPrefab.Price;

        // Kiểm tra tiền
        if (Money < price)
        {
            Debug.Log(
                "[MULTIPLAYER] Không đủ tiền!"
            );

            return;
        }

        // Kiểm tra Gate
        if (!BlueGate.GetComponent<Gate>()
            .CheckAroundHasSoldier())
        {
            return;
        }

        // Trừ tiền
        Money -= price;

        // Tăng số lượng
        AddBlueSoldierCount(soldierType);

        // Spawn
        GameObject spawnedObject = Instantiate(
            prefab,
            BlueGate.transform.position,
            Quaternion.identity
        );

        Soldier soldier =
            spawnedObject.GetComponent<Soldier>();

        soldier.Destination =
            RedGate.transform.position;

        soldier.SpawnPosition =
            BlueGate.transform.position;

        NetworkObject networkObject =
            spawnedObject.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError(
                $"[MULTIPLAYER] {prefab.name} không có NetworkObject!"
            );

            Destroy(spawnedObject);

            return;
        }

        networkObject.Spawn();

        Debug.Log(
            $"[MULTIPLAYER] BLUE spawn {prefab.name}"
        );
    }

    // =========================================================
    // RED PLAYER
    // =========================================================

    public void RedSwordChoosenMultiplayer()
    {
        SpawnRedSoldierServerRpc(0);
    }

    public void RedArcherChoosenMultiplayer()
    {
        SpawnRedSoldierServerRpc(1);
    }

    public void RedSlingerChoosenMultiplayer()
    {
        SpawnRedSoldierServerRpc(2);
    }

    public void RedSpearManChoosenMultiplayer()
    {
        SpawnRedSoldierServerRpc(3);
    }

    public void RedSoldierChoosenMultiplayer()
    {
        SpawnRedSoldierServerRpc(4);
    }

    public void RedCatapultChoosenMultiplayer()
    {
        SpawnRedSoldierServerRpc(5);
    }

    // =========================================================
    // SERVER RPC - RED
    // =========================================================

    [ServerRpc(RequireOwnership = false)]
    private void SpawnRedSoldierServerRpc(
        int soldierType,
        ServerRpcParams rpcParams = default)
    {
        GameObject prefab = GetRedPrefab(soldierType);

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
                $"[MULTIPLAYER] {prefab.name} không có SoldierEnemy!"
            );

            return;
        }

        int price = soldierPrefab.Price;

        // Red dùng AIMoney làm tiền
        if (AIMoney < price)
        {
            Debug.Log(
                "[MULTIPLAYER] RED không đủ tiền!"
            );

            return;
        }

        // Kiểm tra Gate
        if (!RedGate.GetComponent<Gate>()
            .CheckAroundHasSoldier())
        {
            return;
        }

        AIMoney -= price;
        AI.Money = AIMoney;

        AddRedSoldierCount(soldierType);

        GameObject spawnedObject = Instantiate(
            prefab,
            RedGate.transform.position,
            Quaternion.identity
        );

        SoldierEnemy soldier =
            spawnedObject.GetComponent<SoldierEnemy>();

        soldier.Destination =
            BlueGate.transform.position;

        soldier.SpawnPosition =
            RedGate.transform.position;

        NetworkObject networkObject =
            spawnedObject.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError(
                $"[MULTIPLAYER] {prefab.name} không có NetworkObject!"
            );

            Destroy(spawnedObject);

            return;
        }

        networkObject.Spawn();

        Debug.Log(
            $"[MULTIPLAYER] RED spawn {prefab.name}"
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
    // COUNT BLUE
    // =========================================================

    private void AddBlueSoldierCount(int type)
    {
        switch (type)
        {
            case 0:
                player.SwordNumber++;
                break;

            case 1:
                player.ArcherNumber++;
                break;

            case 2:
                player.SpearManNumber++;
                break;

            case 3:
                player.SlingerNumber++;
                break;

            case 4:
                player.SoldierNumber++;
                break;

            case 5:
                player.CatapultNumber++;
                break;
        }
    }

    // =========================================================
    // COUNT RED
    // =========================================================

    private void AddRedSoldierCount(int type)
    {
        switch (type)
        {
            case 0:
                AI.SwordNumber++;
                break;

            case 1:
                AI.ArcherNumber++;
                break;

            case 2:
                AI.SpearManNumber++;
                break;

            case 3:
                AI.SlingerNumber++;
                break;

            case 4:
                AI.SoldierNumber++;
                break;

            case 5:
                AI.CatapultNumber++;
                break;
        }
    }
}