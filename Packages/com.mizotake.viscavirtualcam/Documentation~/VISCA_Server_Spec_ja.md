# Unity VISCA Control Server（TCP/UDP）仕様

本ドキュメントは、Unity 上で VISCA 互換コマンドにより仮想カメラを外部制御するためのサーバー仕様（TCP/UDP）を定義する。ポートや詳細マッピングは運用環境に合わせて設定可能とし、VISCA の終端 0xFF によるフレーミングと ACK/Completion/Error 応答モデルを採用する。

- トランスポート: UDP（低遅延単発）、TCP（セッション/信頼性）。
- フレーミング: 可変長 + 0xFF 終端。1 パケット内の複数フレームに対応。
- 応答: ACK→Completion（完了）/Error。UDP は省略可。TCP は原則返却。
- スレッド: 受信/解析はワーカー、Unity 操作はメインスレッドで適用。
- コマンド: PTZ/フォーカス/露出/プリセット/ホーム/電源（高レベル仕様）。
- 設定: ポート、制御モード（Exclusive/Shared）、レート制限、適用タイミング、対象カメラ列挙、ログなど。
- セキュリティ: 既定ループバック、許可/拒否リスト、アイドル切断。
- デバッグ拡張: PING\n → PONG\n（任意、非 VISCA）でヘルスチェック。

詳細版は今後の実装に合わせて更新（Docs 参照）。

## PTZ サブセット（RS232 相当/over IP 共通マッピング）
- 前提
  - アドレス付きコマンド先頭バイトは `0x8x`（x=カメラID、既定は `0x1` → `0x81`）。
  - メッセージは可変長で終端 `0xFF`。
  - 以下は代表的な VISCA コマンドのサブセット。機種により速度レンジや挙動が異なるため、必要に応じて係数を設定で調整する。

- パン/チルト 連続駆動（速度制御）
  - 形式: ``[0x8x, 0x01, 0x06, 0x01, VV, WW, PP, TT, 0xFF]``
  - `VV`: Pan 速度（推奨 0x01..0x18）
  - `WW`: Tilt 速度（推奨 0x01..0x14）
  - `PP`: Pan 方向（L=0x01, R=0x02, 停止=0x03）
  - `TT`: Tilt 方向（U=0x01, D=0x02, 停止=0x03）
  - マッピング: 方向と速度を Unity 側で角速度[deg/s]に変換し、メインスレッドで `Transform`/Cinemachine に適用。

- パン/チルト 停止
  - 形式: ``[0x8x, 0x01, 0x06, 0x01, VV, WW, 0x03, 0x03, 0xFF]``
  - 備考: `VV/WW` は無視可。双方向停止。

- ズーム（FOV 相当、可変速度）
  - 形式: ``[0x8x, 0x01, 0x04, 0x07, ZZ, 0xFF]``
  - `ZZ`: `0x2p` = Tele（ズームイン）, `0x3p` = Wide（ズームアウト）, `0x00` = 停止。`p` は 0..7（0=最小/停止相当, 7=最大速度）
  - マッピング: Tele で `fieldOfView` を減少、Wide で増加。`[minFov, maxFov]` にクランプ。速度は `p` を係数に変換。

- 注意
  - 速度レンジは機種依存のため、本実装では正規化して `speed 0..7` を内部の角速度/deg/sに線形/曲線マップ可能とする。
  - 絶対/相対位置コマンド（例: パン/チルト絶対位置）は将来拡張。現行は連続駆動＋停止で対応する。

### Unity への変換例（ガイド）
- Pan/Tilt（度/秒）
  - `omegaPan = panSpeedMap(VV) * dir(PP)`、`omegaTilt = tiltSpeedMap(WW) * dir(TT)`
  - `dir`: L/Down = -1, R/Up = +1, Stop = 0
  - 適用: `transform.Rotate(0, omegaPan * dt, 0)` と `transform.Rotate(-omegaTilt * dt, 0, 0)` 等（回転軸はリグの設計に合わせる）
- FOV（度/秒）
  - `omegaFov = zoomSpeedMap(p) * (Tele ? -1 : +1)`
  - 適用: `camera.fieldOfView = Clamp(FOV + omegaFov * dt, minFov, maxFov)`

### デフォルト設定（提案値）
- `panMaxDegPerSec = 120`、`tiltMaxDegPerSec = 90`
- `zoomMaxFovDegPerSec = 40`
- `minFov = 15`, `maxFov = 90`
- `speedMap(p) = (p / 7.0)^gamma`（`gamma=1.0` 既定、曲線調整可）

