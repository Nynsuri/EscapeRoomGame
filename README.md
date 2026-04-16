# Escape Room Game

A 3D first-person escape room game built in **Unity 6000.3.9f1** with **C#**. Progress through four uniquely themed rooms, solve puzzles, collect hidden items, and escape — but your choices determine which ending you get.

---

## About

This project was developed as a bachelor's thesis. The game challenges players to solve a variety of logic, mechanical, and observation-based puzzles across four rooms. A hidden collectible system tracks how thorough the player is, unlocking either a standard or a secret "good" ending.

---

## Rooms

| # | Room | Theme |
|---|------|-------|
| 1 | The Dark Room | Mystery / Exploration |
| 2 | The Spaceship | Sci-Fi |
| 3 | The Western Bar | Wild West |
| 4 | The Chemistry Lab | Science |

---

## Puzzles

**Room 1 — The Dark Room**
- Wire Trace Puzzle
- Clock Puzzle
- Cable Box Puzzle

**Room 2 — The Spaceship**
- Engine Calibrator Puzzle (4 engines with slider + keyboard input; a secret answer unlocks a hidden reward chamber)
- Hacking Puzzle
- Circuit Repair Puzzle

**Room 3 — The Western Bar**
- Piano Puzzle
- Bottle Puzzle
- Shooting Puzzle

**Room 4 — The Chemistry Lab**
- Chemistry Puzzle
- Dissolvable Box
- Keycard Lock

---

## Controls

| Input | Action |
|-------|--------|
| `W A S D` | Move |
| `Mouse` | Look around |
| `Left Shift` | Sprint |
| `Space` | Jump |
| `Left Ctrl` | Crouch |
| `E` | Interact / Pick up |
| `Tab` | Open / close inventory |
| `1–8` | Select inventory slot |
| `Esc` | Pause menu |

---

## Endings

The game has two endings determined by how many collectibles you find and place on their pedestals before reaching the exit:

- **Bad ending** — stay trapped without all collectibles
- **Good ending** — find and place all collectibles before leaving

---

## Architecture Highlights

### Pause System
All puzzles inherit from `BasePuzzle`, which manages a static active-puzzle counter. `PauseMenuManager` checks `BasePuzzle.AnyPuzzleActive` before allowing ESC to open the pause menu — pause is fully blocked while any puzzle UI is open or unresolved.

### Collectible System
`CollectibleManager` is a singleton that tracks picked-up and placed collectibles. It fires an `OnAllPlaced` event when all pedestals are filled. `EndingTrigger` reads this state at the exit to determine which ending scene to load.

### Inventory
Slot-based inventory (8 slots) with `Tab` to toggle. Item use is context-sensitive via the interaction system.
