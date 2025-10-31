# カスタムVISCAコマンドの追加

## 概要

新しいインターフェースベースのコマンドシステムにより、VISCAコマンドを簡単に追加できます。

## 方法1: IViscaCommandインターフェースの実装（推奨）

### 1. 新しいコマンドクラスを作成

```csharp
using System;
using ViscaControlVirtualCam;

public class MyCustomCommand : ViscaCommandBase
{
    public override string CommandName => "MyCustomCommand";

    public override bool TryParse(byte[] frame)
    {
        // 8X 01 04 XX YY FF の形式をチェック
        return ValidateFrame(frame, 6) &&
               CheckBytes(frame, (1, 0x01), (2, 0x04), (3, 0xXX));
    }

    public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
    {
        byte param = frame[4];
        // ハンドラーに処理を委譲
        return handler.HandleMyCustomCommand(param, responder);
    }

    public override string GetDetails(byte[] frame)
    {
        byte param = frame[4];
        return $"{CommandName}: Param=0x{param:X2} [{FormatHex(frame)}]";
    }
}
```

### 2. IViscaCommandHandlerにメソッドを追加

```csharp
public interface IViscaCommandHandler
{
    // 既存のメソッド...

    // 新しいメソッド
    bool HandleMyCustomCommand(byte param, Action<byte[]> responder);
}
```

### 3. PtzViscaHandlerに実装を追加

```csharp
public bool HandleMyCustomCommand(byte param, Action<byte[]> responder)
{
    SendAck(responder, _replyMode);
    _mainThreadDispatcher(() =>
    {
        _model.MyCustomCommand(param);
        SendCompletion(responder, _replyMode);
    });
    return true;
}
```

### 4. コマンドを登録

```csharp
// サーバー起動後に登録
var server = new ViscaServerCore(handler, options);
server.CommandRegistry.Register(new MyCustomCommand());
server.Start();
```

## 方法2: ViscaParserの静的メソッド（レガシー）

後方互換性のために残されていますが、新規実装には推奨されません。

```csharp
public static bool TryParseMyCommand(byte[] frame, out byte param)
{
    param = 0;
    if (frame == null || frame.Length < 6) return false;
    if (frame[1] != 0x01 || frame[2] != 0x04 || frame[3] != 0xXX) return false;
    param = frame[4];
    return frame[^1] == 0xFF;
}
```

## コマンドクラスのヘルパーメソッド

`ViscaCommandBase` 基底クラスが提供するヘルパー:

### ValidateFrame
```csharp
protected static bool ValidateFrame(byte[] frame, int minLength)
```
フレームの基本的な妥当性チェック（null、長さ、終端0xFF）

### CheckBytes
```csharp
protected static bool CheckBytes(byte[] frame, params (int index, byte value)[] checks)
```
複数のバイト位置を一度にチェック

使用例:
```csharp
CheckBytes(frame, (1, 0x01), (2, 0x06), (3, 0x04))
```

### FormatHex
```csharp
protected static string FormatHex(byte[] frame)
```
バイト列を16進数文字列に変換（ログ用）

## 完全な実装例

### Pan/Tilt Homeコマンドの実装

```csharp
using System;
using ViscaControlVirtualCam;

public class PanTiltHomeCommand : ViscaCommandBase
{
    public override string CommandName => "PanTiltHome";

    public override bool TryParse(byte[] frame)
    {
        // 8X 01 06 04 FF
        return ValidateFrame(frame, 5) &&
               CheckBytes(frame, (1, 0x01), (2, 0x06), (3, 0x04));
    }

    public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
    {
        return handler.HandlePanTiltHome(responder);
    }

    public override string GetDetails(byte[] frame)
    {
        return $"{CommandName}: Move to home position [{FormatHex(frame)}]";
    }
}

// IViscaCommandHandlerに追加
public interface IViscaCommandHandler
{
    bool HandlePanTiltHome(Action<byte[]> responder);
}

// PtzViscaHandlerに実装
public bool HandlePanTiltHome(Action<byte[]> responder)
{
    SendAck(responder, _replyMode);
    _mainThreadDispatcher(() =>
    {
        // Memory 0 を呼び出す（ホームポジションとして定義）
        _model.CommandMemoryRecall(0);
        SendCompletion(responder, _replyMode);
    });
    return true;
}

// 登録
server.CommandRegistry.Register(new PanTiltHomeCommand());
```

## 利点

### インターフェースベースのアプローチ
✅ **拡張性**: 新しいコマンドを追加しても既存コードに影響なし
✅ **保守性**: コマンドごとに独立したクラスで管理
✅ **可読性**: 各コマンドのロジックが明確に分離
✅ **テスト性**: コマンドごとに単体テストが容易
✅ **柔軟性**: 実行時にコマンドを追加・削除可能

### レガシーアプローチ（静的メソッド）
⚠️ **拡張性が低い**: ProcessFrameメソッドを毎回修正する必要
⚠️ **保守が困難**: すべてのif文が1箇所に集中
⚠️ **テストしにくい**: 個別のコマンドを分離してテストできない

## まとめ

新しいVISCAコマンドを追加する場合は、`IViscaCommand`インターフェースを実装する方法を推奨します。この方法により、コードの保守性と拡張性が大幅に向上します。
