using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public NetworkButtons Buttons;
}

public enum InputButton
{
    Fire = 0
}