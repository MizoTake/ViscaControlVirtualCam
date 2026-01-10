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
- VISCA over IP behavior: replies must echo the socket nibble (low 4 bits) in `90 4z/5z/6z` and, when an 8-byte VISCA-IP header is received, responses should include a `0x01 0x11` header that mirrors the incoming sequence. Default ports are UDP 52381 / TCP 52380.
- Command Cancel: `8X 2Z FF` is a valid control command and should return `90 6z 04 FF` (CommandCanceled) using the socket from the `2Z` byte.
- Tests: new EditMode tests live under `Packages/com.mizotake.viscavirtualcam/Tests/EditMode/` (e.g., `ViscaResponseTests.cs`) and verify socket-aware replies and cancel handling.
- ドキュメント更新ルール: コード/テストに仕様変更・機能追加を入れた場合は、必ず対応する文書（`Packages/com.mizotake.viscavirtualcam/Documentation~/` 配下や README 類）も更新すること。
- 実装メモ: コマンド処理キュー上限（既定64）は `ViscaServerBehaviour.pendingQueueLimit` で調整可能。ビジー時は `Buffer Full(0x03)` を返す。
- 実装メモ: Pan/Tilt の制御向きは `PtzSettings` の `invertPan`/`invertTilt` で反転設定できる。
- 参照ドキュメント: ソニー FR7 VISCA over IP 仕様書（ILME-FR7_OH_5042055031） https://www.sony.jp/professional/support/manual_pdf/c_c/ILME-FR7_OH_5042055031.pdf （必要に応じて確認すること）。
- 追加設定: PtzSettings で絶対位置反転（invertPanAbsolute/invertTiltAbsolute）を選択可能。PtzTuningProfile で加速度上限を設定し実機寄りの挙動を試せる。
