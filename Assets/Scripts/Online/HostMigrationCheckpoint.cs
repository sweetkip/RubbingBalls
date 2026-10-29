using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class HostMigrationCheckpoint
{
    public int Protocol = 1;
    public string RoomId;
    public string RoomName;
    public string Region;
    public string HostIdentity;
    public int Epoch;
    public int Revision;
    public int NextJoinOrder;
    public int SceneIndex;
    public bool IsLobby;
    public bool SessionVisible;
    public bool EverHadTwoPlayers;
    public string Chat;
    public MigrationPlayerState[] Players;
    public MigrationGameState Game;
    public MigrationVoteState Vote;
}

[Serializable]
public sealed class MigrationPlayerState
{
    public string Identity;
    public int JoinOrder;
    public bool Connected;
    public int GameId = -1;
    public string Name = "NoName";
    public byte ColorIndex;
    public byte SpriteIndex;
    public bool Ready;
    public byte MapVote = 255;
    public bool HasBall;
    public float Damage;
    public float DisplayDamage;
    public int Lives;
    public int ShootsLeft;
    public Vector3 Position;
    public Quaternion Rotation = Quaternion.identity;
    public Vector2 Velocity;
    public float AngularVelocity;
    public Vector3 SpawnPosition;
    public Quaternion SpawnRotation = Quaternion.identity;
    public float ProtectionRemaining;
}

[Serializable]
public sealed class MigrationGameState
{
    public int TotalPlayers;
    public int WinnerId = -1;
    public bool ReturnRunning;
    public float ReturnRemaining;
    public bool StartRunning;
    public float StartRemaining;
}

[Serializable]
public sealed class MigrationVoteState
{
    public byte Phase;
    public byte CandidatesMask;
    public byte WinnerMap;
    public float Remaining;
}

public static class HostMigrationOrder
{
    public static MigrationPlayerState[] Remaining(MigrationPlayerState[] players, string departedIdentity)
    {
        List<MigrationPlayerState> result = new List<MigrationPlayerState>();
        if (players != null)
            foreach (MigrationPlayerState player in players)
                if (player != null && player.Connected && player.Identity != departedIdentity)
                    result.Add(player);
        result.Sort((a, b) => a.JoinOrder.CompareTo(b.JoinOrder));
        return result.ToArray();
    }

    public static string SessionName(string roomId, int epoch)
    {
        return "RBM-" + roomId + "-" + epoch;
    }
}
