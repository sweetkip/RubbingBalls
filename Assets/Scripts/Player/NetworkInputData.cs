using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public NetworkButtons Buttons;
    public Vector2 AimWorldPosition;
    public Vector2 AimOffset;
}

public enum InputButton
{
    Fire = 0
}