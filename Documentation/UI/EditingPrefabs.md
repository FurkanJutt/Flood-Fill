The demo uses saved UI prefabs in `Assets/FloodFill/Prefabs/UI`. The scene contains a connected `FloodFillCanvas` instance; it no longer generates buttons, panels or labels when Play Mode starts.

To edit visually:

1. Open `Assets/FloodFill/Scenes/FloodFill3DDemo.unity` and select `FloodFillCanvas`.
2. On its `Flood Fill Mobile UI 3D` component, use **Preview MainMenu**, **Preview Settings**, or another preview button. These work outside Play Mode.
3. Expand `SafeArea / <screen> / Content` and edit the RectTransforms, Images and TMP text. Each screen is also a separate nested prefab that you can open directly from the Project window.
4. Save the screen prefab, or apply your scene instance overrides to the appropriate prefab. Changes made only during Play Mode remain temporary, as with any Unity object.

`FloodFillCanvas.prefab` holds the Canvas settings, safe area, development FPS overlay, and serialized controller references. The screen assets are `MainMenu`, `GameSetup`, `GameplayHUD`, `HowToPlay`, `Settings`, `PauseMenu`, `GameResults` `LoadingScreen` and `LevelSelectionPanel`.

The `Content` frame is authored at 1080×1920. `FloodFillUIFit3D` scales that frame to fit the available viewport without rewriting its children's positions or sizes. Keep editing the children of Content; disable this fitting component if you intentionally want your own screen scaling. Layout Groups still control their children according to their Inspector settings—for example, menu button spacing is set by the MenuButtons Vertical Layout Group. Disable a Layout Group before positioning those children freely.

Buttons have a `FloodFillUIAction3D` component and a saved On Click event pointing to its `Invoke` method. Its Action and Index identify the operation; the object name does not. A button can therefore be renamed or moved within the Canvas without losing its behavior. Keep that event when restyling a control. If you replace a live HUD element or choice button, update the corresponding serialized reference/list on the Canvas controller.

For dynamic choices and switches, edit the Normal/Selected sprites and colors on `FloodFillUIState3D`. Static controls use the Image component directly. `FloodUISkin.asset` is now an editor construction template; changing it does not restyle existing prefab instances automatically.

Live values such as moves, score, selected color, Continue availability, and saved audio settings still update from gameplay. Those state changes preserve authored layout. The menu's decorative 3D cube is still prepared at runtime independently of the UI, and the development FPS overlay is enabled only in Editor/development players.

The installer at **Tools > Flood Fill > UI > Install Editable UI in Demo Scene** reconnects the existing Canvas prefab if needed. It does not regenerate or overwrite existing prefab assets. Regenerating the 3D demo also reuses the authored UI assets.

Verification used an isolated Unity 6000.5.8f1 project copy: gameplay/audio Play Mode checks; nested prefab and saved-event checks; saving, reloading, renaming and resizing controls; edits to text and selection visuals; and a launch through the renamed Play button. All eight authored screens also rendered outside Play Mode at phone and tablet dimensions. Android and iOS managed player assemblies compiled; native device builds and device frame-rate measurements were not part of these checks.


Level selection configuration
-----------------------------

PLAY opens the authored `LevelSelectionPanel` nested in `FloodFillCanvas`; CUSTOM keeps the existing custom setup. The enabled build scene remains `Assets/FloodFill/Scenes/FloodFill3DDemo.unity`. Its existing Canvas prefab connection supplies the new screen without replacing the scene, game manager, audio manager, camera, or board. Use Preview LevelSelectionPanel on the Canvas controller to inspect it outside Play Mode. The new panel uses an 876×1796 Content frame inside the existing safe area and Canvas Scaler.

- Default mode and difficulty: edit `Assets/FloodFill/Resources/FloodLevelRules.asset`. Mode indices are Full Grid 0, Random Shape 1, Items 2. Difficulty indices are Easy 0, Medium 1, Hard 2, Expert 3. Defaults apply when the selector first opens; returning from numbered gameplay restores that game's category. Medium maps to Normal and Expert maps to Perfect without changing gameplay formulas.
- Grid spacing and tile dimensions: open `LevelSelectionPanel.prefab`, select `Content/LevelGrid`, and edit Grid Layout Group Spacing or Cell Size. Keep five columns and room for six rows. Edit `LevelButton.prefab` to change number, lock, and star positioning. The current-level background expands 15 units beyond the tile to give its glow room. Thirty nested tile instances are reused, with ten active on the last page.
- Progress color and stars: edit Progress Color, Three Star Budget Ratio, and Two Star Budget Ratio on `FloodLevelRules.asset`. A win earns three stars when moves / budget is at most the three-star ratio, two at most the two-star ratio, otherwise one. Defaults are 0.7 and 0.9 and are tuning values, not existing difficulty rules. Ratios are sorted when evaluated. Replays retain the maximum stars and minimum moves. Changing thresholds affects future wins.
- Visual assets: the panel reuses `Assets/Images/Spritesheets/levels_sprite_sheet.png` and the existing `FloodUISkin` primary/secondary and cube icons. Difficulty stars are editable white UI meshes in `FloodLevelStarGraphic3D`. Text stays in TMP components. The top-right pause decoration is disabled outside gameplay. Pagination has four dots for the four actual pages.

