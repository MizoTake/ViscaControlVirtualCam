# VISCA Control Virtual Camera (UPM)

Unity package providing a minimal VISCA-compatible PTZ virtual camera server.

- Runtime: Pure C# PTZ model and VISCA UDP/TCP server core
- Behaviours: Thin MonoBehaviours to apply results to Transforms/Camera
- Editor: Menu to generate a PTZ sample scene

## Install
- Embedded (this repo): present under `Packages/com.mizotake.viscavirtualcam`
- External: Add package from Git URL or disk (select `package.json`).

## Usage
- Menu: `Tools > Visca > Create PTZ Sample Scene`
- Play. Defaults: UDP `52381`, TCP `52380`.

See Documentation~ for protocol details.


