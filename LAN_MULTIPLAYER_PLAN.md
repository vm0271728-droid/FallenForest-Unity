# Fallen Forest — future LAN co-op foundation

Status: architecture only. Multiplayer is **not enabled in the game**.

## Fixed product rule

Future co-op is local-network-only:

- 2–4 players.
- One player hosts the session on the current LAN/Wi-Fi network.
- Other players discover the host by LAN broadcast or connect to a private local IPv4 address.
- No Internet matchmaking.
- No relay servers.
- No cloud account requirement.
- No public-IP direct connection.
- No dedicated server requirement.

The current single-player release remains the only visible/playable mode until the multiplayer feature is explicitly implemented later.

## Architecture already reserved

`Assets/FallenForest/Scripts/Networking/LanMultiplayerFoundation.cs` defines transport-neutral contracts so existing gameplay can later be migrated without tying the project to one networking SDK:

- `PlayerId` — stable logical player identity.
- `PlayerView` / `IPlayerRoster` — lets AI and gameplay query multiple players instead of assuming one global Player object.
- `ISharedObjectiveAuthority` — future host-authoritative shared 10-document objective.
- `IMonsterTargetSelector` — future AI target selection among living players.
- `ILanTransportAdapter` — adapter point for the eventual LAN transport implementation.
- `LanOnlyNetworkPolicy` — explicitly rejects public WAN addresses and disables cloud/relay/matchmaking architecture.
- `SessionContext` — reserved session marker; it remains `SinglePlayer` in current builds.

`LanOnlyNetworkPolicy.RuntimeEnabled` is intentionally `false`, so none of this can start multiplayer yet.

## Planned gameplay authority

When LAN co-op is implemented, the host should own authoritative state for:

- collected documents and document slots;
- run seed and procedural objective placement;
- monster spawn decisions;
- monster target selection;
- final chase trigger;
- end-of-run state.

Clients should own only local input/camera presentation and send their intended movement/actions to the host implementation.

## Horror behavior planned for multiplayer

- Documents are a shared team objective: `10 / 10` for the whole session.
- Locust may choose a different target on each encounter; it must not be hard-wired to one global player.
- Boiled One can later support a player-specific manifestation while the host still records the authoritative encounter state.
- Final chase starts globally after document 10, but each client keeps its own camera/UI presentation.
- A single player's death should not automatically end the whole LAN session unless the eventual design explicitly chooses that rule.

## Save behavior

Single-player saves remain unchanged.

LAN sessions should use temporary host-owned session state rather than writing each client's normal single-player `PlayerPrefs` save. This prevents a co-op run from corrupting or advancing the player's solo save.

## Transport implementation later

The actual transport package is intentionally not selected or installed yet. When implementation starts, it should satisfy `ILanTransportAdapter` and remain usable with no Internet connection. LAN discovery should use a local broadcast/discovery port, while game traffic uses a separate game port.

Default reserved ports in the foundation:

- game: `7777`
- discovery: `47777`

These are defaults only and can be changed before the multiplayer feature ships.
