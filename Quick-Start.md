# Quick Start

> 🇨🇳 中文版：[快速上手.md](快速上手.md) · Full docs: [README.md](README.md)

---

## Launch

**Double-click `启动VirtualBT.bat`**

(Or search "VirtualBT" in the Start menu.)

---

## Control your phone with the PC keyboard and mouse

```
┌─ PC ───────────────┐          ┌─ Phone ─────────┐
│ 1. Bluetooth ON    │          │                  │
│ 2. Virtual Keyboard│ Bluetooth│ 3. Phone's BT    │
│    page            │ ────────▶│    settings:     │
│ 3. Enable          │          │    pair with PC  │
│    advertising ────┼───┐      │                  │
│ 4. Device appears  │   │      │                  │
│    in the list     │   └─────▶│ 5. Receives keys │
└────────────────────┘          └──────────────────┘
```

**Important**: the **phone pairs with the PC**, not the other way round. A phone is not a BLE peripheral, so the PC cannot find it from the scanning page.

---

## Play games (mouse to aim)

1. Phone is connected (shows in the client list)
2. Click **"Enter Mouse Capture (Fullscreen)"**
3. Play

| Key | Action |
|---|---|
| `Esc` (rebindable in Settings) | Leave fullscreen |
| `F1` | Show/hide hint overlay |
| `Numpad + / -` | Sensitivity |
| Everything else | Sent to the phone (and never to the PC window) |

---

## Switch 中文 / English

**Settings → Language → 中文 / English** → restarts automatically

---

## Something wrong?

| Symptom | Fix |
|---|---|
| `RadioNotAvailable` error | **Turn Bluetooth on first**; then close Phone Link / Your Phone |
| Phone can't see the PC | Make sure advertising is enabled on the PC; tap Scan on the phone |
| "Pair" says no permission | Normal — wrong direction (see diagram above) |
| Mouse stops at screen edge | v1.2.0 warps the pointer back automatically; if capture mode reports it cannot move the pointer, note what the status line says (`回中 win32` / `injector` / `none`) and report it |
| Title bar appears at the top edge | Settings → Game-mode fullscreen → **Exclusive** (no title bar, nothing to summon) |

More in [README → Troubleshooting](README.md#troubleshooting).
