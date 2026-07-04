# 🐟 Tide & Table — Dev Doc

A calm, soothing **fish restaurant** game built on top of the Kitchen Chaos
codebase. The game revolves around **beautifully cutting fish** and **plating
seafood dishes**: customers order, you prep (fillet / peel / shuck / ring), plate,
serve — and feed the leftover skeletons & shells to a resident cat.

> This file is the running record of **what we've built** and **how to wire it up
> in the Unity scene**. Update the Changelog at the bottom as we go.

- **Engine:** Unity `6000.3.17f1` (URP 17, Cinemachine 2.10.7, Input System, Timeline)
- **Working title:** *Tide & Table*
- **Base project:** Code Monkey "Kitchen Chaos" (heavily refactored, event-driven)

---

## 1. Design pillars (decided)

| Topic | Decision |
|---|---|
| **Pace / pressure** | **Soft pressure.** No game-over. `zenMode` is on by default so the timer never ends play. Customer patience / tip falloff comes later (Phase 3). |
| **Cutting feel** | **Taps + perfect-slice.** Tap to advance each cut stage; an optional sweet-spot earns a sparkle + small bonus. Missing never punishes. |
| **Camera** | **Over-the-shoulder** cinematic close-up on prep (Cinemachine), with optional depth-of-field blur. Normal walk-around movement is unchanged. |
| **Character** | A cute **cartoon chef kid** (ithappy *Cute Characters*). Knife stays a separate animated prop (no hand-IK needed). Swap-in is deferred, not required to play. |
| **Workflow** | **Claude builds** scripts + ScriptableObjects + the content generator. **You place/arrange** prefabs in the scene. |

---

## 2. Imported asset packs

| Pack | Location | Used for |
|---|---|---|
| Cartoon Seafood Pack (Mnostva) | `Assets/Mnostva_Art/Cartoon_Seafood_Pack` | All fish/shellfish, with built-in cut stages (Whole→Half→Fillet→Slice), skeletons, rings |
| KayKit Restaurant Bits | `Assets/KayKit` | Modular kitchen/restaurant environment, cutting board, knife, order window, tables |
| DOTween | `Assets/Plugins/Demigiant/DOTween` | Juice/tweens (squash, hops, camera DoF blend) |
| Quirky Series – Pets Vol.1 | `Assets/Quirky Series/.../Pets Vol.1` | **Cat** & **Dog** (animator controllers `AC_Cat` / `AC_Dog` with `Idle_A`, `Eat`, `Eyes_Happy`, …) |
| ithappy – Cute Characters | `Assets/ithappy/Cute_Characters` | Modular chef-kid character (idle/walk humanoid anims + controllers) |

> ⚠️ If fish render **pink/magenta**, import the URP materials:
> `Assets/Mnostva_Art/Cartoon_Seafood_Pack/Render_Pipeline/URP_Cartoon_Seafood_Pack.unitypackage`

---

## 3. What's been built — Phase 1 ("The Beautiful Cut")

### 3.1 New scripts

| File | Purpose |
|---|---|
| `Assets/Scripts/Camera/PrepCameraDirector.cs` | Eases an OTS Cinemachine vcam (+ optional DoF Volume) in when you're at a board with a cuttable item / mid-cut, and out when you leave. |
| `Assets/Scripts/Pets/PetController.cs` | Drives the cat/dog animator by **state name** (`Idle_A`, `Eat`, `Eyes_Happy`). `Feed()` plays the eat reaction + a little hop, then returns to idle. Fully null-guarded. |
| `Assets/Scripts/Counters/CatStationCounter.cs` | Drop-off counter that accepts only `petFood` items (skeletons/shells), feeds the pet, and ticks the score. Static event `OnAnyPetFed`. |
| `Assets/Scripts/Counters/CuttingCounter/PerfectSliceIndicator.cs` | Visualises the perfect-slice sweep (moves a `marker` across a track, positions the `sweetZone`). Works world-space or UI. |
| `Assets/Scripts/Game/ZenScoreManager.cs` | Additive, never-fail score: plates served, perfect slices, pets fed. Fires `OnScoreChanged` for UI. |
| `Assets/Scripts/Editor/FishContentGenerator.cs` | **One-click content generator** (menu: `Tide & Table ▸ Generate Fish Content`). See §4. |
| `Assets/Scripts/Editor/SpriteIconGenerator.cs` | **One-click icon generator** (menu: `Tide & Table ▸ Generate Item Icons`). Renders each item prefab to a transparent sprite and assigns it to `KitchenObjectSO.icon`. See §4.4. |
| `Assets/Scripts/ScriptableObjects/RecipeDatabaseSO.cs` | Flat list of all cutting/frying recipes + dishes (auto-filled by the generator). Powers the cookbook. |
| `Assets/Scripts/Cookbook/Cookbook.cs` | Pure logic: traces a dish backwards into ordered **steps** (take → cut → cook → plate). |
| `Assets/Scripts/Cookbook/OrderProcedureLogger.cs` | Component: logs each incoming order's steps to the Console. Assign the RecipeDatabase. See §4.5. |
| `Assets/Scripts/Editor/CookbookPrinter.cs` | Menu `Tide & Table ▸ Print Cookbook` — dumps every dish's steps to the Console (no Play needed). |

