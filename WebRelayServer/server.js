const http = require("http");
const WebSocket = require("ws");

const port = Number(process.env.PORT || 8080);
const server = http.createServer((req, res) => {
  res.writeHead(200, { "Content-Type": "text/plain" });
  res.end("Unity Duel relay is running\n");
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
});
