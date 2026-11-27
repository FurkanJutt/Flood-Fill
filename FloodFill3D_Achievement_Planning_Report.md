# Flood Fill 3D — Achievement Planning Report

**Inspection date:** 27 November 2026, Asia/Karachi. **Purpose:** supplement `FloodFill3D_Game_Functionality.mmd` with the current mechanics, evidence, statistics, and integration requirements needed to design achievements. Rewards are undecided.

**Scope:** read-only source, scene, resource, documentation, and local-save inspection; independent offline replay of saved winning witnesses. Unity was not launched. No gameplay code, assets, settings, or saves were changed. This report is the sole project deliverable. Existing uncommitted changes were inspected as part of the current project, not discarded.

## 1. Findings that must shape the achievement catalog

1. **There is no runtime achievement system or durable player attempt history.** Existing numbered-level completion, best stars, best moves, some best times, and permanent secondary-goal IDs support useful retroactive progress. Lifetime wins, failures, power-up use, streaks, and custom completions do not exist as player statistics.
2. **An active component and cumulative coverage are different measurements.** The HUD fill uses cubes ever included in the active component during this attempt. Victory requires the current connected component to contain every occupied cube. Coverage reaching 100% is not sufficient for victory.
3. **“Newly captured” does not mean first captured in this attempt.** It means membership newly added relative to the immediately preceding active component. Switching between separate regions can count the same cubes again. Achievement capture totals must not sum this field without defining and enforcing deduplication.
4. **Gold stars measure normal-move efficiency; cyan goal diamonds are separate.** Stars use moves/budget thresholds of 70% and 90%. Expert's budget equals a known solution length, which is not always an optimum. Three unassisted stars on every level are not guaranteed, and would be impossible where Expert's known length is proven optimal and positive.
5. **Color Swap and Color Blast cost zero normal moves under the active settings.** They can directly win and improve efficiency stars. Smart Hint costs stock when a usable recommendation is revealed; following it is an ordinary one-move flood. Assisted and unassisted records are currently mixed.
6. **Do not offer “recolor seven cubes with one Blast” for current normal boards.** The implementation supports center plus six neighbors, but all active generators create skins. A skin cube has at least one empty/out-of-bounds face, limiting the target set to at most six. Full Grid's one-cube-thick shell at the supported sizes has at most four occupied face neighbors, hence at most five targets. Actual color changes can be fewer still.
7. **A revealed hint is tracked; a followed hint is not.** There is no durable association between a recommendation and the next accepted move, whether followed manually or by optional automatic execution.
8. **Development data cannot prove human accomplishments.** The Editor Auto Solver executes normal gameplay, writes ordinary progress, and has no runtime eligibility/provenance marker. The inspected local catalog already coexists with extensive solver history. Retroactive import needs an explicit trust policy.
9. **Permanent goal diamonds can accumulate over different wins.** All diamonds in the catalog do not establish that every goal was satisfied during one attempt.
10. **The victory reward cards are presentation placeholders.** Their +100 and +5 labels are not a completed economy or achievement reward implementation. Do not derive currency balances, purchases, or claims from them.

### Status vocabulary

| Label | Meaning in this report |
|---|---|
| Active | Implemented and reachable in the inspected primary game flow/settings. |
| Inactive | Implemented option or legacy route, but not enabled or not the normal main Play route. |
| Development | Editor tooling, previews, verification, or development-build controls; not trustworthy player history. |
| Missing | No complete authoritative runtime/persistent measurement for the stated purpose. |
| Recommended | A future design proposal, not implemented by this audit. |
| Unverified | Requires runtime/device testing, more boards, or a mathematical/search proof beyond this inspection. |

Source aliases such as **G**, **B**, and **C** refer to the exact files and symbols in section 15. “Persisted in session” means only the current checkpoint, not a lifetime ledger.

## 2. Current game functionality and changes from the earlier document

### 2.1 Rules, level identity, modes, and sizes

The active build scene is `Assets/FloodFill/Scenes/FloodFill3DDemo.unity`; the old 2D demo is disabled in build settings. Unity version is `6000.5.8f1`. Normal play is a free-region flood: select one of six palette colors, then click a reachable cube to recolor its entire face-connected, same-color region. The selected region need not be the previously active region. Same-color/no-op clicks are rejected and do not consume a normal move. A valid recoloring costs one move even if it connects to no additional region. Diagonal contact does not connect cubes. [G, B, RG, SIM]

| Feature | Current active behavior | Achievement implications |
|---|---|---|
| Main Play | Numbered selection with 3 modes × 4 difficulties × 100 levels = **1,200 distinct catalog entries**. | Level 20 in different categories is a different level. Use the full identity. |
| Mode 0: Full Grid | A hollow cube shell, not a solid n³ board. | Occupied count is `n³−(n−2)³`, not the volume. |
| Mode 1: Random Shape | Seeded procedural solid generation followed by exterior-surface extraction and one connected playable skin. | Bounding dimensions alone do not establish occupied count or difficulty. |
| Mode 2: Items | Analytic Apple, Pear, Orange, or Banana skin; one connected component retained. Item is `(uint)seed % 4`. | Available in numbered play. Fruit label/selection in custom setup does not implement custom Items play. |
| Numbered difficulty 0/1/2/3 | Easy / Medium / Hard / Expert. | Underlying enum names are Easy / Normal / Hard / Perfect. Normalize labels, preserve integer identity. |
| Numbered size schedule | Levels 1–15: 6; 16–30: 8; 31–45: 10; 46–60: 12; 61–75: 15; 76–90: 18; 91–100: 20. All are equal width/height/depth. | Seven size classes; 15³ is supported despite not appearing in the sampled saved definitions. |
| Full Grid occupied cubes | 6→152, 8→296, 10→488, 12→728, 15→1,178, 18→1,736, 20→2,168. | Prefer actual occupied cubes for capture conditions. |
| Custom setup | Random Shape or Full Grid; sizes 6, 8, 10, 12, 15, 18, 20; all four enum difficulties. FRUITS shows COMING SOON and refuses Start. | No arbitrary rectangular dimensions or other integer sizes in the player setup. Custom goals/catalog records are absent. |
| Legacy campaign | Still implemented through `StartCampaignGame`; five levels per size, every fifth level Random Shape, otherwise Full Grid, random sizes after the structured 35 levels. Separate legacy PlayerPrefs progress. | Inactive as main Play, but reachable from a non-numbered victory's primary action. Do not merge campaign level numbers with the catalog. |
| Custom victory primary action | Label is PLAY AGAIN; `OnResultAction` still calls `StartCampaignGame` for a non-numbered win. Replay uses restart separately. | A label-only change did not make the primary action restart the same custom setup. Document route before designing repeat-custom challenges. |
| Numbered identity | `(mode, difficulty, level)`; index `(mode*4+difficulty)*100+level−1`; seed `unchecked(0x46463344 + index*104729)`. | Deterministic identity is stable; saved immutable definition/hash is the authority for the actual puzzle version. |
| Unlocks | Level 1, previously completed levels, or a level with its predecessor completed. Each category has its own progress. | Percentage = unique completions /100 for that category. Replays do not increase unique completions. |
| Last numbered level | Winning level 100 routes the primary action to selection, with duplicate Levels navigation suppressed in the result layout. | It is a category boundary, not the end of all 1,200 levels. |

The six active palette colors, in index order, are green `(0.25,.78,.38)`, purple `(.62,.32,.86)`, yellow `(.98,.82,.20)`, orange `(1,.52,.16)`, blue `(.20,.48,.95)`, and cyan `(.30,.82,.80)`. Definitions store their palette. Achievement IDs should use palette indices plus a palette/version identity, not rounded display hex strings. [Scene, G, DEF]

### 2.2 Move budgets and solver semantics

Let **S** be the validated winning witness length and **R** the initial region count. Scene settings apply a complexity multiplier **K**: 1 below 80 regions; 1.1 at 80; 1.25 at 150; 1.4 at 250; 1.6 at 400. Extras are computed by rounding away from zero: Hard starts at `max(1,round(.08*S))`, Normal at `max(3,round(.23*S))`, Easy at `max(5,round(.38*S))`; each extra is scaled by K and rounded again. Expert/Perfect = S; other budgets add their extras and enforce at least one move between adjacent difficulty budgets, subject to the calculator's maximum handling. The configured maximum is 1,000,000. [BUD]

`bestSolutionMoves` is a **best-known validated solution**, unless `isProvenOptimal` explicitly says otherwise. The runtime solver uses a free-flood portfolio, memoized lookahead, diverse/anchored beam search, and bounded exact search. Time/state/memory limits can stop proof. Current scene defaults include 128-region exact threshold, beam width 128, six greedy runs, runtime exact budget 40 ms, 100,000 exact states, and 32 MB search memory; development search budgets are larger. These settings do not prove every published witness optimal. A fresh Editor solve may return a different length from a saved level's original witness. [Scene, SOL]

New numbered definitions require a validated, within-budget winning witness and preserve the initial puzzle, palette, budget profile, witness, and generated goals. A later search finding an over-budget route does not imply that the original saved puzzle had no valid route. Never compare a remaining-board continuation directly with the original full-board witness without accounting for moves already spent. [GG, NP]

### 2.3 Score, time, victory, and failure

For a scoring action with positive credited capture **N**, `AwardScore` draws an integer **B in [21,49]**, then adds `B * multiplier`:

| Credited N | 1–3 | 4–6 | 7 | 8 | 9 | 10+ |
|---|---:|---:|---:|---:|---:|---:|
| Multiplier | 1 | 3 | 4 | 5 | 6 | 7 |

There is no ×2 band and no points for N≤0. Maximum award per scoring call is 343. The multiplier is a function of that action's credited capture, not an accumulated combo. Points are not proportional to all cubes in a large capture. Before direct power-up use, normal scoring uses the active-component difference. After the first Swap/Blast, a persisted per-attempt index ledger restricts scoring to previously uncredited component members. Active settings award no direct power-up capture points. A paid hint alone does not activate that scoring guard. [G `AwardScore`, GP `PowerUpScoreCount`]

`ElapsedSeconds` uses unscaled frame delta while active gameplay is eligible and timeScale >0. It begins after preparation, excludes loading/start animation, pause/home-confirmation/settings reached from pause, suspension signaled by `OnApplicationPause`, menu time, and result presentation. Hint targeting/search/focus generally remain playing time; committed power-up animation time is explicitly included. Normal visual waves are not a separate wall-clock exclusion. Continue restores elapsed time; restart resets it. On focus loss the game saves, but focus loss alone does not set the suspension flag—desktop unfocused timing depends on Unity/player behavior. No attempt start/end wall-clock timestamps or lifetime time counter exist. [G `Update`, `PresentPreparedLevel`, lifecycle methods]

The normal move path checks full capture **before** checking exhaustion. Winning on the last allowed move is valid. If the active component is incomplete and the allowance is exhausted, it loses even if cumulative coverage is 100% or an imprecise percentage looks full. `ScheduleResult` sets the state and records progression before the delayed result popup; the popup is presentation, not the authoritative completion. Direct power-up commits perform the same win-before-loss test. [G, GP]

### 2.4 What changed, and what did not

The earlier functionality document already describes numbered modes, solver-derived budgets, elapsed time, efficiency stars, generated goals, diamonds, enlarged results, animation, and placeholder rewards. They are not all new post-document features.

| Area | Relevant current difference or confirmation |
|---|---|
| Power-ups | Major addition: stock, targeting, direct single-/multi-cube edits, bounded hints/camera focus, per-attempt usage, save transactions, previews, and UI integration. |
| Capture display | GameManager `CapturedPercentage` now returns cumulative `CoveredPercentage`; board `CapturedPercentage` remains the active component. New per-cube `covered` flag survives Continue. Earlier description of the HUD as current component is superseded. |
| Score | Added first-direct-power-up guard/credited-index ledger and configurable direct-power-up scoring; original random bands remain. |
| Goals | Direct power-up edits have their own update route and selective compatibility. Existing goal generation/diamond union still applies; no new lifetime goal-type statistics. |
| Stars | Thresholds unchanged; zero-move direct edits now affect how easily high stars can be obtained. No saved assisted/unassisted split. |
| Persistence | Added inventory write-ahead checkpoint/revision recovery; session adds power-up attempt data and coverage. Legacy board-hash compatibility handles the added coverage field. |
| Continue/restart | Continue validates a real unfinished board; unreadable numbered definitions have a confirmed restart/archive route. Restart resets attempt counters, not spent stock or catalog progress. |
| Input integration | Selection-only saves are coalesced for 150 ms; Continue availability is cached with file path/time/length checks. Visual button feedback is separate from gameplay accounting. Palette/other power-up selection can interrupt committed power-up visuals without undoing the paid logical action. |
| Modes, palette, numbered count, size schedule | No relevant replacement found; retain the current values above. Custom FRUITS remains inactive. |
| Solver | Shared free-region solver powers hints; development Auto Solver/summary remain separate tooling. Hint-reveal attribution does not extend to followed-move attribution. |
| Achievement tracking/rewards | Still missing. No achievement UI, tier ledger, claim ledger, purchases, or reliable lifetime player totals were found. |

