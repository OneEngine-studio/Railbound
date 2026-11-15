# Server-authoritative economy

Coins, XP, level, owned operators and challenge progress belong to Nakama. A modified client can read them, never change them.

## Where the data lives

| Data | Where | Who can write |
|---|---|---|
| coins, XP, level, gems, match stats, owned operators, challenge progress, last match reward | Nakama storage `player_economy/profile` (one object per player, `permissionRead 1`, **`permissionWrite 0`**) | only the Nakama module (`railbound_*` RPCs) |
| chosen operator, looks, mode, settings, last 20 matches (display only) | `player_data/*` | the client (they hold nothing with a value) |
| join tickets (match id + user id -> random token) | `railbound_join/<sha256(matchId)>` owned by the player, no client access | only the Nakama module |

The task asked for `player_data` to become write-0. It does not hold the economy: coins/XP/operators were in `player_progress/progress`
(`PermissionWrite 1`), and challenge progress in `player_data/challenges`. Making `player_data` read-only would only have broken
loadout and settings sync. The economy moved to the new write-0 object instead, and `player_progress` and `player_data/challenges` are no longer read or written.
Existing test accounts start from an empty profile (0 coins, nothing bought): their old client-written numbers are not trusted or migrated.

## The RPCs (module: `railbound-gitops/charts/nakama/files/modules/index.js`)

| RPC | Called by | What it does |
|---|---|---|
| `railbound_profile` | player | returns the profile (creates it on first call, rolls challenge periods) and the **server price table** |
| `railbound_buy_operator` `{operatorId}` | player | server price table, checks coins, adds the operator, returns the new profile. `status`: `ok`, `already_owned`, `insufficient_coins`, `unknown_operator` |
| `railbound_claim_challenge` `{tab,key}` | player | pays a challenge the server counted as complete (`not_complete`, `already_claimed`, `unknown_challenge`) |
| `railbound_allocate` `{matchId,players}` | player | unchanged, plus it now returns `token`, the player's join ticket for this match |
| `railbound_validate_join` `{matchId,userId,token}` | game server (HTTP key) | `{valid}`: did this user get this token from `railbound_allocate` for this match? |
| `railbound_report_match` `{matchId,players:[{userId,placement,totalPlayers,kills,survivedSeconds}]}` | game server (HTTP key) | computes XP and coins (`matchReward`), counts stats and challenges, levels up, stores `lastAward`. Idempotent per (player, match id). Ignores any xp/coins the caller sends. Only pays players who have a join ticket for that match. Clamps the numbers (100 players, kills < players, 2 h) |

The two game-server RPCs refuse any call that carries a user session; they need Nakama's `runtime.http_key`.
Profile changes use optimistic concurrency (storage version, 6 retries), so a purchase and a reward at the same moment cannot overwrite each other.

## How the game server learns who is who (connection approval)

1. The lobby calls `railbound_allocate`. Nakama picks the Agones server and answers `host, port, token`. The token is random, stored under that player and that match.
2. The client connects with `NetworkConfig.ConnectionData` = `{"v":1,"m":<matchId>,"u":<Nakama user id>,"t":<token>}` (about 150 bytes), built by `ConnectionApprovalGate.BuildClientPayload` in `GameServerConnection`.
3. The server's `ConnectionApprovalCallback` (`ServerMatchSession.Approve`) refuses the client unless:
   - the payload is well formed (match id and user id present),
   - `m` equals the match id Agones gave this server (waits up to 6 s for the allocation to show up),
   - Nakama says `railbound_validate_join` is `valid` (so the user id cannot be made up and the player really went through allocation),
   - the user is not already connected.
   The refusal reason reaches the client as `DisconnectReason` ("THE GAME SERVER REFUSED THE CONNECTION: ...").
