using System;
using UnityEngine;

public class Gate : MonoBehaviour
{
    [SerializeField]
    private int Health;

    [SerializeField]
    private String oppositeSide;

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
}