## 3. Complete implemented power-up behavior

### 3.1 Shared availability, stock, and transaction rules

Active resource `Resources/FloodPowerUps.asset` enables all three power-ups, inventory saving, custom-game use, and usage tracking. Each starts with **3** stock on a genuinely new inventory and has maximum **99**. Unlimited development stock, direct capture scoring, direct Big/Combo Capture goal progress, extended hint search, and automatic hint execution are **off**. Unlimited stock only applies in Editor/development builds when its setting is enabled. [PS]

Availability requires enabled system and kind, sufficient stock or allowed unlimited mode, a valid playing board/UI, no loading/pause/start animation, and sufficient remaining normal moves for any configured direct-edit move cost. New targeting requires settled board visuals; power-up/palette selection can finish a committed effect's visuals before accepting a new selection. Button highlight/animation completion is not a usage event. [PU]

Stock is global across attempts, modes, and difficulties, stored in `power-up-inventory-v1.json`. It is not reset by replay/restart/failure. Selecting, previewing, calculating, invalid targets, no-op recolors, and cancellation before commitment do not spend stock. Corrupt stock attempts backup recovery; if both copies are unusable it does not grant another starter allocation. Inventory quantities cannot be used to reconstruct lifetime uses because stock can be added, clamped, reset externally, or bypassed in development. [PI]

For a successful paid operation, the inventory transaction records decrement plus the complete after-action checkpoint and increments a revision. Inventory/journal persistence failure rolls back direct-edit board/moves/score/goal/usage/RNG state, or the hint usage increment. If inventory/journal succeeds but the ordinary session write fails, the durable pending checkpoint is recoverable before Continue; this is a committed use. Acknowledgement clears pending state after the session is safe. The inventory revision is a transaction ordering mechanism, not a player usage counter or globally unique attempt ID. [GP, PI]

### 3.2 Per-power-up comparison

| Property | Color Swap | Smart Hint | Color Blast |
|---|---|---|---|
| Actual operation | Recolor exactly one occupied cube to a chosen palette color. Does not recolor its entire old region. | Calculate/reuse a legal region/color recommendation; focus a selectable representative and highlight recommended color/merging region. | Recolor center plus its occupied six-axis immediate neighbors to one chosen palette color, simultaneously logically. Not a radius/diagonal explosion. |
| Player targeting | Choose block and color, in either order; commit when both are valid. | Automatic region recommendation from current snapshot; player normally follows manually. | Choose center and color, in either order. |
| Normal move cost now | 0; settings can assign a nonnegative cost. | Reveal costs 0; actually following consumes **one normal move**. The generic hint `moveCost` field is not a reveal/follow cost implementation. | 0; settings can assign a nonnegative cost. |
| When stock/use counts | Successful durable edit transaction; `used[0]++` if tracking on. | Successfully selectable reveal via `PayForHint` → `CommitHintUse`; `used[1]++` if tracking on. Not on starting calculation. | Successful durable edit transaction; `used[2]++` if tracking on. |
| No-op behavior | Same-color target rejected, no spend. | Invalid/stale/failed recommendation is not a successful paid reveal. Reopening a paid hint can be another use; no lifetime reveal dedupe. | Reject if none of the eligible cells changes color; otherwise one use even if only some targets change. |
| Can split a region? | Yes, removing/recoloring an articulation cube can separate the original color. | Reveal: no. Following uses normal whole-region flood, which does not split its old monochromatic component. | Yes; simultaneously editing a local cross can separate original color components. |
| Can merge regions? | Yes, the changed cube can join multiple separate same-target-color neighbors. | Following can merge adjacent target-color components through the recolored region. | Yes, the recolored cross is center-connected and can join multiple target-color components. Matching-color components elsewhere that do not touch it stay separate. |
| Directly changed maximum | 1. | 0 on reveal; following changes the selected whole region, potentially many cubes. | API target set ≤7; current skins ≤6, Full Grid ≤5; actual differing-color cells ≤target count. |
| Newly connected maximum | Can be much larger than one through joining preexisting regions; bounded by occupied board size. | Recommendation has expected component/new counts; following may connect a large region. | Can exceed number of edited cells through component joining; bounded by board size. |
| Can cause victory? | Yes, if recalc yields all occupied cubes in one component. | Reveal cannot; follow can through ordinary winning move. | Yes, same full-component condition. |
| Direct score now | None; sets guard and updates credited-cell ledger. Optional setting can award a standard random score on fresh credit. | None on reveal. Following uses ordinary score path. | Same as Swap. |
| Capture/coverage | Recalculates center component, `LastNewlyCapturedCount`, current color, covered set. Old component may shrink/become inactive. | Reveal leaves board/coverage unchanged. Follow does ordinary recalc and coverage. | Same recalculation after atomic logical cross-edit. |
| Goals now | `PowerUpChange` observes current component/moves for early expansion, sprint, move limits, and final state. Big/Combo do **not** gain direct progress with current settings. Color targeting may count palette selections. | Revealing does not meet capture goals; following uses normal goal Move/SelectColor logic. Time spent can affect SpeedRun. | Same as Swap. |
| Stars | Lower normal move count can improve stars; no explicit penalty for using it. | Revealing does not change moves; following adds one. No explicit assistance penalty. | Same as Swap. |
| Visuals | 0.22 s configured transition/pop. | Worker search, ~0.6 s automatic focus, pulsing highlight and palette cue. | ~0.32 s configured; center 0.2 s, neighboring delay 0.07 s. |
| Current feedback/events | `Changed`, board `PowerUpBoardChanged`, inventory `Changed`; no typed successful activation payload. | Same general feedback; no dedicated paid-reveal or successful-follow event. | Same as Swap. |

The Blast implementation's direct target neighbors are all face adjacent to the center, so after recoloring all eligible targets to the same color they form one center-connected cross. Count the actual recomputed component, not all remotely matching cubes. A future metric must snapshot before/after rather than assume a fixed gain. [BP]

### 3.3 Hint accuracy, reveal versus following, and cancellation

The hint service uses an immutable snapshot of the **current** board, not just the original level seed. It can reuse a cached validated continuation after checking state compatibility, otherwise runs bounded free-flood search. Settings target 250 ms, maximum 1,000 ms, 30,000 search states, 16 MB; extended search is off. A recommendation can be a validated winning continuation or a legal progress fallback. A fallback is not a guaranteed finish within remaining moves. The recommendation reports color, primary logical cells, merging cells, expected connected/new counts, solution length, and whether a winning continuation was verified. [HS, PU]

Camera focus searches exposed-face samples (up to 96), candidate views, occlusion, and a final selectable representative. An unusable/stale hint is not charged as a successfully usable reveal. Manual camera input can interrupt focus/automatic execution. A displayed usable paid hint stays paid even if ignored or canceled afterward. Changing the board revision invalidates current hint targeting. Optional automatic execution is implemented but disabled; it eventually calls `ExecuteHintMove`, which calls ordinary `SelectColor` and `HandleVoxelClicked` and has **no durable origin marker**. [HC, PU, GP]

**Detectability:** paid reveal count exists in the current attempt. Exact successful follow requires new tracking of recommendation ID, board revision/hash, region membership/representative and color, reveal/paid status, and the next committed move. Manual following of a different cube in the same recommended region must count as the same recommendation. Camera motion, selecting the recommended color, clicking an invalid cube, and a stale recommendation are not successful follows. Record follow once only after the accepted logical move; preserve attribution before the current hint is canceled by board-change callbacks.

Before commitment, Escape, selecting the same kind again, choosing another kind, UI disable/pause/navigation, or board change can cancel pending work/targeting without spending. After commitment, canceling/interrupting visuals finishes/restores the visual representation of the already edited board, without refund or a second use. A canceled paid reveal remains usage. Free development hint preview bypasses stock and usage; it is not reliable evidence of unassisted play. [PU]

### 3.4 Attempts, outcomes, and available measurements

| Situation | Actual persistence/reset behavior |
|---|---|
| Continue | Restores committed board, move count, score, elapsed time, covered flags, power-up used[3], score guard, credited indices, and compatible goal attempt. Pending visual targeting/search/recommendation is not restored. |
| Failure | Stock stays spent. Usage exists in the terminal/current checkpoint until replaced/deleted; no permanent failed-attempt usage total. |
| Win | Stock stays spent. Standard catalog records stars/best moves/goals/time; does not retain power-up usage or assisted flag. |
| Restart/replay | New attempt usage, coverage history, score guard/ledger, moves/score/time/goals reset. Spent global stock is not refunded. Numbered immutable board can be replayed; custom generation may produce a new puzzle. |
| Return home mid-level | Saves an unfinished resumable attempt; this is suspension/navigation, not a recorded abandonment. |
| Start another level | Replaces current attempt; no durable abandonment/restart reason ledger. |
| Unlimited/dev add/free preview | Development controls alter/bypass stock and can contaminate ordinary records; there is no sticky eligibility flag. |

`GetPowerTargets`, `ActiveCells`, `CapturedCells`, `LogicalIndex`, target coordinates, colors, and `LastNewlyCapturedCount` allow direct edits to be **measured at runtime**, but direct changed-cell count is not persisted as a statistic. Differentiate eligible target count from cells whose `ColorIndex` changed. Merged-region count requires labeling connected components on the pre-action board and mapping them to the resulting component; initial graph region IDs alone are wrong after power-up splits. Victory causation can be captured at the successful direct commit by comparing previous nonterminal state to `IsFullyCaptured`; no existing lifetime “power-up finishing blow” field exists. [BP, GP, RG, SIM]

## 4. Complete gameplay statistics inventory

**Reading the tables:** “Session” = only the current checkpoint; “Catalog” = best/unique numbered progress; “Definition” = immutable starting level metadata; “None” = not durably tracked. Derived means computable from the named data, not a stored player counter. Unless a row says otherwise, runtime attempt values reset in `RestartGame`; catalog values survive restart and improve/union on successful numbered results. Historical aggregate fields cannot be reconstructed just because an attempt value exists today.

### 4.1 Progression and outcomes

| Statistic | Exact owner/member and meaning | Tracking, persistence, scope | Update/reset; reconstruction and achievement gap |
|---|---|---|---|
| Total completed / unique completed numbered levels | C `Result.completed` across `Catalog.levels[1200]`. These are the same current statistic, not total wins. | Derived; Catalog; global unique level set. | `Record` on win; never increments for replay. Fully reconstructable from valid catalog, provenance unknown. |
| Completed per mode | C indices for one mode, four difficulties. | Derived Catalog; max400/mode. | Sum completed flags; retroactive. |
| Completed per difficulty | C one difficulty across three modes. | Derived Catalog; max300/difficulty. | Same; retroactive. |
| Completed per mode+difficulty | C `Completed(mode,difficulty)`. | Implemented derived Catalog; max100/category. | Same; retroactive. |
| Category completion percentage | C completed/100; selection progress bar. | Derived Catalog; 12 categories. | Reconstructable; “all categories” requires checking all12, not only current selection. |
| Total wins | G `ScheduleResult(Won)` occurs; catalog overwrites best. | Missing lifetime counter; terminal session may show latest outcome. | Replays are lost historically; need unique terminal attempt ledger. Do not sum completed flags as all wins. |
| Failures | G `State=Lost`. | Current terminal state only, Session; no runtime lifetime history. | Lost checkpoint deleted/replaced; new outcome tracking required. Editor losses are not all player losses. |
| Attempts | No durable runtime attempt ID or started-attempt counter. | Missing. | Need define start after successful playable preparation, not button click or Continue. |
| Replays / restarts | G `RestartCurrentGame`, `RestartGame`, numbered start; audio restart request. | Actions exist; no player counts. | `RestartGame` also creates first/new levels, so cannot count all calls as restarts. Track reason and prior-attempt identity. |
| Abandoned attempts | G `ReturnToMenu` saves unfinished play; new level may overwrite it. | Missing outcome/statistic. | Home/Continue is not abandonment. Define replacement/discard separately and persist once. |
| Custom completions | G non-numbered Won; no C row. | Current outcome only; None globally. | Need custom puzzle/attempt identity and separate counters. |
| Legacy campaign progress | G `CampaignLevel`, `activeCampaignLevel`, campaign PlayerPrefs keys. | Active legacy route; persisted next level, not result history. | Can suggest progression, cannot prove all historical outcomes/times/assistance. Keep separate from numbered catalog. |

