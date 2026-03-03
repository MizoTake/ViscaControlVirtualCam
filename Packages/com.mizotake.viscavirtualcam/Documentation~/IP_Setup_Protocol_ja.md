# IP Setup Protocol Responder (UDP 52380)

## 概要

`IpSetupResponder` は RM-IP500 の AUTO IP SETUP / Auto Assign 向けの探索応答機能です。  
OS の IP 設定変更は行わず、設定は Unity の Inspector 上で管理します。

- Discovery / Assign: UDP `52380`
- VISCA Control: UDP `52381`

## 構成

- `IpSetupResponder`: UDP 52380 受信、ENQ/SET 応答、デバウンス、返信モード制御
- `VirtualDeviceIdentity`: `virtualMac` / `modelName` / `serial` / `softVersion` / `webPort` / `friendlyName`
- `VirtualNetworkConfig`: `logicalIp` / `logicalMask` / `logicalGateway`

## フレーム形式

- `STX = 0x02` で開始
- `ETX = 0x03` で終了
- unit は `0xFF` 区切り
- unit は ASCII テキスト（例: `ENQ:allinfo`, `SETMAC:02:...`）

## 応答仕様

### ENQ

`ENQ:*` を受信した場合、以下を返します。

- `ACK:ENQ`
- `INFO:<selector>`
- `MAC:<virtualMac>`
- `MODEL:<modelName>`
- `MODELNAME:<modelName>`
- `SERIAL:<serial>`
- `SOFTVERSION:<softVersion>`
- `IPADR:<advertisedIp>`
- `MASK:<logicalMask>`
- `GATEWAY:<logicalGateway>`
- `WEBPORT:<webPort>`
- `NAME:<friendlyName>`

返信先は既定でユニキャスト（受信元 IP/Port）です。  
`ipSetupResponderMode = Broadcast` でブロードキャスト返信に切り替えられます。

### SET

`SETMAC` があり、`virtualMac` と一致する場合のみ受理します。

- 受理: `ACK:SET`
- 不一致/不足: `NAK:<reason>`

`IPADR` / `MASK` / `GATEWAY` / `WEBPORT` / `NAME` を受信した場合は、
非シリアライズの内部状態として保持され、応答生成に使われます。

## IPADR の決定

`ipSetupAdvertisedAddressSource` で `IPADR` を決めます。

- `BindAddress`: `ViscaServerBehaviour.bindAddress` と同じ IP を返す
- `CustomAddress`: `ipSetupCustomAdvertisedAddress` の IP を返す

`BindAddress` で `0.0.0.0` のような未確定アドレスを指定した場合は起動時にエラーにします。  
その場合は NIC の具体的な IP を `bindAddress` に設定するか、`CustomAddress` を使ってください。

## デバウンス

同一送信元から短時間に連続する ENQ は  
`ipSetupEnqDebounceMilliseconds`（既定 250ms）で抑制できます。