Numbered levels and saves
------------------------

`FloodLevelProgression3D` stores 1,200 independent entries in the additive `FloodFill3D.LevelCatalog.v1` PlayerPrefs JSON key. Each category has 100 levels, with only level 1 initially unlocked. Wins save completion, best moves and best stars, then unlock the next entry. The existing session JSON gains optional `numberedLevel` and `numberedMode` fields; old version 1 session files remain readable because absent fields default to zero. Existing campaign counters and custom setup preferences remain intact. Old campaign history does not record per-level difficulty/results, so it is not fabricated into completed category entries. Continue still restores legacy sessions and their campaign behavior.

`FloodFillGameManager3D.StartNumberedLevel` validates locks and identity before entering the existing loading pipeline. Stable integer seeds cover both mask generation and initial colors. Board sizes use the existing size options, advancing every 15 numbered levels and capping at size 20. Full Grid uses the existing hollow grid; Random Shape uses the existing procedural generator. Their algorithms, solver settings, move budgets and input remain intact. Replay keeps mode, difficulty, level and seed; NEXT increments within that category; at 100 it opens Levels. Result controls include separate LEVELS and REPLAY actions alongside the existing main menu action.

The original Fruits option had no generator. `FloodItemShape3D` adds connected voxel skins for apple, pear, orange and banana silhouettes, selected deterministically by level seed. Items uses the existing board, solver, difficulty, audio and flood animations. Its board mode is appended as enum value 3, preserving existing serialized values 0 and 2. The existing Custom Fruits placeholder remains unchanged; the new Items levels are accessed through PLAY.

Implementation files
--------------------

New runtime scripts are `FloodLevelSelection3D`, `FloodLevelButton3D`, `FloodLevelProgression3D`, `FloodLevelRules3D`, `FloodLevelStarGraphic3D`, and `FloodItemShape3D`. New editor scripts are `FloodLevelSelectionSetup3D` and `FloodLevelSelectionVerification3D`. Modified scripts are `FloodFillMobileUI3D`, its Authoring and Bindings partials, `FloodFillUIAction3D`, `FloodFillGameManager3D`, `FloodFillSessionPersistence3D`, and `VoxelBoardManager3D`. The existing `FloodFillUIPrefabVerification` editor suite is updated for catalog navigation.

New prefabs are `LevelSelectionPanel` and `LevelButton`; `FloodFillCanvas` stores the new panel reference and `GameResults` adds Levels and Replay. The rules asset is new. No scene file replacement or new scene is required. The installer **Tools > Flood Fill > UI > Install Level Selection** recreates the level selection layout, so save intentional prefab styling changes before using it again. It does not rebuild the other screens.

Testing locked and unlocked levels
----------------------------------

Use an isolated project copy for `FloodFill.EditorTools.FloodLevelSelectionVerification3D.BeginBatch` with Unity `-batchmode -executeMethod`. The verifier requires a project under `/tmp`, uses a separate company/product identity and resets only that test identity's saves. It checks all 12 catalogs, every Items silhouette across all 400 identities, 30/30/30/10 pagination, initial locks, duplicate completion, best result retention, category isolation, reload persistence, legacy schema compatibility, navigation, deterministic replay and real solver-driven gameplay wins in all 12 combinations. It also checks Next, level 100, and renders 876×1796, 750×1334 and 1170×2532 portraits. Outputs are `/tmp/FloodLevelVerification.txt` and `/tmp/LevelSelection-*.png`. On a fresh ordinary Editor save, test locked tiles by tapping level 2 before winning level 1, then verify it becomes playable after a win. Test the end of a category in the isolated verifier instead of modifying real player saves.

Native Android/iOS packages and device touch behavior require the corresponding installed Unity build modules and devices; desktop Play Mode and portrait renders do not establish native build success. Seed reproducibility assumes the same generation settings and algorithm version. The reference's sample progress and stars are not copied into player saves.

Verification results are recorded in `LevelSelectionVerification.txt`; `LevelSelectionPreview.png` shows the fresh-save screen. Persistence was also checked across two separate Unity processes with the verifier’s `SeedPersistenceBatch` and `VerifyRestartedCatalogBatch` methods.

Result actions use `GameResults/Content/ResultActions`, a Vertical Layout Group. Wins show Next Level, Replay, Levels, and Main Menu. Losses show Play Again, Levels, and Main Menu; hiding Replay lets the layout close the gap. Use **Tools > Flood Fill > UI > Repair Result Screen Layout** to repair an older result prefab without rebuilding level selection. `BeginResultBatch` checks both states, transitions back to a win, nonoverlap, card bounds, and portrait renders in the isolated test project.