4. The server remembers `clientId -> userId`. `BRMatchCoordinator` tells `ServerMatchSession` about every elimination (place, time, killer's kills) and when the match ends. A player who leaves a running match counts as eliminated at that moment (placement = alive count then); leaving the lobby or warm-up earns nothing.
5. At match end the server posts all human results in one `railbound_report_match` call (4 attempts), before it exits (`AgonesServerLifecycle` waits for it, up to 25 s). Nakama pays; the client's results screen polls `railbound_profile` until `lastAward.matchId` is its match and shows what the server paid.

`ConnectionApproval` is part of the Netcode config hash, so the client and the server must both have it on: `ConnectionApprovalGate.Configure` does that wherever a NetworkManager starts. This is why **client and server must be built from the same commit**.

Game server settings (env of the Agones pod, set in `charts/agones-fleet`):

| Variable | Meaning |
|---|---|
| `RAILBOUND_NAKAMA_URL` | in-cluster Nakama (`http://nakama.apps.svc.cluster.local:7350`); default is the public address from `ServerConfig` |
| `RAILBOUND_NAKAMA_HTTP_KEY` | `runtime.http_key` from a Kubernetes Secret. **Without it the server refuses every client and reports nothing** (fails closed) |
| `-insecure-join` (command line) | local testing only: accepts a well-formed payload without asking Nakama; no results are reported |
| `-match <id>` (command line) | local testing only: match id of a server not started by Agones |

## Offline and guest

- **Guest login is a normal Nakama account** (device id), so a guest has a server profile like everyone else.
- **Not signed in** (no Nakama session): `PlayerProgression.IsLocalOnly`. A separate profile on the device, never uploaded and never merged with an online one. The store says "OFFLINE - LOCAL ONLY - PURCHASES ARE NOT SAVED ONLINE" and the results screen says "LOCAL ONLY - NOT SAVED ONLINE".
- **Signed in but Nakama unreachable**: the last profile the server sent is shown from a device cache. Purchases and claims fail with "CAN'T REACH THE SERVER - NOTHING WAS CHARGED". Nothing on the device can change the numbers.
- **Signed in, match without a game server** (offline/local host): no rewards ("OFFLINE MATCH - NO REWARDS").

## Deploy order (nothing has been deployed)

1. **Rotate Nakama's HTTP key.** `charts/nakama/values.yaml` still has `runtime.httpKey: defaulthttpkey`, a public default. The two game-server RPCs are only as safe as this key: with the default, anyone can report themselves a win. Choose a strong value, put it in `runtime.httpKey`, and create the matching Secret for the game servers:
   `kubectl -n platform create secret generic railbound-nakama --from-literal=http-key='<same value>'`
2. **Nakama module first**: merge the gitops branch `feat/server-authoritative-economy` (module + fleet env). Argo CD updates `nakama-service`. Check the log line `Railbound module loaded: RPCs railbound_allocate, railbound_validate_join, railbound_profile, ...`. Old clients keep working until the next steps, except that their own coin/XP writes no longer count.
3. **Server image second**: build the Unity server from this commit (the CI bumps the image tag in `game-server` and `agones-fleet`), let the fleet roll. Old servers (no approval, no reporting) must be gone before clients of the new version connect, or those matches pay nothing.
4. **Client last**: build the client from the same commit and release it. A new client against an old server cannot connect (config hash mismatch on `ConnectionApproval`), which is the intended failure.

## What was tested

Against a local Nakama 3.37 (docker, Postgres, a mock of the Agones allocation endpoint), throwaway device accounts only:
`rpc-test.js` checks, among others: price table and profile creation; buy without coins, unknown operator, `__proto__`, client-sent price ignored; a client session cannot call the server RPCs, a wrong HTTP key is refused; a client cannot overwrite `player_economy/profile`, a second player cannot read it; join tokens (same player same token, other player's token, wrong match, made-up user id, missing token); reward numbers (xp 1425 / coins 356 for a 3-kill win after 10 min, level-up with bonus), caller-sent xp/coins ignored, duplicate report, a user without ticket gets nothing, clamping; challenge counting and claim (once only, incomplete, wrong tab); five wins then a purchase; 12 parallel buys charge once; parallel reports + purchase lose no update. 40 of 40 pass.

Not tested (no Unity run was possible for this worktree):
- the C# client and server code ran only through the compiler (all of `Assembly-CSharp` compiles with Unity's Roslyn, apart from one error in `BRLookPreset.cs` about `CycleManager`, a file this change does not touch);
- connection approval, `ServerMatchSession` reporting, the store and the results screen in a real match; the Helm template change (not rendered with `helm`);
- network reachability of `nakama.apps.svc.cluster.local:7350` from the `platform` namespace (a NetworkPolicy could block it).

## Known gaps

- **Skin ownership is not enforced in a match.** The server does not check that the skin a client wears (`PlayerSkinNet`) is owned. It is cosmetic, it costs nothing, but a modified client can wear a locked skin.
- The join ticket travels in clear text over UDP (like all Netcode traffic here); someone who captures it can join that match as that player.
- A match id is chosen by the client (a hash of party members, or a random id for a solo match), so anyone who knows another group's match id can ask `railbound_allocate` for a ticket for it. This was already true for the server address.
- A player can allocate a match alone (a server full of bots) and farm rewards. Rewards are server-computed, but not rate-limited.
- `placement` of a player who quits mid-match is the alive count at that moment (can be off by one).
- `railbound_join` records are not cleaned up.
- Old `player_progress` objects stay in the database (the client wrote them, nothing reads them).
