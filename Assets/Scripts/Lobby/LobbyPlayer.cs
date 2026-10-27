using Fusion;
using UnityEngine;

public class LobbyPlayer : NetworkBehaviour
{
    [SerializeField] private PlayerColorPalette palette;

    [Networked, OnChangedRender(nameof(OnLobbyStateChanged))] public byte ColorIndex { get; private set; }
    [Networked, OnChangedRender(nameof(OnLobbyStateChanged))] public byte SpriteIndex { get; private set; }
    [Networked, OnChangedRender(nameof(OnLobbyStateChanged))] public NetworkBool IsReady { get; private set; }
    [Networked, OnChangedRender(nameof(OnLobbyStateChanged))] public NetworkString<_32> Nickname { get; private set; }

    // Arriba de todo, junto a las otras propiedades [Networked]:
    public const byte NoVote = 255;

    [Networked, OnChangedRender(nameof(OnLobbyStateChanged))] public byte MapVote { get; private set; }


    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            ColorIndex = 0;
            SpriteIndex = 0;
            IsReady = false;
            MapVote = NoVote;
        }

        if (Object.HasInputAuthority)
        {
            SelectColor((byte)PlayerLocalData.SelectedColor);
            SelectSprite((byte)PlayerLocalData.SelectedTexture);
            SetNick(PlayerLocalData.Nickname);
        }

        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    public void SelectColor(byte colorIndex)
    {
        if (!Object.HasInputAuthority)
            return;

        RPC_SelectColor(colorIndex);
    }

    public void SelectMap(byte mapIndex)
    {
        if (!Object.HasInputAuthority)
            return;

        RPC_SelectMap(mapIndex);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SelectMap(byte mapIndex)
    {
        MapVoteController controller = MapVoteController.Instance;

        if (controller == null || !controller.IsVoting)
            return;

        if (mapIndex >= NetworkManager.Instance.MapCount)
            return;

        MapVote = mapIndex;

        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    public void SelectSprite(byte spriteIndex)
    {
        if (!Object.HasInputAuthority)
            return;

        RPC_SelectSprite(spriteIndex);
    }

    public void SetReady(bool ready)
    {
        if (!Object.HasInputAuthority)
            return;

        RPC_SetReady(ready);
    }

    public void SetNick(string nick)
    {
        if (!Object.HasInputAuthority)
            return;
        RPC_SetNick(nick);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SelectColor(byte colorIndex)
    {
        if (IsReady)
            return;

        if (palette == null || palette.CountColors == 0)
            return;

        if (colorIndex >= palette.CountColors)
            return;

        ColorIndex = colorIndex;

        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SelectSprite(byte spriteIndex)
    {
        if (IsReady)
            return;

        if (palette == null || palette.CountSprites == 0)
            return;

        if (spriteIndex >= palette.CountSprites)
            return;

        SpriteIndex = spriteIndex;

        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetReady(bool ready)
    {
        if (MapVoteController.Instance != null && MapVoteController.Instance.IsActive)
            return;

        IsReady = ready;

        PreGameLobbyUI.Instance?.RefreshLobby();

        NetworkManager.Instance?.CheckAllPlayersReady();
    }

    public void ResetMapVote()
    {
        if (!Object.HasStateAuthority)
            return;

        MapVote = NoVote;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetNick(string nick)
    {
        if (nick != null && nick.Length > 20)
            nick = nick.Substring(0, 20);

        Nickname = nick ?? "";
        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    private void OnLobbyStateChanged()
    {
        PreGameLobbyUI.Instance?.RefreshLobby();
    }
}