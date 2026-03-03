# Sony IP Setup 対応仕様（RM-IP500 Auto Assign）

## 1. 目的

RM-IP500 の Auto Assign で、ViscaControlVirtualCam を実機同様の流れで登録できる状態にする。

## 2. 通信仕様

- Protocol: Sony IP Setup Protocol
- Transport: UDP
- Port: `52380`
- Discovery destination: `255.255.255.255`
- Frame: `STX(0x02)` + unit 群（`0xFF` 区切り）+ `ETX(0x03)`

## 3. 実装済みフロー（実機準拠）

1. `ENQ:network` を受信
2. `MAC` 先頭 + `INFO:network` 応答（固定順）
3. `SETMAC:<mac>` を受信
4. `ACK:<mac>` 応答（1 unit のみ）
5. RM-IP500 が VISCA(`52381`) 開始

## 4. ENQ:network 応答

応答 unit は以下の固定順:

1. `MAC:<mac>`
2. `INFO:network`
3. `MODEL:<model>`
4. `SOFTVERSION:<version>`
5. `IPADR:<ip>`
6. `MASK:<mask>`
7. `GATEWAY:<gw>`
8. `NAME:<name>`
9. `WRITE:on`

実装上の注意:

- `ACK:network` は返さない
- `MAC` unit は先頭に置く
- `SOFTVERSION` は必須
- `IPADR` は実IPv4（`127.0.0.1` は禁止）
- `KEY:VALUE` の `:` 後に余計なスペースを入れない
- 各 unit は `0xFF` 終端、全体は `0x02 ... 0x03`

## 5. SETMAC 応答（最重要）

`SETMAC` 受信時の応答は次のみ:

- `ACK:<mac>`

制約:

- 応答は 1 unit のみ
- 追加 unit（`MAC:...` など）は付与しない

## 6. アドレス広告方針

`ViscaServerBehaviour` の `ipSetupAdvertisedAddressSource` で `IPADR` の元を選択:

- `BindAddress`: `bindAddress` と同じIPを広告
- `CustomAddress`: `ipSetupCustomAdvertisedAddress` を広告

どちらのモードでもループバック（`127.0.0.1`）は拒否する。

## 7. 既定識別値

既定で以下を使用（実機寄せ）:

- `MAC:88-C9-E8-xx-xx-xx`
- `MODEL:IPCA`
- `SOFTVERSION:2.10`
- `NAME:CAM1`

## 8. デバッグ確認

Wireshark で以下の順序を確認:

1. `ENQ:network`
2. `MAC:...`（先頭）+ `INFO:network ...`
3. `SETMAC:...`
4. `ACK:...`
5. その後 `UDP 52381` の VISCA 通信
