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

Continue validation and restoration
-----------------------------------

Main Menu Continue is visible and interactable only for a saved, unfinished game with moves remaining. Empty, missing, malformed, completed, exhausted, or structurally inconsistent saves do not enable it. Availability is checked against the actual session file when the menu opens and again when Continue is pressed. Merely visiting Levels or returning from a screen without a game no longer creates a false Continue flag.

Continue restores the saved voxel board before opening gameplay, then uses the existing loading presentation and start animation. It restores the saved level identity, seed, colors, captured component, selected color, moves, total budget, score and camera state. A cleared in-memory board is rebuilt from the file; menu-preview parenting is restored before framing it. An unsuccessful restore leaves the menu visible with Continue unavailable. The loading presentation also checks for an actual nonempty board. Older Playing checkpoints retain their saved budget even when solver-profile metadata is absent. Terminal saved sessions are not offered as Continue games. Invalid files are not overwritten just to validate them.

The restart confirmation compares a selected tile to the validated saved identity, rather than assuming the current manager fields still describe that save. Runtime implementation changes are in `FloodFillGameManager3D`, `FloodFillMobileUI3D` and `VoxelBoardManager3D`. `FloodContinueVerification3D.BeginBatch` tests all 12 mode/difficulty combinations, exact restoration after clearing scene voxels, menu previews, stale flags and invalid files. `BeginColdBatch` verifies restoration in a separate Unity process, including old solver metadata and paused-state recovery. Both require an isolated project under `/tmp`. Results are in `ContinueVerification.txt`.

Gameplay elapsed time
---------------------

The center gameplay HUD card displays **TIME ELAPSED** in mm:ss, switching to hh:mm:ss after an hour. Capture progress remains on the fill bar. The clock counts real active gameplay time, excluding loading, start animations, pause screens, menus and completed games. New levels and restarts reset it. Continue restores the saved elapsed time; older checkpoints without this optional field start at zero. The serialized `captured` text reference is retained to preserve authored Canvas bindings.

`FloodElapsedTimeVerification3D.BeginBatch` verifies the authored HUD, formatting, progress bar, timer lifecycle, persistence, old checkpoints and real-time counting at 4x playback in an isolated `/tmp` project. Results are in `ElapsedTimeVerification.txt`.

Auto Solver elapsed time
------------------------

Auto Solver live statistics show **Elapsed time**, using the same active gameplay clock as the HUD. Summary level rows and attempt history save **Level time** with tenths of a second; in-progress attempts are checkpointed about once per second, as well as on moves, search changes, stop and completion. A win or loss freezes the recorded duration. Each restarted board records its own time. Level time includes any active play before the solver was started and follows the HUD pause/Continue behavior.

The existing attempt wall duration is retained under **Solver time**, counting time since starting Auto Solve including search and pauses. **Search ms** remains the computation duration. Older history keeps its existing solver duration and shows — for level time when it was never recorded. Existing difficulty sections and summary filters are preserved. `AutoSolverSummaryVerification.VerifyStore` and `AutoSolverWorkflowVerification` cover duration formatting, legacy rows, active attempts, real wins, next-level separation and disk persistence; `VerifySeparateProcess` checks timing after restarting Unity.

Procedural optional secondary goals
----------------------------------

Every newly prepared numbered level has up to three optional goals. Capturing the entire board within the move budget remains the only win condition. Goals do not change the budget, allowed colors, scoring, solver, camera, audio, animations or unlocking. Zero-goal wins remain valid. Existing efficiency stars on level tiles are preserved; secondary-goal stars are separate data. Earned secondary goals use a separate diamond row below completion stars; the result screen remains unchanged.

The runtime implementation is under `Assets/FloodFill/Scripts/ThreeD/Goals/`:

- `FloodSecondaryGoalModels3D`: versioned level and goal definitions, compact snapshots, solution traces and serializable attempt state.
- `FloodSecondaryGoalSettings3D`: Inspector tuning asset loaded from `Resources/FloodSecondaryGoals.asset`.
- `FloodSecondaryGoalGenerator3D`: witness analysis, candidate construction, feasibility validation and weighted selection.
- `FloodSecondaryGoalTracker3D`: per-selection/per-move tracking, deadlines, finalization and progress states.
- `FloodNumberedLevelPersistence3D`: additional immutable-definition methods on the existing `FloodFillSessionPersistence3D` service.
- `FloodFillGameManager3D.Goals`: loading, replay, Continue, generation and optional-reward integration in the existing manager.
- `FloodSecondaryGoalsUI3D` / `FloodGoalDismissRaycast3D`: floating HUD panel and tap dismissal.

