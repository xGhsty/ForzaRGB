# ForzaRGB

**ForzaRGB** synchronizes your Corsair iCUE LINK RGB fans with Forza Horizon 6 telemetry data - colors change based on engine RPM, car class, and driving state.

[![License: CC BY-NC-ND 4.0](https://img.shields.io/badge/License-CC%20BY--NC--ND%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc-nd/4.0/)

---

## ⬇️ Download

> **Just want to use it?** Download the latest release — no need to install anything extra.

👉 **[Download latest release](../../releases/latest)**

After downloading:
1. Extract the zip
2. Download `iCUESDK.dll` from [Corsair SDK releases](https://github.com/CorsairOfficial/cue-sdk/releases) and place it next to `ForzaRGB.exe`
3. Run `ForzaRGB.exe`

---

## ⚠️ Disclaimer

ForzaRGB is an unofficial fan project and is not affiliated with, endorsed by, or connected to Microsoft, Turn 10 Studios, Playground Games, or Corsair. "Forza Horizon" is a trademark of Microsoft Corporation. "Corsair" and "iCUE" are trademarks of Corsair Gaming, Inc.

The application icon was created by the author for this project. It incorporates the Forza logo which is property of Microsoft Corporation and is used here purely for identification purposes in a non-commercial fan project.

---

## 💬 About

ForzaRGB is a personal hobby project built for fun. It was developed with the assistance of AI (Claude by Anthropic) as a learning and creative exercise - the ideas, design decisions, and testing were all done by me.

If you have strong opinions about AI-assisted development, that's fair - but this project exists purely for fun and to build something I actually wanted to have.

---

## ⚠️ Important - iCUE SDK

The `iCUESDK.dll` file is **not included** in this repository because it is proprietary software owned by Corsair. You must download it separately:

1. Go to https://github.com/CorsairOfficial/cue-sdk/releases
2. Download the latest `iCUESDK_x.x.xx.zip`
3. Extract and place the DLL next to `ForzaRGB.exe`

ForzaRGB will automatically find any DLL with "iCUESDK" in the filename - no need to rename it. It will tell you if the DLL is missing when you launch it.

---

## ✨ Features

- **Car class colors** - each class (D/C/B/A/S1/S2/R/X) has its own color matching the in-game badges
- **RPM-based color shift** - color darkens as RPM rises, then transitions to red at redline
- **3-stage blink warning** - slow → normal → fast blink as you approach and hit the rev limiter
- **Adaptive redline learning** - automatically learns each car's real RPM limit per gear change and saves it to a local database
- **Electric vehicle support** - detects EVs and shows a speed-based aqua fill effect instead of RPM colors
- **Pump head animation** - idle FH6-themed animation (magenta/green/blue) always runs on the pump head
- **Idle animation** - fans animate when the game is inactive

---

## 🖥️ Supported Hardware

ForzaRGB was built and tested on the following setup:

- **Corsair iCUE LINK TITAN 240 RX** (pump head + 2x RX RGB fans)
- **Corsair iCUE LINK System Hub**

It may work with other iCUE LINK devices, but this has not been tested. The app detects devices automatically - if your setup uses a different LED group ID than `0x000B` (pump) or `0x000C` (fans), the lighting may not work correctly.

Support for other Corsair devices may be added in the future.

---

## 📋 Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Corsair iCUE](https://www.corsair.com/icue) (running in background)
- **iCUE LINK System Hub** with RX RGB fans (tested on Corsair iCUE LINK TITAN 240 RX)
- [iCUE SDK DLL](https://github.com/CorsairOfficial/cue-sdk/releases) - download `iCUESDK_x.x.xx.zip`, extract `iCUESDK.dll` and place it next to `ForzaRGB.exe`
- Forza Horizon 6

---

## 🔧 Setup

### 1. Enable iCUE SDK
Open iCUE → Settings (gear icon) → **SDK** tab → enable **iCUE SDK**

### 2. Enable Data Out in Forza Horizon 6
Settings → HUD & Gameplay → **Telemetry** section:
- Output: **ON**
- IP: `127.0.0.1`
- Port: `7777`

### 3. Build and run
```bash
dotnet build
dotnet run
```

Or publish a standalone exe:
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

Place `iCUESDK.dll` next to the resulting `ForzaRGB.exe`.

### 4. Launch order
1. iCUE (running in background)
2. ForzaRGB.exe
3. Forza Horizon 6

---

## ⚙️ How it works

```
FH6 UDP telemetry (port 7777)
        ↓
  ForzaUdpService  →  parses packet → extracts RPM, gear, speed, car class
        ↓
  IcueService      →  maps data to RGB colors
        ↓
  iCUE LINK System Hub  →  RX RGB fans + TITAN pump head
```

### Color logic

| RPM range | Color |
|-----------|-------|
| 0–60% | Car class color (darkening) |
| 60–90% | Transition to red |
| 90%+ | Pure red `(255, 0, 0)` |

### Blink stages

| Stage | Speed | Threshold |
|-------|-------|-----------|
| 1 - slow | 150ms | 80% max RPM |
| 2 - normal | 110ms | ~92% learned max |
| 3 - fast | 35ms | ~98% learned max (true redline) |

### Car class colors (matching in-game badges)

| Class | Color |
|-------|-------|
| D | Cyan |
| C | Yellow |
| B | Orange |
| A | Red |
| S1 | Purple |
| S2 | Blue |
| R | Magenta |
| X | Lime green |

---

## 📁 Files

| File | Description |
|------|-------------|
| `Program.cs` | Entry point, connects services |
| `ForzaPacket.cs` | FH6 UDP packet structure (324 bytes) |
| `ForzaUdpService.cs` | Listens for UDP telemetry |
| `IcueService.cs` | Controls LEDs via iCUE SDK |
| `IcueSdk.cs` | P/Invoke wrapper for iCUESDK.dll |
| `RpmColorMapper.cs` | Maps RPM/class to RGB colors |
| `CarRpmDatabase.cs` | Saves learned RPM limits per car |
| `car_rpm_data.json` | Auto-generated, not included in repo |

---

## 📄 License

© 2026 xGhosty - [CC BY-NC-ND 4.0](LICENSE)

You may share this project freely for non-commercial purposes with attribution. You may not modify and redistribute it, and you may not use it commercially.
