# Character skins (player and bot models)

Every playable model is a **skin**: a copy of the MIL-R5 soldier hierarchy (`Assets/Project/character/Player_1`) whose meshes are the
model's own. All skins share the MIL-R5 skeleton, so the existing `CharacterSkinRetargeter`, hand/leg IK and ragdoll drive all of them.

| piece | where |
|---|---|
| catalog (id, name, price, prefab, bots allowed) | `Assets/Project/Resources/CharacterSkinCatalog.asset` |
| baked skin prefabs and meshes | `Assets/Project/character/Skins/` |
| editor tools | `Tools > Railbound > Characters`, `CharacterSkinBaker`, `CharacterSkinSetup` |
| runtime | `Project.Character`: `CharacterSkinApplier`, `PlayerSkinNet` (players), `BRNetBot` (bots) |
| ownership and coins | `PlayerProgression.OwnsOperator / BuyOperatorAsync`: the owned list and the price table are the server's (Nakama RPC `railbound_buy_operator`, see `docs/server-authoritative-economy.md`). **When you change a price in the catalog, change `OPERATOR_PRICES` in the Nakama module too.** |
| lobby | `LobbyCharacterPreviewManager` (operators), `LobbyStoreScreen` (locked cards, PREVIEW, BUY) |

## Current skins

| id | model | source |
|---|---|---|
| `player1` | MIL-R5 (Player_1) | the original, owned from the start |
| `striker` | Striker-14 jumpsuit | `jumpsuit-low-poly-game-ready-striker14` (FBX) |
| `cammy` | Cammy | `cammy-game-ready-character` (FBX; hair and clothes materials made by `CharacterSkinImport`) |
| `tactical` | Tactical | `game-ready-low-poly-character-tactical` (FBX) |
| `swat` | S.W.A.T. operator | `game-ready-swat-character` (FBX, bones prefixed `swat:`) |

Earlier glb models (federation, ghost, modern soldier, a glb S.W.A.T.) were removed: they had no skeleton.

## Adding a model

The model must be **an FBX rigged to a Mixamo-style skeleton** (bones `mixamorig:Hips`, `mixamorig:LeftUpLeg`, ...; another prefix such as
`swat:` is accepted). A static mesh with no skeleton cannot be animated: upload it to mixamo.com for auto-rigging first.

1. Put the FBX (and its textures) under `Assets/Project/character/<name>/`. Check that every material has its textures; materials that
   do not can be replaced in `CharacterSkinImport` (see how cammy and tactical are done).
2. Bake it: `CharacterSkinBaker.Bake("<id>", "<fbx path>", overrides)` (writes `Skin_<id>.prefab` and its meshes). The baker puts the
   skeleton into the model's true bind pose, scales and turns the model onto the MIL-R5 skeleton, re-poses arms, legs and fingers into the
   T-pose, re-binds the weights by bone name and hangs loose parts (hair, props) on the nearest bone.
3. Add an entry in `CharacterSkinSetup.CreateOrRefreshCatalog`: id, name, price, `Bot Eligible`.
4. The lobby store shows it as a locked operator automatically; bots pick a random usable, bot-eligible skin.

Test aid: start a build with `-skin <id>` to wear a skin whatever the profile says.