Existing `FloodFillGameManager3D`, session DTO/persistence, `FloodLevelProgression3D` and the solver's `FloodMoveSimulator` receive small integration changes. `GameplayHUD.prefab` gains the authored Goals hierarchy; existing HUD transforms are preserved. `FloodSecondaryGoalsSetup3D` installs that hierarchy, and `FloodSecondaryGoalsVerification3D` exercises it and real gameplay.

Generation and joint validation

The existing move-budget solver still runs once during preparation. Its graph and winning region/color sequence are reused on the existing worker, with cancellation and its original bounded search/fallback. The trace uses `FloodMoveSimulator.Apply`, `CountComponent` and a membership-copy accessor; it does not implement another Flood Fill engine. New-cube counts compare the new selected component with the previous captured component, rather than subtracting aggregate sizes: this is essential because this game permits recoloring any connected region. The live board's `LastNewlyCapturedCount` is used during actual play.

All ten types are implemented: Move Saver, Final Color, Big Capture, Quick Start, Combo Capture, Speed Run, Limited Color Usage, Final Sprint, Fast Expansion and Final Color + Efficiency. Tight/large/massive/strict variants use the same types with difficulty tuning. A move-saving target is published only when a demonstrated sequence beats the normal budget. No move count is called optimal unless the original solver proved it. Limited Color Usage counts actual successful palette selections, including choices without applying a move; its witness uses color changes in the solution. Restrictions are considered only for colors common on the initial board with meaningfully limited demonstrated usage.

Candidates come from the actual trace, occupied count, region structure, budget and difficulty settings. The 12 mode/difficulty pools follow the requested eligibility lists. Weighted selection uses a local seeded random instance and avoids overlapping families (efficiency/final-color, expansion/sprint, and big/combo captures). Each selected goal is replayed through the same lightweight tracker using the same winning trace, ensuring joint feasibility. Speed Run is explicitly marked estimated timing: conservative human decision and camera allowances are not a proof of human performance.

Final Sprint starts after the move that first reaches the displayed percentage. That crossing move consumes zero sprint moves; the following move consumes one. Completion on the deadline counts. Quick Start/Fast Expansion use ceiling(occupied voxels × displayed percentage / 100), and the deadline is inclusive. Big Capture counts only newly captured cubes; Combo Capture counts distinct accepted moves. Duplicate move numbers are ignored.

Validation-move limits, candidate limits, existing solver search budgets and a candidate-generation time budget bound the work. If only a subset is valid, only that subset is saved (`requestedGoalCount` records three separately). Optional candidate-generation errors return zero optional goals rather than inventing goals. A numbered board without a validated within-budget solution is not published; the loading screen reports an inconclusive preparation and permits retry, without declaring it proven unsolvable. Existing custom/campaign fallback behavior is preserved.

Persistence and rewards

Immutable definitions are stored at `Application.persistentDataPath/numbered-levels-v1/<mode>_<difficulty>_<level>.json`. Mode is Full Grid=0, Random Shape=1, Items=2; difficulty is Easy=0, Medium=1, Hard=2, Expert=3. Each format-v1 definition includes its identity, actual seed, generator and goal-generator versions, procedural parameters, item identifier where applicable, palette, initial board, original move budget/profile, witness sequence, goal definitions/targets, reduced goal count and candidate-generation timing.

The compact snapshot stores four bytes per occupied voxel: three bytes of logical coordinate index and one byte containing its color and initial captured/starting flags, encoded as base64 in compact JSON. Dimensions/mode/seed/current color are explicit. All occupied coordinates, initial colors and starting flags are restored exactly; replay does not depend on future procedural-generator behavior. A SHA-256 board checksum and witness/goal validation detect damaged or inconsistent definitions. Initial publication writes a temporary file and moves it into place; existing definitions are never overwritten or silently rerolled. Unsupported versions or damaged files are preserved and reported. Future schema changes require an explicit migration; version 1 has no destructive migration of player data. A bounded eight-definition cache avoids repeated replay validation without retaining the entire catalog in memory.

