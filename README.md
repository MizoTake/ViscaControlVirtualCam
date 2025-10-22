# ViscaControlVirtualCam

Unity 上で VISCA 互換コマンドにより仮想 PTZ カメラを制御する最小サーバー実装です（Pan/Tilt は Transform 回転、Zoom は Camera の FOV）。サンプルシーン生成と、ACK/Completion を返す UDP/TCP Raw VISCA サーバーを含みます。

## 要件
- Unity 2022.3 LTS（検証: `2022.3.62f2`）
- クライアントから UDP 52381 へ到達可能であること

## 特長
- UDP Raw VISCA（1 データグラム = 1 フレーム、終端 `0xFF`）
- TCP Raw VISCA（ストリームを `0xFF` 区切りでフレーミング）
- PT 連続駆動、Zoom 可変速度、PT 絶対位置
- 速度カーブ（γ）、角度/FOV 制限、簡易 ACK/Completion/Error 応答

## クイックスタート
- Unity でプロジェクトを開く。
- メニュー: `Tools > Visca > Create PTZ Sample Scene` でサンプル作成。
- 再生（Play）開始。既定で UDP `52381`、TCP `52380` を待受。
- VISCA フレーム（16 進バイト列）を送信:
  - PT Drive: `81 01 06 01 VV WW PP TT FF`
  - Zoom Var: `81 01 04 07 ZZ FF`（`ZZ=2p:Tele, 3p:Wide, p=0..7, 00=Stop`）
  - PT Absolute: `81 01 06 02 VV WW p1 p2 p3 p4 t1 t2 t3 t4 FF`

例（左＋上 中速）: `81 01 06 01 10 05 01 01 FF`

## コンポーネント
- `ViscaServer`（任意の GameObject に付与）
  - `udpPort`（既定 `52381`）、`replyMode`（AckOnly/AckAndCompletion/None）、`verboseLog`。
  - `transport`（`UdpRawVisca` / `TcpRawVisca` / `Both`）。`tcpPort`（既定 `52380`）、`maxClients`。
  - メインスレッドで適用するため `PtzController` にバインドします。
- `PtzController`
  - `panPivot`/`tiltPivot` に回転軸、`targetCamera` に FOV 対象カメラ。
  - 制限: `pan/tilt/zoom` 最大速度、FOV `[min,max]`、角度レンジ。
  - 速度カーブ: `speedGamma`（VISCA 速度→deg/s 変換）。

## ビルド
- Editor: File → Build Settings → Build。
- CLI: バッチビルドが必要な場合は `BuildScript.BuildStandalone` を用意してください。

## トラブルシューティング
- 応答がない: ファイアウォール、`0xFF` 終端、有効な 1 パケット 1 コマンドを確認。
- 軸: Pan = yaw（右が正）、Tilt = pitch（上が正）。Tele は FOV 減少、Wide は増加。

## 備考
- 詳細仕様は `Assets/ViscaControlVirtualCamera/Docs/` を参照。
- 今後: Sony VISCA-over-IP ヘッダ対応、TCP、プリセット/フォーカス/露出 等。
