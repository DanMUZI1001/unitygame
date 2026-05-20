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

Upload the generated `WebGLBuild` folder to a static web host such as:

- Vercel
- Netlify
- itch.io
- GitHub Pages

The included `vercel.json` expects the WebGL output at:

```text
WebGLBuild/
```

## 3. Deploy the Relay Server

The relay server is in:

```text
WebRelayServer/
```

Run locally:

```powershell
cd WebRelayServer
npm install
npm start
```

Local game server URL:

```text
ws://localhost:8080
```

For public HTTPS hosting, deploy the relay to Render, Railway, Fly.io, or another Node host.
The browser game should use a secure WebSocket URL:

```text
wss://your-relay-server.example.com
```

## 4. Play Online

1. Open the WebGL game link in two browsers or on two PCs.
2. Use the same relay server URL.
3. Use the same room code.
4. One player clicks `Host P1`.
5. The other clicks `Client P2`.
