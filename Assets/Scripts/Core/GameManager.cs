using System;
using System.Collections;
using FortressClash.Core;
using TMPro;
using UnityEngine;

public struct PlayerHas
{
    public int SwordNumber;
    public int ArcherNumber;
    public int SlingerNumber;
    public int SpearManNumber;
    public int SoldierNumber;
    public int CatapultNumber;
}

public struct AIHas
{
    public int SwordNumber;
    public int ArcherNumber;
    public int SlingerNumber;
    public int SpearManNumber;
    public int SoldierNumber;
    public int CatapultNumber;
    public int SwordMoney;
    public int ArcherMoney;
    public int SlingerMoney;
    public int SpearManMoney;
    public int CatapultMoney;
    public int SoldierMoney;
    public int Money;
}

public class GameManager : Singleton<GameManager>
{
    public GameObject BlueGate;
    public GameObject RedGate;
    public GameObject Sword;
    public GameObject Archer;
    public GameObject Slinger;
    public GameObject SpearMan;
    public GameObject Soldier;
    public GameObject Catapult;


    public GameObject RedSword;
    public GameObject RedArcher;
    public GameObject RedSlinger;
    public GameObject RedSpearMan;
    public GameObject RedSoldier;
    public GameObject RedCatapult;

    public int Money;

    public int AIMoney;

    // Số tiền nhận được mỗi giây
    public int moneyPersecond;

    [SerializeField]
    private TextMeshProUGUI numberMoney;

    // Thời gian delay giữa mỗi lần chọn lính
    [SerializeField]
    private float chooseDelay = 5f;

    // Đang trong thời gian chờ hay không
    private bool canChoose = true;

    public PlayerHas player;

    public AIHas AI;

    void Start()
    {
        numberMoney.SetText(Money.ToString());
        setMoneyforAI();

        // Bắt đầu tăng tiền theo giây
        StartCoroutine(AddMoneyPerSecond());
    }

    // ==========================================
    // TĂNG TIỀN THEO GIÂY
    // ==========================================
    private IEnumerator AddMoneyPerSecond()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            Money += moneyPersecond;
            AI.Money += moneyPersecond;
            AIMoney = AI.Money;

