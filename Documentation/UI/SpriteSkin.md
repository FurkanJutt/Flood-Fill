The UI is now authored in `Assets/FloodFill/Prefabs/UI`, with a connected `FloodFillCanvas` instance in the demo scene. See [EditingPrefabs.md](EditingPrefabs.md) for the visual editing workflow.

Static artwork is assigned directly on prefab Images. Choices and switches use `FloodFillUIState3D` for their Normal/Selected sprites and colors. The original sprite-sheet slice names, rectangles and sprite IDs are retained.

`Assets/FloodFill/Resources/FloodUISkin.asset` supplies the editor's initial construction template. Runtime binds the existing prefab controls and updates game state; it does not generate or restyle their layout from this template. The editor-only builder is excluded from player compilation.
