# SimpleV2Ray

A simple client in terminal hosting V2Ray (v2ray-core) on Windows.

![screenshot.png](./img/screenshot.png)

## Features

- Auto system proxy (HTTP) setup based on V2Ray configuration.
- Outbound stats monitoring.

## Quick Start

1. Put `v2ray.exe` and `v2ctl.exe` into folder `v2ray-core`.
2. Edit V2Ray configuration in file `config.json`.
   - Make sure you have one inbound that accepts `http` protocol.
   - Make sure all of the outbounds have tags so that they can be shown in the stats.
3. Run `SimpleV2Ray.exe`.