### 4.2 Board information

| Statistic | Owner/member and exact meaning | Tracking/persistence/scope | Lifecycle, reconstruction, gap |
|---|---|---|---|
| Dimensions | B `Width`, `Height`, `Depth`; session `board.width/height/depth`; DEF `snapshot`. | Active, Session + Definition; attempt/level. | Set generation/restore; fully reconstructable where board exists. Not historical for all completed custom games. |
| Actual occupied cubes | B `TotalVoxelCount`, `ActiveCells.Count`; DEF compact payload count. | Active, Session/Definition derivable; level. | Do not multiply dimensions for skins. Catalog alone does not store it. |
| Board shape/mode | G `SelectedBoardMode`, `NumberedMode`; saved `volumeMode`. | Active, Session/Definition; mode/category. | Catalog mode index0 Full Grid differs from custom setup index0 Random Shape. Normalize using mode identity. |
| Item/fruit | DEF `itemIdentifier`; ITEM seed mapping. | Definition for numbered Items; current board/session seed can infer with correct generator. | No general player fruit-completion counters. Custom fruit selection is inactive. |
| Seed/version/hash | B `LastGenerationSeed`; DEF `seed`, `id`, `boardHash`, generator versions, generationParameters. | Active Session/Definition. | Seed alone is not a complete puzzle identity; custom Full Grid may report seed0 while palette colors use Unity randomness. Preserve snapshot/hash. |
| Initial connected region count | DEF `regionCount`, SOL/RG `RegionCount`. | Active Definition; generated solver result; level. | Restore from definition or build graph from initial snapshot. Current altered board cannot reconstruct original regions reliably without definition. |
| Current region count | RG built from current B snapshot; SIM `FloodComponentAnalysis.Count` for solver state. | Computable; no maintained gameplay scalar/history. | Rebuild after accepted edit; power-up splits invalidate old graph. Need committed telemetry for achievement event. |
| Available colors | B `PaletteCount`, G `colors`, DEF `palette.Length`. | Active; definition palette persists, six now. | Available palette count differs from colors still present on board; latter needs scan. |
| Final color | G `CurrentPlayerColor`, B saved `currentPlayerColor`; on actual win uniform color. | Active, Session; no catalog final-color history. | Sample at terminal win; cannot reconstruct winning final color from stars/bestMoves. Need ledger for unique color finishes. |

### 4.3 Moves and solver metrics

| Statistic | Owner/member and exact meaning | Tracking/persistence/scope | Update/reset; reconstructability/gap |
|---|---|---|---|
| Normal moves used | G `MoveCount`; +1 accepted ordinary flood, +configured cost for direct powers (0 now). | Active Session; attempt; best saved separately. | Increment after accepted logical operation. Not number of button presses/palette selections. |
| Original allowance | G `CurrentMoveBudget`; session `currentMoveBudget`; DEF `moveBudget`. | Active Session/Definition; level. | Chosen difficulty profile at load; persists through Continue. Catalog bestMoves alone lacks budget. |
| Remaining moves | `max(0,CurrentMoveBudget−MoveCount)`. | Derived Session; attempt/move. | Available immediately; not historical minimum. |
| Original verified length | DEF `witnessColors.Length`, `witnessRegions`; profile `bestSolutionMoves`. | Active Definition/profile; level. | Definition is original reference. `LastSolverResult` can be cleared after edits/Continue; do not depend on it alone. |
| Best recorded winning moves | C `Result.bestMoves` when `completed`. | Active Catalog; unique numbered level. | Minimum across wins, includes assisted/dev runs. Check completed before treating default0 as a record. |
| Proven optimum | SOL `IsProvenOptimal` + validated `BestMoveCount`; DEF/profile `isProvenOptimal`. | Conditional, Definition/profile or transient solve. | Only assert optimal if flag + validated solution + matching puzzle/rules. Most sampled definitions unproven. |
| Productive moves | Could mean new component membership, region reduction, or unique coverage gain; these are different. | Missing counters/history. | Define “productive” explicitly, preferably normal action reduces component count or gains first-time coverage; track at commit. |
| Zero-capture moves | B `LastNewlyCapturedCount==0` for latest action only. | Transient; None aggregate. | A legal move can recolor without merge; current-component difference can be misleading for remote targeting. Need dedicated normal-action counts. |
| Different target colors used | Goal `Attempt.colorSelections` records selection changes, not committed targets. | Selection counts Session only for compatible numbered goals. | Cannot derive exact move-color diversity from this. Need a per-attempt committed-target bitset with separate normal/direct origins. |
| Consecutive productive moves | No dedicated field. | Missing. | Track on accepted normal moves with defined productivity and reset conditions; Continue must preserve streak state. |
| Largest capture in one move | B `LastNewlyCapturedCount` latest; goal BigCapture `progress[i].value` is max for assigned goal. | Partial Session; no universal max/history. | Goal type may not be assigned; direct captures excluded currently. Add universal normal/PU/unique-gain maxima as distinct metrics. |
| Regions merged in a move | Pre/post connected labels, RG/SIM facilities. | Computable but no runtime maintained count/event/history. | Count distinct pre-action components joined by the edited source, excluding source; not initial graph node count or directly edited cubes. |
| Capture percentage gained | `100*(coveredAfter−coveredBefore)/TotalVoxelCount` for first-time coverage; active size delta is a different, possibly negative measure. | Inputs available; delta/history missing. | Snapshot before logical commit; not `LastNewlyCapturedCount/total` unless explicitly using previous-component membership semantics. |

### 4.4 Capture metrics and their hazards

| Statistic | Owner/member and exact meaning | Tracking/persistence/scope | Update/reset; reconstruction and missing tracking |
|---|---|---|---|
| Current active component | B `CapturedVoxelCount`, `CapturedCells`, `CapturedPercentage`. | Active; captured flags in Session; current action/attempt. | Rebuilt around clicked/edited root. Can decrease when switching elsewhere. |
| Largest component anywhere now | Rebuild RG/current component analysis, scan weights. | Computable, not B's active-size scalar or maintained lifetime max. | A larger disconnected component can exist away from active root. Need scan or updated labels. |
| Largest active component reached | Could be observed from B; some goal progress stores selected maxima. | No universal persisted maximum. | Current board alone does not recover earlier peak after splits/switches. |
| Newly captured | B `LastNewlyCapturedCount`: count of members in the current active set but not the preceding active set. | Active last-action runtime; not general session scalar/history. | Recomputed ordinary/direct action. Not merge count, net growth, or unique lifetime capture. |
| Capture events | No authoritative capture-event count. | Missing. | Audio waves/animation callbacks are not a deduplicated action ledger. |
| Sum of captures across actions | No stored sum. | Missing. | Summing the latest-new field would count revisits; do not call it unique cubes. |
| Unique covered cubes this attempt | B `coveredVoxels`, `CoveredVoxelCount`, `CoveredPercentage`; saved voxel `covered`. | Active, Session; attempt unique set, includes initial active component. | Union on recalc; reset/new board, restore via Continue. This is current HUD fill. |
| Previously covered cubes active again | Intersect before-covered with new active; exclude preceding active as appropriate. | Inputs available before action; no revisit count/history. | Snapshot first. Current saved covered/captured flags do not reveal how often revisited. |
| Normal-only capture gains | Ordinary `RecolorConnectedRegion`, G `HandleVoxelClicked`. | Latest common counters only; attribution history missing. | Direct and ordinary actions share capture fields. Need origin-specific counts and first-time index sets. |
| Power-up-only capture gains | BP recalc + GP commit; fresh score ledger is not a capture origin ledger. | Latest common counters; no dedicated persisted totals. | Snapshot directly edited IDs and first-time covered IDs. Do not infer from stock decrease. |

**Concrete switching example:** component A contains10 cubes, B contains5, and they remain separate after the chosen action. Switching activity to B can report5 newly captured even if B was active earlier; current active size becomes5. Covered union may stay15 rather than increase. Returning to A can report10 new relative to B while covered remains15. Both components being covered does not make the board uniform. Likewise, `|afterActive−beforeActive|` differs from `afterSize−beforeSize` and from `|afterActive−beforeCovered|`.

**Exploits to exclude:** farmable revisits, splitting/reconnecting with direct edits, counting initial covered cubes as newly earned action progress, counting all eligible Blast targets as recolored, counting every region graph node as a distinct current merge, and awarding victory on a rounded/HUD coverage percentage. The score guard reduces repeated score credit after direct power use, but does not redefine all goal/capture fields or provide a universal anti-farming achievement counter. Initial component coverage should be subtracted if a challenge specifically requires cubes newly reached by player actions.

### 4.5 Scores and timing

| Statistic | Owner/member | Tracking/persistence/scope | Update/reset; reconstruction and gap |
|---|---|---|---|
| Current score | G `Score`; session `score`. | Active Session; attempt. | Increased by `AwardScore`; reset restart. Not reproducible exactly from witness because random base draws are not saved as history. |
| Final winning score | G `Score` when Won; latest terminal session temporarily retains. | No runtime catalog score/history. | Capture at terminal commit; solver summary has only development run score. |
| Best score | No C field. | Missing. | Need per-level/mode record with size/rules/assistance context. |
| Lifetime accumulated score | No field/ledger. | Missing. | Sum committed eligible attempts once; decide completed-only vs all attempts. Avoid incentivizing capture loops. |
| Score per move / action | Difference in G.Score; local random base/multiplier inside `AwardScore`. | Computable during commit; no payload/history. | Direct edit can cost0; don't divide score by zero or confuse action with normal move. |
| Maximum multiplier | Local multiplier is calculable from credited N; cap7. | No maintained max; None history. | Additional snapshot/payload needed for actual credited multiplier. |
| Consecutive max-multiplier moves | No field. | Missing. | Define accepted normal-move streak and reset on lower/no-score actions; cannot infer from final score. |
| Power-up score | Setting off; optional direct score uses fresh indices and ordinary formula. | No separate score subtotal. | Track source and points delta if enabled; hint-follow scoring is normal-move scoring. |
| Random score base | `UnityEngine.Random.Range(21,50)`. | Per-call random; no durable sequence/RNG state in normal save. | Replaying same seed does not guarantee same score. Rollback protects a failed direct transaction's RNG state only. |
| Active gameplay time | G `ElapsedSeconds`, formatted by `ElapsedTimeText`. | Active Session; attempt. | Update conditions in §2.3; resets restart, restores Continue. |
| Total attempt/wall duration | No runtime timestamps. | Missing; solver-only wall timer is separate. | Must define inclusion of offline/menu/pause intervals and store start/outcome timestamps if needed. |
| Best completion time | C `bestCompletionSeconds`, `hasCompletionTime`. | Partial Catalog; numbered level. | Updated by `RecordSecondaryGoals` on valid goal-tracker wins, including zero-goal tracker. Legacy/incompatible restored tracker can leave it absent. Check flag, not default0. |
| Lifetime active time | No accumulator. | Missing. | Increment with durable monotonic attempt checkpoints to avoid double counting elapsed on every save/Continue. |
| Pause count/duration | G `audioPaused`, `PauseGame/ResumeGame`; audio bool event. | Current state only; no totals. | No historical pause count; not inferable from best time. |
| Suspension/background duration | G `elapsedTimerSuspended`, lifecycle save. | Current flag only; no durable durations. | `OnApplicationFocus(false)` only saves; device suspension behavior untested here. |
| Continue time | Session `elapsedSeconds` restored; presentation timer reactivated after load. | Active. | Do not count restored elapsed as new lifetime time again. Crash between saves can lose unsaved elapsed time. |

### 4.6 Stars and secondary goals

