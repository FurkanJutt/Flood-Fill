New levels use framing padding 1.05 instead of 1.22. The camera still fits the board's bounding sphere, preserving room for rotation, but the initial portrait cube occupies about 80% of the screen width instead of about 68%.

The demo's board-surface clearance is 0.75 world units instead of 13. The outside-board collision constraint remains enabled, including its near-plane allowance. Removing the excessive fixed clearance prevents it from overriding proportional framing on small boards at wider aspect ratios. Voxel size and spacing are unchanged; every board size uses the same relative starting framing.

Unity 6000.5.8f1 batch verification covered sizes 6, 8, 10, 12, 15, 18 and 20 at tall-phone, standard-phone, tablet and landscape aspect ratios. It checked equal initial projected widths across sizes, board-corner visibility through yaw/pitch sweeps at starting zoom, near-plane safety, the outside-board zoom clamp and view reset. The minimum measured default-zoom orbit edge margin was 3.09%. Manual pan/zoom remain available. Existing saved camera views are still restored when continuing a session.

Raw results: `camera-framing-verification.txt`. Run `FloodFill.EditorTools.FloodCameraFramingVerification.Run` in an isolated batch project copy to repeat the checks.