## トランスポート
- 優先: UDP 52381（VISCA over IP 既定ポート。任意設定可。例: 52380）
  - 目的: OBS PTZ や一般的なハードウェアコントローラとの互換。
  - 受信: ユニキャスト。デフォルトでブロードキャストは無効。
  - 応答: 既定は ACK/Completion を返信（設定で抑制可）。
- 互換モード（`compatibilityMode`）
    - RawVisca: ヘッダ無し、VISCA フレーム（0xFF 終端）のみ。
    - SonyViscaOverIp: ベンダ仕様の UDP ヘッダ/ソケット管理に対応（資料に従い実装）。
    - RawVisca の取り扱い: UDP は「1 パケット = 1 フレーム（8X ... FF）」の単一コマンド固定。複数同梱は非対応。
- 選択可: TCP 52381（任意設定可。例: 52380）
  - セッション/信頼性重視の用途向け。複数接続は `maxClients` で制限。
  - 応答は原則返却（ACK/Completion/Error）。

## 設定項目（トランスポート抜粋）
- `enabledTransports`: { UDP, TCP }
- `udpPort`: int（既定 52381）
- `tcpPort`: int（既定 52381）
- `bindAddress`: IPAddress | Any（LAN 受信時は 0.0.0.0 等）
- `udpReply`: None | ErrorOnly | AckAndResult（既定 AckAndResult）
- `compatibilityMode`: RawVisca | SonyViscaOverIp（既定 SonyViscaOverIp）

## 互換ノート（OBS PTZ / ハードウェア）
- OBS PTZ 等の既存コントローラは UDP/52381 を前提に送出するケースが多い。
- ヘッダ有無・ソケット管理の要否はコントローラ/機器の仕様に依存するため、`compatibilityMode` で切替。
- LAN から制御する場合は OS のファイアウォールで UDP 52381 の受信を許可し、`bindAddress` を NIC もしくは `0.0.0.0` に設定する。

## ネットワークバイトオーダー
- LAN への送受信におけるヘッダ数値フィールド（例: Payload Length, Sequence Number）は、Network Byte Order（ビッグエンディアン）で扱う。
- VISCA ペイロードはバイト列としてそのまま（可変長 + `0xFF` 終端）。

## VISCA over IP モード（推奨）
- 概要
  - 送受信は UDP/52381 を使用。
  - Sony VISCA over IP のヘッダ付きパケットに対応し、ヘッダを解析して VISCA ペイロードを抽出して処理。
  - 複数クライアントとのやり取りにおいて、シーケンス番号/ソケット識別子を用いて応答を正しく対応付ける。

- ソケット管理（概念）
  - Control Socket Open: クライアントの要求に応じて制御ソケットを確立し、以後のコマンド/応答に利用。
  - KeepAlive: 規定間隔で生存確認メッセージを受信/送信。一定時間無通信でソケットを破棄。
  - Close: 明示クローズ要求に応じてソケットを解放。

- パケット処理（概念）
  - ヘッダ検証: バージョン/フラグ/長さ/シーケンス等を検証し、異常時はエラーパケットを返す。
  - ペイロード: ヘッダ後方の VISCA バイト列（終端 0xFF）を 1 フレームとして解析。複数フレームが含まれる場合は逐次処理。
  - 応答: 受理時に ACK、適用完了時に Completion、問題発生時に Error を、受信パケットのシーケンス/識別に対応付けて返送。

- 競合/占有
  - `controlMode = Exclusive` では最初の Open を制御権者とし、他は参照のみ。
  - `controlMode = Shared` では同時制御を許可し、最後着のコマンド優先/レート制限で緩和。

- 実装注意
  - ヘッダ仕様の詳細（フィールド構成/値範囲/エラーコード）は、対象機器の公式ドキュメントに厳密に従うこと。
  - 応答ヘッダには受信のシーケンス/識別子を反映し、再送/順序入替に対するロバスト性を確保。
  - RawVisca 互換の必要がある場合は `compatibilityMode=RawVisca` に切替える。

### ソニー実装互換ポイント（要件）
- ソケット数: 最大 2（同時実行 2 コマンドまで）。
  - サーバは 2 つの制御ソケットのみを確立/保持。
  - 2 つを超える同時実行要求は拒否（推奨: `Buffer Full` = `90 60 03 FF` または該当 `Z` で `90 6Z 03 FF`）。