| Statistic | Owner/member and meaning | Tracking/persistence/scope | Update/reset; reconstruction and gap |
|---|---|---|---|
| Current attempt gold stars | C `Stars(MoveCount,budget)`, meaningful final rating on win. | Derived; no dedicated stored attempt-rating field. | 3 if ratio≤.7; 2 if≤.9; else1. Loss should not award computed stars. |
| Best saved stars | C `Result.stars`. | Active Catalog; per numbered level. | Max across wins; includes assisted/dev records. |
| Total earned stars | Sum of best C.stars for completed entries. | Derived Catalog; max3,600 theoretical rating capacity. | Replays award improvements, not another copy of prior stars. Cumulative all-win star sum is missing. |
| Unique three-star levels | Count completed rows with stars==3. | Derived Catalog. | Retroactive, assistance/provenance unavailable. |
| Goals assigned/available | DEF `goals.Length`, `requestedGoalCount=3`. | Active Definition; level; 0–3 allowed. | Definitions generated lazily. Catalog alone cannot establish total offered goals across undiscovered levels. |
| Permanent goals earned | C `earnedGoalIds`; `GoalEarned`, `SecondaryStars`. | Active Catalog union; per level/category/global derived. | Goal IDs require level identity as namespace. Maximum3 per current definition, subject to version/legacy handling. |
| Newly earned on this win | C `RecordSecondaryGoals` local `newlyEarned`; `SecondaryGoalEarned(mode,difficulty,level,id)`. | Transient typed event after catalog save. | Event fires only for IDs not already present; no historical acquisition time/order. |
| Goals met/failed now | GT `Attempt.progress[i]` (`value`, `met`, `failed`, `sprintStart`), `colorSelections`, `lastMove`. | Active compatible numbered Session. | Reset restart; Continue validated; loss does not bank transient success. |
| Goals satisfied this winning attempt | GT `Complete` returns bool[] for ALL assigned goals, regardless prior permanent ownership. | Available at result; no winning-mask history. | Capture result immediately. Later permanent display cannot reconstruct same-attempt completion. |
| Goal types earned | Join catalog goal IDs to saved DEF.goals types. | Partially reconstructable where matching definitions exist. | Missing definitions/version changes prevent safe attribution. Aggregate type totals not maintained. |
| All available diamonds earned | Every current DEF.goal ID in C earned IDs. | Derivable from matching definition + catalog. | Require goals.Length>0; don't award vacuously on0. For “three diamonds” explicitly require3 assigned. |
| All goals in one attempt | GT.Complete all true on one actual win. | Transient, not catalog history. | New terminal result mask/ledger required; permanent union is insufficient. |

### 4.7 Power-up, streak, and engagement statistics

| Statistic | Owner/member and meaning | Tracking/persistence/scope | Update/reset; reconstruction and gap |
|---|---|---|---|
| Inventory per kind | PI `Quantity(kind)`, Store `quantities[3]`. | Active global stock file. | Successful spend/add/clamp; not reset each attempt. Quantities are not lifetime use. |
| Lifetime uses per kind | No runtime usage ledger. | Missing. | Inventory revision includes transactions, not reliable per-kind lifetime attribution. |
| Uses this attempt | G `PowerUpsUsed(kind)`, GP `powerUpAttempt.used[3]`. | Active Session if tracking enabled. | Committed Swap/Blast or paid hint reveal; reset restart; restore Continue. |
| Uses on successful / failed attempts | Current usage + terminal G.State can be sampled. | No durable outcome-separated totals. | Need ledger at authoritative outcome, once per attempt. |
| Directly recolored cubes | BP target list/old colors vs final colors. | Computable transient; no counter. | Exclude cells already target color; do not count later automatically joined cells as direct edits. |
| Regions merged by power | Pre/post labels and target/root. | Computable; no counter/event. | Rebuild after splits; count pre-components joined under an explicit definition. |
| Power-up-assisted victory | Any committed per-attempt usage with track enabled is evidence of assistance. | Derivable only for current attempt; not Catalog. | Paid reveal counts assistance even ignored. Legacy missing usage does not prove no assistance. Free dev previews require provenance. |
| Victory without powers | Current used==[0,0,0] is only conditionally valid. | Not permanently recorded. | Requires tracking enabled for whole attempt, no dev assists, valid complete history. Add sticky assistance/eligibility flags. |
| Smart Hints revealed | Current used[SmartHint] counts successfully paid usable reveals. | Session only, no lifetime. | Free preview excluded; each paid reveal is separate. |
| Hints successfully followed | No recommendation→accepted move attribution. | Missing. | New recommendation identity and successful-follow tracking; not selection/hint count. |
| Consecutive wins | No runtime outcome stream/streak. | Missing. | Need loss/restart/abandon policy and ordering across Continue. |
| Consecutive three-star wins | Current best stars can't reconstruct outcome sequence. | Missing. | Require terminal attempt ratings, not level best. |
| Consecutive unassisted wins | Neither durable order nor reliable historical assistance. | Missing. | Track going forward with eligibility and assistance snapshot. |
| Consecutive Expert wins | Category labels exist; ordered win history absent. | Missing. | Specify whether non-Expert outcomes break the streak or are filtered out. |
| Distinct active days | No runtime durable dates. | Missing. | Developer summary timestamps are not player engagement. Define valid activity, timezone/day boundary, dedupe. |
| Consecutive active days | No date ledger. | Missing. | Clock changes/offline behavior require a policy. |
| Consecutive completion days | No dated player wins. | Missing. | Require eligible successful completion/day; don't infer from session mtime or catalog. |

## 5. Secondary goals: exact constraints and coexistence

Goals are optional numbered-level challenges, permanently awarded **only on a win**. Each stored goal has `id,type,target,deadline,color,count,seconds,estimatedTiming`; progress stores current value, met/failed flags, and sprint start. Permanent ownership is visually separate and can override current-attempt presentation. [GM, GT, GEN, GS]

| Type (enum index) | Exact implemented completion test / updates | Power-up implications |
|---|---|---|
| MoveSaver (0) | Win in ≤target normal moves; mark failed after exceeding. | Zero-cost direct edits can help. |
| FinalColor (1) | Win with final uniform color==goal.color. | Direct finishing edit can satisfy. |
| BigCapture (2) | At least target newly-active cubes on one accepted normal move; stores max. | Direct edits excluded with current compatibility setting; following hint is normal. Metric can include revisits. |
| QuickStart (3) | Reach target **active component** count by deadline moves. | Direct change can satisfy at real MoveCount; not HUD coverage. |
| ComboCapture (4) | At least count qualifying captures of ≥target; not necessarily consecutive. | Direct progress excluded now; later normal reconnects still use common newly-active metric. |
| SpeedRun (5) | Win with ElapsedSeconds≤seconds. | Active targeting/search time can matter. Goal deadline is an estimate, not proven human completion time. |
| LimitedColorUsage (6) | Selecting designated color at most target times; counts changes of palette selection. | Power targeting can change selected color even when subsequently canceled. This is not “use at most N different colors.” |
| FinalSprint (7) | After first active-component threshold crossing, finish within deadline additional moves. | Zero-cost edit can cross threshold; moves remain real normal cost. |
| FastExpansion (8) | Same tracked early active-count/deadline pattern with a later generated window. | Direct edits observed, as QuickStart. |
| FinalColorEfficiency (9) | Win with designated final color and ≤target moves. | Both direct-edit final color and move savings can help. |

Normal `Move(number,...)` suppresses duplicate/non-increasing move numbers. Power updates use `PowerUpChange` because zero-cost edits must not invent moves. The compatibility flag controls Big/Combo direct capture progress specifically; it does **not** disable all indirect goal benefits. Color selection, final color, real move count, early active expansion, sprint, and elapsed time still matter. A missed early goal can be recovered by a direct edit at exactly the allowed deadline while its real move count remains within the test. A loss marks this attempt's goal results unearned even if transient progress had been met. [GT]

Generation simulates one within-budget winning witness and only accepts candidate goals demonstrated by it. Requested count3 is bounded by ≤30 candidates, ~30 ms generation, 2,048 trace moves, family exclusions, and valid conditions. Generator exceptions can yield zero goals without blocking a playable level. Mutually exclusive families keep only one from each of `{MoveSaver,FinalColor,FinalColorEfficiency}`, `{QuickStart,FastExpansion,FinalSprint}`, and `{BigCapture,ComboCapture}`. Speed and LimitedColorUsage are separate families. Thus returned structural goals have a common witness, but not every level is guaranteed three goals. [GEN, GG]

Current tuning uses capture fraction / move slack / early fraction / human seconds per move: Easy `.45/.65/.35/10`; Medium `.65/.5/.3/7`; Hard `.8/.2/.25/6`; Expert `.95/0/.2/5`. Minimum meaningful capture is max(2,ceil(.02*occupied)); minimum move saving1, minimum combo count2; sprint crossing70%. Speed estimate is `max(60, L*humanSecondsPerMove + cbrt(occupied)*8 + initialRegions*.12)`, scaled for Random Shape1.25/Items1.15 and rounded up to5 seconds. Demonstrating SpeedRun sets an estimated time in simulation; it does not prove a mobile player can perform the route in that time. [GS, GEN]

## 6. Existing event/callback inventory and authoritative hooks

There is no general typed gameplay event bus for achievements. Existing presentation/audio notifications are useful signals but are not durable, exactly-once semantic events. The table lists actual current surfaces and future hook recommendations without adding any events.

| Desired event | Existing surface / payload / trigger | Duplicate/ordering risks and future authoritative location |
|---|---|---|
| Level started | G `RestartGame`, loading pipeline, `PresentPreparedLevel`; audio `LevelReset()`/`LevelReset` no payload; G `UIChanged()` no payload. | Reset fires for first start/replay/reconfiguration, before success. Hook after playable preparation with identity+new attempt ID; not every reset/UI refresh/start animation. |
| Continue restored | G `ContinueSavedGame`, `TryRestoreSavedSessionCore`; no dedicated restored event. | Loading/UI refresh occurs repeatedly; restore is not a new attempt. Persist same attempt ID and emit one resume notification per successful restoration. |
| Normal move committed | G `HandleVoxelClicked` after B returns true; no typed move event. B `PowerUpBoardChanged()` increments revision on ordinary moves too. | Board notification precedes manager MoveCount/score/goals updates. Hook after all accepted move accounting, before/with outcome checkpoint. |
| Capture completed | Audio `FloodStarted(FloodWave)` from B normal recolor; payload AffectedCubes, NewlyCaptured, Duration, FirstWaveStart, LastWaveStart, WaveStep. | Audio wave describes logical/visual scheduling, not animation completion or unique capture credit. No direct-PU capture equivalent. Snapshot logical sets at accepted action, not per-cell tween callbacks. |
| Region merged | No dedicated event; graph/simulator can calculate. | Compute pre/post current component labels at action commit; one event per action, not each touching face. |
| Score awarded | G `AwardScore(int)` private method; no score event. | UIChanged may fire several times. Capture actual random base, credited capture, multiplier, delta at the scoring call, then commit with action. |
| Goal progress / provisional completion | GT `Changed` no payload on selection, move, power change, and Complete. | Not every Changed means new met goal; Complete can be invoked during recovery/evaluation. Use accepted action snapshot or final mask, not changed-event counting. |
| Power successfully activated | GP `CommitPowerUpRecolor` true after durable PI.Commit; PU `Changed` and PI `Changed`, both no payload. | Board change occurs before durable write and may roll back; inventory Changed also covers stock adds. Hook only successful committed operation with kind/action/revision/cell deltas. |
| Hint revealed | PU `PayForHint` → GP `CommitHintUse` success; no dedicated typed event. | Focusing/HintsDisplayed can precede payment/selectability; refresh repeats. Emit only once after paid successful reveal; free dev preview separately classified. |
| Hint followed | GP `ExecuteHintMove` exists; manual follow enters regular move path. | Method call can fail to produce a move and misses manual equivalence. Match recommendation to next accepted action before invalidation, once. |
| Level won/failed | G `ScheduleResult(state)`; audio `ResultScheduled(bool won)` via `FloodAudioSignals.Result`; then catalog/session and delayed RevealResult. | Bool has no attempt ID or stats. Audio fires before record; future recovery can restore records without this signal. Authoritative idempotent terminal transition must span normal and power journal recovery. |
| Restart | G `RestartCurrentGame` sends audio `RestartRequested()` before `RestartGame`. | Request has no board/outcome payload; `RestartGame` also handles initial setup. Hook successful replacement with reason and old/new attempt IDs. |
| Abandoned | No event. G ReturnToMenu, StartGame, selection navigation are relevant methods. | Returning home can preserve Continue; do not treat as abandonment. Define irrecoverable replacement/discard boundary. |
| New best moves | C `Record` updates min bestMoves; no event. | Repeated equal win is not improvement. Compare old/new before write in future transaction. |
| New best time | C `RecordSecondaryGoals` updates min time; no event. | Recovery/repeated write can repeat evaluation; compare flagged old/new, store attempt result. |
| New star record | C `Record` max stars; no event. | Compare old/new; award only delta/unique tier completion. |
| New permanent goal | C `SecondaryGoalEarned(int mode,int difficulty,int level,string id)` AFTER catalog save for each previously absent ID. | Existing set union suppresses ordinary duplicates, including recovery replay; notifications can be missed if process stops after save before event. A single win may emit several distinct goal events. |
| Category completed | No event; C `Completed()==100` can derive. | Check false→true transition/set once, not every selection refresh. |
| Game saved | SP `Save(data)` returns bool; no saved event; PI.Acknowledge may run. | Selection, camera/navigation/lifecycle and gameplay saves are frequent. A save is not progress. Future action ledger must not re-add totals on every save. |
| Game loaded | SP `TryLoad(out data)`, NP `TryLoadLevel`; no general loaded event. | Availability polls/cache and definition loads aren't successful Continue. Hook actual validated restore/presentation. |
| Pause/menu/popup | Audio `PauseChanged(bool)`, `MenuContextChanged(bool)`, `PopupChanged(bool)`; manager/UI methods. | Presentation context may change repeatedly, not lifecycle outcomes. Useful for timing policy only with transition guards. |
| Manager/visual readiness | G `ManagerReady`, `UIChanged`; audio `ManagerAvailable/Removed(manager)`; B `StartAnimationChanged`; HC `HintFocusInterrupted`. | Lifecycle/rebind/animation events can repeat; do not count as attempts or actions. |

