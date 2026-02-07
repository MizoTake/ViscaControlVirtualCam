# PTZチューニング（実機寄り挙動）

このパッケージでは、実機の「慣性」「減速」「望遠時の動きの重さ」を再現するための調整項目を用意しています。

## PtzSettings（基本設定）

- **Pan/Tilt/Zoom 最大速度**: 速度の上限です。大きいほど速く動きます。
- **Pan/Tilt 最小速度**: 速度の下限です。BRC-X400 は通常操作で 0.5°/s が下限です。
- **プリセット速度**: プリセット呼び出し時の最大/最小速度を分けて設定できます。
- **FOV最小/最大**: ズーム範囲です。最小が望遠、最大が広角。
- **絶対位置の可動範囲**: パン/チルトの角度制限。
- **Move Damping**: 絶対位置移動（従来方式）の減衰係数。大きいほど早く追従。
- **Speed Gamma**: VISCA速度値のカーブ（1=線形）。
- **VISCA速度バイト範囲**: パン/チルト速度の最小/最大値。
- **反転設定**: 入力方向と絶対位置マッピングの反転。
- **ズーム連動**:
  - **Enable Pan/Tilt Speed Scale By Zoom**: 望遠時にパン/チルト速度を抑える。
  - **Pan/Tilt Speed Scale At Tele**: 望遠端（最小FOV）での速度倍率。
- **スローパン/チルト**: 低速モードの最大/最小速度を設定します。
- **レンズプロファイル**: センサーサイズと焦点距離からFOVを算出します（Unityは垂直FOV）。
- **ズーム位置マッピング**: `zoomPositionTeleAtMax` がオンの場合、ズーム位置の最大値を望遠側として扱います。

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
- Pan Max (Normal): 101 deg/s
- Tilt Max (Normal): 91 deg/s
- Pan Min (Normal): 0.5 deg/s
- Tilt Min (Normal): 0.5 deg/s
- Pan Max (Preset): 300 deg/s
- Tilt Max (Preset): 126 deg/s
- Pan Min (Preset): 1.1 deg/s
- Tilt Min (Preset): 1.1 deg/s
- Pan Range: -170 .. 170 deg
- Tilt Range: -20 .. 90 deg
- FOV Range (Vertical): 2.1 .. 40.4 deg
- Zoom Max FOV Speed: 25 deg/s
- ズーム連動パン/チルト速度: Teleで0.5x
- レンズ: 1/2.5型センサー(5.76x3.24mm), f=4.4..88.0mm
- スローパン/チルト: 0.5..60 deg/s

※ズーム速度は公開仕様からの推定値です。必要に応じて調整してください。

### PTZ_Tuning_BRC-X400（PtzTuningProfile）
- 加速度: Pan 800 / Tilt 600 / Zoom 300 (deg/s^2)
- 減速度: Pan 900 / Tilt 700 / Zoom 350 (deg/s^2)
- Target Braking: ON
- Stop Distance: Pan 0.2 / Tilt 0.2 / Zoom 0.15 (deg)

## 追加カメラプリセットについて

### PTZ_Panasonic_AW-HE40（PtzSettings）
- Pan Max (Normal): 90 deg/s
- Tilt Max (Normal): 90 deg/s
- Pan/Tilt Max (Preset): 300 deg/s
- Pan Range: -175 .. 175 deg
- Tilt Range: -30 .. 90 deg
- FOV Range (Vertical): 2.02 .. 55.76 deg
- レンズ: 1/2.3型センサー(6.17x4.55mm), f=4.3..129mm

※FOVは1/2.3型センサーの代表寸法と焦点距離から算出しています。ズーム速度は公開仕様がないため暫定値です。必要に応じて調整してください。

### PTZ_PTZOptics_PT20X（PtzSettings）
- Pan Speed: 1.7 .. 100 deg/s
- Tilt Speed: 1.7 .. 69.9 deg/s
- Pan Range: -170 .. 170 deg
- Tilt Range: -30 .. 90 deg
- FOV Range (Vertical): 1.89 .. 34.1 deg
- センサー: 1/2.7型 CMOS
- レンズ: 20x (f=4.42..88.5mm)

※ズーム速度は公開仕様がないため暫定値です。必要に応じて調整してください。

### 参照
- Panasonic AW-HE40 Brochure (July 2018) の仕様表
- PTZOptics PT20X-USB G2 User Manual (Rev 1.5, 8/20) の Product Specifications