### 3.2 Modified scripts

| File | Change |
|---|---|
| `Assets/Scripts/Counters/CuttingCounter/CuttingCounter.cs` | **Multi-stage** chained cutting; spawns a **byproduct** (skeleton) to an assigned scrap tray; **perfect-slice** rhythm + `OnAnyPerfectSlice`; exposes `IsCutting`, `HasCuttableItem`, `RhythmPhase`, `GetCameraPose()` for camera/visuals. |
| `Assets/Scripts/Counters/CuttingCounter/CuttingCounterVisual.cs` | Knife trigger + DOTween squash, slice particles, perfect sparkle (all optional refs). |
| `Assets/Scripts/ScriptableObjects/CuttingRecipeSO.cs` | Added `byproduct` (KitchenObjectSO) and cosmetic `ProcessVerb` enum (Cut/Fillet/Slice/Peel/Shuck/Ring/Chop). |
| `Assets/Scripts/ScriptableObjects/KitchenObjectSO.cs` | Added `petFood` flag. |
| `Assets/Scripts/Players/Player.cs` | Added `GetSelectedCounter()` (used by the camera director). |
| `Assets/Scripts/Game/GameManager.cs` | Added `zenMode` (default **on**) — running timer no longer ends the game. |
| `Assets/Scripts/ScriptableObjects/SFXSO.cs` | Added empty clip slots: `slice`, `slicePerfect`, `peel`, `shuck`, `petEat`, `petPurr`. |
| `Assets/Scripts/Sound/SFXManager.cs` | Wired perfect-slice + pet-feed SFX; **null/empty guards** so unwired clips never throw. `slice` falls back to `chop`. |
| `Assets/Scripts/Utilities/StaticDataReset.cs` | Resets `CatStationCounter` statics on scene load. |
| `Assets/Scripts/KitchenObjects/KitchenObject.cs` | Removed a dead `using UnityEditor;` (would have broken player builds). |

### 3.3 How it fits together (flow)

```
Whole fish (ContainerCounter source)
   → CuttingCounter: alt-interact repeatedly
        • each stage swaps the mesh (Whole→Half→Fillet→Sashimi)
        • perfect-slice sweet spot → sparkle + bonus
        • filleting drops a Fish_Skeleton onto the scrap tray
   → carry the portion to a Plate (plated visual reveals per ingredient)
   → DeliveryCounter: matches an open order → served (+score)
   → carry the skeleton/shell to the CatStationCounter → cat eats & purrs (+score)
```

Prep camera (`PrepCameraDirector`) leans into the OTS close-up the whole time you're
at the board, then eases back out when you step away.

---

## 4. Content generator (run this first)

**Menu bar → `Tide & Table ▸ Generate Fish Content`.**

It creates everything under `Assets/_FishGame/` (the burger content is left
untouched) and is **idempotent** — edit the data tables at the bottom of
`FishContentGenerator.cs` and re-run any time.

Produces (under `Assets/_FishGame/`):
- `Materials/Seafood_Cooked.mat` — warm tint that makes a mesh read as "grilled/cooked" (the pack ships one shared material, so cooked = tint)
- `KitchenObjectSO/` + `Prefabs/` — an SO + item prefab per stage **and** per cooked variant
- `CuttingRecipeSO/` — prep chains (fillet / peel / shuck / ring / chop)
- `FryingRecipeSO/` — stove recipes (Fillet → Grilled, Rings → Calamari, …)
- `MenuRecipeSO/` + `PlatedVisuals/` — dishes and their plated presentation prefabs
- `_FishMenu.asset` — the `MenuSO` listing every dish

