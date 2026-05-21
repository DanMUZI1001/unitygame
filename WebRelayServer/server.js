const fs = require("fs");
const http = require("http");
const path = require("path");
const WebSocket = require("ws");

const port = Number(process.env.PORT || 8084);
const webRoot = path.resolve(__dirname, "..", "WebGLBuild");

const server = http.createServer((req, res) => {
  const urlPath = decodeURIComponent(req.url.split("?")[0]);

  if (urlPath === "/health") {
    res.writeHead(200, { "Content-Type": "text/plain", "Cache-Control": "no-store" });
    res.end("ok\n");
    return;
  }

  const relativePath = urlPath === "/" ? "index.html" : urlPath.replace(/^\/+/, "");
  const filePath = path.resolve(webRoot, relativePath);

  if (!filePath.startsWith(webRoot) || !fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
    res.writeHead(404, { "Content-Type": "text/plain" });
    res.end("WebGLBuild file not found. Build Unity WebGL into WebGLBuild/ first.\n");
    return;
  }

  const isBuildFile = filePath.startsWith(path.join(webRoot, "Build"));
  const headers = {
    "Content-Type": getContentType(filePath),
    "Cache-Control": filePath.endsWith("index.html") || isBuildFile ? "no-store" : "public, max-age=3600"
  };

  if (filePath.endsWith(".br")) {
    headers["Content-Encoding"] = "br";
  }

  if (filePath.endsWith(".gz")) {
    headers["Content-Encoding"] = "gzip";
  }

  res.writeHead(200, headers);
  fs.createReadStream(filePath).pipe(res);
});

const wss = new WebSocket.Server({
  server,
  perMessageDeflate: false
});
const rooms = new Map();
const clients = new Map();
let nextClientId = 1;

function send(socket, message) {
  if (socket && socket.readyState === WebSocket.OPEN) {
    socket.send(message);
  }
}

function getRoom(code) {
  if (!rooms.has(code)) {
    rooms.set(code, { host: null, client: null, lastInit: null, lastSnapshot: null });
  }

  return rooms.get(code);
}

function makeRoomCode() {
  return Math.random().toString(36).slice(2, 6).toUpperCase();
}

function getOpenRoom() {
  for (const [code, room] of rooms) {
    if (room.host && !room.client) {
      return { code, room };
    }
  }

  return null;
}

function broadcastLobby() {
  const users = [];

  for (const client of clients.values()) {
    users.push([
      client.id,
      encodeURIComponent(client.name || "Player"),
      client.ability || 0,
      client.x || 0,
      client.z || 0,
      client.roomCode || ""
    ].join(","));
  }

  const message = "LOBBY|" + users.join(";");

  for (const client of clients.values()) {
    send(client.socket, message);
  }
}

function leaveRoom(socket, notifyPeer) {
  if (!socket.roomCode) {
    return;
  }

  const room = getRoom(socket.roomCode);

  if (room.host === socket) {
    room.host = null;
    if (notifyPeer) {
      send(room.client, "SYS|Host disconnected");
      send(room.client, "LOBBY_BACK");
    }
  }

  if (room.client === socket) {
    room.client = null;
    if (notifyPeer) {
      send(room.host, "SYS|Client disconnected");
      send(room.host, "LOBBY_BACK");
    }
  }

  socket.roomCode = null;
  socket.role = null;
}

function enterRoom(socket, roomCode, role) {
  const room = getRoom(roomCode);
  leaveRoom(socket, true);

  socket.roomCode = roomCode;
  socket.role = role;

  if (role === "HOST") {
    if (room.host && room.host !== socket) {
      send(room.host, "SYS|Host replaced by a new connection");
      room.host.close();
    }

    room.host = socket;
    room.lastInit = null;
    room.lastSnapshot = null;
    send(socket, "ROOM|" + roomCode + "|HOST");

    if (room.client) {
      send(socket, "SYS|Client connected");
    }
  } else {
    if (!room.host) {
      send(socket, "SYS|Room has no host");
      socket.roomCode = null;
      socket.role = null;
      return;
    }

    if (room.client && room.client !== socket) {
      send(room.client, "SYS|Client replaced by a new connection");
      room.client.close();
    }

    room.client = socket;
    send(socket, "ROOM|" + roomCode + "|CLIENT");
    send(room.host, "SYS|Client connected");

    if (room.lastInit) {
      send(socket, room.lastInit);
    }

    if (room.lastSnapshot) {
      send(socket, room.lastSnapshot);
    }
  }

  const client = clients.get(socket.clientId);
  if (client) {
    client.roomCode = roomCode;
  }

  broadcastLobby();
}

