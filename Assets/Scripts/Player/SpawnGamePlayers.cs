using UnityEngine;

public class PlayerSpawnPoints : MonoBehaviour
{
    [Header("2 Players")]
    [SerializeField] private Transform[] twoPlayerSpawns;

    [Header("3 Players")]
    [SerializeField] private Transform[] threePlayerSpawns;

    [Header("4 Players")]
    [SerializeField] private Transform[] fourPlayerSpawns;

    public Transform[] GetSpawnPoints(int playerCount)
    {
        switch (playerCount)
        {
            case 2:
                return twoPlayerSpawns;

            case 3:
                return threePlayerSpawns;

            case 4:
                return fourPlayerSpawns;

            default:
                return null;
        }
    }
}