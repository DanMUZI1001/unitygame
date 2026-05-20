using UnityEngine;

public class CodingTest : MonoBehaviour
{
    private void Start()
    {
        int number = 10;
        float height = 181.5f;
        bool isPlayer = true;
        string playerName = "Player";

        Debug.Log($"{playerName}: number={number}, height={height}, isPlayer={isPlayer}");
    }
}