- キュー/同時実行ポリシー:
  - 既定はキューしない（直ちに `Buffer Full`）。設定で短キューを許可可（ただし 2 を超える同時実行を作らない）。
- 再送（Retransmission）とシーケンス:
  - 同一 `Sequence` の再送パケットは重複として扱い、直前の応答（ACK/Completion/Error）を再送する（idempotent）。
  - 未処理/処理中で ACK 済みの場合は、ACK を即時再送。処理完了後に Completion を送る。
  - 受信順序が逆転しても、`Sequence` に基づき最新を優先。古い再送は無視/再応答。
- キャンセル（Cancel）:
  - 同一ソケット `Z` 上で矛盾する新コマンド（例: 逆方向 PT 駆動/Stop）が到着した場合、先行コマンドを中断とみなし `Canceled`（`0x04`）で終了させるか、Stop を優先して Completion を返す。
  - 明示キャンセルは Control Command（`PayloadType=0x02 0x00`）により、対象 `socketId` もしくは `sequence` を指定してキャンセル（実装で選択、機器仕様に合わせる）。
  - キャンセル結果は `Error: 90 6Z 04 FF` を推奨（もしくは Stop 完了を `Completion` として返送）。
  - PT ドライブ停止推奨手順: 一時停止や逆方向指示の前に約 200ms 待ってからキャンセル→再発行（誤検出や余韻の吸収のため）。

### パケット構造（VISCA over IP）
- 物理ポート: UDP 52381（推奨。任意設定可、例: 52380）、同ポートの TCP 受信もオプションで許可。
- 構造: 先頭 8 バイトをヘッダ、その後ろに 1〜16 バイトのペイロード（VISCA フレーム）
  - ヘッダ（8B）: ベンダ仕様。典型的には Version/Flags、PayloadLength、SocketId、Sequence、Type 等のフィールドで構成。
  - ペイロード（1..16B）: VISCA バイト列。フレームは終端 `0xFF` を含み、1 パケットあたり 1 フレームのみとする。
- 妥当性検証
  - `PayloadLength` が 1..16 の範囲外、または実データ長と不一致 → 破棄し Error 応答（可能なら）。
  - ペイロード末尾が `0xFF` でない → Malformed Frame として Error。
  - ヘッダのソケット/シーケンスは応答に反映し、オリジンへ返送。

#### ヘッダ: Byte0–1 = Payload Type（例）
- `0x01 0x00`: VISCA Command（通常コマンド）
- `0x01 0x10`: Inquiry（問い合わせ）
- `0x01 0x11`: Reply（問い合わせへの応答）
- `0x02 0x00`: Control Command（ソケット管理/KeepAlive 等）

取り扱いポリシー:
- VISCA Command: 受理時 ACK、完了時 Completion を返送。
- Inquiry: クエリハンドラに転送し、`Reply`（0x01 0x11）の形式で応答ペイロードを返送。
- Reply: 本実装が送出した Inquiry に対する応答として関連付け、待機中でなければログ記録のみ。
 - Control Command: ソケット Open/Close/KeepAlive など、状態管理コマンドとして処理（本実装では内容を解釈せず、末尾が `0xFF` の場合 Completion を返す。終端なしなど不正ペイロードは Syntax(0x02)）。

#### ヘッダ: Byte2–3 = Payload Length（ビッグエンディアン）
- 意味: 後続ペイロード（VISCA バイト列）の長さ（バイト数）。
- 範囲: 1〜16（上限16）。0 または 16超は不正で即 Error（MessageLength）。
- エンディアン: ビッグエンディアン（Network Byte Order）。
- 検証: 受信実長と一致しない場合は長さ不一致として Error（可能であればヘッダを反映して返信）。

#### ヘッダ: Byte4–7 = Sequence Number（32-bit）
- 意味: 送信ごとに +1 されるシーケンス番号（ロールオーバー可）。
- 型/並び: 32-bit（ビッグエンディアン推奨）。
- 用途: 応答（ACK/Completion/Error）を要求元へ対応付けるため、受信値をそのまま応答ヘッダにエコーする。
- 範囲/ロールオーバー: 0x00000000〜0xFFFFFFFF。最大値到達後は 0 に戻る（クライアント側でモジュロ比較を推奨）。
- 実装ノート: サーバーは順序保証をしない（UDP）。必要に応じてクライアント側で重複/逆順を抑制する。

