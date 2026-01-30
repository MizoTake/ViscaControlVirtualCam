# PTZチューニング（実機寄り挙動）

このパッケージでは、実機の「慣性」「減速」「望遠時の動きの重さ」を再現するための調整項目を用意しています。

## PtzSettings（基本設定）

- **Pan/Tilt/Zoom 最大速度**: 速度の上限です。大きいほど速く動きます。
- **FOV最小/最大**: ズーム範囲です。最小が望遠、最大が広角。
- **絶対位置の可動範囲**: パン/チルトの角度制限。
- **Move Damping**: 絶対位置移動（従来方式）の減衰係数。大きいほど早く追従。
- **Speed Gamma**: VISCA速度値のカーブ（1=線形）。
- **VISCA速度バイト範囲**: パン/チルト速度の最小/最大値。
- **反転設定**: 入力方向と絶対位置マッピングの反転。
- **ズーム連動**:
  - **Enable Pan/Tilt Speed Scale By Zoom**: 望遠時にパン/チルト速度を抑える。
  - **Pan/Tilt Speed Scale At Tele**: 望遠端（最小FOV）での速度倍率。

## PtzTuningProfile（詳細調整）

### 加減速制限
- **Enable Acceleration Limit**: 速度変化に上限をかける（慣性再現）。
- **Pan/Tilt/Zoom Accel**: 最大加速度（度/秒^2）。
- **Pan/Tilt/Zoom Decel**: 最大減速度（度/秒^2）。

### 目標ブレーキ（絶対位置）
- **Enable Target Braking**: 絶対位置移動を「ブレーキ方式」で行う。
- **Pan/Tilt/Zoom Stop Distance**: 目標に到達したとみなす距離（スナップ距離）。

### 目標ブレーキ方式の挙動
- VISCAのAbsoluteコマンドに含まれる速度値（vv/ww）を速度上限として使用します。
- 目標までの残距離から、停止に間に合う最大速度を計算して減速します。
- Enable Target Braking がオフの場合は、従来の Move Damping 方式（指数減衰）で追従します。

## 使い分けの目安
- **実機っぽい挙動**を目指す場合は、Target Brakingをオンにし、Decelを少し大きめに設定。
- **簡易で滑らかな追従**が欲しい場合は、従来の Move Damping を使う。

## BRC-X400 プリセットについて
`Tools > Visca > Create PTZ Presets (Indoor Outdoor Fast BRC-X400)` で以下のプリセットが生成されます。

### PTZ_BRC-X400（PtzSettings）
- Pan Max: 300 deg/s
- Tilt Max: 126 deg/s
- Pan Range: -170 .. 170 deg
- Tilt Range: -20 .. 90 deg
- FOV Range: 3.5 .. 70 deg
- Zoom Max FOV Speed: 25 deg/s
- ズーム連動パン/チルト速度: Teleで0.5x

※FOV最小値・ズーム速度は公開仕様からの推定値です。必要に応じて調整してください。

### PTZ_Tuning_BRC-X400（PtzTuningProfile）
- 加速度: Pan 800 / Tilt 600 / Zoom 300 (deg/s^2)
- 減速度: Pan 900 / Tilt 700 / Zoom 350 (deg/s^2)
- Target Braking: ON
- Stop Distance: Pan 0.2 / Tilt 0.2 / Zoom 0.15 (deg)
