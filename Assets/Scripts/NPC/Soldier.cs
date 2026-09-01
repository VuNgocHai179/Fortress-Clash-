using System;
using UnityEngine;

public class Soldier : MonoBehaviour
{
    public float Velocity;

    public bool BlueSide;

    public Vector2 Destination;
    public Vector2 SpawnPosition;


    void Start()
    {

    }

    void Update()
    {
        MoveToDestination();
    }

    private void MoveToDestination()
    {
        transform.position = Vector2.MoveTowards(
            transform.position,
            Destination,
            Velocity * Time.deltaTime
        );
    }
}