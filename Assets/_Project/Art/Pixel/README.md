# Pixel Art Foundation

- Author sprites at 32 pixels per Unity unit.
- Store production art by purpose; keep temporary verification sprites under `Test`.
- PNG files below this folder are imported as single, full-rect sprites with Point filtering, no mip maps, and no compression.
- Use `Nyoice/Pixel Art/Apply Import Settings` after adding or replacing source images.
- Drawing order is Environment `0`, Urinals `10`, NPC `20`, NPC Gauge `30`, and World UI `40`.
- Switch the generated stage with `Nyoice/Pixel Art/Use Legacy Visuals` or `Use Pixel Visuals`.
