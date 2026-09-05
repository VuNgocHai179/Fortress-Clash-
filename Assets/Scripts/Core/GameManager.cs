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
        if (!CanChoose())
            return;

        int price = Sword.GetComponent<Soldier>().Price;

        if (!isEnoughMoney(price))
            return;

        spentMoney(price);

        Debug.Log("SwordChoosen");

        GameObject SwordSpawn = Instantiate(
            Sword,
            BlueGate.transform.position,
            Quaternion.identity
        );

        SwordSpawn.GetComponent<Soldier>().Destination =
            RedGate.transform.position;

        SwordSpawn.GetComponent<Soldier>().SpawnPosition =
            BlueGate.transform.position;

        StartCoroutine(ChooseCooldown());
    }

    public void ArcherChoosen()
    {
        if (!CanChoose())
            return;

        int price = Archer.GetComponent<Archer>().Price;

        if (!isEnoughMoney(price))
            return;

        spentMoney(price);

        Debug.Log("ArcherChoosen");

        GameObject ArcherSpawn = Instantiate(
            Archer,
            BlueGate.transform.position,
            Quaternion.identity
        );

        ArcherSpawn.GetComponent<Archer>().Destination =
            RedGate.transform.position;

        ArcherSpawn.GetComponent<Archer>().SpawnPosition =
            BlueGate.transform.position;

        StartCoroutine(ChooseCooldown());
    }

    public void SpearChoosen()
    {
        if (!CanChoose())
            return;

        Debug.Log("SpearChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public void SlingshotChoosen()
    {
        if (!CanChoose())
            return;

        int price = Slinger.GetComponent<Slinger>().Price;

        if (!isEnoughMoney(price))
            return;

        spentMoney(price);

        Debug.Log("SlingshotChoosen");

        GameObject SlingerSpawn = Instantiate(
            Slinger,
            BlueGate.transform.position,
            Quaternion.identity
        );

        Slinger slingerComponent = SlingerSpawn.GetComponent<Slinger>();

        slingerComponent.Destination =
            RedGate.transform.position;

        slingerComponent.SpawnPosition =
            BlueGate.transform.position;

        StartCoroutine(ChooseCooldown());
    }

    public void SoldierChoosen()
    {
        if (!CanChoose())
            return;

        Debug.Log("SoldierChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public void CatapultChoosen()
    {
        if (!CanChoose())
            return;

        Debug.Log("CatapultChoosen");

        StartCoroutine(ChooseCooldown());
    }

    public void Addmoney(int money)
    {
        Money += money;
        numberMoney.SetText(Money.ToString());
    }
}