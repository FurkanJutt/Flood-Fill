The runtime mobile UI uses the new artwork in `Assets/Images/Spritesheets` through `Assets/FloodFill/Resources/FloodUISkin.asset`. This asset holds direct sprite references, so the skin is available in player builds without editor APIs or runtime texture slicing.

The menu, setup choices, gameplay HUD, palette, pause menu, settings controls, help/back buttons and results screen use the new sprites. Results keep live score/move text, show the crown for wins, and switch the action icon between next level and retry. Existing loading, input locks, audio bindings, saved settings and Show Current Block behavior remain in place.

Resizable backgrounds use nine-slice borders. The original artist slice names, rectangles and sprite IDs are retained. Text remains live TMP text. Import filtering is bilinear, mipmaps remain off, and the sheets allow a 4096 maximum size for clear UI icons. The touch hand is imported as a single sprite.

Restart Play Mode to rebuild an already-open runtime UI after changing artwork or skin references.

Verification: Unity 6000.5.8f1 in an isolated project copy. Existing native Play Mode checks passed (733 assertions). All skin sprite references resolved and menu, settings, setup, HUD, pause and results rendered at 1080×1920 with Metal. Previews inspect screen presentation; gameplay/loading behavior is covered by the Play Mode run. These checks are not a target-device performance benchmark.
