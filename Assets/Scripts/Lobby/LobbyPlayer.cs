using Fusion;
using UnityEngine;

public class LobbyPlayer : NetworkBehaviour
{
    [SerializeField] private PlayerColorPalette palette;

    [Networked, OnChangedRender(nameof(OnLobbyStateChanged))]
    public byte ColorIndex { get; private set; }

    [Networked, OnChangedRender(nameof(OnLobbyStateChanged))]
    public NetworkBool IsReady { get; private set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            ColorIndex = 0;
            IsReady = false;
        }

        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    public void SelectColor(byte colorIndex)
    {
        if (!Object.HasInputAuthority)
            return;

        RPC_SelectColor(colorIndex);
    }

    public void SetReady(bool ready)
    {
        if (!Object.HasInputAuthority)
            return;

        RPC_SetReady(ready);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SelectColor(byte colorIndex)
    {
        if (IsReady)
            return;

        if (palette == null || palette.Count == 0)
            return;

        if (colorIndex >= palette.Count)
            return;

        ColorIndex = colorIndex;

        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetReady(bool ready)
    {
        IsReady = ready;

        PreGameLobbyUI.Instance?.RefreshLobby();

        NetworkManager.Instance?.CheckAllPlayersReady();
    }

    private void OnLobbyStateChanged()
    {
        PreGameLobbyUI.Instance?.RefreshLobby();
    }
}