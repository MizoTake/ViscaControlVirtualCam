# VISCA Command Logging

## 概要

受信したVISCAコマンドの詳細をログに出力する機能です。Blackmagic ATEM等のスイッチャーから送信されるコマンドの内容を確認できます。

## Unityエディタからの設定

### ViscaServerBehaviour インスペクタ

`ViscaServerBehaviour` コンポーネントのインスペクタから簡単にログ設定を変更できます:

![ViscaServerBehaviour Inspector](inspector-logging.png)

#### Logging セクション

1. **Verbose Log**: 一般的なログを有効化（接続イベント、エラーなど）
2. **Log Received Commands**: 受信した全VISCAコマンドの詳細ログを有効化
3. **Log Level**: ログレベルを選択
   - **None**: ログ出力なし
   - **Errors**: エラーのみ
   - **Warnings**: エラーと警告
   - **Info**: エラー、警告、接続イベント
   - **Commands**: すべての受信コマンド（詳細）
   - **Debug**: すべてのデバッグ情報を含む

#### 実行時の制御

プレイモード中は、インスペクタから以下の操作が可能です:
- **Start Server** / **Stop Server**: サーバーの起動/停止
- **Runtime Log Level**: 実行中にログレベルを動的に変更

## コードからの設定

`ViscaServerOptions` でログ出力を制御できます:

```csharp
var options = new ViscaServerOptions
{
    VerboseLog = true,              // 一般的なログを有効化
    LogReceivedCommands = true,     // 受信コマンドの詳細ログを有効化
    Logger = Debug.Log              // Unity のログに出力
};
```

## ログ出力例

### Pan/Tilt Drive (可変速度移動)
```
RX: PanTiltDrive: PanSpeed=0x10, TiltSpeed=0x05, PanDir=Right, TiltDir=Up [81-01-06-01-10-05-02-01-FF]
```

### Pan/Tilt Absolute (絶対位置移動)
```
RX: PanTiltAbsolute: PanSpeed=0x10, TiltSpeed=0x10, PanPos=0x8000 (32768), TiltPos=0x8000 (32768) [81-01-06-02-10-10-08-00-00-00-08-00-00-00-FF]
```

### Zoom Variable (可変速度ズーム)
```
RX: ZoomVariable: Direction=Tele, Speed=7 (0x27) [81-01-04-07-27-FF]
RX: ZoomVariable: Direction=Wide, Speed=5 (0x35) [81-01-04-07-35-FF]
```

### Zoom Direct (絶対位置ズーム) - Blackmagic拡張
```
RX: ZoomDirect: ZoomPos=0xABCD (43981) [81-01-04-47-0A-0B-0C-0D-FF]
```

### Focus Variable (可変速度フォーカス) - Blackmagic拡張
```
RX: FocusVariable: Direction=Far [81-01-04-08-02-FF]
RX: FocusVariable: Direction=Near [81-01-04-08-03-FF]
RX: FocusVariable: Direction=Stop [81-01-04-08-00-FF]
```

### Focus Direct (絶対位置フォーカス) - Blackmagic拡張
```
RX: FocusDirect: FocusPos=0x1234 (4660) [81-01-04-48-01-02-03-04-FF]
```

### Iris Variable (可変速度絞り) - Blackmagic拡張
```
RX: IrisVariable: Direction=Open [81-01-04-0B-02-FF]
RX: IrisVariable: Direction=Close [81-01-04-0B-03-FF]
```

### Iris Direct (絶対位置絞り) - Blackmagic拡張
```
RX: IrisDirect: IrisPos=0x5678 (22136) [81-01-04-4B-05-06-07-08-FF]
```

### Memory Recall (プリセット呼び出し) - Blackmagic拡張
```
RX: MemoryRecall: MemoryNumber=5 [81-01-04-3F-02-05-FF]
```

### Memory Set (プリセット保存) - Blackmagic拡張
```
RX: MemorySet: MemoryNumber=3 [81-01-04-3F-01-03-FF]
```

## ログフォーマット

各ログエントリには以下の情報が含まれます:

1. **コマンド名**: VISCAコマンドの種類
2. **パラメータ**: コマンド固有のパラメータ（速度、方向、位置など）
3. **16進数値**: パラメータの生の16進数値
4. **生バイト列**: 受信した完全なVISCAフレーム（ハイフン区切り）

## 対応コマンド

### 標準VISCA
- PanTiltDrive (可変速度 Pan/Tilt)
- PanTiltAbsolute (絶対位置 Pan/Tilt)
- ZoomVariable (可変速度ズーム)

### Blackmagic PTZ Control 拡張
- ZoomDirect (絶対位置ズーム)
- FocusVariable (可変速度フォーカス)
- FocusDirect (絶対位置フォーカス)
- IrisVariable (可変速度絞り)
- IrisDirect (絶対位置絞り)
- MemoryRecall (プリセット呼び出し)
- MemorySet (プリセット保存)

## トラブルシューティング

### ログが出力されない場合

1. `VerboseLog` が `true` に設定されているか確認
2. `Logger` コールバックが設定されているか確認
3. `LogReceivedCommands` が `true` に設定されているか確認（コマンドログの場合）

### 不明なコマンドが表示される場合

```
RX: Unknown(XX YY ZZ) [81-XX-YY-ZZ-...-FF]
WARNING: Command not handled
```

このメッセージは、パーサーが認識できないVISCAコマンドを受信したことを示します。新しいコマンドのサポートが必要な場合は、`ViscaParser.cs` にパーサーを追加してください。

## カスタムログ処理

独自のログ処理を実装することもできます:

```csharp
var options = new ViscaServerOptions
{
    VerboseLog = true,
    LogReceivedCommands = true,
    Logger = (message) =>
    {
        // ファイルに保存
        File.AppendAllText("visca_log.txt", $"{DateTime.Now}: {message}\n");

        // Unityコンソールにも出力
        Debug.Log($"[VISCA] {message}");
    }
};
```
