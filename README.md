# Forsaken

Forsaken is a 2D action-platformer prototype built in Unity. The project explores fast movement, combat, revival/checkpoint systems, and atmospheric presentation.

## Project Overview

- **Genre:** 2D action platformer
- **Engine:** Unity `6000.0.44f1`
- **Primary focus:** responsive movement, combat loops, enemy behaviors, and progression systems

## Current Gameplay Scope

- Player movement with platforming controls
- Melee and ranged combat systems
- Enemy AI behaviors
- Checkpoints and respawn flow
- Interactive loot/chest flow
- Early UI and menu systems

## Repository Structure

```text
.
├── Forsaken/                 # Unity project root
├── assets.md                 # Third-party asset tracking
├── progress1.md              # Historical project status report
├── progress2.md              # Historical project status report
├── progress3.md              # Status report template
├── progress4.md              # Status report template
└── writeup.md                # Final postmortem template
```

## Getting Started

1. Install Unity Hub.
2. Install Unity Editor version `6000.0.44f1`.
3. Open Unity Hub and add the project at:
   `/home/runner/work/Forsaken/Forsaken/Forsaken`
4. Open the project and load a scene from `Assets/Scenes`.
5. Press **Play** to run locally.

## Build (WebGL)

1. Open **File → Build Profiles** in Unity.
2. Select **WebGL**.
3. Build into an output folder (for example, the existing root-level web build files).
4. Verify output locally before publishing.

## Contributing

- Use short-lived feature branches.
- Keep commits scoped and descriptive.
- Open pull requests with a clear summary of gameplay impact.
- Update documentation when systems or assets change.

## Documentation Index

- `/assets.md` — source and usage tracking for external assets.
- `/progress1.md` and `/progress2.md` — archived status reports.
- `/progress3.md` and `/progress4.md` — reusable status report templates.
- `/writeup.md` — final project postmortem template.
