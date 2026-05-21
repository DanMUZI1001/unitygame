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
    } else if (url.indexOf("://") === -1) {
      var explicitProtocol = window.location.protocol === "https:" ? "wss://" : "ws://";
      url = explicitProtocol + url;
    }

    var socket = new WebSocket(url);
    window.__unityDuelSocket = socket;
    window.__unityDuelObjectName = objectName;
    window.__unityDuelSendQueue = [];

    socket.onopen = function () {
      if (socket !== window.__unityDuelSocket) {
        return;
      }

      var queue = window.__unityDuelSendQueue || [];
      window.__unityDuelSendQueue = [];
      for (var i = 0; i < queue.length; i++) {
        socket.send(queue[i]);
      }

      SendMessage(objectName, "OnWebSocketOpen", "");
    };

    socket.onmessage = function (event) {
      if (socket !== window.__unityDuelSocket) {
        return;
      }

      SendMessage(objectName, "OnWebSocketMessage", String(event.data));
    };

    socket.onerror = function () {
      if (socket !== window.__unityDuelSocket) {
        return;
      }

      SendMessage(objectName, "OnWebSocketError", "connection failed");
    };

    socket.onclose = function () {
      if (socket !== window.__unityDuelSocket) {
        return;
      }

      SendMessage(objectName, "OnWebSocketClose", "");
    };
  },

  WS_Send: function (messagePtr) {
    var message = UTF8ToString(messagePtr);
    var socket = window.__unityDuelSocket;

    if (socket && socket.readyState === WebSocket.OPEN) {
      socket.send(message);
    } else if (socket && socket.readyState === WebSocket.CONNECTING) {
      window.__unityDuelSendQueue = window.__unityDuelSendQueue || [];
      if (window.__unityDuelSendQueue.length < 16) {
        window.__unityDuelSendQueue.push(message);
      }
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
