# VISCA Control Virtual Camera

Unity上でVISCAプロトコルによるPTZカメラ制御をシミュレートするパッケージです。

## サンプルシーン

`Assets/ViscaControlVirtualCamera/Scenes/PTZ_Sample.unity` を開いて実行してください。

## 機能

### VISCAサーバー

Blackmagic ATEM等のスイッチャーからのVISCA制御を受信し、Unity上のVirtual Cameraを制御します。

#### 対応コマンド
- **標準VISCA**: Pan/Tilt Drive, Pan/Tilt Absolute, Zoom Variable
- **Blackmagic拡張**: Zoom Direct, Focus Variable/Direct, Iris Variable/Direct, Memory Recall/Set

### ログ機能

受信したVISCAコマンドの詳細をUnityコンソールに出力できます。

#### ViscaServerBehaviour インスペクタ設定

**Logging** セクション:
- **Verbose Log**: 一般ログを有効化
- **Log Received Commands**: 受信コマンドの詳細ログを有効化
- **Log Level**: ログフィルタリングレベル（None/Errors/Warnings/Info/Commands/Debug）

実行中にログレベルを動的に変更できます。

詳細は `Packages/com.mizotake.viscavirtualcam/Documentation~/CommandLogging.md` を参照してください。

### プリセット（ScriptableObject）

- 作成: Project ビューの `Create > Visca > PTZ Settings` または `Tools > Visca > Create PTZ Presets (Indoor/Outdoor/Fast)`
- 差し替え: `PtzControllerBehaviour.settings` を入れ替えると挙動が変わります
- 即時反映: インスペクタの `Apply Settings Now` ボタンで適用（実行中でも可）

## 接続方法

1. サンプルシーンを実行
2. ViscaServerBehaviour の設定を確認（デフォルト: UDP 52381ポート）
3. Blackmagic ATEMなどから `<UnityのIPアドレス>:52381` に接続
4. ATEMからPTZ制御を送信すると、Unity上のカメラが動作します

## ログ出力例

```
[VISCA] VISCA UDP server started on 52381
[VISCA] RX: PanTiltDrive: PanSpeed=0x10, TiltSpeed=0x05, PanDir=Right, TiltDir=Up [81-01-06-01-10-05-02-01-FF]
[VISCA] RX: ZoomDirect: ZoomPos=0xABCD (43981) [81-01-04-47-0A-0B-0C-0D-FF]
[VISCA] RX: MemoryRecall: MemoryNumber=5 [81-01-04-3F-02-05-FF]
```