#### ペイロード: Byte8 以降 = VISCA フレーム
- 形式: `[0x8X, ...., 0xFF]`
- 先頭: `0x8X`（送信/命令）。`X=1` は IP モードを示す識別として使用（本サーバ既定値）。
- 応答: `[0x9X, ...., 0xFF]`（受信/応答）。`X=1` を既定とし、ヘッダのシーケンス番号と対応付けて返送。
- 長さ: ヘッダの `PayloadLength` と一致、終端は必ず `0xFF`。
- 制約: 1 パケットに 1 フレーム（VISCA over IP / RawVisca いずれも単一コマンド）。

### ペイロードのみ（ヘッダなし / UDP RawVisca）
- 形式: `[0x8X, ..., 0xFF]` を UDP ペイロードとしてそのまま送受信（ヘッダ無し）。
- 単位: 1 パケット = 1 コマンド（複数同梱は非対応）。
- 応答: `[0x90, 0x4Z, 0xFF]`（ACK）, `[0x90, 0x5Z, 0xFF]`（Completion）, `[0x90, 0x6Z, EE, 0xFF]`（Error）。Z は既定 `1`。
- 妥当性: 末尾 0xFF 必須、サイズは 1..16B を推奨（設定で上限可変）。

### 実装ガイド（応答の対応付け）
- 受信ヘッダから `socketId`/`sequence` を抽出し、応答側ヘッダに同値を設定。
- 受理時に ACK、適用完了時に Completion を送信。エラー時は Error とし、可能ならエラーコードをペイロードに含める。

### ACK と終了通知（Completion）
- ヘッダ
  - `Byte0–1`（Payload Type）: `0x01 0x11`（Reply）
  - `Byte2–3`（Length）: ペイロード長をビッグエンディアンで設定（1..16）
  - `Byte4–7`（Sequence）: 受信要求の値をエコー
- ペイロード（例）
  - ACK: `[0x90, 0x40, 0xFF]`（受理直後に送出）
  - Completion: `[0x90, 0x50, 0xFF]`（適用完了時に送出）
  - Error: `[0x90, 0x60, <ErrorCode>, 0xFF]`
- タイミング
  - ACK はパース成功直後に即時送信（メインスレッド適用前）。
  - Completion はメインスレッドでの適用完了後に送信。非同期適用が発生する場合は完了時点で送る。
- 省略
  - 帯域/実装方針により ACK を省略する設定も可能だが、互換性重視時は送出を推奨。

#### ペイロード形式（VISCA over IP 準拠）
- ACK: `[0x90, 0x4Z, 0xFF]`
- Completion: `[0x90, 0x5Z, 0xFF]`
- Error: `[0x90, 0x6Z, <ErrorCode>, 0xFF]`
- Z: ソケット番号（`0x0`〜`0xF`）。Control Open 時に割当/管理し、以降の応答に用いる。RawVisca では既定 `Z=1`。

#### エラーコード（例）
- `0x02` Syntax（構文不正/未サポート） → 例: `90 60 02 FF`（Z=0）
- `0x03` Buffer Full（内部バッファ不足） → 例: `90 60 03 FF`（Z=0）
- `0x04` Canceled（処理中断/取消） → 例: `90 6Z 04 FF`
- `0x05` No Socket（ソケット未確立/無効） → 例: `90 6Z 05 FF`
- `0x41` Not Executable（現在状態で実行不可） → 例: `90 6Z 41 FF`

備考
- `0x6Z` の Z はソケット番号。サーバがソケットを特定できないエラーでは Z=0（`0x60`）を使用。
- 具体的な割当は機器仕様に準拠し、本実装では上記を推奨初期値として採用する。

### キャンセル（ペイロード指示）
- コマンド: `8X 2Z FF`
  - `X`: 送信種別（通常は `1`）。
  - `Z`: ソケット番号（0x0〜0xF）。
- 動作: ソケット `Z` 上で実行中のコマンドを中断する。
- 応答: 直後に「Command Canceled」を返却（正常動作として扱う）。
  - 形式: `90 6Z 04 FF`（Error クラスの 0x04=Canceled を用いるが、キャンセル成功通知として期待値）。
  - 実装メモ: ACK は省略可（仕様上は即時に Canceled を返す挙動を優先）。
- 実行中コマンドがない場合も同様に `90 6Z 04 FF` を返却する（キャンセル要求を正常応答として扱う）。

