mergeInto(LibraryManager.library, {
  WS_Connect: function (urlPtr, objectNamePtr) {
    var url = UTF8ToString(urlPtr);
    var objectName = UTF8ToString(objectNamePtr);

    if (window.__unityDuelSocket) {
      window.__unityDuelSocket.close();
    }

    if (!url || url === "auto") {
      var protocol = window.location.protocol === "https:" ? "wss://" : "ws://";
      url = protocol + window.location.host;
    }

    var socket = new WebSocket(url);
    window.__unityDuelSocket = socket;
    window.__unityDuelObjectName = objectName;

    socket.onopen = function () {
      SendMessage(objectName, "OnWebSocketOpen", "");
    };

    socket.onmessage = function (event) {
      SendMessage(objectName, "OnWebSocketMessage", String(event.data));
    };

    socket.onerror = function () {
      SendMessage(objectName, "OnWebSocketError", "connection failed");
    };

    socket.onclose = function () {
      SendMessage(objectName, "OnWebSocketClose", "");
    };
  },

  WS_Send: function (messagePtr) {
    var message = UTF8ToString(messagePtr);
    var socket = window.__unityDuelSocket;

    if (socket && socket.readyState === WebSocket.OPEN) {
      socket.send(message);
    }
  },

  WS_Close: function () {
    var socket = window.__unityDuelSocket;

    if (socket) {
      socket.close();
      window.__unityDuelSocket = null;
    }
  }
});
