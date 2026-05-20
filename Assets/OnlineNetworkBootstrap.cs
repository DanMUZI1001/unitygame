using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public class OnlineNetworkBootstrap : MonoBehaviour
{
    private readonly ConcurrentQueue<string> incomingMessages = new ConcurrentQueue<string>();

    private OneVsOneGame game;
    private bool isHost;
    private bool isClient;
    private bool connected;
    private bool initSent;
    private float nextSendTime;
    private string status = "Offline";
    private string serverUrl = "ws://localhost:8080";
    private string roomCode = "room1";

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

        if (!connected || game == null)
        {
            return;
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
                nextSendTime = Time.time + 0.05f;
                SendSnapshot();
            }
        }
        else if (isClient && Time.time >= nextSendTime)
        {
            nextSendTime = Time.time + 0.03f;
            SendInput();
        }
    }

    private void OnGUI()
    {
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18
        };

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = Color.white }
        };

        Rect panel = new Rect(Screen.width * 0.5f - 300f, Screen.height - 154f, 600f, 140f);
        GUI.Box(panel, "");
        GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, 560f, 22f), "Web Online: " + status, labelStyle);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 36f, 80f, 24f), "Server", labelStyle);
        serverUrl = GUI.TextField(new Rect(panel.x + 88f, panel.y + 34f, 235f, 26f), serverUrl);
        GUI.Label(new Rect(panel.x + 340f, panel.y + 36f, 60f, 24f), "Room", labelStyle);
        roomCode = GUI.TextField(new Rect(panel.x + 392f, panel.y + 34f, 110f, 26f), roomCode);

        if (GUI.Button(new Rect(panel.x + 12f, panel.y + 82f, 175f, 32f), "Host P1", buttonStyle))
        {
            StartHost();
        }

        if (GUI.Button(new Rect(panel.x + 210f, panel.y + 82f, 175f, 32f), "Client P2", buttonStyle))
        {
            StartClient();
        }

        if (GUI.Button(new Rect(panel.x + 408f, panel.y + 82f, 175f, 32f), "Disconnect", buttonStyle))
        {
            Disconnect();
        }
    }

    private void StartHost()
    {
        Disconnect();
        isHost = true;
        isClient = false;
        status = "Connecting host to " + serverUrl;
        PrepareLocalGameRole(true);
        WS_Connect(serverUrl, gameObject.name);
    }

    private void StartClient()
    {
        Disconnect();
        isHost = false;
        isClient = true;
        status = "Connecting client to " + serverUrl;
        WS_Connect(serverUrl, gameObject.name);
    }

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
        status = isHost ? "Host connected. Waiting for P2." : "Client connected. Waiting for sync.";
        SendLine("JOIN|" + roomCode + "|" + (isHost ? "HOST" : "CLIENT"));
    }

    public void OnWebSocketMessage(string message)
    {
        incomingMessages.Enqueue(message);
    }

    public void OnWebSocketClose(string reason)
    {
        connected = false;
        status = "Disconnected";
    }

    public void OnWebSocketError(string error)
    {
        connected = false;
        status = "WebSocket error: " + error;
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
        WS_Close();
        status = "Offline";

        if (game != null)
        {
            game.SetOnlineRole(false, false);
        }
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
