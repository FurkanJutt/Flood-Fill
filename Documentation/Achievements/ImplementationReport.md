# Flood Fill 3D achievement implementation

## Delivered system

The Main Menu now opens a native Achievements screen. The implementation includes the actual catalog assets, database, settings, four UI prefabs, assigned sprites, persistent tracking, notifications and an Editor authoring window. Definitions are predefined, never randomized or regenerated at runtime.

The revised PDF supplies **112 achievements across 17 categories and 242 tiers**: 47 single-tier and 65 three-tier achievements. **103 achievements / 215 tiers are enabled**. All 112 definitions and evaluators are retained. See [the full manifest](CatalogManifest.md) and [portable catalog](Catalog.v1.json) for names, categories, thresholds, hidden flags and disabled reasons. Seven hidden definitions retain their catalog status.

## Compatibility decisions

Nine definitions remain disabled, with their original thresholds preserved:

| IDs | Names | Reason / enabling requirement |
|---|---|---|
| 046–049 | Color Artist, Strategic Thinker, Blast Specialist, Powered Up | Production grants only three of each power-up once; no legitimate replenishment exists. Their first milestones exceed available lifetime stock. Connect a real inventory replenishment path before enabling. |
| 052–055 | Chain Reaction, Final Touch, Blast Finish, Good Advice | The first milestones require more paid Swap, Blast or Hint uses than starter stock permits. The evaluators and tracking exist; enable after legitimate replenishment exists. |
| 051 | Maximum Impact | Seven directly recolored Blast cubes are impossible with current surface-skin generators. Full Grid allows at most five; Random Shape and Items at most six. Requires compatible solid-board gameplay. |

Debug grants and unlimited inventory never make these achievements eligible. Their usage statistics still accumulate for ordinary play. No requirements were lowered and no currency, inventory refill or achievement rewards were invented. Extremely large time, score and capture milestones retain the source thresholds; long-term practical attainment has not been play-tested.

## Architecture and files

Runtime code lives in `Assets/FloodFill/Scripts/ThreeD/Achievements/`:

- `Core/`: serializable catalog, filters, tiers, rewards, attempts, statistics, progress and provider interfaces; generic event-driven reducer; validation.
- `Runtime/`: ScriptableObject authoring wrappers, database/settings, persistence, service and game-manager tracking partial.
- `UI/`: screen, virtualized card, detail panel, notification queue and mobile UI integration partial.

Editor code lives in `Assets/FloodFill/Editor/Achievements/`: authoring window, explicit asset installer/repair tool and isolated verification tools. Pure reducer tests live in `Tools/FloodAchievements.Tests/`.

Actual content lives in `Assets/FloodFill/Achievements/Definitions/` (112 assets), the achievement database, `Assets/FloodFill/Resources/FloodAchievements.asset`, and `Assets/FloodFill/Prefabs/UI/Achievements/` (screen, card, detail and notification prefabs). MainMenu and FloodFillCanvas prefabs contain navigation/notification bindings. AchievementScreen is an inactive, serialized child of SafeArea in the canvas, like the other screens. Navigation toggles that existing object; it never clones the screen. Achievement cards remain pooled prefab instances. Existing scenes reference the canvas prefab; no scene rewrite is needed.

The supplied achievement sheet is sliced into 112 stable sprites through Unity's sprite data provider. Existing stars, buttons, frames and audio are reused. Image pixels are unchanged. No runtime procedural icons, shaders, particles or artwork were added.

## Gameplay integration

Accepted actions and authoritative terminal outcomes drive progress. Existing mode, difficulty, level identity, size, move budget, score, timer, stars and secondary-goal state are reused. Additional tracking records attempts, failures, restarts, replays, strict personal-best improvements, final colors, target-color masks, no-progress moves, unassisted eligibility, paid Hint recommendations and follows, power-up uses, directly edited cubes, connected cubes, merged regions, multipliers, finish attribution, UTC days and ordered streaks.

