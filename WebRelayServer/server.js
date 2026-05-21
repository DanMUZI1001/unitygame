const fs = require("fs");
const http = require("http");
const path = require("path");
const WebSocket = require("ws");

const port = Number(process.env.PORT || 8080);
const webRoot = path.resolve(process.cwd(), "WebGLBuild");

const server = http.createServer((req, res) => {
  const urlPath = decodeURIComponent(req.url.split("?")[0]);
  const relativePath = urlPath === "/" ? "index.html" : urlPath.replace(/^\/+/, "");
  const filePath = path.resolve(webRoot, relativePath);

  if (!filePath.startsWith(webRoot) || !fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
    res.writeHead(404, { "Content-Type": "text/plain" });
    res.end("WebGLBuild file not found. Build Unity WebGL into WebGLBuild/ first.\n");
    return;
  }

  const headers = { "Content-Type": getContentType(filePath) };

  if (filePath.endsWith(".br")) {
    headers["Content-Encoding"] = "br";
  }

  if (filePath.endsWith(".gz")) {
    headers["Content-Encoding"] = "gzip";
  }

  res.writeHead(200, headers);
  fs.createReadStream(filePath).pipe(res);
});

const wss = new WebSocket.Server({ server });
const rooms = new Map();

function send(socket, message) {
  if (socket && socket.readyState === WebSocket.OPEN) {
    socket.send(message);
  }
}

function getRoom(code) {
  if (!rooms.has(code)) {
    rooms.set(code, { host: null, client: null });
  }

  return rooms.get(code);
}

wss.on("connection", (socket) => {
  socket.roomCode = null;
  socket.role = null;

  socket.on("message", (data) => {
    const message = String(data);
    const parts = message.split("|");

    if (parts[0] === "JOIN") {
      const roomCode = parts[1] || "room1";
      const role = parts[2] === "HOST" ? "HOST" : "CLIENT";
      const room = getRoom(roomCode);

      socket.roomCode = roomCode;
      socket.role = role;

      if (role === "HOST") {
        room.host = socket;
        send(socket, "SYS|Host joined room " + roomCode);
      } else {
        room.client = socket;
        send(socket, "SYS|Client joined room " + roomCode);
        send(room.host, "SYS|Client connected");
      }

      return;
    }

    if (!socket.roomCode) {
      send(socket, "SYS|Join a room first");
      return;
    }

    const room = getRoom(socket.roomCode);

    if (socket.role === "HOST") {
      send(room.client, message);
    } else {
      send(room.host, message);
    }
  });

  socket.on("close", () => {
    if (!socket.roomCode) {
      return;
    }

    const room = getRoom(socket.roomCode);

    if (room.host === socket) {
      room.host = null;
      send(room.client, "SYS|Host disconnected");
    }

    if (room.client === socket) {
      room.client = null;
      send(room.host, "SYS|Client disconnected");
    }
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
