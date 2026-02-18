# VISCA Control Virtual Camera (UPM)

Blackmagic PTZ Controlに対応したVISCA互換PTZバーチャルカメラサーバーのUnityパッケージです。

## 機能

### コア機能
- **Pure C# 実装**: コアロジックにUnity依存なし
- **VISCA プロトコル対応**: UDP/TCP経由の標準VISCA（Raw VISCA）
- **VISCA Forwarding**: 仮想カメラと実機カメラへの同時/単独ルーティング
- **Blackmagic PTZ Control**: ATEM スイッチャー向け拡張サポート
- **永続的メモリプリセット**: PlayerPrefsを使ったカメラ位置の保存/呼び出し
- **拡張可能なコマンドシステム**: インターフェースベースのアーキテクチャでカスタムコマンド追加が容易

### 対応コマンド
- **Pan/Tilt**: Drive（速度制御）とAbsolute（位置制御）
- **Zoom**: 可変速度とDirect位置指定
- **Focus**: 可変速度とDirect位置指定（Blackmagic）
- **Iris**: 可変制御とDirect位置指定（Blackmagic）
- **メモリプリセット**: SetとRecallで永続化対応（Blackmagic）

### アーキテクチャ
- **Runtime**: Pure C# PTZモデルとVISCAサーバーコア
- **Commands**: レジストリパターンによるモジュラーコマンドクラス
- **Behaviours**: Transform/Cameraに結果を適用する薄いMonoBehaviour層
- **Editor**: カスタムインスペクターとシーンセットアップツール

## インストール

### 方法1: Git URL（推奨）
Unity Package Managerから直接インストール:
1. Unity Package Manager を開く（Window → Package Manager）
2. "+" ボタンをクリック → "Add package from git URL..."
3. 以下のURLを入力:
```
https://github.com/MizoTake/ViscaControlVirtualCam.git?path=/Packages/com.mizotake.viscavirtualcam
```

### 方法2: 埋め込みパッケージ
このリポジトリをクローンして使用する場合、パッケージは `Packages/com.mizotake.viscavirtualcam` にあります。

### 方法3: ローカルディスク
ダウンロードしたパッケージをローカルから追加:
1. Unity Package Manager → "+" → "Add package from disk..."
2. パッケージフォルダ内の `package.json` を選択

## クイックスタート

### 1. サンプルシーンの作成
- メニュー: `Tools > Visca > Create PTZ Sample Scene`
- 設定済みのPTZカメラリグを含む新しいシーンが作成されます

### 1.5. サンプルプリセットの導入（任意）
- Unity Package Managerの **Samples** から `PTZ Preset Samples` をインポート
- Indoor/Outdoor/Fast/BRC-X400/パナソニック AW-HE40/PTZOptics PT20X と BRC-X400 チューニングが含まれます

### 2. 設定
- **ViscaServerBehaviour**: ネットワーク設定（UDP/TCPポート、ログ）
- **Operation Mode**:
  - `VirtualOnly`: Unityのみ制御（既存動作）
  - `RealOnly`: 実機へ転送し、実機応答をリレー
  - `Linked`: Unityと実機を同時制御（応答は実機のみリレー）
- **Real Camera Forwarding**: 実機IP/UDPポート（既定 `192.168.1.10:52381`）
- **PtzControllerBehaviour**: メモリの永続化、カメラの可動範囲
- **PtzSettings**: 速度カーブ、移動の減衰、FOV範囲、Pan/Tilt反転（速度/絶対位置別）、望遠時の速度抑制
- **PtzSettings 追加項目**: 最小速度・プリセット速度・スローパン/チルト・レンズプロファイル（センサー/焦点距離）・ズーム位置速度モード
- **PtzTuningProfile（任意）**: 物理機っぽい加減速・減速上限、目標ブレーキ方式、停止距離、速度スムージングを設定可能
- **Pending Queue Limit**: ViscaServerBehaviourでキュー上限（既定64、ソケット単位）を調整し、ビジー時は `Buffer Full(0x03)` を返す挙動を設定可能
- **PTZプリセット生成**: `Tools > Visca > Create PTZ Presets (Indoor Outdoor Fast BRC-X400 AW-HE40 PT20X)` で複数プリセットを生成

### 3. 実行
- 再生ボタンを押す
- デフォルトポート: UDP `52381`, TCP `52380`
- ATEMスイッチャーやVISCAコントローラーから接続

## メモリプリセット

メモリプリセットは自動的にPlayerPrefsに保存され、Unityセッションをまたいで永続化されます。

### 永続化の有効/無効
- `PtzControllerBehaviour` → Memory Presets → Enable Persistent Memory
- キープレフィックスはプロジェクトごとにカスタマイズ可能

### VISCAコマンド
- **Memory Set**: `8X 01 04 3F 01 pp FF` （現在位置をプリセット `pp` に保存）
- **Memory Recall**: `8X 01 04 3F 02 pp FF` （プリセット `pp` を呼び出し）

### 対応範囲
- プリセット0-9が起動時に自動ロードされます
- 各プリセットに保存される値: Pan, Tilt, FOV, Focus, Iris

## ドキュメント

詳細は `Documentation~/` フォルダを参照してください:
- **CustomCommands.md**: カスタムVISCAコマンドの追加方法
- **ViscaCommandCoverage.md**: 実装済みコマンド一覧

## ログ設定

`ViscaServerBehaviour` でログを設定できます:
- **Verbose Log**: 一般的なログ（接続、エラーなど）
- **Log Received Commands**: 受信コマンドの詳細ログ（パラメータ付き）
- **Log Level**: 重要度でフィルタ（None/Errors/Warnings/Info/Commands/Debug）

## ネットワークプロトコル

### UDP Raw VISCA（デフォルト: ポート 52381）
- コネクションレス、1パケット1フレーム
- ローカルネットワーク制御に最適

### TCP Raw VISCA（デフォルト: ポート 52380）
- コネクション指向、フレームフレーミング対応
- 複数クライアント対応（最大数は設定可能）

## 拡張

### カスタムコマンドの追加

1. **コマンドクラスの作成**（`Runtime/Core/Commands/` 内）
```csharp
public class MyCommand : ViscaCommandBase
{
    public override string CommandName => "MyCommand";
    public override bool TryParse(byte[] frame) { /* ... */ }
    public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder) { /* ... */ }
    public override string GetDetails(byte[] frame) { /* ... */ }
}
```

2. **ハンドラーインターフェースに追加**
```csharp
// IViscaCommandHandler.cs 内
bool HandleMyCommand(byte param, Action<byte[]> responder);
```

3. **ハンドラーの実装**
```csharp
// PtzViscaHandler.cs 内
public bool HandleMyCommand(byte param, Action<byte[]> responder) { /* ... */ }
```

4. **コマンドの登録**
```csharp
// ViscaCommandRegistry.cs の RegisterDefaultCommands() 内
Register(new MyCommand());
```

完全な実装例は `Documentation~/CustomCommands.md` を参照してください。

## 動作環境

- Unity 2020.3 以降
- .NET Standard 2.1
