using UnityEngine;
using Fusion;

public class GameManager : NetworkBehaviour
{
    [Networked] public int player1Healths {  get; set; }
    [Networked] public int player2Healths { get; set; }
    [Networked] public int player3Healths { get; set; }
    [Networked] public int player4Healths { get; set; }
}
