using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public class OnlineNetworkBootstrap : MonoBehaviour
{
    private const float HostSnapshotInterval = 0.05f;
    private const float ClientInputInterval = 0.03f;
    private const float FirstReconnectDelay = 0.25f;
    private const float MaxReconnectDelay = 2f;

    private readonly ConcurrentQueue<string> incomingMessages = new ConcurrentQueue<string>();

    private OneVsOneGame game;
    private bool isHost;
    private bool isClient;
    private bool connected;
    private bool initSent;
    private bool shouldReconnect;
    private bool manualDisconnect;
    private int reconnectAttempts;
    private float reconnectAt;
    private float nextSendTime;
    private float nextLobbySendTime;
    private string status = "Offline";
    private string serverUrl = "auto";
    private string roomCode = "room1";
    private string playerName;
    private DuelAbility lobbyAbility;

    public static string LocalClientId { get; private set; } = "";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WS_Connect(string url, string objectName);

    [DllImport("__Internal")]
    private static extern void WS_Send(string message);

    [DllImport("__Internal")]
    private static extern void WS_Close();
#else
    private static void WS_Connect(string url, string objectName) { }
    private static void WS_Send(string message) { }
    private static void WS_Close() { }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnPlay()
    {
        if (FindAnyObjectByType<OnlineNetworkBootstrap>() != null)
        {
            return;
        }

        new GameObject("Online Network Bootstrap").AddComponent<OnlineNetworkBootstrap>();
    }

    private void Awake()
    {
        gameObject.name = "Online Network Bootstrap";
        DontDestroyOnLoad(gameObject);
        playerName = "Player" + UnityEngine.Random.Range(1000, 9999);
        lobbyAbility = (DuelAbility)UnityEngine.Random.Range(0, System.Enum.GetValues(typeof(DuelAbility)).Length);
    }

    private void Start()
    {
        ConnectLobby();
    }

    private void Update()
    {
        if (game == null)
        {
            game = FindAnyObjectByType<OneVsOneGame>();
        }

        while (incomingMessages.TryDequeue(out string message))
        {
            ProcessMessage(message);
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            CreateRoom();
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            JoinOpenRoom();
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            LeaveRoom();
        }

        if (!connected)
        {
            TryReconnect();
            return;
        }

        if (game == null)
        {
            return;
        }

        if (!isHost && !isClient && Time.time >= nextLobbySendTime)
        {
            nextLobbySendTime = Time.time + 0.25f;
            Vector3 position = game.GetLobbyLocalPosition();
            SendLine($"LPOS|{Format(position.x)}|{Format(position.z)}");
        }

        if (isHost)
        {
            if (!initSent && game.Player1 != null && game.Player2 != null)
            {
                SendLine($"INIT|{game.CurrentMapIndex}|{(int)game.Player1.Ability}|{(int)game.Player2.Ability}");
                initSent = true;
            }

            if (Time.time >= nextSendTime)
            {
                nextSendTime = Time.time + HostSnapshotInterval;
                SendSnapshot();
            }
        }
        else if (isClient && Time.time >= nextSendTime)
        {
            nextSendTime = Time.time + ClientInputInterval;
            SendInput();
        }
    }

    private void StartHost()
    {
        isHost = true;
        isClient = false;
        initSent = false;
        status = "Room " + roomCode + " host";
        if (game != null)
        {
            game.StartRound(UnityEngine.Random.Range(0, 14), lobbyAbility, (DuelAbility)UnityEngine.Random.Range(0, System.Enum.GetValues(typeof(DuelAbility)).Length));
        }
        PrepareLocalGameRole(true);
    }

    private void StartClient()
    {
        isHost = false;
        isClient = true;
        initSent = false;
        status = "Room " + roomCode + " client. Waiting for sync.";
    }

    private void ConnectLobby()
    {
        isHost = false;
        isClient = false;
        shouldReconnect = true;
        manualDisconnect = false;
        reconnectAttempts = 0;
        status = "Connecting lobby to " + serverUrl;
        WS_Connect(serverUrl, gameObject.name);
    }

    private void CreateRoom()
    {
        if (!connected)
        {
            ConnectLobby();
            return;
        }

        SendLine("CREATE");
    }

    private void JoinOpenRoom()
    {
        if (!connected)
        {
            ConnectLobby();
            return;
        }

        SendLine("JOIN_OPEN");
    }

    private void LeaveRoom()
    {
        isHost = false;
        isClient = false;
        initSent = false;
        SendLine("LEAVE_ROOM");

        if (game != null)
        {
            game.ShowLobby();
        }
    }

    private void OnGUI()
    {
        const float width = 300f;
        Rect panel = new Rect(16f, Mathf.Max(16f, Screen.height - 154f), width, 138f);
        Color oldColor = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = Color.white;

        string roomText = string.IsNullOrEmpty(roomCode) ? "-" : roomCode;
        GUI.Label(new Rect(panel.x + 10f, panel.y + 8f, width - 20f, 42f), "Online: " + (connected ? "Connected" : "Connecting") + "\n" + status);
        GUI.Label(new Rect(panel.x + 10f, panel.y + 50f, width - 20f, 22f), "Room: " + roomText);

        bool canUseRoomButtons = connected && !isHost && !isClient;
        GUI.enabled = canUseRoomButtons;
        if (GUI.Button(new Rect(panel.x + 10f, panel.y + 78f, 132f, 26f), "Create 1v1"))
        {
            CreateRoom();
        }

        if (GUI.Button(new Rect(panel.x + 158f, panel.y + 78f, 132f, 26f), "Join Open"))
        {
            JoinOpenRoom();
        }

        GUI.enabled = connected && (isHost || isClient);
        if (GUI.Button(new Rect(panel.x + 10f, panel.y + 110f, 280f, 22f), "Leave Room"))
        {
            LeaveRoom();
        }

        GUI.enabled = true;
        GUI.color = oldColor;
    }

    public string Status => status;
    public string RoomCode => roomCode;
    public bool IsOnlineConnected => connected;
    public bool IsInRoom => isHost || isClient;

    private void PrepareLocalGameRole(bool host)
    {
        if (game == null)
        {
            game = FindAnyObjectByType<OneVsOneGame>();
        }

        if (game != null)
        {
            game.SetOnlineRole(true, host);
        }
    }

    public void OnWebSocketOpen(string unused)
    {
        connected = true;
        reconnectAttempts = 0;
        status = "Lobby connected";
        SendLine("HELLO|" + playerName + "|" + (int)lobbyAbility);

        if (isHost || isClient)
        {
            SendLine("JOIN|" + roomCode + "|" + (isHost ? "HOST" : "CLIENT"));
        }
    }

    public void OnWebSocketMessage(string message)
    {
        incomingMessages.Enqueue(message);
    }

    public void OnWebSocketClose(string reason)
    {
        connected = false;
        initSent = false;
        status = manualDisconnect ? "Disconnected" : "Disconnected. Reconnecting...";
        ScheduleReconnect();
    }

    public void OnWebSocketError(string error)
    {
        connected = false;
        status = "WebSocket error: " + error;
        ScheduleReconnect();
    }

    private void ProcessMessage(string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length == 0)
        {
            return;
        }

        if (parts[0] == "SYS")
        {
            status = parts.Length > 1 ? parts[1] : "Server message";
            if (isHost && status.Contains("Client connected"))
            {
                initSent = false;
            }

            return;
        }

        if (game == null)
        {
            game = FindAnyObjectByType<OneVsOneGame>();
        }

        if (game == null)
        {
            return;
        }

        switch (parts[0])
        {
            case "ID":
                if (parts.Length > 1)
                {
                    LocalClientId = parts[1];
                }
                break;
            case "LOBBY":
                if (!isHost && !isClient && game != null)
                {
                    game.ApplyLobbyUsers(parts.Length > 1 ? parts[1] : "");
                    status = "Lobby connected. F1 create, F2 join";
                }
                break;
            case "ROOM":
                if (parts.Length < 3)
                {
                    return;
                }

                roomCode = parts[1];
                if (parts[2] == "HOST")
                {
                    StartHost();
                }
                else
                {
                    StartClient();
                }
                break;
            case "LOBBY_BACK":
                isHost = false;
                isClient = false;
                initSent = false;
                if (game != null)
                {
                    game.ShowLobby();
                }
                status = "Back in lobby";
                break;
            case "INIT":
                if (!isClient || parts.Length < 4)
                {
                    return;
                }

                int mapIndex = ParseInt(parts[1]);
                DuelAbility p1Ability = (DuelAbility)ParseInt(parts[2]);
                DuelAbility p2Ability = (DuelAbility)ParseInt(parts[3]);
                game.StartRound(mapIndex, p1Ability, p2Ability);
                game.SetOnlineRole(true, false);
                status = "Synced as Player 2";
                break;
            case "IN":
                if (!isHost || parts.Length < 7)
                {
                    return;
                }

                game.ApplyRemoteInput(new DuelInputState
                {
                    MoveX = ParseFloat(parts[1]),
                    MoveZ = ParseFloat(parts[2]),
                    Jump = parts[3] == "1",
                    Attack = parts[4] == "1",
                    SkillOne = parts[5] == "1",
                    SkillTwo = parts[6] == "1"
                });
                break;
            case "SNAP":
                if (!isClient || parts.Length < 13)
                {
                    return;
                }

                game.ApplyNetworkSnapshot(
                    new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])),
                    Quaternion.Euler(0f, ParseFloat(parts[4]), 0f),
                    ParseInt(parts[5]),
                    new Vector3(ParseFloat(parts[6]), ParseFloat(parts[7]), ParseFloat(parts[8])),
                    Quaternion.Euler(0f, ParseFloat(parts[9]), 0f),
                    ParseInt(parts[10]),
                    ParseFloat(parts[11]),
                    Encoding.UTF8.GetString(Convert.FromBase64String(parts[12])));
                break;
        }
    }

    private void SendInput()
    {
        if (game == null)
        {
            return;
        }

        DuelInputState input = game.ReadPlayer2LocalInput();
        SendLine($"IN|{Format(input.MoveX)}|{Format(input.MoveZ)}|{Bool(input.Jump)}|{Bool(input.Attack)}|{Bool(input.SkillOne)}|{Bool(input.SkillTwo)}");
    }

    private void SendSnapshot()
    {
        if (game == null || game.Player1 == null || game.Player2 == null)
        {
            return;
        }

        Transform p1 = game.Player1.transform;
        Transform p2 = game.Player2.transform;
        string winner = Convert.ToBase64String(Encoding.UTF8.GetBytes(game.WinnerMessage));

        SendLine(
            "SNAP|" +
            $"{Format(p1.position.x)}|{Format(p1.position.y)}|{Format(p1.position.z)}|{Format(p1.eulerAngles.y)}|{game.Player1.Health}|" +
            $"{Format(p2.position.x)}|{Format(p2.position.y)}|{Format(p2.position.z)}|{Format(p2.eulerAngles.y)}|{game.Player2.Health}|" +
            $"{Format(game.TimeLeft)}|{winner}");
    }

    private void SendLine(string line)
    {
        if (!connected && !line.StartsWith("JOIN|", StringComparison.Ordinal))
        {
            return;
        }

        WS_Send(line);
    }

    private void Disconnect()
    {
        connected = false;
        initSent = false;
        shouldReconnect = false;
        manualDisconnect = true;
        WS_Close();
        status = "Offline";

        if (game != null)
        {
            game.SetOnlineRole(false, false);
        }
    }

    private void TryReconnect()
    {
        if (!shouldReconnect || manualDisconnect || Time.unscaledTime < reconnectAt)
        {
            return;
        }

        reconnectAttempts++;
        status = (isHost ? "Reconnecting host..." : "Reconnecting client...") + " attempt " + reconnectAttempts;
        WS_Connect(serverUrl, gameObject.name);
        ScheduleReconnect();
    }

    private void ScheduleReconnect()
    {
        if (!shouldReconnect || manualDisconnect)
        {
            return;
        }

        float delay = Mathf.Min(MaxReconnectDelay, FirstReconnectDelay * Mathf.Pow(2f, reconnectAttempts));
        reconnectAt = Time.unscaledTime + delay;
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private float ParseFloat(string value)
    {
        return float.Parse(value, CultureInfo.InvariantCulture);
    }

    private int ParseInt(string value)
    {
        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    private string Bool(bool value)
    {
        return value ? "1" : "0";
    }
}
