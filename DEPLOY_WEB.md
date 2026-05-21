# Web Deployment

This project can run in a browser as a Unity WebGL build.

## 1. Build Unity WebGL

In Unity:

1. Open `File > Build Profiles` or `File > Build Settings`.
2. Select `WebGL`.
3. Click `Switch Platform`.
4. Build to the project-root folder named `WebGLBuild`.

The included batch build method is:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\UnityProject\Storage' -executeMethod WebGLBuildScript.Build -logFile 'C:\UnityProject\Storage\webgl-build.log'
```

Close the Unity Editor before using batchmode.

## 2. Deploy the WebGL Build

Option A, simplest: deploy this whole repository to Render.
The included Node server serves both:

- the Unity WebGL files in `WebGLBuild/`
- the WebSocket relay on the same domain

In this mode, leave the in-game server field as:

```text
auto
```

The WebGL client will connect to the current page host automatically.

Option B: use separate hosts.
Upload the generated `WebGLBuild` folder to a static web host such as Vercel, Netlify, itch.io, or GitHub Pages.

## 3. Deploy the Relay Server

The relay server is in:

```text
WebRelayServer/
```

Run locally from the repository root:

```powershell
cd WebRelayServer
npm install
cd ..
node WebRelayServer/server.js
```

Local browser URL:

```text
http://localhost:8084
```

Lobby controls:

```text
Create 1v1 button or 1: Create room
Join Open button or 2: Join open room
Leave Room button or 3: Leave room
```

Local in-game server URL:

```text
auto
```

For public HTTPS hosting, deploy the relay to Render, Railway, Fly.io, or another Node host.
The browser game should use a secure WebSocket URL:

```text
wss://your-relay-server.example.com
```

## 4. Play Online

1. Open the WebGL game link in two browsers or on two PCs.
2. Use the same relay server URL. If the game and relay are on the same Render app, use `auto`.
3. One player clicks `Create 1v1` or presses `1`.
4. The other player clicks `Join Open` or presses `2`.
5. The match uses one shared camera, not split screen.