**Required payload for a future committed action:** persistent attempt ID, sequential action ID, level/puzzle/rules identity, origin (manual ordinary / hint-follow / Swap / Blast / developer solver / preview), pre/post moves/score/time, direct changed cube IDs, before/after active and covered sets/counts, pre/post component labels/counts, final color, inventory revision/use delta, assistance/provenance, and optional terminal result. Expensive sets can be summarized after calculating deduplicated deltas; do not require a full-board allocation or graph search on every UI frame.

## 7. Feasibility analysis with actual evidence

### 7.1 Saved-data sample and method

Read-only inspection of the local macOS data directory `~/Library/Application Support/DefaultCompany/Flood Fill` found **28 saved numbered definitions**: Full Grid Easy21, Hard3, Expert4; no saved Medium, Random Shape, or Items definitions in this sample. All28 offer three goals; none claims a proven optimal witness. The sample is user/development data, not a representative catalog census.

An independent Python replay decoded the four-byte compact voxel format, rebuilt six-neighbor monochromatic regions in logical-index order matching RG, applied saved region/color moves, and checked final uniformity plus region metadata. **All28 witnesses reached uniform completion.** This validates the sampled logical routes in an independent reconstruction, not Unity rendering, source hash validation, human speed, global optimality, or all1,200 entries. No save was loaded through game methods that could trigger recovery writes.

| Evidence | Observed result | Design use/limit |
|---|---|---|
| Sample sizes/occupancy | 6/152, 8/296, 10/488, 12/728, 18/1,736, 20/2,168. | Confirms shell counts. No sampled15³ definition; formula/generation supports it. |
| Easy definitions | 21; witness12–49 moves, allowance18–79; all witness routes rate3 stars. | Easy3-star challenges have demonstrated examples; does not prove every Easy level. |
| Hard definitions | 3; witnesses12–13, allowance13–14; each rates1 star using its saved witness. | Higher stars require a shorter route or direct assistance. |
| Expert definitions | 4; witness equals allowance; saved witness rates1 star. | A known route is not proof of an impossible improvement; no optimality claim in these definitions. |
| Largest previous-active-set additions per witness | Range19–169 cubes; peak fractions5.07%–17.76% of the board. | 10/25/50 captures have clear scale support,100 needs larger boards. These are not automatically first-time coverage deltas. |
| Witness target-color diversity | All28 used all6 target colors. | No sample supports a universal “win using≤2 colors” promise. This does not prove such routes impossible. |
| Small example | Full Grid Hard3, seed1200161022:152 cubes,94 regions,12-move witness,13 budget, peak27 new active cubes (~17.76%). | Concrete evidence for single-action capture scale; witness itself is1 star. |
| Large example | Full Grid Easy96, seed1188955019:2,168 cubes,1,470 regions,49-move witness,79 budget, peak169 (~7.80%). | Different size needs different speed/capture expectations. |
| Goal types in28 definitions | BigCapture22; MoveSaver19; QuickStart16; LimitedColorUsage6; SpeedRun6; FinalColor5; ComboCapture5; FinalSprint4; FastExpansion1; FinalColorEfficiency0. | Generator supports compound final-color goal despite no sample instance. Goal diversity must not assume equal frequency. |

The local PlayerPrefs catalog has1,200 slots, **504 completed entries**, **882 best-star sum**, **40 permanent goal IDs**, and **15 entries with a stored best time**. Completed category counts: Full Grid Easy100, Medium100, Hard4, Expert100; Random Shape Expert100; Items Expert100; other categories0. These numbers illustrate available fields and historical gaps, **not verified human accomplishments**.

The Editor summary contains **314 records:303 Won,3 Lost,4 Window closed,3 Play Mode ended,1 Restarted/level changed**. Winning runs span all modes and supported sizes, with4–79 recorded moves;14 winning records have `provenOptimal=true`. These are development runs with historical settings/versions and partial/custom runs, not current player lifetime statistics or proof for today's saved definitions. Do not use summary wall times as human speed thresholds.

### 7.2 Supported, conditional, and unsuitable conditions

| Candidate | Feasibility judgment and reason |
|---|---|
| Maximum stars by difficulty | Rating function can return3 for any difficulty if moves≤floor(.7*budget),2 if≤floor(.9*budget), otherwise1. Actual unassisted maximum is constrained by puzzle optimum; not established for all boards. Direct powers can reduce counted normal moves; zero-move successful direct completion would rate3. Stock/reachable board states still matter. |
| All levels three stars without powers | **Do not promise.** For positive proven optimum S and Expert budget S, no unassisted win can use≤.7S. Higher-difficulty tight budgets may make3 stars unattainable even if Easy routes earn3. Sampled witnesses are not all optimality proofs. |
| All levels offer3 goals | **False as a guarantee.** Requested3, allowed0–3; all sampled28 happen to have3. |
| All assigned goals in one win | Structural goals generated against the same witness have common demonstrated route. Speed is estimated, and real interaction/selection timing can differ. Require nonempty valid assigned set, and measure actual final mask. Do not promise human/device feasibility universally. |
| Beat original solver witness | Possible only if original is unproven and a shorter legal route exists, or if using direct powers changes the action model. Never describe assisted fewer-normal-moves as beating the same solver problem. Skip/replace challenge on proven-optimal or unqualified puzzles. |
| Match an original witness | Known feasible on a validated starting definition within its budget; player execution differs. Useful low-volume optional unassisted benchmark. Do not require reproducing exact region order if equal/better length suffices. |
| Finish with≤1 or2 target colors | Not proven universally or on sampled witnesses; all28 used6. Legal free-region play does not force a fixed target-color diversity, but topology, budget, and assistance constrain it. Gate behind actual constrained-search proof for selected puzzles; do not assign globally. |
| Swap joins multiple separate regions | Supported by six-neighbor topology: one edited bridge cube can connect distinct target-color components. Needs pre-label measurement; runtime sample proof not performed. Never equate neighbors with distinct components. |
| Blast changes7 cubes | **Unavailable in normal generated skins**, despite API capacity7. Current Random/Items skin max6 eligible cells; Full Grid max5. Eligible count may exceed actual changed count. Recommend effective4-cell or measured multi-region bridge conditions instead. |
| Direct power wins | Supported: color an isolated last mismatching cube on an otherwise uniform connected board with Swap, or a compatible local set with Blast; same ordinary full-component victory test. Not guaranteed possible from every board with starter stock. |
| Large single-action captures | Sampled ordinary routes show19–169 previous-active additions.10/25 are attainable examples;50/100 should be size-filtered/optional. Unique first-time gain thresholds need separate replay measurement before finalizing. |
| Universal very short speed target | Unsuitable. Six different occupied-count scales, irregular visibility, camera navigation, hints, pause policy, and device response matter. Use size/mode cohorts or validated goal-specific estimated targets; test on mobile. |
| Universal score target | Unstable across sizes/moves/capture pattern and random base draws; not a pure skill measurement. Same puzzle replay can score differently. No current best score history. Avoid main progression gates based on luck/score farming. |
| All code-supported dimensions in custom | No. Player setup exposes seven equal-sided sizes only. Codec accepts up to64/axis and board internals have broader dimension APIs; these are not additional playable custom choices. |
| Perfect all-catalog goal completion percentage | Denominator not yet available for all lazy-generated definitions; total may be <3,600. Use fixed category completions or count goal IDs; percentage only over an explicitly versioned known assigned set. |

The skin bound follows directly from surface extraction: each retained Random/Items cube touches missing exterior/solid-neighbor space in at least one of six directions, so it cannot have six occupied neighbors. Keeping the largest skin component cannot increase degree. Full Grid has its stronger shell bound at n≥6. This is a rules/topology conclusion, unlike unproven shortest-solution or human-timing claims. [B, PROC `ExtractExteriorSurfaceFlags`, ITEM, BP]

## 8. Persistence, compatibility, and historical limitations

| Storage | Structure and active contents | Save timing / compatibility / limitations |
|---|---|---|
| PlayerPrefs `FloodFill3D.LevelCatalog.v1` | C Catalog v1,1,200 Results: completed,stars,bestMoves,bestCompletionSeconds,hasCompletionTime,earnedGoalIds. | Win writes catalog; goals/time and stars/moves are separate writes, not one all-result transaction. Defaults for missing old fields; invalid catalog version/length falls back to new in-memory catalog. No full outcome history, attempt ID, score, assisted status, or timestamps. |
| `numbered-levels-v1/{mode}_{difficulty}_{level}.json` | DEF v1 / generator v1 / goal generator v1: identity,seed,hash,palette,compact initial board,profile,budget,witness,initial region/capture counts,goals,generation parameters,item identifier. | Written first successful numbered generation; existing file immutable unless explicit unreadable-definition restart. Lazy set, not all catalog definitions prebundled. Read validates identity/hash/graph/witness/goal demonstration. |
| `flood-fill-3d-session-v1.json` | SP Session v1: setup and numbered/campaign identity,moveCount,score,elapsedSeconds,currentMoveBudget,selectedColorIndex,gameState,profile,board,camera,secondaryGoalAttempt,powerUpRevision,powerUpAttempt. | Checkpoints accepted moves, results, preparation, navigation, lifecycle and relevant camera/setup changes. Palette-only saves coalesce150ms. Writes temp then copy to main; not an append history and not the same atomic replace strategy as stock. |
| Board checkpoint | Dimensions,mode,current color,generation seed,each voxel x/y/z/color/captured/covered/starting. | Restores logical board and coverage; elapsed animation/targeting isn't replayed. Legacy missing covered can fall back to captured, losing earlier unrecorded coverage history. |
| Session power attempt | `used[3]`,scoreGuardActive,rewardedCubeIndices; global revision on session. | Continue preserves committed usage/score protection. Restart resets; terminal/navigation replacement loses historic totals. Legacy missing fields do not establish unassisted play. |
| Session secondary attempt | levelId,lastMove,initialCaptured,colorSelections[],progress[] with value/sprintStart/met/failed. | Restored only if matching definition/schema/count and consistent move history. Incompatible/missing history after moves disables tracker rather than inventing partial goal rewards. This can also prevent best-time recording. |
| `power-up-inventory-v1.json`, `.bak`, temporary file | PI Store v1,quantities[3],revision,pendingSession,hasPendingSession. | Atomic replace with backup; complete post-action write-ahead checkpoint. Recovery before session load/Continue availability; acknowledge after safe session. Recovering a winning numbered power transaction may record catalog/goal results again idempotently. Not a purchase/use history. |
| Unreadable definition restart archive | Existing damaged definition moved to `.replaced-<ticks>.bak` after explicit confirm, replacement regenerated through normal pipeline. | Old file preserved, old category progress retained. New puzzle/hash/goal IDs can differ; achievement version policy needed. This report did not invoke it. |
| Other PlayerPrefs | Setup mode/size/difficulty/fruit,audio,display toggles; legacy campaign level/random-size keys. | Preferences/legacy progression, not lifetime achievements. Never interpret setup selection as completion. |
| `UserSettings/FloodFillSolverSummary.json` plus backup/temp | Development AutoSolverAttempt list; see below. | Editor-only run reporting, not shipped player save. Contains unfinished and failed runs; never substitute for missing runtime player history. |

