using System;
using UnityEngine;

public class Gate : MonoBehaviour
{
    [SerializeField]
    private int Health;

    [SerializeField]
    private String oppositeSide;

    [SerializeField]
    private float checkRadius = 2f;

    public void getHitted(int damage)
    {
        Health = Mathf.Max(
            0,
            Health - damage
        );

        if(Health == 0) sendMessgeWinner();
    }

    private void sendMessgeWinner()
    {
        GameManager.Instance.Winner(oppositeSide);
    }

    public bool CheckAroundHasSoldier()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(
            transform.position,
            checkRadius
        );

        foreach (Collider2D collider in colliders)
        {
            if (collider.GetComponent<Soldier>() != null && oppositeSide == "red")
            {
                return false;
            }
            else if(collider.GetComponent<SoldierEnemy>() != null && oppositeSide == "blue")
            {
                return false;
            }
        }

        return true;
    }
}
