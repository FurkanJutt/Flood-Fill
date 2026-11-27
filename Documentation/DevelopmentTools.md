# In-game development tools

Open **Settings → Development Tools**. The panel is authored under the canvas SafeArea, uses the existing UI styles and appears in the canvas Inspector's screen-preview list.

- **Start Auto Solver** analyzes the current unfinished board on a background task using the shared solver engine, then returns to gameplay and plays its validated plan. A plan exceeding the remaining move budget is reported and is not played. Load or Continue a level first.
- **Stop Solver** cancels analysis/playback. Opening Settings also stops playback; opening the development panel pauses the game. Back returns to Settings.
- **Restart Level** uses the existing restart flow. **Next Level** starts the next numbered level only when the current level has a completion record, and never advances beyond level 100.
- **Show FPS** controls the existing FPS counter and saves the preference. The counter updates only while visible and is available in player builds.
- **Refill Power-ups** adds stock up to each power-up's configured maximum. It uses the existing inventory persistence and reports save failures. These are development grants, not achievement rewards.

The compact status shows level, moves, elapsed game time, solver status and inventory counts. In-game solver moves count toward achievements through the normal gameplay checkpoint flow. Development refills still mark the current attempt ineligible; Editor-only solver simulations remain excluded. Generation, seeds, move budgets, scoring and actual power-up behavior are unchanged.

The panel and Settings button are serialized in FloodFillCanvas; no runtime screen creation is used. Editor repair: **Tools → Flood Fill → UI → Install Development Tools**. Script: `Assets/FloodFill/Scripts/ThreeD/UI/Mobile/FloodFillMobileUI3D.Development.cs`.

Verification used an isolated Unity player profile: panel navigation, FPS preference, capped inventory refill, an actual asynchronous solver move, cancellation, and solver achievement progress passed. Android/iOS compilation is checked separately; physical device behavior has not been tested.

Final Unity integration verification passed, including the Settings button’s serialized click handler. Android and iOS production scripts compiled successfully (70 assemblies each). No existing serialized canvas objects were removed.

Mobile achievement correction: older builds explicitly excluded the in-game solver. The updated solver counts its wins. A one-time migration of saved numbered-level records restores reliable completion/star/goal/best-time achievements, preserving existing progress and claim state without inventing action totals. Legacy unfinished solver checkpoints become eligible for reliable outcomes with incomplete-history restrictions retained.

Regression for mobile achievement eligibility: seven consecutive actual in-game solver wins passed; First Steps completed, Easy Going reached 7/10, and loading the saved achievement file retained both. The regression harness uses an isolated profile and overrides only its batch-run exclusion to simulate ordinary device eligibility. Physical device testing remains outstanding.
