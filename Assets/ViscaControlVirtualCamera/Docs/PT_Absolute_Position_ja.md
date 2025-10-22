# Pan–Tilt 絶対位置（Absolute Position）

- 目的: Pan / Tilt の絶対位置指定を VISCA で受け取り、Unity カメラ/リグへ角度として適用する。
- 適用トランスポート: UDP（VISCA over IP, RawVisca）/ TCP（Raw VISCA）
- 応答: 受理で ACK、到達/適用完了で Completion、問題時は Error。

## コマンド形式（共通ペイロード）
- 形式: `8X 01 06 02 VV WW p1 p2 p3 p4 t1 t2 t3 t4 FF`
  - `VV` = Pan 速度（0x01–0x18）
  - `WW` = Tilt 速度（0x01–0x14）
  - `p1..p4` = Pan 位置（各バイトの下位ニブルのみ有効）
  - `t1..t4` = Tilt 位置（各バイトの下位ニブルのみ有効）
  - X=1（IP/通常）を既定とする
- 速度省略: 一部実装では `VV/WW` を省略。省略時は既定速度を適用。

## 位置エンコード（ニブル単位）
- 各位置は 16bit の値を 4 ニブルに分割し、各ニブルを下位 4bit として 1 バイトに載せる（上位 4bit は 0）。
- 復元:
  - `P = (p1&0x0F)<<12 | (p2&0x0F)<<8 | (p3&0x0F)<<4 | (p4&0x0F)`
  - `T = (t1&0x0F)<<12 | (t2&0x0F)<<8 | (t3&0x0F)<<4 | (t4&0x0F)`
- 範囲: `0x0000..0xFFFF`（実機依存で有効範囲は狭い場合あり）。

## Unity 角度マッピング
- 可動域は設定で保持（機種差対応）。例（要調整）:
  - `panMinDeg=-170, panMaxDeg=+170`
  - `tiltMinDeg=-30, tiltMaxDeg=+90`
- 角度変換（Unsigned 比率モデル）:
  - `panDeg = Lerp(panMinDeg, panMaxDeg, P / 65535.0)`
  - `tiltDeg = Lerp(tiltMinDeg, tiltMaxDeg, T / 65535.0)`
- 代替（Centered モデル）:
  - 中心 `0x8000` を 0 度と見なし、対称域に線形マップ（設定で選択）。

## 動作と応答
- 受信直後に ACK を返却。
- 目標角へスムーズに移行（`damping/accel/decel` 設定）し、到達/適用完了で Completion。
- 矛盾コマンド到着時は先行をキャンセル（`90 6Z 04 FF`）した上で新指示に更新。
- FR7 推奨: 直前のドライブ停止から絶対位置指示まで 200ms 程度の待機→キャンセル→再発行。

## エラー
- 不正長/値: `Syntax`（`90 6Z 02 FF`）
- 同時実行超過: `Buffer Full`（`90 6Z 03 FF`、ソニー 2 ソケット制限）
- 対象なし/実行不能: `Not Executable`（`90 6Z 41 FF`）
- ソケット不正: `No Socket`（`90 6Z 05 FF`）

## 例
- 中央（P=0x8000, T=0x8000, 中間速度）
  - `81 01 06 02 10 10 08 00 00 00 08 00 00 00 FF`
- 左端/上端（P=0x0000, T=0xFFFF, 中間速度）
  - `81 01 06 02 10 10 00 00 00 00 0F 0F 0F 0F FF`
