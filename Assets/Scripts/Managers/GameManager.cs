using UnityEngine;
using Fusion;

public class GameManager : NetworkBehaviour
{
    [Networked] public int ScoreA {  get; set; }
    [Networked] public int ScoreB { get; set; }
}