Mutable completion, best moves and legacy stars remain in the existing PlayerPrefs level catalog. Additive fields save individual earned goal IDs and best completion time; `SecondaryStars(mode,difficulty,level)` exposes their count. Awards merge across successful replays and never remove an earlier achievement. A failed attempt cannot award a permanent goal star. `FloodLevelProgression3D.SecondaryGoalEarned(mode,difficulty,level,goalId)` fires for newly persisted awards; `SecondaryGoals.Changed` exposes temporary progress updates. Reward-save errors are isolated so optional rewards cannot interrupt the normal win/progression path.

The existing session checkpoint has an optional `secondaryGoalAttempt` containing selection counts, per-goal values/deadlines and the last accepted move. Continue restores this together with the saved active-play clock. Restart creates clean attempt state and retains permanent achievements. Old checkpoints remain loadable, but a partially played pre-feature checkpoint has no trustworthy historical goal events: its goals are available on a fresh replay, without fabricating progress or retroactive stars. Existing completed-level records and efficiency stars are preserved; there is no retroactive objective history.

Floating HUD and configuration

The authored hierarchy is `GameplayHUD / Content / GoalsUI` (the exact parent follows ProgressTrack), containing `DismissTap`, `GoalsTrigger / ChecklistIcon + Label`, and `FloatingGoalsPanel / Header + GoalsContainer / GoalRow1..3`. There is no new canvas, page, EventSystem or gameplay manager. The trigger sits immediately below the progress bar on the left; the panel floats below it in the marked reference region. The panel is hidden on entry, closes on trigger toggling or an outside tap, and does not pause or shift the board. Rows show unfilled diamonds for pending or missed goals, and filled diamonds for goals reached this attempt or previously earned. Reached-goal text matches the filled diamond’s cyan; missed-goal text remains red. Temporary goal completion still requires winning before its award is saved. The trigger count shows permanently earned goals, so temporary progress is not mistaken for saved stars.

The existing Input System/EventSystem handles mouse and touch. Outside drags remain available to orbit/pan; the panel closes without consuming the drag. Only the release frame of an outside tap enables a transparent dismissal raycast, preventing the same tap from selecting a color or pressing another HUD control. There is no permanent full-screen raycast blocker. The palette selector also guards the consumed frame.

Configure the following in `Assets/FloodFill/Resources/FloodSecondaryGoals.asset`:

- `pools`: mode × 4 + difficulty, in Full Grid/Random Shape/Items order. Each weight array follows `SecondaryGoalType`: Move Saver, Final Color, Big Capture, Quick Start, Combo Capture, Speed Run, Limited Color Usage, Final Sprint, Fast Expansion, Final Color + Efficiency. Zero disables a type; positive weights change its selection frequency. Saved definitions remain fixed after tuning changes.
- `difficulties`: capture fraction of witnessed performance, fraction of available move slack, early-move fraction, sprint milestone, human seconds per move. Expert uses the tightest demonstrated values.
- Minimum new capture, occupied-board capture fraction, minimum move savings and minimum combo moves reject trivial thresholds. All final targets must still pass the witness.
- Timing: minimum speed seconds, camera seconds per axis, region inspection seconds and geometry multipliers. Calibrate on actual mobile playtests. The existing manager's Dynamic Move Solver settings control search time/memory, independently of candidate-generation milliseconds, validation-move limit and candidate limit.
- `colorNames` and optional `descriptionFormats` override text. Description tokens are `{color}`, `{target}`, `{deadline}`, `{count}`, `{seconds}`. Formats follow the same goal-type order.

Edit GoalsTrigger and FloatingGoalsPanel RectTransforms in `GameplayHUD.prefab` to change position and dimensions. Edit ChecklistIcon TMP text/font to change its icon, row fonts/colors for presentation, and standard Image sprites and tint for the rounded navy/cyan styling. The trigger includes a larger invisible touch area around its compact visible body. Keep them inside the existing fitted safe-area content. The final victory-screen star presentation remains deferred. Use **Tools > Flood Fill > UI > Install Secondary Goals** only when installing a missing hierarchy; it preserves an already installed hierarchy.

Verification results and desktop timing/storage measurements are recorded in `SecondaryGoalsVerification.txt`. Tests use an isolated project and save identity and do not modify the original project's player saves, solver summary, scenes, level selection or result UI. Mobile script compilation checks, when recorded there, do not substitute for APK/IPA installation or human timing calibration on devices.



Level-tile goal diamonds
-----------------------

Yellow/gray completion stars now remain independent of optional-goal rewards. `FloodLevelButton3D.goalDiamonds` displays three separate slots beneath the stars, using `diamond_filled_512` for permanent secondary-goal achievements and `diamond_unfilled_512` for unearned slots. Locked levels hide diamonds and retain the lock icon; unlocked unplayed levels show three empty slots. Existing saved goal IDs are reused without migration. The red-star sprite reference and tint behavior have been removed from the tiles.