After running, **check the Console** for the summary line and any "missing mesh" warnings.

### 4.1 The prep → cook flow (the core idea)

The **fillet / meat stage is the branch point**. After you process a fish to its
fillet (or shuck a mollusk to its meat), you choose:
- **Cut further** at the board → a **raw** dish (sashimi), or
- **Take it to the stove** → a **cooked** dish (grilled / seared / fried / steamed).

So prep *and* cooking are both core. Cooking is **calm**: gentle fry times, no burn
alarms, and overcooking does nothing harmful. *(Future stations like
dressing/seasoning slot in the same way — a counter that swaps item A → B via a
recipe SO.)*

### 4.2 Species in the menu (full pack)

The exact tables live at the bottom of `FishContentGenerator.cs` (edit + re-run to
extend). Current coverage:

| Group | Species | Prep | Cook |
|---|---|---|---|
| Fish | Salmon, Tuna, Sea Bass | Whole→Half→Fillet→Sashimi | Fillet → grilled/seared |
| Fish | Sardine, Anchovy | Whole→Half→Fillet | Sardine fillet → grilled |
| Shellfish | Shrimp | Whole→Peeled | → grilled |
| Shellfish | Lobster ×2 (tail + claws) | Whole→Split→Tail/Claw | → cooked |
| Shellfish | Crab | Whole→Meat (+shell) | → steamed |
| Mollusks | Mussel, Oyster, Scallop | shuck: Closed→Open→Meat (+shell) | Mussel/Scallop → cooked; Oyster raw |
| Cephalopods | Squid, Octopus | Squid→Rings, Octopus→Slice | → fried/grilled |

Filleting/shucking drops a **scrap** (skeleton / crab/mussel/oyster/scallop shell) →
all flagged `petFood` for the cat station.

### 4.3 Dishes (~21)

Raw bar: Salmon/Tuna/Sea Bass Sashimi, Sashimi Trio, Fresh Oysters, Shrimp Cocktail,
Crab Plate. Cooked: Grilled Salmon, Seared Tuna, Grilled Sea Bass, Grilled Sardines,
Fried Calamari, Grilled Shrimp, Steamed Mussels, Seared Scallops, Grilled Octopus,
Lobster Tail, Lobster Claws, Steamed Crab. Combos: Seafood Platter, Grill Combo.

> Plate matching is **subset-based** (existing `MenuManager`), so adding the
> ingredients in any order resolves to the right dish and auto-upgrades to bigger
> platters as you add more.

### 4.4 Item icons (run after content)

**Menu bar → `Tide & Table ▸ Generate Item Icons`.**

Renders every item prefab under `_FishGame/KitchenObjectSO` to a transparent PNG
in `_FishGame/Icons/`, imports it as a Sprite, and assigns it to that item's
`KitchenObjectSO.icon`. The order tickets (`OrderUI`) read these icons, so this is
what makes each recipe readable at a glance.

- **Pipeline-correct:** uses URP's `SubmitRenderRequest` + a black/white two-pass
  to reconstruct true transparency.
- **Idempotent + restylable:** tweak the constants at the top of
  `SpriteIconGenerator.cs` (`IconSize`, `ViewAngle`, `Padding`) and re-run to
  restyle every icon at once.
- Uses **layer 31** for an isolated capture rig — if you actually use layer 31 for
  something, change `IsolationLayer`.

### 4.5 Cookbook / knowing how to make each dish

The generator also writes `_FishGame/_RecipeDatabase.asset` (every cutting + frying
recipe + dish). From it you can see the exact procedure for any dish:

- **Whole cookbook, instantly:** menu `Tide & Table ▸ Print Cookbook` dumps every
  dish's steps to the Console — no Play mode. Great for building a real cookbook UI.
- **Per-order, while playing:** add an `OrderProcedureLogger` to a manager object and
  assign the RecipeDatabase; each incoming order prints its steps.

Example output for *Grilled Salmon*:

