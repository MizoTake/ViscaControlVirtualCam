# VISCA Command Coverage

このドキュメントは、実装済みのVISCAコマンドと未実装のコマンドを一覧化しています。

## 実装済みコマンド ✓

### Pan/Tilt Control
| コマンド | バイトシーケンス | 実装状況 |
|---------|----------------|---------|
| Pan/Tilt Drive | `8X 01 06 01 VV WW PP TT FF` | ✓ 実装済み |
| Pan/Tilt Absolute | `8X 01 06 02 [VV WW] p1 p2 p3 p4 t1 t2 t3 t4 FF` | ✓ 実装済み |

### Zoom Control
| コマンド | バイトシーケンス | 実装状況 |
|---------|----------------|---------|
| Zoom Variable | `8X 01 04 07 ZZ FF` | ✓ 実装済み |
| Zoom Direct | `8X 01 04 47 p1 p2 p3 p4 FF` | ✓ 実装済み (Blackmagic) |

### Focus Control
| コマンド | バイトシーケンス | 実装状況 |
|---------|----------------|---------|
| Focus Variable | `8X 01 04 08 ZZ FF` | ✓ 実装済み (Blackmagic) |
| Focus Direct | `8X 01 04 48 p1 p2 p3 p4 FF` | ✓ 実装済み (Blackmagic) |

### Iris/Exposure Control
| コマンド | バイトシーケンス | 実装状況 |
|---------|----------------|---------|
| Iris Variable | `8X 01 04 0B ZZ FF` | ✓ 実装済み (Blackmagic) |
| Iris Direct | `8X 01 04 4B p1 p2 p3 p4 FF` | ✓ 実装済み (Blackmagic) |

### Memory/Preset Control
| コマンド | バイトシーケンス | 実装状況 |
|---------|----------------|---------|
| Memory Recall | `8X 01 04 3F 02 pp FF` | ✓ 実装済み (Blackmagic) |
| Memory Set | `8X 01 04 3F 01 pp FF` | ✓ 実装済み (Blackmagic) |

---

## 一般的だが未実装のコマンド

### Camera Power
| コマンド | バイトシーケンス | 優先度 | 備考 |
|---------|----------------|-------|------|
| Power On | `8X 01 04 00 02 FF` | 低 | Unityでは不要（常時ON） |
| Power Off | `8X 01 04 00 03 FF` | 低 | Unityでは不要 |

### Pan/Tilt Extended
| コマンド | バイトシーケンス | 優先度 | 備考 |
|---------|----------------|-------|------|
| Pan/Tilt Home | `8X 01 06 04 FF` | 中 | プリセット0として実装可能 |
| Pan/Tilt Reset | `8X 01 06 05 FF` | 低 | 位置リセット |
| Pan/Tilt Limit Set | `8X 01 06 07 0W 0p 0p 0p 0p 0t 0t 0t 0t FF` | 低 | 可動範囲制限 |

### Zoom Extended
| コマンド | バイトシーケンス | 優先度 | 備考 |
|---------|----------------|-------|------|
| Zoom Tele (Standard) | `8X 01 04 07 02 FF` | 低 | Zoom Variableで代用可 |
| Zoom Wide (Standard) | `8X 01 04 07 03 FF` | 低 | Zoom Variableで代用可 |
| Zoom Stop | `8X 01 04 07 00 FF` | 低 | Zoom Variable(0x00)で代用可 |
| Digital Zoom | `8X 01 04 06 pp FF` | 低 | Virtual Cameraでは不要 |

### Focus Extended
| コマンド | バイトシーケンス | 優先度 | 備考 |
|---------|----------------|-------|------|
| Focus Auto | `8X 01 04 38 02 FF` | 中 | Auto Focus ON |
| Focus Manual | `8X 01 04 38 03 FF` | 中 | Manual Focus |
| Focus One Push AF | `8X 01 04 18 01 FF` | 低 | ワンプッシュAF |
| Focus Near Limit | `8X 01 04 28 p1 p2 p3 p4 FF` | 低 | 最近接距離設定 |

