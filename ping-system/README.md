# ping-system

Point at something and press **middle mouse** to ping it. Everyone on your team who has the mod sees the marker.

- **Visible through walls.** Off-screen pings stick to the edge of your screen.
- **Smart labels.** Pings show what you pointed at: `Enemy: <name>`, `Valuable $1,200` (updates live as it takes damage), item names, player names, `Cart`, `Extraction`, or just `Here`.
- **Follows moving objects.** Ping a valuable or an enemy and the marker stays on it.
- **Player colors.** Each player's pings have their own color and show their name and the distance.
- One ping per player at a time. A new ping replaces your old one, and pings fade after 6 seconds.

## Configuration

Edit in r2modman's **Config editor** (`wuguishifu.PingSystem.cfg`) or in game via REPOConfig.

| Setting | Default | Description |
|---|---|---|
| `PingKey` | `<Mouse>/middleButton` | Input System binding path, e.g. `<Keyboard>/g`, `<Keyboard>/z`, `<Mouse>/forwardButton` |
| `PingDuration` | `6` | Seconds a ping stays visible (1–60) |
| `MaxDistance` | `150` | How far you can ping, in meters |
| `Cooldown` | `0.4` | Minimum seconds between your pings |
| `MarkerScale` | `1` | Size of markers and text (0.5–3) |

## Multiplayer

Every player who wants to send or see pings needs the mod. Players without it won't see anything and aren't affected. The host doesn't need it if they don't want to use pings.

## Changelog

### 1.0.0
- Initial release.