### Continue validity and loss of historical detail

`IsResumableSession` requires compatible version and game state Playing/Calculating, finite nonnegative elapsed time, valid setup/mode/difficulty/dimensions/colors, a connected valid unfinished board, and moveCount below allowance. Terminal boards, missing board, exhausted allowance, and restore failure disable Continue. Actual restore rechecks the board; availability caching is not proof of successful restore. Continue restores the **same ongoing attempt** conceptually; there is currently no persistent attempt GUID to make that identity available externally. [G, SP, IR]

Board hashes deliberately ignore the newly added covered field; compatibility also accepts relevant legacy representations so coverage should not invalidate the original immutable puzzle. Compact initial definitions store color/captured/starting flags, not ongoing coverage. Save versions remain1 with additive fields; lack of new fields must be treated as unknown history, not a verified zero. [NP, GM]

### Development solver summary contents

`AutoSolverAttempt` records id,boardRunId,kind,mode,difficulty,level,seed,width,height,depth,voxels,moveBudget,startingMoves,moves,solverMoves,score,searches,plannedMoves,searchMilliseconds,elapsedSeconds,levelElapsedSeconds,hasLevelElapsedTime,provenOptimal,searchResult,algorithm,outcome,UTC timestamps,and unityVersion. Here **plannedMoves is the solver's required continuation length**, `moves` is game move count, `solverMoves` counts applied solver moves, `elapsedSeconds` is solver-record wall duration, and `levelElapsedSeconds` is the game's active-play timer. These are not interchangeable. [AS]

Recorder begins/updates a record for the current editor board generation, records changes and roughly once per second while running, updates failed and unfinished records, and handles searches, cancellation, window/play-mode termination, restart/level change. Summary groups by difficulty and level; wins/attempts/tries-to-first-win/best-winning-moves are derived. Custom identity includes extra seed/dimension/run identity. BoardRunId uses editor play-session GUID, manager entity ID and generation; it is not durable player attempt identity across Continue/app launches. Historical records may lack newer time flags. The log is useful engineering evidence, not a basis for user streaks, acquisition dates, or assistance-free completion.

## 9. Retroactive progress: what can and cannot be imported

| Candidate progress | Existing evidence | Safe extent and conditions |
|---|---|---|
| Unique numbered wins, mode/difficulty/category completion | C.completed with full slot identity. | Reconstruct once; import snapshot/set membership, not repeated additions on startup. Historical provenance may be unknown/development-contaminated. |
| Best stars, total best-star sum, unique three-star levels | C.stars on completed entries. | Reconstruct unconditional rating records; cannot label unassisted. Do not infer every prior winning rating. |
| Best moves / efficiency against allowance | C.bestMoves + matching DEF budget. | Reconstruct an unconditional best-move ratio where definition exists. Cannot prove the moves were manual/unassisted or that same win met other conditions. |
| Best completion time | C.hasCompletionTime+seconds. | Import only flagged finite valid times; missing flag is unknown. Fastest time and best moves might come from different wins. |
| Permanent diamonds / goal-type ownership | C earned IDs + matching DEF goals. | Union unique `(level identity,goalId)`; type/assigned-set counts only if definition available and compatible. Acquisition dates and same-attempt masks unavailable. |
| Completed size/fruit cohort | Completed catalog index + size schedule; matching DEF item identity/seed when needed. | Size schedule-derived numbered cohort can be imported with version assumptions. Item details best joined to saved definition; do not require regenerating saves. |
| Current unfinished usage/coverage/time/goal progress | Session with complete valid history. | Continue progress only; no completed achievement should be fabricated by treating it as a win. Eligibility stays unknown where fields/provenance absent. |
| Lifetime wins/failures/attempts/restarts/replays/custom wins | No player history. | Start tracking from installation of the future system; label as “since tracking began” where relevant. Unique completions are not a lower-level replacement for total wins. |
| Power lifetime usage, direct edits, hint follows, assisted/unassisted win totals | Only stock/current attempt. | Start new tracking. Do not estimate from starter stock−quantity or current zeros. |
| Unique capture/action/merge/score histories | Only current board/score and partial goal values. | No reliable historical reconstruction; new tracking required. |
| Streaks/day engagement/lifetime active time | No player outcome/date stream. | Start new tracking; file modification times/editor timestamps are unsuitable. |
| All-goals same win / special combined performance | Best fields and permanent union lose correlation. | Cannot reconstruct; require one future terminal result satisfying all predicates. |

**Recommended import policy:** preserve existing progress for ordinary unconditional progression achievements if the product accepts historical records; mark imported evidence as `LegacyUnknown` so it cannot silently unlock manual/unassisted/competitive challenges. In developer profiles, use a separate test achievement store rather than claiming human progress. If rewards are later retroactively granted, completion import and reward claims must still be idempotent and policy-controlled; no rewards are implemented or specified here.

## 10. Missing tracking requirements, prioritized

| Priority | Needed future capability | Conditions it enables |
|---|---|---|
| P0 | Persistent attempt GUID and action sequence, puzzle/rules/definition hash, durable terminal status and processed-result IDs. | Exactly-once wins, retries, Continue, recovery, improvements; foundation for every reward-bearing counter. |
| P0 | Sticky provenance and assistance: Editor solver/dev preview/unlimited/add-stock flags, manual origin, paid hints/direct powers, history completeness. | Trusted wins, unassisted challenges, fair solver comparisons. Flags must survive Continue and never be erased by toggling settings. |
| P0 | Terminal snapshot including mode/diff/level/kind, occupied size,starting witness/optimality,used moves,budget,time,score,rating,goal mask,used powers,final color. | Per-attempt predicates, correlations, streaks, replay records. |
| P0 | Authoritative committed-operation result for ordinary/direct moves and paid hint reveal, including successful journal recovery. | No counting previews/rollbacks/canceled operations, or missed recovered wins. |
| P1 | Normal/direct target-color masks; actual changed indices; active and unique-coverage deltas; universal largest capture; pre/post current region labels/counts. | Capture, color economy, effective power use and merge challenges. |
| P1 | Recommendation identity and one-time follow association including manual equivalence. | Successful hint-follow conditions rather than reveal spam. |
| P1 | Per-level best records with eligibility context and explicit old/new comparison; universal best-time path independent of optional goals. | Fair replay improvement and speed records, reliable notifications. |
| P1 | Lifetime aggregates + unique level/goal sets + per-mode/difficulty filters, updated from deduped terminal/action ledger. | Progressive tiers without rescanning editor history. |
| P2 | Ordered eligible outcome stream, streak policy, dated valid activity and completion days, durable active-time deltas. | Streak/engagement categories. |
| P2 | Versioned achievement definitions and separate completed/claimed state per tier, migration/import marker/outbox. | Backward-compatible unlocks, future reward claims, replay-safe notifications. |

No item in this table was implemented during the audit.

## 11. Recommended categories, conditions, and provisional milestones

All thresholds below are **planning recommendations**, not existing achievements. Favor unique accomplishments and improvements over repeated stock spending or low-value actions. “Existing” means data supports ordinary predicates, subject to legacy provenance; “New” requires the missing tracking above. Avoid creating several named achievements with identical underlying progress and thresholds.

| Category | Recommended meaningful condition | Provisional tiers / scope | Support and feasibility gate |
|---|---|---|---|
| General progression | Complete unique numbered entries. | 1,10,25,50,100,250,500;1,200 as long-term catalog completion. | Existing C. One main series, not a duplicate total-win series. Catalog cap1,200. |
| Modes | Complete unique entries in a selected mode. | 10,50,100,200,400 per mode. | Existing C; mode cap400. Mirror only if mode variety is a product goal. |
| Difficulties | Complete unique entries in a selected difficulty. | 10,50,100,200,300. | Existing C; difficulty cap300. Expert completion is feasible with its validated witnesses, independent of high stars. |
| Category completion | Finish one100-level mode+difficulty category; then additional full categories. | 1,3,6,12 categories. | Existing C; distinct from ordinary level-count milestones. |
| Stars | Improve total saved gold stars and collect unique3-star levels. | Best-star sum100,300,600,1,200; optional2,400 only after catalog attainability review.3-star unique5,25,100. | Existing C; do not demand3,600 or all Expert3-stars without proof/explicit assistance policy. These two series should be pruned if too redundant. |
| Secondary goals | Earn unique permanent diamonds. | 3,15,50,150,300; later scale after definition census. | Existing C goal IDs; no universal3,600 promise. |
| Goal variety | Earn goals from different supported types. | 3,5,8 types; all10 only after confirming compound goal availability across eligible content. | Existing + matching DEF partly; missing type history when definitions absent. |
| Same-attempt goals | Win while meeting every assigned goal in that attempt. | One win with≥2 goals; optional one with exactly3. | New final mask history. Do not count permanent union or0-goal boards. |
| Power-ups: effective Swap | Use one Swap to join≥2 distinct neighboring target-color components, then win. | Single accomplishment; optional on3 distinct levels. | New pre/post labels and terminal correlation; plausible topology, validate concrete boards before shipping. No raw “use100 powers.” |
| Power-ups: effective Blast | Actually change≥4 occupied cubes with one Blast, then win. | Single accomplishment; optional distinct-level extension after testing. | New actual-change count; Full Grid can have5 eligible. No seven-cube requirement. |
| Power-ups: decisive finish | A committed Swap or Blast directly causes an eligible victory. | One per kind, or one combined hidden discovery. | New action cause/outcome correlation. Mechanically supported; avoid redundant medals for identical finish. |
| Smart Hint | Follow a paid valid recommendation successfully and complete that attempt. | One introductory accomplishment; optional different-level small tier3/5 if stock acquisition later supports it. | New follow attribution. Starter stock is finite; avoid mandatory long use tiers until replenishment/economy exists. |
| Unassisted skill | Complete distinct levels with no paid hint/direct power/development assist. | 1,10,25,50. | New sticky eligibility/terminal history. Existing zero-used array alone is insufficient for legacy import. |
| Cube capture | One accepted normal action adds a large newly-active component while ultimately winning. | 10,25,50;100 only on larger eligible boards. | New universal max/terminal linkage; sample supports scale. Explicitly name component additions; for first-time unique gain, revalidate these thresholds separately. |
| Unique exploration | Cover first-time cubes across **distinct won numbered levels**, credit each level at most once. | Prefer size/level milestones instead of a second series that just sums fixed board sizes. | New ledger if retained. Do not reward cumulative revisits; this is often redundant with progression. |
| Region merging | Merge≥2,3,5 distinct pre-action components into the recolored region on one normal move, then win. | Provisional single-action tiers; validate5 on real routes. | New component-label telemetry. Large recolored source can border many components; six-neighbor per-cube bounds do not cap whole-region merges to6. |
| Move efficiency | Win with≥10%,20%,30% allowance remaining on eligible unique levels. | Prefer a small skill series or size-filtered conditions, not duplicate gold-star thresholds. | Existing bestMoves+DEF for unconditional record; New for unassisted. Skip unsupported Expert30% promises. |
| Solver benchmark | Match or improve an original validated witness length without powers. | One selected puzzle; optionally5 distinct benchmark puzzles. | New eligibility; saved DEF baseline exists. “Beat” only on curated unproven boards with known shorter witness. Never offer “beat proven optimum.” |
| Board dimensions | Complete one numbered level at each supported size. | Individual first20³ challenge or collection of all7 sizes. | Catalog schedule supports retroactive numbered cohorts. No hidden unsupported sizes/rectangles. |
| Mode/shape variety | Complete Full Grid, Random Shape, and Items; finish all4 item kinds. | One mode sampler; one4-item collection. | Catalog modes existing; item attribution via DEF/seed/version. Player setup fruit choice is not proof of an item win. |
| Speed | Improve a personal best active completion time on a comparable board without assistance; optional curated6³ timed challenge. | One improvement, then improvements on3/10 distinct levels; fixed60/90/120-second targets only after mobile cohort testing, not asserted feasible here. | Best time partial; New trusted improvements/previous records. Use flag and same board/rules. Do not require pause-free unless separately measured. |
| Score | Optional personal-best score improvement on a fixed, curated rules/size cohort. | Single exploratory achievement; defer absolute5k/10k targets until score distributions/anti-loop policy are measured. | New best-score record/eligibility; random draw matters. Avoid score grinding and “maximum RNG” requirements. |
| Winning streak | Consecutive eligible wins on different numbered levels. | 3,5,10; Expert variant3/5 only if it replaces, rather than duplicates, a redundant series. | New ordered outcomes/policy. Loss resets; restart/abandon policy must be decided explicitly. |
| Three-star / unassisted streak | Optional3 or5 successive eligible wins meeting the condition, only on feasible cohorts. | Keep at most one additional skill streak family. | New terminal rating/assistance order. Not derivable from saved best stars. |
| Color challenge | Finish with each of6 final colors across eligible wins. | One6-color collection; optional curated final-color+move challenge. | New final-color history. Current goal types can offer curated constraints; low target-color diversity deferred pending proof. |
| Custom games | Win distinct custom board hashes with valid generated rules. | First win;3 distinct size classes or modes; all7 sizes as optional separate collection. | New custom history/identity. Random/fullgrid only in setup; do not ask for custom fruit. Avoid repeatedly rerolling tiny easy boards. |
| Replay improvements | Improve moves or time on3,10,25 distinct previously completed levels. | Credit each improved level once per chosen series; require strict improvement. | New old/new result event/history; existing current best alone cannot prove improvement occurred after tracking began. |
| Hidden discoveries | Natural power-up finishing blow, meaningful bridge, or all assigned goals + improved move record in one win. | Single conditions only, documented internally and shown after discovery. | New correlated terminal/action telemetry. No secret failure/waste/stock-depletion incentives. |
| Long-term engagement | Distinct days with an eligible successful completion. | 3,7,15,30 nonconsecutive completion days. | New dated outcomes; prefer nonconsecutive days over punitive daily streaks. If consecutive-day tiers are desired,3/7 only after timezone/offline policy. |