### Auto Exposure (AE)
| コマンド | バイトシーケンス | 優先度 | 備考 |
|---------|----------------|-------|------|
| AE Mode | `8X 01 04 39 pp FF` | 低 | 露出モード設定 |
| Shutter Speed Direct | `8X 01 04 4A p1 p2 p3 p4 FF` | 低 | シャッター速度 |
| Gain Direct | `8X 01 04 4C p1 p2 p3 p4 FF` | 低 | ゲイン設定 |
| Bright Direct | `8X 01 04 4D p1 p2 p3 p4 FF` | 低 | 明るさ設定 |

### White Balance
| コマンド | バイトシーケンス | 優先度 | 備考 |
|---------|----------------|-------|------|
| WB Mode | `8X 01 04 35 pp FF` | 低 | ホワイトバランスモード |
| WB One Push | `8X 01 04 10 05 FF` | 低 | ワンプッシュWB |
| Red Gain Direct | `8X 01 04 43 00 00 p1 p2 FF` | 低 | Rゲイン |
| Blue Gain Direct | `8X 01 04 44 00 00 p1 p2 FF` | 低 | Bゲイン |

### Picture Effects
| コマンド | バイトシーケンス | 優先度 | 備考 |
|---------|----------------|-------|------|
| Backlight Comp | `8X 01 04 33 pp FF` | 低 | 逆光補正 |
| Wide Dynamic Range | `8X 01 04 3D pp FF` | 低 | WDR ON/OFF |
| Picture Effect | `8X 01 04 63 pp FF` | 低 | ピクチャーエフェクト |
| Noise Reduction | `8X 01 04 53 pp FF` | 低 | ノイズリダクション |

### Inquiry Commands (応答系)
| コマンド | バイトシーケンス | 優先度 | 備考 |
|---------|----------------|-------|------|
| Pan/Tilt Position Inquiry | `8X 09 06 12 FF` | 中 | 現在位置問い合わせ |
| Zoom Position Inquiry | `8X 09 04 47 FF` | 中 | ズーム位置問い合わせ |
| Focus Position Inquiry | `8X 09 04 48 FF` | 低 | フォーカス位置問い合わせ |
| Focus Mode Inquiry | `8X 09 04 38 FF` | 低 | フォーカスモード問い合わせ |

---

## 実装の推奨順

Unity Virtual Camera用途を考慮した優先度付け:

### 優先度: 高
現在の実装で主要な制御は完了しています。

### 優先度: 中
必要に応じて追加を検討:
1. **Pan/Tilt Home** (`8X 01 06 04 FF`) - ホームポジション移動
2. **Focus Auto/Manual** (`8X 01 04 38 pp FF`) - AF/MF切り替え
3. **Position Inquiry系** - 現在位置の問い合わせ（デバッグ用）

### 優先度: 低
Virtual Camera用途では不要:
- Camera Power系
- White Balance系
- Picture Effect系
- Digital Zoom

---

## Blackmagic ATEMで使用される主要コマンド

Blackmagic ATEMスイッチャーから送信される可能性が高いコマンド:

✓ **実装済み**
- Pan/Tilt Drive
- Pan/Tilt Absolute
- Zoom Variable
- Zoom Direct
- Focus Variable/Direct
- Iris Variable/Direct
- Memory Recall/Set

**未実装だが送信される可能性あり**
- Pan/Tilt Home (プリセット0として実装済みの場合は不要)
- Position Inquiry系 (状態確認用)

---

## カスタムコマンド追加方法

新しいVISCAコマンドの追加方法については、[CustomCommands.md](CustomCommands.md) を参照してください。

インターフェースベースの新しいアプローチでは、以下の手順で簡単に追加できます:
1. `IViscaCommand`インターフェースを実装した新しいコマンドクラスを作成
2. `IViscaCommandHandler`にメソッドを追加
3. `PtzViscaHandler`に実装を追加
4. サーバー起動時に`CommandRegistry.Register()`で登録

詳細な手順と実装例は [CustomCommands.md](CustomCommands.md) を参照してください。

---

## まとめ

現在の実装で、Blackmagic ATEM等のスイッチャーからの**PTZ制御に必要な主要コマンドは網羅**されています。

追加で実装を検討すべきコマンド:
1. Pan/Tilt Home（ホームポジション）
2. Focus Auto/Manual切り替え
3. Position Inquiry（デバッグ/状態確認用）

その他のコマンド（電源、ホワイトバランス、ピクチャーエフェクト等）はUnity Virtual Camera用途では不要です。
