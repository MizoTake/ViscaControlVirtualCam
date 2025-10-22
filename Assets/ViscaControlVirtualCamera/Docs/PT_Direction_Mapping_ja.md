# Pan–Tilt Drive 形式と方向マッピング（統一表記）

- 形式（Pan–Tilt 連続駆動）
  - `81 01 06 01 VV WW XX YY FF`
  - `VV` = Pan 速度（0x01–0x18）
  - `WW` = Tilt 速度（0x01–0x14）
  - `XX` = Pan 方向、`YY` = Tilt 方向

- 方向（XX YY）
  - `03 03` = 停止
  - `01 03` = 左（Pan: Left）
  - `02 03` = 右（Pan: Right）
  - `03 01` = 上（Tilt: Up）
  - `03 02` = 下（Tilt: Down）
  - 斜め（Diagonal）: Pan(01/02) × Tilt(01/02) の組み合わせ
    - `01 01` = 左上（Left + Up）
    - `01 02` = 左下（Left + Down）
    - `02 01` = 右上（Right + Up）
    - `02 02` = 右下（Right + Down）

- 備考
  - `0x00` の速度は最小速度（0x01）に丸める。停止は速度ではなく方向 `03` で指示する。
  - 逆方向/矛盾コマンドは約 200ms 待機 → キャンセル（90 6Z 04 FF）→ 新コマンド再発行を推奨（Sony FR7 推奨動作）。
  - 応答は ACK（90 4Z FF）/ Completion（90 5Z FF）/ Error（90 6Z EE FF）。