### Threshold principles for the eventual catalog designer

* Keep unique-level progress primary. Caps are1,200 global,400/mode,300/difficulty,100/category,12 categories,7 sizes,6 palette colors,4 current item kinds.
* Theoretical3,600 stars and up-to3,600 goals are capacity calculations, not achievable-content guarantees. Do not set mandatory “perfect everything” targets without a current catalog feasibility census.
* Do not require stock expenditures beyond sustainable supplies. Starter3 per kind and maximum99 do not establish any acquisition loop or economy.
* Distinguish first-time coverage, latest-active additions, actual merge count, directly recolored cells, and score-credit cells in player-facing conditions.
* Use one strong series per genuinely different accomplishment; avoid padding the catalog with totalwins and totalcompleted measuring identical no-replay behavior, or gold stars and exactly equivalent remaining-budget milestones.
* Require winning the attempt for skill actions where failure farming would otherwise dominate. Use distinct-level caps for repeatable actions.
* Calibrate speed, score, merges, and unique-gain thresholds with eligible runtime telemetry/device tests before freezing rewards. The sample is too narrow for universal balance claims.

## 12. Exploits, edge cases, and safeguards

| Risk | Current evidence / consequence | Future safeguard (recommendation) |
|---|---|---|
| Repeated UI/audio/animation callbacks | UIChanged/Changed/Result presentation may repeat or fire before persistence. | Count committed actions and terminal attempt IDs, never button/tween/sound completion. |
| Winning power journal recovery | PI acknowledges/recovers catalog and goals; G also records result. | Same persisted action/attempt outcome ID for both paths; ledger dedupe and retry-safe progress/outbox. |
| Normal result crash between separate writes | Goal/time catalog write, stars/moves write, session save are separate. | Future outcome transaction/outbox must reconcile incomplete processing; do not assume existing record update is fully atomic. |
| Continue counting again | Restore copies moves/time/usage/coverage. | Same attempt ID; action sequence/watermarks and elapsed-time delta baseline persist. |
| Restarts/replay | Attempt counters reset; global stock/progress survives. | New attempt ID with reason; unique-level goals use sets; total attempts/wins process exactly once. |
| Home as false abandonment | Home saves an unfinished game that may continue. | Mark suspended, not failed/abandoned; abandonment only by explicit discard/replacement policy. |
| Remote-region capture farming | NewlyCaptured can count revisits after active-root changes. | Unique index union or true component-merge measure; do not sum latest differences as unique captures. |
| Split/reconnect score/capture | Direct powers can split topology; score guard protects post-power credit but not every metric. | Track direct/normal effects separately, prevent reuse credit; don't assume score guard is a lifetime achievement ledger. |
| Zero-move/direct-power stars | Stars ignore power stock and award on normal MoveCount. | Either allow assisted rating achievements explicitly or require sticky unassisted status; do not change existing star rules as part of planning. |
| Hint cancellation/ignored reveal | Paid usable reveal remains spent; calculation/preview cancellation free. | Count successful paid reveal once; useful-follow predicate distinct, not based on opening UI. |
| Dev solver / unlimited / added stock / free preview | Ordinary saves/catalog can be affected; trackUsage can be disabled. | Separate dev profile/store and sticky per-attempt ineligibility; do not sanitize by later toggling off. Legacy unknown stays unknown. |
| Goal type partial tracking | Universal max not stored unless matching goal assigned; old saves disable tracker. | Universal action metrics independent of optional goal selection; no fabricated missing history. |
| Best fields from different wins | Best moves, best time, best stars and goal union are independently updated. | For compound conditions evaluate a single terminal result, never combine independent personal bests. |
| Definition replacement/version change | Same category slot may get replacement board/goals; IDs/hash differ. | Separate catalog-slot uniqueness from puzzle-version challenge evidence; retire/map old goal ownership deliberately. |
| Goal ID collisions | IDs describe conditions and can repeat on different levels. | Use `(level identity,definition/goal version,goalId)` uniqueness, not bare string globally. |
| Float/rounded percentage | Near-full can visually round100; cumulative coverage can really reach100 before win. | Full integer component membership/IsFullyCaptured for victory; derive integer ratio thresholds carefully, cap incomplete displays as needed. |
| Score randomization | Same legal route can produce different scores; cap per call rewards repeated scoring opportunities. | Avoid luck-based mandatory tiers and uncapped repeated-action totals; persist actual credited score if used. |
| Timer focus/pause/device differences | Unscaled active timer excludes specified states, not total wall duration. | State the timing definition; preserve Continue; test mobile background/focus. No wall-time inference from current best. |
| Clock manipulation for daily tiers | No server clock/day ledger now. | Define local timezone/day policy and monotonic progression checks; don't promise tamper resistance from local JSON alone. |
| Corrupt/edited saves | Local JSON/PlayerPrefs are not secure competitive evidence. | Validate schema/bounds/identity, preserve unknown/corrupt data, isolate test store; don't invent server verification. |
| Inventory write failures | Logical edit can roll back or journal later recover; animation may already notify board revision. | Achievement action must share durable commit result; rollback notifications never unlock. |

## 13. Future tracking architecture — planning only

### 13.1 Definitions and stable identity

Use a versioned achievement definition catalog with stable string IDs, e.g. `progress.unique_numbered` and stable tier IDs such as `t010`, separate from localized titles/descriptions. Each definition specifies metric/predicate, aggregation (unique set/cumulative/max/single-attempt/streak), eligible game kinds and origins, mode/difficulty/size filters, rules version, visibility/hidden policy, ordered tier thresholds, and a future optional reward-definition reference. A single-completion achievement is one tier. Never derive identity from displayed title, array position, or localized difficulty name.

Numbered level identity is mode+difficulty+level. Puzzle challenge identity adds definition hash/generator/rules version. Custom identity needs board hash plus rules/palette; seed0 is not sufficient. An attempt GUID survives Continue; a restart/new game creates a different GUID. Actions receive sequential IDs within that attempt, including zero-normal-move powers and paid hint reveals. Existing inventory revision can help reconcile power transactions, but cannot replace attempt/action identity.

### 13.2 Authoritative input and processing

Recommended flow:

`validated playable start/restore → committed logical action or paid reveal → durable attempt checkpoint/outcome → idempotent achievement reducer → persisted tier completion + notification outbox → future independent reward claim`

The achievement reducer consumes immutable committed facts, not Unity UI events. Extend the future transaction design to include normal moves and terminal outcomes as well as the existing power journal. Do not count ordinary board PowerUpBoardChanged before manager accounting. Recover pending committed actions/outcomes through the same reducer after a crash; never emit a second effective result for the same attempt/outcome.

Maintain a processed-action watermark or equivalent event IDs and a processed-terminal-attempt set/ledger. Unique accomplishments keep unique keys. Cumulative metrics add deltas once; maxima use max; ratio/percentage achievements reference a frozen, versioned denominator; mode/difficulty filters use normalized identities. Multi-condition challenges evaluate one terminal snapshot. Streaks reduce an ordered eligible outcome stream with a specified loss/restart/abandon/filter policy.

Performance should match the existing lightweight game: snapshot cheap scalar deltas at logical commits, calculate component labels only when a relevant metric requires them, reuse buffers, and never perform new full-board analysis in every UI refresh. Record direct changed cells before edit and actual first-time covered cells before union. Persist only the compact statistics/evidence needed for recovery and review, rather than an indefinitely growing full-board replay log.

### 13.3 Persistent state and independent tier rewards

A future versioned achievement store should include:

| State group | Planned information |
|---|---|
| Save header | Schema version,definition catalog version,tracking epoch/profile ID,eligibility/import policy version. |
| Metric progress | Deduped aggregates,unique level/goal keys,maxima,streak state,day sets,and relevant last processed attempt/action watermarks. |
| Per-tier state | **Independent** completion bool/time/evidence and **independent** reward-claimed bool/time/idempotency token. Each tier remains individually addressable. |
| Current attempt tracking | Persistent GUID,seq,metadata,history completeness,sticky assistance/provenance,action aggregates,hint recommendation association,elapsed-time checkpoint baseline. |
| Outcome/recovery | Compact pending committed outcome/action facts and processing status,shared idempotency keys with recovery path. |
| Notifications | Pending unlock IDs/tiers with presentation acknowledgement; completion survives a crash even if toast has not been shown. |
| Migration/import | Last migration,retroactive snapshot/import marker,unknown legacy provenance,retired-definition mappings. |

A single win can cross several tiers; evaluate each tier independently. Completing tier3 does not imply tier1's reward has been claimed. Opening the achievement window or replaying an unlock notification must not grant anything. Later reward claims need an atomic or journaled receipt keyed by profile+achievement+tier+reward-definition version, coordinated with the eventual reward store. Reward definition references and claim status can be designed now; currency values, economy, purchase flows, and actual distribution remain out of scope.

### 13.4 Retroactive initialization, migration, and testing

Initialize from a read-only catalog/definition snapshot exactly once per import version. Recompute unique-set metrics safely rather than repeatedly adding snapshot totals. Mark imported proof `LegacyUnknown`; allow only the catalog predicates whose trust policy permits that origin. Newly introduced stats begin from the tracking epoch and should not claim full historical totals. Do not fabricate dates for old completions; imported timestamp means import time, not the original accomplishment date.

Keep save schema and achievement catalog versions separate. Support additive fields, removed/renamed tier mappings, preserved claimed receipts, and stable IDs across localization. Changing a denominator, goal generator, difficulty rule, score rule, or assistance policy requires an explicit version/migration decision. Existing damaged saves must be preserved; empty/default fields mean unknown where historical evidence was never recorded.

Development tools should use isolated files/profiles and visibly mark simulated progress. Carry solver/dev provenance into attempts before any assisted operation, including free previews and inventory grants. Runtime Editor solver integration currently lacks that marker; this is required future work. For shipping migration, decide how ordinary existing dev-contaminated catalog data is treated rather than silently trusting it for skill achievements.

Future verification should cover: duplicate result/recovery processing; crash between journal/session/catalog/achievement writes; Continue without new attempt; action/save retry; normal and direct last-move win; actual terminal loss; no-op/canceled/failed paid operations; paid hint ignored/followed manually/followed by equivalent region cube; stale hint; zero-cost power sequencing; split/reconnect/revisit; no-goal and legacy-history attempts; records from different wins; category boundaries; tier jumps and independent claims; definition replacement; dev/unlimited tracking toggle; local-day changes; mobile pause/background; incomplete rounded100% display. These are recommendations for future implementation tests, not tests run against changed production code in this audit.

## 14. Unverified behavior and decisions still needed