Numbered levels are keyed by their actual mode/difficulty/identity. Custom records retain puzzle identity. Unique-level conditions prevent replay farming; count-based conditions use accepted eligible attempts. Catalog percentages derive from configured category capacity, rather than hardcoded totals. Size-specific tier overrides retain the PDF requirements.

Capture credit uses a persistent per-puzzle unique-cell bitmap. Restarting or changing roots does not repeatedly award already credited cubes. Each attempt also maintains a compact union-history forest: legitimate first connections of separate components count, but splitting and reconnecting a previously joined component cannot manufacture bridge progress. Direct edits, physically connected cubes and historically new connections are distinct statistics.

A paid Smart Hint is assisted play even if ignored. Following credit requires an accepted equivalent region/color action against the paid recommendation's board revision. Free previews, Editor-only automated solver runs, debug stock and unlimited mode invalidate eligibility. The in-game Development Tools solver counts toward achievements. Legacy attempts with unknown assistance history cannot earn unassisted-only conditions.

First records establish baselines; improvements require a strict improvement over an existing record. One-to-three-star progression includes an intermediate two-star result. Assigned/earned goal IDs are correlated per attempt. Goal-category mastery requires all expected level definitions to be known and all actual assigned goals earned; levels with no goals neither fabricate a goal award nor block mastery. A category must contain at least one real goal.

Gameplay scoring formulas, generation, seeds, budgets, gold ratings, inventory quantities and power-up effects are unchanged. Power-up hooks participate in existing transactional checkpoint/rollback paths.

## Persistence, Continue and recovery

Achievement state is separate: `Application.persistentDataPath/flood-fill-achievements-v1.json`, with atomic temporary-file replacement and a `.bak` recovery copy. Unsupported or unrecoverable data is preserved rather than overwritten. New fields in the existing session schema are additive; no existing save reset or version migration is required.

Attempts have stable GUIDs and action sequence numbers. A short pending-action outbox travels with session checkpoints and is removed only after durable achievement acknowledgement. Terminal receipts prevent duplicate wins/failures on repeated loads. Continue restores the same attempt and its connection history. Legacy Continue baselines elapsed time so old time is not counted twice. Restart abandonment is committed with the replacement playable checkpoint; a failed load does not falsely abandon the still-loadable save.

Writes are debounced (two seconds by default), with critical/lifecycle flushing. Active-time sampling uses existing gameplay time, excludes pause/loading/menu/background time, and is checkpointed periodically. UTC calendar conditions use an injectable clock and guard against basic backward-clock duplication. Local timestamps are not a server-authoritative anti-cheat mechanism.

On first use, reliable historical completion/stars/goals/bests and saved-definition metadata are imported quietly and idempotently. Metadata is read without rebuilding graph/witness solutions. Old totals, scores, assistance history, action counts, power uses, streaks and active days are not invented. Historical records do not imply proven manual/unassisted play. Previously completed imports are marked seen and do not replay old toasts.

The implementation and verification did not reset player progress or inventory. SHA-256 comparison confirmed all **1,077 pre-existing production save files** remained unchanged. Unity Play tests used separate company/product profiles.

## Rewards and future services

Every tier supports its own reward entries. Catalog rewards are empty by design. Claim state is separate from completion and persists per stable tier/reward ID. No reward handler is installed, so configuring a reward alone cannot fake a successful grant.

`IAchievementRewardService` is the integration boundary. A future economy adapter must grant the entire requested batch durably and idempotently using the supplied profile/tier/reward transaction token. Profile identity is persisted before use. Claims are marked only after success; failed calls can retry with the same token. Exactly-once external grants require the adapter to honor this contract.

`IAchievementCatalogProvider` supplies serializable DTOs to the reducer. ScriptableObjects provide the current local catalog; future Firebase/remote providers can supply the same validated DTOs and stable icon keys. JSON contains no Sprite references or machine-specific paths. No Firebase SDK, package, account or network call was added.

## Player UI and notifications

The screen includes category selection, All/In Progress/Completed filtering, reward filtering when configured, achievement/tier totals, completion percentage, progress bars and detail panels. Cards use three existing sprite star slots: one-tier completion fills all three; two-tier support maps first/full completion to one/three; three-tier definitions fill their actual slots.