`LevelButton.prefab` uses number y=31, star row y=-10, and diamond row y=-43. Diamonds are 24×24 at x=-26, 0, +26; earned yellow stars retain 41.954×41.954 and gray stars retain 30×30. Grid and tile sizes remain unchanged. Edit the GoalDiamond1..3 RectTransforms to adjust the row. **Tools > Flood Fill > UI > Install Goal Diamonds** installs missing objects and references without duplicating them; the full level-selection installer also includes the row. `FloodGoalDiamondsVerification3D.BeginBatch` checks independent counts, saved rewards 0–3, empty/locked levels, tint reset, sizes, category reuse and repeated installation. Results and preview are in `GoalDiamondsVerification.txt` and `GoalDiamondsPreview.png`.

Unfilled goal diamonds use opacity 165/255; filled goal diamonds remain fully opaque. The runtime binding resets alpha for every slot when switching levels or categories.

Floating-panel goal diamonds replace the DONE/MISSED/EARNED status labels. `FloodSecondaryGoalsUI3D.rowDiamonds` references one 24×24 icon per row, with a left text margin to avoid overlap. Filled icons stay fully opaque; unfilled icons use alpha 165/255. Completed text uses #80F1F9, sampled from the light cyan face of `diamond_filled_512`; missed text retains the prior red. The Inspector exposes `completedGoalTextColor` for tuning. **Install Secondary Goals** also installs row icons idempotently. Verification covers all progress states, permanent awards, removed labels and fresh-attempt color/icon reset.

Home confirmation and pause Settings
------------------------------------

The gameplay Home button uses its own Home action, separate from Pause. Home opens `HomeConfirmation.prefab` with the question **Are you sure you want to quit to home?**, CONFIRM and BACK. The game and elapsed clock pause while confirmation is open. BACK resumes the same game; CONFIRM uses the existing ReturnToMenu save path, so the unfinished board, moves, elapsed time and goal progress remain available through Continue. MAIN MENU in Pause uses the same confirmation; backing out of that route returns to the paused menu.

Pause now contains RESUME, RESTART, SETTINGS and MAIN MENU. SETTINGS opens the existing settings screen without resuming gameplay. Its BACK action returns to Pause, with gameplay and the clock still paused; RESUME explicitly resumes. Settings opened from the main menu still returns to the main menu. Settings continue using the existing audio controls and saving behavior. Gameplay modals retain the gameplay audio context.

The enum actions Home, ConfirmHome, CancelHome and SettingsBack were appended to preserve existing serialized action values. The authored gameplay HUD Home binding, pause actions and settings Back binding are updated; `FloodFillMobileUI3D` stores the context for both return routes. The confirmation joins the existing safe-area screen hierarchy and Editor screen previews, with no extra Canvas or EventSystem.

**Tools > Flood Fill > UI > Install Home Confirmation and Pause Settings** updates the relevant controls and installs the confirmation prefab into `FloodFillCanvas`. It preserves existing controls and adds Settings only once. The standard UI authoring builder also assigns the new actions. Edit `HomeConfirmation.prefab` to change the message or confirmation button layout, and `PauseMenu.prefab` for the new Settings control. Verification uses real authored button callbacks, pause/clock/audio checks, and saved Continue restoration in an isolated project. Results and previews are in `PauseNavigationVerification.txt`, `HomeConfirmationPreview.png` and `PauseSettingsPreview.png`.

Reference-style victory popup
------------------------------

Winning uses the authored `VictoryPopup.prefab`, separate from the existing loss popup. Its 1000×1840 card sits inside a fitted 1080×2000 content frame, leaving visible margins around the larger modal on phone and tablet screens. The reference composition includes crown, rising laurels behind the LEVEL COMPLETE banner, level number, MOVES / BEST / TIME ELAPSED statistics, efficiency stars, reward cards and NEXT LEVEL / REPLAY / HOME / LEVELS actions.

`FloodVictoryPopup3D.Present` reads live moves, budget, numbered level, saved best move count, frozen active-play elapsed time and the current win's efficiency rating. The rewards are display-only constants **+100** coins and **+5** gems. This UI never credits a wallet or persists reward currency. Secondary-goal achievement rules and level progression remain unchanged. On level 100, NEXT LEVEL becomes LEVELS and the duplicate lower LEVELS action is hidden; HOME remains centered without an empty slot. Losses keep the existing OUT OF MOVES popup and controls.

