using FortressClash.Core;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{

    public GameObject BlueGate;

    public GameObject RedGate;

    public GameObject Sword;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SwordChoosen()
    {
        Debug.Log("SwordChoosen");
        GameObject SwordSpawn = Instantiate(Sword, BlueGate.transform.position,Quaternion.identity);
        SwordSpawn.GetComponent<Soldier>().Destination = RedGate.transform.position;
        SwordSpawn.GetComponent<Soldier>().SpawnPosition = BlueGate.transform.position; 
    }

    public void ArcherChoosen()
    {
        Debug.Log("ArcherChoosen");
    }

    public void SpearChoosen()
    {
        Debug.Log("SpearChoosen");
    }

    public void SlingshotChoosen()
    {
        Debug.Log("SlingshotChoosen");
    }

    public void SoldierChoosen()
    {
        Debug.Log("SoldierChoosen");
    }

    public void CatapultChoosen()
    {
        Debug.Log("CatapultChoosen");
    }
}
