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
http://localhost:8080
```

In the game, leave the server field as:

```text
auto
```

## Deploy

Click the Deploy to Render button above, sign in, approve the blueprint, and wait for the deploy to finish.

Render serves both the Unity WebGL build and the online relay from the same URL.