```
📖 Grilled Salmon
   1. Take Salmon from the container
   2. Cut Salmon → Salmon Half at the board (×3)
   3. Fillet Salmon Half → Salmon Fillet at the board (×3)  (leaves Fish Skeleton for the cat)
   4. Cook Salmon Fillet → Grilled Salmon on the stove (~6s)
   5. Plate everything and deliver it at the serving counter
```

---

## 5. 🔧 In-scene wiring guide

Do these in the `GameScene`. Checkboxes track progress.

### Step 0 — Generate content
- [ ] Run `Tide & Table ▸ Generate Fish Content`; confirm `Assets/_FishGame/` is populated and the Console has no errors.
- [ ] (If fish are pink) import the seafood **URP** unitypackage (see §2).

### Step 1 — Menu
- [ ] Select `DeliveryManager` → set **Menu** = `_FishGame/_FishMenu.asset`.
- [ ] Select `MenuManager` → set **Menu** = `_FishGame/_FishMenu.asset`.

### Step 2 — Cutting station
- [ ] On a `CuttingCounter`, set **Cutting Recipes** = all assets in `_FishGame/CuttingRecipeSO`.
- [ ] Add a small `ClearCounter` next to it as the **scrap tray**; assign it to the cutting counter's **Byproduct Output**.
- [ ] (Optional) Create an empty above the board → assign to **Focus Point** (where the prep camera looks).
- [ ] On the `CuttingCounter_Visual`, assign **itemAnchor** (the counter's item spawn point); (Later) optionally hook up **sliceParticles** / **perfectSparkle** particle systems.
- [ ] (Later) (Optional) Add a `PerfectSliceIndicator` (world-space rig above the board): assign `cuttingCounter`, `root`, `marker`, `sweetZone`.

### Step 2.5 — Stove (cooking)
- [ ] Use a `StoveCounter` (the burger game already has a `StoveCounter` prefab — reuse it, re-skin with a KayKit `stove`/`pan`). Set its **Frying Recipes** = all assets in `_FishGame/FryingRecipeSO`.
- [ ] That's it for logic: carry a fillet/meat onto the stove → it cooks (sizzle while frying), then sits ready. No burning in calm mode.

### Step 3 — Fish source(s)
- [ ] Add a `ContainerCounter` per whole fish you want available; set **kitchenObject** = `Salmon_Whole` (and `Tuna_Whole`, `SeaBass_Whole`, `Sardine_Whole`, `Squid_Whole`, `Octopus_Whole`, `Shrimp_Whole`, `Lobster_Whole`, `Crab_Whole`, `Mussel_Closed`, `Oyster_Closed`, `Scallop_Closed`, …). Dress each with a KayKit crate / ice display.

### Step 4 — Cat station (Later)
- [ ] Drop `Quirky Series ▸ … ▸ Prefabs ▸ Cat.prefab` into a cosy corner.
- [ ] Add a `PetController` to the cat; assign its **Animator** (defaults match `AC_Cat`).
- [ ] Create a `CatStationCounter` (a bowl visual + a collider on the **player interact layer** + a `spawnPoint`); assign the **Pet** = the cat's `PetController`.

### Step 5 — Camera
- [ ] Add a **CinemachineBrain** to the Main Camera (if not present).
- [ ] Create a **gameplay vcam** (Priority 10) at your normal kitchen angle.
- [ ] Create a **Prep vcam**, set **Body = Do Nothing** and **Aim = Do Nothing**, then **position/rotate it by hand** in the Scene view for the over-the-shoulder board shot. Priority 0. *(Do Nothing = no procedural movement, so the angle stays exactly where you put it. Tip: frame it in the Game/Scene view, then `GameObject ▸ Align With View`.)*
- [ ] Add an empty `PrepCameraDirector`; assign **prepCamera** = the Prep vcam. The director only blends it in/out — it never moves it.
- [ ] (Optional, multiple boards) put an empty child on each `CuttingCounter` where you want that board's camera, and assign it to the counter's **Camera Pose**; the director snaps the prep vcam to it per station.
- [ ] (Later) (Optional, for the dreamy blur) add a **global Volume** with *Depth of Field*, weight 0 → assign to **prepVolume**.

> **Adjusting the angle:** just select the Prep vcam and move/rotate it (or move the per-station *Camera Pose* empty). Make sure its Body **and** Aim are both *Do Nothing* — if you see it swinging to weird angles, one of them is still set to a procedural mode (Transposer/Composer).

### Step 6 — Managers & polish
- [ ] Add a `ZenScoreManager` alongside the other managers (GameManager, DeliveryManager, …).
- [ ] (Optional) Add an `OrderProcedureLogger` and assign `_FishGame/_RecipeDatabase.asset` to log each order's steps. Or just use `Tide & Table ▸ Print Cookbook` (§4.5).
- [ ] (Later) drop SFX clips into the `SFXSO` asset (`slice`, `slicePerfect`, `peel`, `shuck`, `petEat`, `petPurr`) + an ambient track on `MusicManager`.
- [ ] Run `Tide & Table ▸ Generate Item Icons` to auto-fill `KitchenObjectSO.icon` so order tickets show art (§4.4).
- [ ] (Later) swap `PlayerVisual` for an ithappy chef-kid body; keep/assign the humanoid controller.

### Step 7 — Play test
- [ ] Grab a whole salmon → place on the board → alt-interact to fillet through the stages.
- [ ] Confirm: camera leans OTS, perfect-slice sparkles, skeleton lands on the tray, plating reveals, delivery matches an order, cat eats when fed.

---

## 6. ⚠️ Notes & assumptions

- **Cinemachine namespace:** code uses the 2.10.7 `Cinemachine` API (confirmed in `packages-lock.json`). If `PrepCameraDirector` ever errors on the namespace, it's an auto-reference hiccup — flag it.
- **Plated offsets & item scale** are first-pass guesses (real mesh sizes unknown). Expect to nudge `*_Plated` prefabs and item prefab scales in the editor.
- **Order icons:** run `Tide & Table ▸ Generate Item Icons` (§4.4) to auto-fill `KitchenObjectSO.icon` for every item; tickets show blank slots until you do.
- **Zen timer HUD:** the running timer freezes (doesn't end the game) in `zenMode`; we'll hide/replace it later.
- **SFX:** all new sound slots are empty and null-guarded — silent until you wire clips.
- **Re-modelling an item:** change the mesh **inside** the item prefab's visual child — do **not** point a `KitchenObjectSO.prefab` at a raw model FBX, or it loses its `KitchenObject`/`PlateKitchenObject` component and `Spawn` will NRE. (This bit the Plate: its SO pointed at KayKit `plate.prefab`.)

---

## 7. 🔜 Roadmap

- **Phase 2 — Variety:** ✅ mostly done — full pack species, shucking, lobster, and **cooking** are in. Remaining: a dedicated **dressing/seasoning** station (sauces, garnish, lemon), distinct **peel/shuck** counter feel, more _2/_3 visual variants.
- **Phase 3 — Service & zen tuning:** customers at the `wall_orderwindow`, patient timers + soft tip falloff, satisfaction meter, plating polish.
- **Phase 4 — Content & dressing:** full menu, restaurant environment build-out (KayKit), day/session structure, soft scoring UI, save.

---

## 8. 📓 Changelog

### 2026-06-30 — Phase 1 systems + content pipeline
- Reimagined the design as a calm fish restaurant; locked the 5 design pillars (§1).
- Built the multi-stage cutting system, byproduct/skeleton spawning, perfect-slice sweet spot, OTS prep camera, cat station + pet controller, zen score, and SFX hooks (§3).
- Wrote the one-click `FishContentGenerator` and seeded **21 items / 13 cutting chains / 8 dishes** (§4).
- Confirmed DOTween modules + Cinemachine 2.10.7 resolve correctly; removed a build-breaking `using UnityEditor;` from `KitchenObject.cs`.
- Created this dev doc.

### 2026-07-04 — Restaurant builder v3 (root cause: "_decorated" pieces)
- **Root cause of the clutter found:** KayKit `wall_decorated` / `wall_orderwindow_decorated` / decorated tables are **single meshes with furniture baked in** (wall + stove + cabinet + hood + food). Lining walls with them duplicated a furnished unit per segment and buried the containers.
- v3: **plain `wall` pieces everywhere** (only functional variants: order window, curtain windows, doorway+door), plain fridge/tables (dressed by hand with bowls/plates), room enlarged to **28×20**, containers at z/x=±13 walls with 3-unit gaps, island at (±1.5, 3.5), divider bar `-10..+10` with 4-unit walk gaps at both ends. Rule added to builder header: never use `_decorated` for plain surfaces.

### 2026-07-03 — Restaurant builder v2 (cozy & walkable)
- v1 was cluttered: it reused the scene's old counters, which carried hand-placed decor children (stove/hood/cabinet units repeated at every slot), and packed too much wall dressing into 22×16.
- v2: **all pre-existing counters are parked** (disabled, under `== OLD COUNTERS (parked, disabled) ==` — delete when happy) and the 13 containers + 4 stations are **instantiated fresh from the `_New` prefabs**. Room grew to **24×18**; containers spread over **three walls at 3-unit spacing** (fish north, shellfish west, mollusks east); **cook island** (Cutting + Stove) in the open centre; divider = Trash | bar | Delivery | bar | Plates with walk gaps both ends; dressing reduced to single varied anchors (fridge NW, sink+dishrack NE, few bar props, dining set, one crate corner). Re-runs reuse builder-owned counters — no duplicates.

### 2026-07-02 — Restaurant scene builder
- Added `RestaurantSceneBuilder` (`Tide & Table ▸ Build Restaurant`, plus `Clear Restaurant Build`). One click builds the whole restaurant in the **Restaurant** scene from KayKit prefabs: checker kitchen + styleB dining floors, decorated perimeter walls (order window, curtained windows, doorway + door), north wall of **10 fish containers** + fridge + cabinets, west wall of **3 shellfish containers**, east wall sink (decor) → **Stove** + extractor hood → **Plates counter**, central prep island (table | **Cutting counter** | table), a **sushi-bar divider** with the **Delivery counter** in the middle, stools, dining tables, jars/plates/crates dressing.
- It **sweeps loose KayKit env pieces** (old hand-placed floors/walls/props) but never touches game counters/player/cameras/UI; **reuses existing counters** (renames + assigns the 13 container SOs via SerializedObject), instantiates missing ones, **parks duplicates** disabled at x=40, and adds **invisible perimeter/divider colliders** (empty GameObjects only — no components added to prefabs). Idempotent; measured-bounds placement; full Undo group.

### 2026-06-30 — Cookbook / order procedures
- Added a **recipe graph** (`RecipeDatabaseSO`, auto-filled by the generator) and `Cookbook` logic that traces any dish backwards into ordered steps (take → cut → cook → plate).
- `OrderProcedureLogger` logs each incoming order's steps while playing; `Tide & Table ▸ Print Cookbook` dumps every dish's steps in-editor. This is the data a cookbook UI will use next.

### 2026-06-30 — Full pack + cooking
- Expanded the generator to use the **whole seafood pack**: salmon, tuna, sea bass, sardine, anchovy, shrimp, lobster ×2, crab, mussel, oyster, scallop, squid, octopus (~60 items, ~30 cut recipes).
- Added **cooking**: a tinted `Seafood_Cooked` material, cooked item variants, and **FryingRecipeSO** generation for the stove. The fillet/meat stage is now the **branch point** (cut → raw / stove → cooked). Calm cooking (no burn alarms).
- Menu grew to **~21 dishes** (raw bar + grilled/seared/fried/steamed + combos). Plated visuals tint cooked ingredients.
- Wiring guide: added **Step 2.5 — Stove**.

### 2026-06-30 — Item icon generator
- Added `SpriteIconGenerator` (`Tide & Table ▸ Generate Item Icons`): renders each item prefab to a transparent sprite (URP `SubmitRenderRequest` + black/white alpha reconstruction) and assigns it to `KitchenObjectSO.icon`, so order tickets become readable. Idempotent + restylable via constants (§4.4).

### 2026-06-30 — Prep camera fix
- Reworked `PrepCameraDirector` to **stop driving the prep vcam procedurally** (the old Follow/LookAt override caused wild angles). The shot is now a **hand-composed static vcam** (Body/Aim = Do Nothing); the director only blends priority + DoF.
- Replaced `CuttingCounter.focusPoint` with an optional **`cameraPose`** transform so each board can have its own composed shot (the director snaps the vcam to it). Updated Step 5 wiring accordingly.

<!-- Add new dated entries above this line as we build. -->
