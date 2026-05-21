# Unity Duel WebGL

[![Deploy to Render](https://render.com/images/deploy-to-render-button.svg)](https://render.com/deploy?repo=https://github.com/DanMUZI1001/unitygame)

Unity WebGL 1v1 duel game with a Node WebSocket relay server.

## Play Locally

```powershell
cd WebRelayServer
npm install
cd ..
node WebRelayServer/server.js
```

Open:

```text
http://localhost:8084
```

In the game, leave the server field as:

```text
auto
```

Lobby controls:

```text
Create 1v1 button or 1: Create room
Join Open button or 2: Join open room
Leave Room button or 3: Leave room
```

The game uses the Node WebSocket relay, so two browser tabs or two PCs that open the same URL can meet in the online lobby and enter a 1v1 room.

Match camera:

```text
Single shared camera, no split screen
```

## Deploy

Click the Deploy to Render button above, sign in, approve the blueprint, and wait for the deploy to finish.

Render serves both the Unity WebGL build and the online relay from the same URL.
