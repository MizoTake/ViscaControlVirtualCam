# 最小サブセット仕様: PTZ と速度のみ

- 対象: Pan / Tilt / Zoom(FOV) とその速度のみを扱う。
- 互換: 一般的な VISCA 実装（PTZOptics / Axis / Sony FR7 など）に沿う値とレンジに調整可能。
- トランスポート: UDP/52381（VISCA over IP 推奨。任意設定可）/ TCP（Raw VISCA フォールバック）。
- フレーミング:
  - VISCA over IP: 8B ヘッダ + 1–16B ペイロード（... 0xFF、1パケット=1フレーム）。
  - Raw（UDP/TCP）: ペイロードのみ `[0x8X, ..., 0xFF]`、1パケット/1フレーム。
- 応答: ACK（`90 4Z FF`）/ Completion（`90 5Z FF`）/ Error（`90 6Z EE FF`）。Z=ソケット番号（Raw は既定=1）。

---

## 1) Pan–Tilt Drive（相対・連続駆動）

- コマンド（共通ペイロード）
  - 形式: `8X 01 06 01 VV WW PP TT FF`
  - `VV`=Pan 速度, `WW`=Tilt 速度
  - `PP`=Pan 方向（L=01, R=02, Stop=03）
  - `TT`=Tilt 方向（U=01, D=02, Stop=03）
  - X=1（IP/通常）を既定とする

- 推奨レンジ（互換）
  - Pan 速度 `VV`: 0x01..0x18（24段、最小~最大）
  - Tilt 速度 `WW`: 0x01..0x14（20段）
  - 狭レンジ（0x01..0x07）クライアントは内部でスケールアップ（線形）
  - `0x00` は最小速度 `0x01` として扱う（Stop は `PP/TT=0x03` で表現）

- 動作
  - 受理後に ACK、適用（状態反映）完了で Completion を返送。
  - 逆方向/矛盾コマンドは約 200ms 待機 → キャンセル（`90 6Z 04 FF`）→ 新コマンド適用を推奨。
  - Stop 条件: 指定軸の `PP` または `TT` を `0x03`。両軸停止時に必要に応じて Completion を返す。

- Unity 適用（例）
  - 角速度: `omegaPan = panMax * map(VV) * dir(PP)`、`omegaTilt = tiltMax * map(WW) * dir(TT)`
  - `dir`: L/Down=-1, R/Up=+1, Stop=0
  - `map(v)`: `((v - vmin) / (vmax - vmin))^gamma`（`gamma=1.0` 既定）
  - 既定: `panMaxDegPerSec=120`, `tiltMaxDegPerSec=90`（設定で調整）

- エラー
  - 不正値/長さ: `Syntax`（`90 6Z 02 FF`）
  - 同時実行超過: `Buffer Full`（`90 6Z 03 FF`、ソニー 2 ソケット制限に準拠）
  - 実行不能: `Not Executable`（`90 6Z 41 FF`）
  - ソケット不正: `No Socket`（`90 6Z 05 FF`）

- 例
  - 左 中速 / 上 低速: `81 01 06 01 10 05 01 01 FF`
  - 両軸停止: `81 01 06 01 10 05 03 03 FF`
  - 右のみ 中速（チルト停止）: `81 01 06 01 10 05 02 03 FF`

---

## 2) Zoom（FOV 相当、可変速度）

- コマンド（共通ペイロード）
  - 形式: `8X 01 04 07 ZZ FF`
  - `ZZ`: `0x2p` Tele（ズームイン）, `0x3p` Wide（ズームアウト）, `0x00` Stop
  - `p`: 0..7（7=最速）。狭レンジクライアントは内部スケール対応。

- Unity 適用（例）
  - `omegaFov = zoomMaxFovPerSec * map(p) * (Tele?-1:+1)`
  - `camera.fieldOfView = Clamp(FOV + omegaFov * dt, minFov, maxFov)`
  - 既定: `zoomMaxFovPerSec=40`, `minFov=15`, `maxFov=90`, `gamma=1.0`

- エラー: Pan–Tilt と同様（Syntax/BufferFull/NotExecutable/NoSocket）

---

備考
- ここでは PTZ の速度のみを扱う。絶対位置/フォーカス/露出/プリセットは対象外。
- 実機差への追従は、速度レンジ/マッピング係数/ガンマを設定で調整して実現する。