Locked hidden entries conceal titles, descriptions, icons and requirements until revealed. Details distinguish progress, completion and claims. Attention remains for unseen completions. Cards are pooled/virtualized instead of creating 112 active rows. Button animation uses child visuals so touch targets remain stable.

Toasts are bounded to eight queued items, do not intercept raycasts and defer during the victory sequence. Overflow remains discoverable through unseen state/menu attention. Existing audio is optional. Opening an old save does not flood the player with historical notifications.

UI-only rendered previews: [Achievements](FloodAchievements-Screen.png), [Main Menu](FloodAchievements-Menu.png). The diagnostic screenshot camera renders UI only, so the existing 3D menu hero is absent from that preview; it was not removed from production.

## Authoring workflow

Open **Tools → Flood Fill → Achievement Menu**. Select an existing definition or create/duplicate one, configure category/icon/visibility/enabled state, condition/filter fields, one to three tiers and optional rewards. Fields are contextual to the metric; validation reports incompatible filters, IDs, tier ordering, missing icons and feasibility issues. Editing supports Undo. Duplication generates new IDs; normal edits retain IDs. Do not reuse deleted IDs or change persistent IDs for an existing player achievement.

The canvas Inspector includes AchievementScreen in its existing screen-preview switches. These toggle the authored screen outside Play Mode.

Export JSON writes definition DTOs only. Import validates both incoming data and the prospective merged catalog, previews new/updated/unchanged/disabled counts, and asks before applying. Merge uses stable IDs; unrelated definitions and all player progress remain intact. It is not a destructive replacement.

The **Isolated Testing** area uses a separate in-memory reducer and `UserSettings/FloodAchievementTestProgress.json`. It can simulate progress/wins, complete selected tiers, preview stars/hidden/reward states, reload definitions and reset only isolated test state with confirmation. Live and saved player data inspection is read-only. These Editor-only controls are not part of player builds. The explicit setup tool is for authoring repair, never startup catalog generation.

## Performance and validation

Evaluation is indexed by relevant events, not a full catalog scan every frame. Gameplay probes reuse buffers and inspect affected connectivity. UI updates visible rows; time events are sampled. Historical import reads lightweight metadata. Persistence stores compact summaries, unique keys/bitmaps and pending unacknowledged actions rather than permanent full move histories.

Validation results:

- **220 pure logic assertions passed**, including the actual 112-definition catalog, unique/replay rules, hidden/meta behavior, goal correlation, percentage masters, color sequences, improvements, clocks, reward retry tokens, pending checkpoints and legacy resumes. Parameterized mode/difficulty/size tests cover supported combinations in the reducer; these are not playthroughs of every generated board.
- **171 Unity assertions passed**: real asset/reference/sprite checks, JSON merge preservation, pooled UI/navigation/hidden behavior, actual paid power-up actions and Hint attribution, large legitimate Swap bridges, split/reconnect suppression, Continue identity, pause, final-budget victory, terminal deduplication and persistence without reward grants.
- Android/iOS script compilation is recorded in [Verification.txt](Verification.txt). Compilation is not an APK/IPA build or device performance/touch test.
- No actual Android/iOS device testing was performed. Long-duration calendar thresholds and all possible generated puzzles were not exhaustively played.

The isolated Unity project emitted an unrelated internal `UnityEditor.Search.SearchDatabase` cache exception on startup. Feature assertions completed successfully; no achievement exception was used to bypass a failed test. Verification does not require deleting production Editor caches.

## Mobile in-game solver correction

The original Development Tools solver explicitly marked attempts ineligible; level progression continued, but achievement checkpoints were ignored. In-game solver play now uses eligible normal gameplay outcomes. Editor-only simulations, debug inventory operations and unlimited mode retain their separate exclusions. A one-time import version 2 reconciles reliable saved numbered completions, including existing unfinished metadata records. It preserves capture deduplication, failures, completed tiers and claims, and does not fabricate historical action counters or streaks.
