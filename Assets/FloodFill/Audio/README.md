The demo scene already contains this audio prefab. Assign clips on FloodAudioManager before Play; during Play, select its persistent runtime instance and use **Refresh / Reapply Audio Settings**.

The manager creates two music sources and a reusable SFX pool automatically. Do not add sources or wire cube/button audio manually. The existing Settings menu controls its real mixer volumes.

Full setup, tuning, fallback behavior, import recommendations, file inventory and verification results: `Documentation/AudioSystem.md` at the project root.