1. **Full catalog feasibility:** only28 current saved definitions were independently replayed. There is no full1,200-definition census/proof here, no exhaustive constrained-color search, and no universal3-star/three-goal guarantee. Historical solver summaries are broader but not same-version player proof.
2. **Device timing and feel:** no Unity Play Mode/mobile build or interaction test was run. Actual hint focusing/selectability, large board speed goals, suspension/focus clock behavior, and animation/input responsiveness need device testing.
3. **Eligibility policy:** decide whether ordinary progression imports all existing catalog records, and whether assisted stars count for star achievements. Legacy data cannot retroactively distinguish human/manual, hints, direct powers, Editor solver, or disabled usage tracking.
4. **Streak policy:** determine whether restart, discarded unfinished attempt, other difficulty play, custom games, or an app exit interrupts each streak. A resumable home visit should not automatically be a loss.
5. **Goal completion policy:** distinguish all current diamonds across replays from all assigned goals in one win; require≥1 or a specified assigned count. Define handling of archived/replaced goal definitions and undiscovered denominator.
6. **Power effectiveness:** validate concrete curated states for proposed bridge/merge/4-cell Blast challenges. Seven-cell blasts are ruled out for generated skins, but not every skin necessarily provides six eligible targets or every targeted cell a differing color.
7. **Score achievements:** decide whether to include them given RNG, mode/size dependence, missing best-score history, and revisit-scoring behavior. No final absolute thresholds are justified by this audit.
8. **Custom/campaign navigation:** the non-numbered win primary action still starts legacy campaign despite PLAY AGAIN text. Resolve intended scope before authoring custom consecutive/replay challenges; this inspection did not change it.
9. **Stock acquisition:** no achievement reward economy, purchases, or reliable replenishment loop is established by current placeholders/dev stock buttons. Avoid mandatory repeated-use requirements until that design exists.
10. **Best-time coupling:** current record writing depends on a valid numbered goal tracker. Decide whether future universal time records should be independent from optional goals and legacy compatibility.
11. **Calendar and trust:** choose local/UTC/account timezone, offline handling, and clock rollback policy. Local storage is not tamper-resistant; no online achievement service is implemented or assumed.
12. **Rewards:** values/types/tier rewards/claim UX remain deliberately undecided. This report proposes only the independent state and idempotency foundations.

## 15. Source reference index

Paths below are relative to project root. Symbols are authoritative anchors; line numbers are inspection-time entry points and may shift with future edits. Read the active scene/resource values as well as class defaults.

| Alias | Source and principal anchors |
|---|---|
| Baseline | [FloodFill3D_Game_Functionality.mmd](FloodFill3D_Game_Functionality.mmd), especially chapters14–18,20+, scoring/goal/save sections. Earlier pre-power-up functionality, not current implementation authority. |
| Scene | [FloodFill3DDemo.unity](Assets/FloodFill/Scenes/FloodFill3DDemo.unity), manager settings around152–185, board around1210; [EditorBuildSettings.asset](ProjectSettings/EditorBuildSettings.asset), [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt), [ProjectSettings.asset](ProjectSettings/ProjectSettings.asset). |
| G | [FloodFillGameManager3D.cs](Assets/FloodFill/Scripts/ThreeD/Gameplay/FloodFillGameManager3D.cs): timer/interaction properties94–133; StartNumberedLevel171, StartConfiguredGame179, StartCampaignGame185, RestartCurrentGame197; Pause/Resume243; Continue/ReturnToMenu260–325; SelectColor329; HandleVoxelClicked347; RestartGame375; PresentPreparedLevel; AwardScore1037; ScheduleResult1062; IsResumableSession1129; TryRestoreSavedSessionCore; SaveSession1263; lifecycle1408+. |
| UI | [FloodFillMobileUI3D.cs](Assets/FloodFill/Scripts/ThreeD/UI/Mobile/FloodFillMobileUI3D.cs): sizes34; setup169–176; StartGame/FRUITS around530; Continue/RefreshContinue; OnResultAction633; pause/home confirmation methods. |
| IR | [FloodFillGameManager3D.InputResponsiveness.cs](Assets/FloodFill/Scripts/ThreeD/Gameplay/FloodFillGameManager3D.InputResponsiveness.cs): QueueSelectionSave, FlushSelectionSave, ReadSessionStamp, TryCachedContinue, CacheContinue. |
| B | [VoxelBoardManager3D.cs](Assets/FloodFill/Scripts/ThreeD/Board/VoxelBoardManager3D.cs): Width/Height/Depth117+, captured/covered/fully-captured142–157; color generation218+; persistent board capture/validation/restore; RecolorConnectedRegion558; mask generation1041+; RecalculateCapturedRegion and newly-active difference1357+. |
| C | [FloodLevelProgression3D.cs](Assets/FloodFill/Scripts/ThreeD/Progression/FloodLevelProgression3D.cs): Catalog/Result, Index, Get, Unlocked, Completed, Next, Seed, Record, SecondaryGoalEarned, RecordSecondaryGoals, Stars. [FloodLevelRules.asset](Assets/FloodFill/Resources/FloodLevelRules.asset): active .7/.9 ratios. |
| SP | [FloodFillSessionPersistence3D.cs](Assets/FloodFill/Scripts/ThreeD/Persistence/FloodFillSessionPersistence3D.cs): FloodFillSessionSaveData3D, VoxelBoardSaveData3D, VoxelSaveData3D, OrbitCameraSaveData3D, SavePath/TryLoad/Save/Delete. |
| NP | [FloodNumberedLevelPersistence3D.cs](Assets/FloodFill/Scripts/ThreeD/Goals/FloodNumberedLevelPersistence3D.cs): LevelId/LevelPath, BoardHash/MatchesBoardHash, TryLoadLevel, SaveLevel; immutable definition validation/cache. Confirmed unreadable-definition archive flow also in G. |
| GM / DEF | [FloodSecondaryGoalModels3D.cs](Assets/FloodFill/Scripts/ThreeD/Goals/FloodSecondaryGoalModels3D.cs): enum7+, goal/progress/attempt models, FloodNumberedLevelDefinition3D43+, Snapshot/SolverResult, CompactInitialBoard3D65+, SecondarySolutionTrace89+. |
| GG | [FloodFillGameManager3D.Goals.cs](Assets/FloodFill/Scripts/ThreeD/Goals/FloodFillGameManager3D.Goals.cs): GoalSettings, PrepareGoalTrace, PublishNumberedDefinition, RestoreNumberedDefinition, RestoreSecondaryAttempt. |
| GT | [FloodSecondaryGoalTracker3D.cs](Assets/FloodFill/Scripts/ThreeD/Goals/FloodSecondaryGoalTracker3D.cs): Changed, Attempt, SelectColor, Move, PowerUpChange, Complete, State. |
| GEN | [FloodSecondaryGoalGenerator3D.cs](Assets/FloodFill/Scripts/ThreeD/Goals/FloodSecondaryGoalGenerator3D.cs): Trace, Generate, candidate conditions/families, Demonstrated; common-witness validation and speed estimation. |
| GS | [FloodSecondaryGoalSettings3D.cs](Assets/FloodFill/Scripts/ThreeD/Goals/FloodSecondaryGoalSettings3D.cs) and [FloodSecondaryGoals.asset](Assets/FloodFill/Resources/FloodSecondaryGoals.asset): active tuning/weights/generation limits. |
| PS | [FloodPowerUpSettings3D.cs](Assets/FloodFill/Scripts/ThreeD/PowerUps/FloodPowerUpSettings3D.cs): kind/state enums, definitions, attempt model, settings and Unlimited guard. [FloodPowerUps.asset](Assets/FloodFill/Resources/FloodPowerUps.asset): active stock/cost/timing/search/goal/score flags. |
| PU | [FloodPowerUps3D.cs](Assets/FloodFill/Scripts/ThreeD/PowerUps/FloodPowerUps3D.cs): Ready/CanSelect/Available, Changed, Select, HandlePalette/HandleCube, ExecuteTarget, BeginHint/ReceiveHint/TryHintCandidate, RevealHint, PayForHint, CancelActivePowerUp, Update, development buttons. |
| GP | [FloodFillGameManager3D.PowerUps.cs](Assets/FloodFill/Scripts/ThreeD/PowerUps/FloodFillGameManager3D.PowerUps.cs): PowerUpsUsed, reset/capture/restore attempt, PowerUpScoreCount, SelectPowerUpColor, CommitPowerUpRecolor, CommitHintUse, ExecuteHintMove. |
| BP | [VoxelBoardManager3D.PowerUps.cs](Assets/FloodFill/Scripts/ThreeD/PowerUps/VoxelBoardManager3D.PowerUps.cs): ActiveCells/CapturedCells/LogicalIndex/PaletteCount, PowerUpRevision/PowerUpBoardChanged, GetPowerTargets, TryPowerRecolor, CollectRegion, visual preview/finish/reset. |
| PI | [FloodPowerUpInventory3D.cs](Assets/FloodFill/Scripts/ThreeD/PowerUps/FloodPowerUpInventory3D.cs): Store/StorePath, Ensure/Write, Quantity/Available, Commit, Acknowledge, RecoverPendingSession, Add, Changed. |
| HS / HC | [FloodHintService3D.cs](Assets/FloodFill/Scripts/ThreeD/PowerUps/FloodHintService3D.cs): recommendation/result/plan models and current-state search; [OrbitCameraController3D.Hints.cs](Assets/FloodFill/Scripts/ThreeD/PowerUps/OrbitCameraController3D.Hints.cs): TryFindHintView, IsHintSelectable, FocusHint, HintFocusInterrupted. |
| RG / SIM | [FloodFillRegionGraph3D.cs](Assets/FloodFill/Scripts/ThreeD/Solver/FloodFillRegionGraph3D.cs): TryBuild, region colors/weights/logical representatives/neighbor arrays; [FloodMoveSimulator.cs](Assets/FloodFill/Scripts/ThreeD/Solver/FloodMoveSimulator.cs): Apply, CountComponent, CopyLastComponent, FloodComponentAnalysis. |
| SOL | [FloodFillSolverModels3D.cs](Assets/FloodFill/Scripts/ThreeD/Solver/FloodFillSolverModels3D.cs): difficulty, settings, profile, result/validated/optimality fields; [FloodFillMoveSolver3D.cs](Assets/FloodFill/Scripts/ThreeD/Solver/FloodFillMoveSolver3D.cs), [FloodSolver.cs](Assets/FloodFill/Scripts/ThreeD/Solver/FloodSolver.cs), [FloodSolverValidator.cs](Assets/FloodFill/Scripts/ThreeD/Solver/FloodSolverValidator.cs): bounded search/validation. |
| BUD | [MoveBudgetCalculator3D.cs](Assets/FloodFill/Scripts/ThreeD/Solver/MoveBudgetCalculator3D.cs): Calculate, GetComplexityMultiplier, ScaleExtra, Round. |
| PROC / ITEM | [ProceduralVoxelShapeGenerator.cs](Assets/FloodFill/Scripts/ThreeD/Generation/ProceduralVoxelShapeGenerator.cs): exterior flood, ExtractExteriorSurfaceFlags890+, BuildLargestConnectedSurface; [FloodItemShape3D.cs](Assets/FloodFill/Scripts/ThreeD/Generation/FloodItemShape3D.cs): seed-selected fruit analytic solid, shell extraction, connected-component retention. |
| AUDIO | [FloodAudioSignals.cs](Assets/FloodFill/Scripts/ThreeD/Audio/FloodAudioSignals.cs): FloodWave and all existing audio-facing lifecycle/result/color/pause/menu events. |
| AS | [AutoSolverSummaryStore.cs](Assets/Development/AutoSolver/Editor/AutoSolverSummaryStore.cs): AutoSolverAttempt, aggregate/store persistence; [AutoSolverSummaryRecorder.cs](Assets/Development/AutoSolver/Editor/AutoSolverSummaryRecorder.cs): Begin/SearchFinished/Observe/Stop/Finish/CaptureLevelTime; [AutoSolverPlaybackController.cs](Assets/Development/AutoSolver/Editor/AutoSolverPlaybackController.cs), [AutoSolverAdapter.cs](Assets/Development/AutoSolver/Editor/AutoSolverAdapter.cs), [AutoSolverWindow.cs](Assets/Development/AutoSolver/Editor/AutoSolverWindow.cs): editor execution and ordinary gameplay integration. |

**Evidence boundaries:** local catalog counts and summary counts are inspection snapshots, not game constants. Definition capacities and algorithms above come from current source/active configuration. Offline witness replay did not generate new definitions, change records, call recovery methods, or prove optimality. The final achievement catalog should use these constraints and explicitly gate all conditions marked conditional/unverified before implementation.
