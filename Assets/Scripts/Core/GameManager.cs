using System.Collections;
using FortressClash.Core;
using TMPro;
using UnityEngine;

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

    public int Money;

    // Số tiền nhận được mỗi giây
    public int moneyPersecond;

    [SerializeField]
    private TextMeshProUGUI numberMoney;

    // Thời gian delay giữa mỗi lần chọn lính
    [SerializeField]
    private float chooseDelay = 1f;

    // Đang trong thời gian chờ hay không
    private bool canChoose = true;

    void Start()
    {
        numberMoney.SetText(Money.ToString());

        // Bắt đầu tăng tiền theo giây
        StartCoroutine(AddMoneyPerSecond());

        RedSwordChoosen();
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

    public void SwordChoosen()
    {
        spawnSoldier(Sword);
        spawnEnemy(RedSword);

        Debug.Log("SwordChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public void ArcherChoosen()
    {
        spawnSoldier(Archer);

        Debug.Log("ArcherChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public void SpearChoosen()
    {
        spawnSoldier(SpearMan);

        Debug.Log("SpearChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public void SlingshotChoosen()
    {
        spawnSoldier(Slinger);

        Debug.Log("SlingshotChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public void SoldierChoosen()
    {
        spawnSoldier(Soldier);

        Debug.Log("SoldierChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public void CatapultChoosen()
    {
        spawnSoldier(Catapult);

        Debug.Log("CatapultChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public void RedSwordChoosen()
    {
        spawnEnemy(RedSword);

        StartCoroutine(ChooseCooldown());
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
        if (!CanChoose())
            return;

        int price = spawn.GetComponent<SoldierEnemy>().Price;

        if (!isEnoughMoney(price))
            return;

        spentMoney(price);

        GameObject Spawn = Instantiate(spawn,
        RedGate.transform.position,
        Quaternion.identity);

        Spawn.GetComponent<SoldierEnemy>().Destination =
            BlueGate.transform.position;

        Spawn.GetComponent<SoldierEnemy>().SpawnPosition =
            RedGate.transform.position;
    }
}