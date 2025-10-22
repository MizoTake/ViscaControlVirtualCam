# ViscaControlVirtualCam

Unity 上で VISCA 互換コマンドにより仮想 PTZ カメラを制御する最小実装です。Pan/Tilt は Transform 回転、Zoom は Camera の FOV で表現します。サンプルシーン生成と、ACK/Completion を返す UDP/TCP Raw VISCA サーバーを含みます。

本リポジトリは UPM（Unity Package Manager）対応の埋め込みパッケージを同梱しています。
パッケージ名: `com.mizotake.viscavirtualcam`（`Packages/com.mizotake.viscavirtualcam`）。

## 要件
- Unity 2022.3 LTS（検証: `2022.3.62f2`）
- クライアントから UDP 52381 / TCP 52380 へ到達可能

## 特長
- UDP Raw VISCA（1 パケット = 1 フレーム、終端 `0xFF`）
- TCP Raw VISCA（ストリームを `0xFF` 区切りでフレーミング）
- PT 連続駆動、Zoom 可変速度、PT 絶対位置
- 速度カーブ（γ）、角度/FOV 制限、簡易 ACK/Completion/Error 応答

## クイックスタート
- Unity を開き、メニュー `Tools > Visca > Create PTZ Sample Scene` でサンプル作成
- 再生（Play）開始。既定で UDP `52381` / TCP `52380` を待受
- VISCA フレーム（16 進）例:
  - PT Drive: `81 01 06 01 VV WW PP TT FF`
  - Zoom Var: `81 01 04 07 ZZ FF`（`ZZ=2p:Tele, 3p:Wide, p=0..7, 00=Stop`）
  - PT Absolute: `81 01 06 02 VV WW p1 p2 p3 p4 t1 t2 t3 t4 FF`

- コントローラーには [ViscaCamLink](https://github.com/misorrek/ViscaCamLink)を使用

## コンポーネント
- `PtzControllerBehaviour`（呼び出し側 MonoBehaviour）
  - `panPivot`/`tiltPivot`、`targetCamera` を指定
  - 内部で純 C# の `PtzModel` を保持し、`Update` で Transform/FOV に反映
- `ViscaServerBehaviour`（呼び出し側 MonoBehaviour）
  - `transport`（`UdpRawVisca`/`TcpRawVisca`/`Both`）、`udpPort`、`tcpPort`、`replyMode`、`maxClients` を設定
  - 内部で純 C# の `ViscaServerCore` を起動し、`PtzViscaHandler` を介して PTZ を適用

## Pure C# コア
- `PtzModel` — PTZ 状態・コマンド・ステップ更新（MonoBehaviour 非依存）
- `ViscaServerCore` — UDP/TCP 受信・フレーミング・パース（MonoBehaviour 非依存）
- `PtzViscaHandler` — VISCA コマンドを `PtzModel` に橋渡し

## UPM インストール方法
- 埋め込み（同梱）: 本プロジェクトでは `Packages/com.mizotake.viscavirtualcam` が既に含まれています。
- 外部プロジェクトへ導入:
  - Unity メニューの「Window > Package Manager」→「+」→「Add package from disk...」で、本リポジトリ内の `Packages/com.mizotake.viscavirtualcam/package.json` を指定。
  - または Git URL を指定して追加（Git リモート公開時）。
    - 例（ブランチ指定）: `https://github.com/MizoTake/ViscaControlVirtualCam.git?path=Packages/com.mizotake.viscavirtualcam#main`
    - 例（タグ指定）: `https://github.com/MizoTake/ViscaControlVirtualCam.git?path=Packages/com.mizotake.viscavirtualcam#0.1.0`

## ビルド
- Editor: File → Build Settings → Build
- CLI: 必要なら `BuildScript.BuildStandalone` を用意

## トラブルシューティング
- 応答なし: ファイアウォール、`0xFF` 終端、1 パケット 1 コマンドを確認
- 軸定義: Pan=yaw（右+）、Tilt=pitch（上+）。Tele=FOV 減少、Wide=増加

## 仕様参照
- パッケージ内 `Packages/com.mizotake.viscavirtualcam/Documentation~` を参照（VISCA/PTZ 詳細）

## 外部リンク（Git 公開時）
- Repository: https://github.com/MizoTake/ViscaControlVirtualCam
- Documentation: https://github.com/MizoTake/ViscaControlVirtualCam/tree/main/Packages/com.mizotake.viscavirtualcam/Documentation~
- Changelog: https://github.com/MizoTake/ViscaControlVirtualCam/blob/main/Packages/com.mizotake.viscavirtualcam/CHANGELOG.md