### 実装ステータス（パッケージ版）
- VISCA over IP ヘッダ受信時は `0x01 0x11` ヘッダで Sequence をエコーして返信する（ペイロード長 1–16 のみ）。
- ACK/Completion/Error はソケット nibble（`Z`）を必ず反映 (`90 4Z/5Z/6Z ... FF`)。Raw VISCA 受信時の既定 `Z=1`。
- Command Cancel (`8X 2Z FF`) に対応し、即時に `90 6Z 04 FF` を返す。ソケット番号は `2Z` バイトから抽出。
- VISCA over IP の Payload Length は 1〜16 を厳格に検証し、超過/不一致は `Message Length(0x01)` で応答。
- バッファ満杯時は `Buffer Full(0x03)` を返却（内部キュー上限超過時）。状態不整合や処理不能例外時は `Not Executable(0x41)` を返す。
- 構文不正/長さ超過時は Syntax(0x02) を返却。
- Control Command（`0x02 0x00`）はペイロード終端 `0xFF` を確認し、終端ありは Completion で応答、終端なしなど不正は Syntax(0x02)。

## Raw VISCA（Serial 互換）モード（TCP）
- 概要
  - トランスポート: TCP（既定ポートは 52381。運用で変更可）。
  - フレーミング: ヘッダ無し、VISCA フレーム（`... 0xFF`）をストリーム上で連続受信。1 ストリームに複数フレームを連結可能。
  - 想定: RS-232 相当のコマンド体系を TCP 経由で利用するシナリオ向け。

- 受信/解析
  - ストリームを `0xFF` で分割してフレーム抽出（1..16B を上限目安、設定で `maxFrameSize` 調整可）。
  - 先頭は `0x8X`（通常 `0x81`）。不正/超過は Syntax/Buffer Full として応答。

- 応答
  - ACK: `[0x90, 0x40, 0xFF]`
  - Completion: `[0x90, 0x50, 0xFF]`
  - Error: `[0x90, 0x60, <ErrorCode>, 0xFF]`
  - ソケット番号 `Z` はヘッダが無いので使用しない（既定 `Z=0`/`1`相当で解釈されるがバイト値は `0x40/0x50/0x60` を固定）。

- セッション/排他
  - 接続ごとに状態を保持。`controlMode` により Exclusive/Shared を選択。
  - 再送検出: ヘッダが無いのでアプリ側で短時間内の重複コマンドを抑制（オプション）。

- PTZ/FOV マッピング
  - 上述の「PTZ サブセット」に準拠。Pan/Tilt ドライブ停止時は 200ms 待機→キャンセル→再発行の推奨は同様に適用。

- 備考
  - PING などの ASCII 拡張はデバッグ時のみ有効化可（既定は無効）。
  - RS-232 実機互換を重視する場合、速度レンジ/応答のタイミングを設定で調整する。

### TCP VISCA フォールバック（OBS-PTZ 互換目的）
- 目的: OBS-PTZ などの TCP VISCA クライアントと接続できない環境での互換確保。
- 仕様: 本 Raw VISCA（TCP）を有効化し、1 コマンド = 1 フレーム（`8X ... FF`）で処理。応答は RS-232 スタイル（ACK/Completion/Error）。
- 推奨設定: `enabledTransports` に TCP を含め、ポートは既定 52381（任意で 52380 等に変更可）。
- 排他: 互換性重視のため `controlMode=Exclusive` を推奨（複数接続時は最初の接続が制御権）。

### サポートコマンド（最小実装セット）
- Pan/Tilt Drive（連続駆動）
  - `8X 01 06 01 VV WW PP TT FF`
  - `VV`=Pan 速度, `WW`=Tilt 速度, `PP`=L(01)/R(02)/Stop(03), `TT`=U(01)/D(02)/Stop(03)
- Pan/Tilt Stop（停止）
  - `8X 01 06 01 VV WW 03 03 FF`（`VV/WW` は任意）
- Zoom（FOV 相当、可変速度）
  - `8X 01 04 07 ZZ FF`（`ZZ`=2p Tele / 3p Wide / 00 Stop, p=0..7）
- 応答
  - ACK: `90 40 FF`、Completion: `90 50 FF`、Error: `90 60 EE FF`
  - エラーは Syntax(02)/BufferFull(03)/Canceled(04)/NotExecutable(41) を実装
- 任意（実装余力があれば）
  - Home: `8X 01 06 04 FF`（初期位置復帰）

非対応時の方針
- 未サポート/未知コマンドは `Syntax`（`90 60 02 FF`）。
- 実行不能は `Not Executable`（`90 60 41 FF`）。
- 同時実行要求は `Buffer Full`（`90 60 03 FF`）。
