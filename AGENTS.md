# Repository Guidelines

## Project Structure & Module Organization
- `Assets/ViscaControlVirtualCamera/` — source, docs, and future scripts.
- `Assets/Scenes/` — sample scenes (e.g., `SampleScene.unity`).
- `Packages/` — Unity package manifest and dependencies.
- `ProjectSettings/` — project config (Unity 2022.3.62f2).
- `Library/`, `Logs/`, `Temp/`, `UserSettings/` — generated; do not commit.

## Build, Test, and Development Commands
- Open in Editor: use Unity Hub with the recorded version (`ProjectSettings/ProjectVersion.txt`).
- Build (batchmode example):
  - `"<UnityPath>\\Unity.exe" -batchmode -quit -projectPath "<repo>" -executeMethod BuildScript.BuildStandalone -logFile Logs/build.log`
  - If no `BuildScript`, build via Editor: File → Build Settings.
- Run tests (Unity Test Framework):
  - `"<UnityPath>\\Unity.exe" -batchmode -quit -projectPath "<repo>" -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml -logFile Logs/tests.log`
  - Replace `EditMode` with `PlayMode` for play mode tests.

## Coding Style & Naming Conventions
- Language: C# (Unity). Indentation: 4 spaces; UTF-8.
- Braces: Allman style. One public type per file.
- Naming: `PascalCase` for types/methods/properties; `camelCase` for locals/params; `_camelCase` for private fields; constants `UPPER_SNAKE`.
- Namespaces: `ViscaControlVirtualCam.*`.
- Layout: place scripts under `Assets/ViscaControlVirtualCamera/Scripts/` mirroring feature folders.

## Testing Guidelines
- Framework: Unity Test Framework (NUnit). Put tests in `Assets/ViscaControlVirtualCamera/Tests/`.
- Names: mirror target file and suffix with `Tests` (e.g., `PtzControllerTests.cs`).
- Keep tests deterministic; avoid scene/global state when possible. Prefer dependency injection and interfaces for I/O.

## Commit & Pull Request Guidelines
- Commits: follow Conventional Commits (e.g., `feat: add PTZ absolute move`).
- PRs: include purpose, linked issues, testing notes, and screenshots/GIFs for scene/UI changes. Update docs under `Assets/ViscaControlVirtualCamera/Docs/` when protocols or behavior change.

## Security & Configuration Tips
- Keep `.meta` files; enable Force Text serialization. Never commit secrets.
- Large binaries (videos/textures): consider Git LFS.
- Do not modify `ProjectSettings/` casually; document any required changes in the PR.

## Agent-Specific Notes
- Scope your edits to `Assets/ViscaControlVirtualCamera/` unless explicitly requested. Respect these guidelines across the entire repo tree.
