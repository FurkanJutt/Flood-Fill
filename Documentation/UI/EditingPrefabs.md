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

Auto Solver numbered level controls
----------------------------------

In **Tools > Flood Fill > Auto Solver**, Restart Level replays the active numbered level with its existing seed. Next Level uses the result-screen action to advance within the same mode and difficulty; it is disabled at level 100. Legacy campaign sessions retain their existing Next behavior. Both actions keep the gameplay HUD synchronized.

**Auto Next Level** advances after a win once playback finishes, with a one-second result delay. It stops at level 100. **Auto Start Solver** continues solving newly loaded boards only after you press **Start Auto Solve**. Checking it alone leaves the board untouched. The two checkboxes are independent and default off; enable both, then press Start Auto Solve for continuous solving. **Stop** ends the run without changing the checkboxes, so press Start Auto Solve again to resume automation. Closing the window, changing scenes, or leaving Play Mode also ends the run. Failed searches and manually stopped playback do not automatically retry on the same board. Automation stays idle while the board is hidden in menus. Changing boards clears the previous search and solution.

The isolated batch entry point `FloodFill.Development.AutoSolver.AutoSolverWorkflowVerification.BeginBatch` exercises deterministic restart, manual Next, independent checkbox behavior, actual solver wins on successive Items levels, saved completion, the level-100 boundary, and the hidden-menu guard. Its report is `/tmp/FloodAutoSolverWorkflowVerification.txt`.

Selecting the numbered level matching the current Continue save now opens `LevelSelectionPanel/RestartConfirmation`. Its message warns that continuing will restart the level. CONTINUE resets that same mode, difficulty and level; BACK dismisses the modal without changing the save or selection. Other categories and the Main Menu Continue action do not show this prompt. Dialog styling and button references are editable on the level-selection prefab. **Tools > Flood Fill > UI > Install Saved Level Restart Confirmation** installs it into an older prefab without rebuilding the selector. The isolated `BeginRestartConfirmationBatch` verifier checks save preservation, confirmation, normal resume, identity matching, and portrait layouts.

Solver summary history
----------------------

Open **Solver Summary** from the Auto Solver window (also available outside Play Mode), or **Tools > Flood Fill > Solver Summary**. The new Editor window groups rows into Easy, Medium, Hard, and Expert sections; Normal maps to Medium and Perfect to Expert. The default **All levels** table includes every recorded level, including failed, stopped, and in-progress runs. Each row shows the latest attempt’s status, moves, total budget, seed, capture percentage and timing, together with total attempts, losses, wins, and whether that level has ever been completed. Tries to win and best moves remain blank until a win. Rows update when a solver run starts, while moves are played, on failure, and when replayed. **Completed levels** is an optional filter; **Attempt history** retains each individual run. Search filters match mode, level, seed, kind or outcome. Level-row counts always include all attempts for that level, including attempts hidden by the filter.

Switch to **Attempt history** for retries, replays, stopped runs and unsuccessful searches. It includes outcome, search result, starting moves, solver-applied moves, total moves and the original total budget. A try begins when Auto Solve starts on a playable board, or when a calculated solution is actually played with Step. Analysis alone does not create a try. A board restart creates another try; stopping/resuming or closing/reopening the solver on the same board within the same Play session reuses that try. A new Play session starts a new run. Repeated search calculations on one board increment Searches rather than Attempts. Elapsed time includes pauses and time between resuming the same run; Search ms measures completed search calculations.

The history is saved automatically to `UserSettings/FloodFillSolverSummary.json`, independently of player saves and level progression. **Show Save File** reveals this project-local JSON. Writes use a temporary file and preserve the previous version as `.bak`. No attempt history is fabricated for levels completed before recording was added. If a summary cannot be read, its file is preserved and a visible error prevents overwriting it. Unfinished runs read after restarting the Editor are marked Interrupted.

The implementation is Editor-only: `AutoSolverSummaryWindow`, `AutoSolverSummaryStore`, and `AutoSolverSummaryRecorder`, with checks in `AutoSolverSummaryVerification` and the existing `AutoSolverWorkflowVerification`. To verify in an isolated `/tmp` project, run `AutoSolverSummaryVerification.VerifyStore`, which also launches real solver workflow checks; after it exits, run `AutoSolverSummaryVerification.VerifySeparateProcess` to check saved real wins in a fresh Unity process. Results are recorded in `SolverSummaryVerification.txt` alongside this guide.

Completion percentage and solver budget checks
---------------------------------------------

The HUD reserves 100% for a fully captured board. Results show exact captured/total voxel counts and a percentage capped below 100 while any voxel remains, together with moves used / total budget. This fixes a reported Full Grid / Expert level 20 loss: seed 1212414315 had 295 / 296 voxels captured (99.66216%) after 17 / 17 moves, which previously rounded to 100% on screen. The actual loss was valid.

The separate editor solver could find a valid 18-move plan for that 17-move level and play it anyway. Playback now rejects any plan exceeding the remaining budget before applying a move. On a fresh, unplayed board, automatic solving can instead reuse the engine solution that established the budget, after independently validating it against the current board graph. It never applies that initial plan to a partially played board. No difficulty formulas or win/loss rules change. The reported board was reproduced and the budget's 17-move solution was played through to a real win with all 296 voxels captured.

Summary search results now say Within budget or Over budget, while the attempt Outcome becomes Won only after the live game wins. Attempt history includes planned move count. Earlier searchResult values called Solved are displayed as Plan found (legacy); existing attempts are preserved. `AutoSolverCompletionVerification.BeginBatch` checks the exact reported board, over-budget blocking, truthful near-completion text and winning on the final allowed move. `BeginWindowBatch` checks automatic solution selection through the actual Auto Solver window. Results are in `CompletionVerification.txt`.

Auto Solver live statistics use the same strict completion rule: 295 / 296 displays 99.66%, and only exact full capture displays 100%. Solver moves played retains its count after playback stops (17 / 18 in the reported failure), while a newly analyzed plan starts at zero. A blocked-playback reason is also shown in live Solver Status. These display cases are covered by the completion verifier.

Both the level summary and attempt history include **Moves required**, the length of the latest recorded solver plan from its analysis position. This differs from actual Moves used and Given moves (the allowed budget), and does not claim a proven minimum. Entries without a recorded plan length show —. The value reuses the saved `plannedMoves` field.

After winning the final numbered level, the primary result action becomes LEVELS and the secondary LEVELS button is hidden. The Vertical Layout Group closes that slot, leaving LEVELS, REPLAY and MAIN MENU with normal spacing. Ordinary wins and losses restore the secondary LEVELS action. Result verification covers both level-100 outcomes and returning to an ordinary result screen.

Result buttons now have `FloodFillResultButtonSlot3D` layout rows that account for the visible body of each sprite, including nine-slice scaling and Canvas overrides. This prevents an invisible primary-button wrapper or glow padding from enlarging the gap above Replay. Hiding an action hides its entire row. Edit ResultActions / Vertical Layout Group Spacing to change the equal visible gaps. After changing a button sprite, run **Tools > Flood Fill > UI > Repair Result Button Spacing** in a graphics-enabled Editor to recalibrate padding without replacing existing fonts, graphics, or Canvas overrides. Result tests now use the actual scene and Canvas overrides and measure visible button bounds.