wss.on("connection", (socket) => {
  socket.clientId = String(nextClientId++);
  socket.roomCode = null;
  socket.role = null;

  clients.set(socket.clientId, {
    id: socket.clientId,
    socket,
    name: "Player " + socket.clientId,
    ability: 0,
    x: Math.cos(Number(socket.clientId) * 1.7) * 4,
    z: Math.sin(Number(socket.clientId) * 1.7) * 4,
    roomCode: ""
  });

  send(socket, "ID|" + socket.clientId);
  broadcastLobby();

  socket.on("message", (data) => {
    const message = String(data);
    const parts = message.split("|");

    if (parts[0] === "HELLO") {
      const client = clients.get(socket.clientId);
      if (client) {
        client.name = parts[1] || client.name;
        client.ability = Number(parts[2] || 0);
      }

      broadcastLobby();
      return;
    }

    if (parts[0] === "LPOS") {
      const client = clients.get(socket.clientId);
      if (client) {
        client.x = Number(parts[1] || 0);
        client.z = Number(parts[2] || 0);
      }

      broadcastLobby();
      return;
    }

    if (parts[0] === "CREATE") {
      enterRoom(socket, parts[1] || makeRoomCode(), "HOST");
      return;
    }

    if (parts[0] === "JOIN_OPEN") {
      const open = getOpenRoom();
      if (!open) {
        send(socket, "SYS|No open room");
        return;
      }

      enterRoom(socket, open.code, "CLIENT");
      return;
    }

    if (parts[0] === "LEAVE_ROOM") {
      leaveRoom(socket, true);
      const client = clients.get(socket.clientId);
      if (client) {
        client.roomCode = "";
      }
      send(socket, "LOBBY_BACK");
      broadcastLobby();
      return;
    }

    if (parts[0] === "JOIN") {
      const roomCode = parts[1] || "room1";
      const role = parts[2] === "HOST" ? "HOST" : "CLIENT";
      enterRoom(socket, roomCode, role);
      return;
    }

    if (!socket.roomCode) {
      send(socket, "SYS|Join a room first");
      return;
    }

    const room = getRoom(socket.roomCode);

    if (socket.role === "HOST") {
      if (parts[0] === "INIT") {
        room.lastInit = message;
      } else if (parts[0] === "SNAP") {
        room.lastSnapshot = message;
      }

      send(room.client, message);
    } else {
      send(room.host, message);
    }
  });

  socket.on("close", () => {
    leaveRoom(socket, true);
    clients.delete(socket.clientId);
    broadcastLobby();
  });
});

server.listen(port, () => {
  console.log(`Unity Duel relay listening on ws://localhost:${port}`);
  console.log(`Serving WebGL files from ${webRoot}`);
});

function getContentType(filePath) {
  const cleanPath = filePath.replace(/\.(br|gz)$/i, "");
  const ext = path.extname(cleanPath).toLowerCase();

  switch (ext) {
    case ".html":
      return "text/html";
    case ".js":
      return "application/javascript";
    case ".wasm":
      return "application/wasm";
    case ".data":
      return "application/octet-stream";
    case ".json":
      return "application/json";
    case ".css":
      return "text/css";
    case ".png":
      return "image/png";
    case ".jpg":
    case ".jpeg":
      return "image/jpeg";
    case ".ico":
      return "image/x-icon";
    default:
      return "application/octet-stream";
  }
}
