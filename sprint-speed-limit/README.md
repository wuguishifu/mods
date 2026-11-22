# sprint-speed-limit

Caps how many **sprint speed upgrades** each player can have. Once a player hits the cap, any further sprint speed upgrades they use are consumed and do nothing.

## Configuration

| Setting | Default | Range | Description |
|---|---|---|---|
| `MaxSprintSpeedUpgrades` | `3` | 0–100 | Maximum sprint speed upgrades per player. |

Edit it in r2modman's **Config editor** (`wuguishifu.SprintSpeedLimit.cfg`), in game via REPOConfig, or in `BepInEx/config/wuguishifu.SprintSpeedLimit.cfg`. Changes apply to the next upgrade used, with no restart needed.

## Multiplayer

**All players should install the mod and use the same limit.** Each client applies upgrades locally, so a player without the mod may see the upgrade apply on their end.

## Notes

- Works with mods that grant sprint upgrades through the vanilla upgrade path (e.g. MoreUpgrades, BetterTeamUpgrades).
- Players who are already above the limit in an existing save keep their current level; they just can't gain more.

## Changelog

### 1.0.0
- Initial release.