The existing `level_complete_sprite_sheet` provides the banner, laurels, crown, stars, coins, gem, buttons, icons and colored confetti. The crown and star-row glows use ordinary Unity UI Images with the existing `cube_glow_512` sprite. The crown glow rotates slowly and gently pulses in scale and opacity on unscaled time while the popup is visible; closing restores its authored appearance. The procedural glow renderer has been removed. The decorative voxel cube and its rendering code have been removed. Text and button callbacks reuse the current UI font and `FloodFillUIAction3D` actions. The entire popup is an ordinary child of the existing safe area, with no extra Canvas or EventSystem.

Celebration starts once per popup opening. Laurel branches scale, rotate and rise outward from behind the banner; stars pop in sequentially with an overshoot; a preallocated pool of 60 confetti Images launches from the left and right and follows animated arcs with spin, paper-like scaling and fade-out. Tweens use unscaled time and are killed on closing/replay/navigation. Confetti objects are reused and do not intercept raycasts. Repeated statistics refreshes do not restart the animation.

Use **Tools > Flood Fill > UI > Install Victory Popup** to install a missing popup and Canvas reference. It preserves an already authored popup. In the prefab, edit Content/PopupCard for modal dimensions; Crown, Banner and the laurel RectTransforms for the hero composition; Statistics and reward cards for text/spacing; and button objects for action layout. `laurelDuration`, `starPopDuration` and `confettiDuration` on `FloodVictoryPopup3D` tune animation timing. Child confetti Image sizes tune visibility. The `heroGlow` reference binds the animated crown glow Image; its RectTransform controls position and base size. Fixed reward labels are intentionally overwritten with +100/+5 during presentation until reward logic is implemented.

`FloodVictoryPopupVerification3D.BeginBatch` uses an isolated project and save identity to play real solver-validated wins and a real move-budget loss, check live data/rewards, pause game time while the celebration runs, verify cleanup and refresh behavior, exercise Replay/Next/final-level navigation, and check phone/tablet popup margins. Results and previews are recorded in `VictoryPopupVerification.txt`, `VictoryPopupPreview.png`, `VictoryPopupCelebration.png` and `VictoryPopupTablet.png`. Original player saves, solver history and unrelated scene/Canvas edits are preserved.

Victory confetti now uses 60 preauthored particles (30 per side) and a 3.5-second base flight time. With staggered launches and per-particle timing variation, the full celebration lasts about five seconds. Laurel and star timings are unchanged. The victory installer upgrades the older 36-particle/2.5-second defaults while preserving custom flight times.

Standard UI panel backgrounds
------------------------------

The custom FloodGoalPanelGraphic3D runtime script and metadata have been removed. Six former custom graphics in GameplayHUD, HomeConfirmation and VictoryPopup now use ordinary sliced Unity UI Images with small pre-baked rounded-background sprites in `Assets/FloodFill/Art/UI/PanelBackgrounds`. Their original serialized component IDs, button target references, raycast settings and RectTransform geometry are retained. The Goals trigger sprite also preserves its transparent top/bottom padding, keeping the expanded touch area.

`FloodUIBackgroundSprites3D` is an Editor-only asset/style helper used by the UI installers; it generates standard 128×128 sliced sprites and adds no runtime renderer or behavior. Borders, fill colors, corner radii and padding are baked into those textures; Image tint stays white. Existing inspector-authored panels are recognized by sprite asset path, so installers remain idempotent. Runtime uses only Unity UI Images for these backgrounds.

Victory statistics count up from zero when the popup opens: moves used animate while the allowed-move budget stays fixed, and elapsed time uses the shared mm:ss / hh:mm:ss formatter. The default count-up lasts 1.2 seconds after a 0.2-second delay, with a small completion pulse. Both timings are Inspector-configurable on `FloodVictoryPopup3D`. The counters use unscaled time, land on exact frozen totals, survive statistics refreshes without resetting, and cancel/reset their text scales on navigation. Gameplay/save statistics are not modified by these display animations.

The two reward sparkles use independent random brightness fades and slow opposite spins (8–16 degrees per second). They run on unscaled time for the entire time the victory popup is open, including after the one-shot celebration ends. Closing the popup restores their authored colors and rotations. The `rewardSparkles` references bind the existing Images; the installer also wires older prefabs.