            numberMoney.SetText(Money.ToString());
        }
    }

    private bool isEnoughMoney(int money)
    {
        if (money <= Money)
            return true;

        Debug.Log("khong du dui ga");
        return false;
    }

    private void spentMoney(int money)
    {
        Money -= money;
        numberMoney.SetText(Money.ToString());
    }

    // Kiểm tra có được chọn lính hay không
    private bool CanChoose()
    {
        if (!canChoose)
        {
            Debug.Log("Đang chờ, chưa thể chọn lính!");
            return false;
        }

        return true;
    }

    // Bắt đầu thời gian delay
    private IEnumerator ChooseCooldown()
    {
        canChoose = false;

        yield return new WaitForSeconds(chooseDelay);

        canChoose = true;
    }

    public virtual void SwordChoosen()
    {
        spawnSoldier(Sword);
        player.SwordNumber++;

        Debug.Log("SwordChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public virtual void ArcherChoosen()
    {
        spawnSoldier(Archer);
        player.ArcherNumber++;

        Debug.Log("ArcherChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public virtual void SpearChoosen()
    {
        spawnSoldier(SpearMan);
        player.SpearManNumber++;

        Debug.Log("SpearChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public virtual void SlingshotChoosen()
    {
        spawnSoldier(Slinger);
        player.SlingerNumber++;

        Debug.Log("SlingshotChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public virtual void SoldierChoosen()
    {
        spawnSoldier(Soldier);
        player.SoldierNumber++;

        Debug.Log("SoldierChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public virtual void CatapultChoosen()
    {
        spawnSoldier(Catapult);
        player.CatapultNumber++;

        Debug.Log("CatapultChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public void RedSwordChoosen()
    {
        spawnEnemy(RedSword);
        AI.SwordNumber++;
    }

    public void RedArcherChoosen()
    {
        spawnEnemy(RedArcher);
        AI.ArcherNumber++;
    }

    public void RedSoldierChoosen()
    {
        spawnEnemy(RedSoldier);
        AI.SoldierNumber++;
    }

    public void RedSlingerChoosen()
    {
        spawnEnemy(RedSlinger);
        AI.SlingerNumber++;
    }

    public void RedCatapultChoosen()
    {
        spawnEnemy(RedCatapult);
        AI.CatapultNumber++;
    }

    public void RedSpearManChoosen()
    {
        spawnEnemy(RedSpearMan);
        AI.SpearManNumber++;
    }

    public void Addmoney(int money)
    {
        Money += money;
        numberMoney.SetText(Money.ToString());
    }

    public void Winner(string winner)
    {
        if(winner == "blue")
        {
            Debug.Log("blue win");
        }
        else if (winner == "red")
        {
            Debug.Log("red win");
        }
    }

    private void spawnSoldier(GameObject spawn)
    {
        if (!CanChoose())
            return;
        if(!BlueGate.GetComponent<Gate>().CheckAroundHasSoldier()) return;

        int price = spawn.GetComponent<Soldier>().Price;

        if (!isEnoughMoney(price))
            return;

        spentMoney(price);

        GameObject Spawn = Instantiate(spawn,
        BlueGate.transform.position,
        Quaternion.identity);

        Spawn.GetComponent<Soldier>().Destination =
            RedGate.transform.position;

        Spawn.GetComponent<Soldier>().SpawnPosition =
            BlueGate.transform.position;
    }

    private void spawnEnemy(GameObject spawn)
    {
        if(!RedGate.GetComponent<Gate>().CheckAroundHasSoldier()) return;
        int price = spawn.GetComponent<SoldierEnemy>().Price;
        if(AIMoney<price) return;

        AI.Money -= price;
        AIMoney = AI.Money;

        GameObject Spawn = Instantiate(spawn,
        RedGate.transform.position,
        Quaternion.identity);

        Spawn.GetComponent<SoldierEnemy>().Destination =
            BlueGate.transform.position;

        Spawn.GetComponent<SoldierEnemy>().SpawnPosition =
            RedGate.transform.position;
    }

    private void setMoneyforAI()
    {
        AI.SwordMoney = RedSword.GetComponent<SoldierEnemy>().Price;
        AI.ArcherMoney = RedArcher.GetComponent<SoldierEnemy>().Price;
        AI.SlingerMoney = RedSlinger.GetComponent<SoldierEnemy>().Price;
        AI.CatapultMoney = RedCatapult.GetComponent<SoldierEnemy>().Price;
        AI.SpearManMoney = RedSpearMan.GetComponent<SoldierEnemy>().Price;
        AI.SoldierMoney = RedSoldier.GetComponent<SoldierEnemy>().Price;
        AI.Money = AIMoney;
    }
    public void soldierDown(String name)
    {
        switch(name)
        {
            case "BlueSword":
                {
                    player.SwordNumber--;
                    return;
                }
            case "BlueArcher":
                {
                    player.ArcherNumber--;
                    return;
                }
            case "BlueSlinger":
                {
                    player.SlingerNumber--;
                    return;
                }
            case "BlueSoldier":
                {
                    player.SoldierNumber--;
                    return;
                }
            case "BlueCatapult":
                {
                    player.CatapultNumber--;
                    return;
                }
            case "BlueSpearMan":
                {
                    player.SpearManNumber--;
                    return;
                }
            case "RedSword":
                {
                    AI.SwordNumber--;
                    return;
                }
            case "RedArcher":
                {
                    AI.ArcherNumber--;
                    return;
                }
            case "RedSlinger":
                {
                    AI.SlingerNumber--;
                    return;
                }
            case "RedSoldier":
                {
                    AI.SoldierNumber--;
                    return;
                }
            case "RedCatapult":
                {
                    AI.CatapultNumber--;
                    return;
                }
            case "RedSpearMan":
                {
                    AI.SpearManNumber--;
                    return;
                }
        }
    }
}