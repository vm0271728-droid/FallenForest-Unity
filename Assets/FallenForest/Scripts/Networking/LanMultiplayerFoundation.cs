using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace FallenForest.Networking
{
    /// <summary>
    /// Future session types. The shipping game remains SinglePlayer until the LAN feature is
    /// explicitly enabled and a transport adapter is implemented.
    /// </summary>
    public enum SessionMode
    {
        SinglePlayer = 0,
        LanHost = 1,
        LanClient = 2
    }

    /// <summary>
    /// Stable logical player identity. Gameplay systems should eventually target PlayerId / IPlayerRoster
    /// instead of assuming there is exactly one Player object in the scene.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public static readonly PlayerId Invalid = new(-1);

        public PlayerId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value >= 0;

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? $"Player-{Value}" : "Player-Invalid";

        public static bool operator ==(PlayerId left, PlayerId right) => left.Equals(right);
        public static bool operator !=(PlayerId left, PlayerId right) => !left.Equals(right);
    }

    /// <summary>
    /// Read-only view used by AI, objectives and cinematics. It deliberately contains only generic
    /// scene references so the networking layer does not depend on a specific transport package.
    /// </summary>
    public readonly struct PlayerView
    {
        public PlayerView(PlayerId id, Transform root, Camera camera, bool isLocal, bool isAlive)
        {
            Id = id;
            Root = root;
            Camera = camera;
            IsLocal = isLocal;
            IsAlive = isAlive;
        }

        public PlayerId Id { get; }
        public Transform Root { get; }
        public Camera Camera { get; }
        public bool IsLocal { get; }
        public bool IsAlive { get; }
        public bool IsValid => Id.IsValid && Root != null;
    }

    /// <summary>
    /// Contract that future multiplayer-aware gameplay systems can query instead of using
    /// FindFirstObjectByType for a single player.
    /// </summary>
    public interface IPlayerRoster
    {
        IReadOnlyList<PlayerView> Players { get; }
        bool TryGetLocalPlayer(out PlayerView player);
        bool TryGetPlayer(PlayerId id, out PlayerView player);
    }

    /// <summary>
    /// Host-authoritative shared objective contract. In LAN co-op the document counter is shared,
    /// while the single-player implementation continues to use GameProgress directly.
    /// </summary>
    public interface ISharedObjectiveAuthority
    {
        int DocumentsCollected { get; }
        int RequiredDocuments { get; }
        event Action<int, PlayerId> DocumentCollected;
        bool TryCollectDocument(int documentSlot, PlayerId collector);
    }

    /// <summary>
    /// AI hook for selecting among multiple living players without coupling monster code to a
    /// networking SDK. The future host will own authoritative monster target selection.
    /// </summary>
    public interface IMonsterTargetSelector
    {
        bool TrySelectTarget(Vector3 monsterPosition, out PlayerView target);
    }

    /// <summary>
    /// Transport-neutral contract reserved for a future LAN implementation. No implementation is
    /// registered in the current game, so this cannot open sockets or start a network session yet.
    /// </summary>
    public interface ILanTransportAdapter
    {
        bool IsRunning { get; }
        bool IsHost { get; }
        int ConnectedPlayers { get; }
        void StartHost(int gamePort, int discoveryPort, int maxPlayers);
        void StartClient(string privateAddress, int gamePort);
        void Stop();
    }

    [Serializable]
    public sealed class LanMultiplayerSettings
    {
        [SerializeField, Range(2, 4)] private int maxPlayers = 4;
        [SerializeField, Range(1024, 65535)] private int gamePort = 7777;
        [SerializeField, Range(1024, 65535)] private int discoveryPort = 47777;

        public int MaxPlayers => maxPlayers;
        public int GamePort => gamePort;
        public int DiscoveryPort => discoveryPort;
    }

    /// <summary>
    /// Hard policy for the requested future mode: local network only. No cloud matchmaking,
    /// relay, account service or public-IP peer is allowed by this layer.
    /// </summary>
    public static class LanOnlyNetworkPolicy
    {
        // Intentionally false. This branch only lays the architecture; it does not add multiplayer UI,
        // socket discovery, host/client buttons or networked gameplay to the game.
        public const bool RuntimeEnabled = false;

        public const int DefaultGamePort = 7777;
        public const int DefaultDiscoveryPort = 47777;
        public const int MaximumPlayers = 4;

        public static bool InternetBackendAllowed => false;
        public static bool RelayAllowed => false;
        public static bool PublicMatchmakingAllowed => false;

        /// <summary>
        /// Accept only loopback, link-local, or RFC1918 IPv4 addresses. Public WAN addresses are rejected.
        /// A later discovery implementation must additionally verify that the peer is reachable through
        /// the device's current local interface/subnet before connecting.
        /// </summary>
        public static bool IsAllowedPeerAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address) || !IPAddress.TryParse(address, out IPAddress ip))
                return false;

            if (IPAddress.IsLoopback(ip))
                return true;

            byte[] bytes = ip.GetAddressBytes();
            if (bytes.Length != 4)
                return false;

            // 10.0.0.0/8
            if (bytes[0] == 10)
                return true;

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;

            // 169.254.0.0/16 link-local
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;

            return false;
        }
    }

    /// <summary>
    /// Global session marker prepared for later dependency injection. It defaults permanently to
    /// SinglePlayer in the current game because no networking bootstrap calls SetFutureMode.
    /// </summary>
    public static class SessionContext
    {
        private static SessionMode mode = SessionMode.SinglePlayer;

        public static SessionMode Mode => mode;
        public static bool IsMultiplayer => mode != SessionMode.SinglePlayer;
        public static bool IsHost => mode == SessionMode.LanHost;

        public static void ResetToSinglePlayer()
        {
            mode = SessionMode.SinglePlayer;
        }

        // Reserved for the future LAN bootstrap. Kept internal so current gameplay/UI cannot enable it.
        internal static void SetFutureMode(SessionMode futureMode)
        {
            if (!LanOnlyNetworkPolicy.RuntimeEnabled && futureMode != SessionMode.SinglePlayer)
                throw new InvalidOperationException("LAN multiplayer runtime is intentionally disabled in this build.");

            mode = futureMode;
        }
    }
}
