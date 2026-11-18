The demo uses saved UI prefabs in `Assets/FloodFill/Prefabs/UI`. The scene contains a connected `FloodFillCanvas` instance; it no longer generates buttons, panels or labels when Play Mode starts.

To edit visually:

1. Open `Assets/FloodFill/Scenes/FloodFill3DDemo.unity` and select `FloodFillCanvas`.
2. On its `Flood Fill Mobile UI 3D` component, use **Preview MainMenu**, **Preview Settings**, or another preview button. These work outside Play Mode.
3. Expand `SafeArea / <screen> / Content` and edit the RectTransforms, Images and TMP text. Each screen is also a separate nested prefab that you can open directly from the Project window.
4. Save the screen prefab, or apply your scene instance overrides to the appropriate prefab. Changes made only during Play Mode remain temporary, as with any Unity object.

`FloodFillCanvas.prefab` holds the Canvas settings, safe area, development FPS overlay, and serialized controller references. The screen assets are `MainMenu`, `GameSetup`, `GameplayHUD`, `HowToPlay`, `Settings`, `PauseMenu`, `GameResults` and `LoadingScreen`.

The `Content` frame is authored at 1080×1920. `FloodFillUIFit3D` scales that frame to fit the available viewport without rewriting its children's positions or sizes. Keep editing the children of Content; disable this fitting component if you intentionally want your own screen scaling. Layout Groups still control their children according to their Inspector settings—for example, menu button spacing is set by the MenuButtons Vertical Layout Group. Disable a Layout Group before positioning those children freely.

Buttons have a `FloodFillUIAction3D` component and a saved On Click event pointing to its `Invoke` method. Its Action and Index identify the operation; the object name does not. A button can therefore be renamed or moved within the Canvas without losing its behavior. Keep that event when restyling a control. If you replace a live HUD element or choice button, update the corresponding serialized reference/list on the Canvas controller.

For dynamic choices and switches, edit the Normal/Selected sprites and colors on `FloodFillUIState3D`. Static controls use the Image component directly. `FloodUISkin.asset` is now an editor construction template; changing it does not restyle existing prefab instances automatically.

Live values such as moves, score, selected color, Continue availability, and saved audio settings still update from gameplay. Those state changes preserve authored layout. The menu's decorative 3D cube is still prepared at runtime independently of the UI, and the development FPS overlay is enabled only in Editor/development players.

The installer at **Tools > Flood Fill > UI > Install Editable UI in Demo Scene** reconnects the existing Canvas prefab if needed. It does not regenerate or overwrite existing prefab assets. Regenerating the 3D demo also reuses the authored UI assets.

Verification used an isolated Unity 6000.5.8f1 project copy: gameplay/audio Play Mode checks; nested prefab and saved-event checks; saving, reloading, renaming and resizing controls; edits to text and selection visuals; and a launch through the renamed Play button. All eight authored screens also rendered outside Play Mode at phone and tablet dimensions. Android and iOS managed player assemblies compiled; native device builds and device frame-rate measurements were not part of these checks.
