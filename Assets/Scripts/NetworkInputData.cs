using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector2 Direction;
    public NetworkButtons Buttons;
}

public enum InputButton
{
    Fire = 0
}