using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

public enum DuelAbility
{
    DashMaster,
    FireMage,
    IceMage,
    Healer,
    Thunder,
    Wind,
    Stone,
    Shadow,
    Poison,
    Magnet
}

public struct DuelInputState
{
    public float MoveX;
    public float MoveZ;
    public float AimYaw;
    public bool Jump;
    public bool Attack;
    public bool SkillOne;
    public bool SkillTwo;
}

public class OneVsOneGame : MonoBehaviour
{
    private const float MatchTime = 180f;
    private const float MatchMapScale = 2f;
    private const float OuterWallHeight = 8f;
    private const float ArenaHalfWidth = 8.75f;
    private const float ArenaHalfLength = 5.75f;

    private readonly string[] mapNames =
    {
        "Classic",
        "Cross",
        "Pillars",
        "Corridor",
        "Corners",
        "Maze",
        "Holes",
        "Void Cross",
        "Split Void",
        "Islands",
        "Jump Steps",
        "Sky Pads",
        "Big Arena",
        "Tower Heights"
    };

    private DuelPlayer player1;
    private DuelPlayer player2;
    private SplitScreenCameraFollow player1Camera;
    private SplitScreenCameraFollow player2Camera;
    private Transform mapRoot;
    private Transform lobbyRoot;
    private readonly Dictionary<string, GameObject> lobbyAvatars = new Dictionary<string, GameObject>();
    private readonly Dictionary<Color32, Material> visibleMaterials = new Dictionary<Color32, Material>();
    private float timeLeft;
    private string winnerMessage = "";
    private int currentMapIndex;
    private Vector2 currentMapSize = new Vector2(18f, 12f);
    private bool onlineMode;
    private bool onlineHost;
    private bool lobbyMode;
    private bool matchRunning;
    private bool scaleMatchGeometry;
    private int lastMapIndex;
    private DuelAbility lastPlayer1Ability;
    private DuelAbility lastPlayer2Ability;
    private string lastPlayer1Name = "Player 1";
    private string lastPlayer2Name = "Player 2";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnPlay()
    {
        if (FindAnyObjectByType<OneVsOneGame>() != null)
        {
            return;
        }

        new GameObject("1v1 Game").AddComponent<OneVsOneGame>();
    }

    [Preserve]
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PreserveRuntimePrimitiveColliders()
    {
        _ = typeof(BoxCollider);
        _ = typeof(CapsuleCollider);
        _ = typeof(SphereCollider);
        _ = typeof(CharacterController);
    }

    private void Start()
    {
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
        Time.fixedDeltaTime = 1f / 60f;

        CreateCameraAndLight();
        ShowLobby();
    }

    private void Update()
    {
        if (lobbyMode)
        {
            return;
        }

        if (player1 == null || player2 == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(winnerMessage))
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartCurrentRound();
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                ReturnToMenu();
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            ReturnToMenu();
            return;
        }

        if (!matchRunning)
        {
            return;
        }

        timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
        CheckFallDeaths();

        if (player1.Health <= 0)
        {
            winnerMessage = BuildWinMessage(player2, "Player 2");
        }
        else if (player2.Health <= 0)
        {
            winnerMessage = BuildWinMessage(player1, "Player 1");
        }
        else if (timeLeft <= 0f)
        {
            EndByTime();
        }
    }

    private void StartRound()
    {
        StartRound(Random.Range(0, mapNames.Length), GetRandomAbility(), GetRandomAbility());
    }

    public void StartRound(int mapIndex, DuelAbility p1Ability, DuelAbility p2Ability)
    {
        StartRound(mapIndex, p1Ability, p2Ability, "Player 1", "Player 2");
    }

    public void StartRound(int mapIndex, DuelAbility p1Ability, DuelAbility p2Ability, string player1Name, string player2Name)
    {
        winnerMessage = "";
        timeLeft = MatchTime;
        lobbyMode = false;
        matchRunning = true;
        lastMapIndex = Mathf.Clamp(mapIndex, 0, mapNames.Length - 1);
        lastPlayer1Ability = p1Ability;
        lastPlayer2Ability = p2Ability;
        lastPlayer1Name = string.IsNullOrWhiteSpace(player1Name) ? "Player 1" : player1Name;
        lastPlayer2Name = string.IsNullOrWhiteSpace(player2Name) ? "Player 2" : player2Name;

        ClearLobby();
        ClearOldRound();
        currentMapIndex = lastMapIndex;
        BuildMap(currentMapIndex);

        player1 = CreatePlayer(
            lastPlayer1Name,
            GetPlayerSpawnPosition(1),
            Color.blue,
            KeyCode.W,
            KeyCode.S,
            KeyCode.A,
            KeyCode.D,
            KeyCode.Space,
            KeyCode.Mouse0,
            KeyCode.E,
            KeyCode.R,
            p1Ability);

        player2 = CreatePlayer(
            lastPlayer2Name,
            GetPlayerSpawnPosition(2),
            Color.red,
            KeyCode.UpArrow,
            KeyCode.DownArrow,
            KeyCode.LeftArrow,
            KeyCode.RightArrow,
            KeyCode.Space,
            KeyCode.Mouse0,
            KeyCode.E,
            KeyCode.R,
            p2Ability);

        player1.SetOpponent(player2);
        player2.SetOpponent(player1);
        ConfigurePlayerInputForCurrentMode();
        AssignSingleCameraTargets();
    }

    public void ShowLobby()
    {
        lobbyMode = true;
        onlineMode = false;
        matchRunning = false;
        winnerMessage = "";
        ClearOldRound();
        ClearLobby();

        mapRoot = new GameObject("Lobby Map").transform;
        lobbyRoot = new GameObject("Lobby Avatars").transform;
        currentMapSize = new Vector2(24f, 16f);
        CreateBlock("Lobby Floor", Vector3.zero, new Vector3(24f, 0.25f, 16f), new Color(0.22f, 0.33f, 0.32f), true);
        CreateBlock("Lobby Center", new Vector3(0f, 0.15f, 0f), new Vector3(4f, 0.35f, 4f), new Color(0.32f, 0.42f, 0.38f), true);
        CreateLobbyText("LobbyText", "ONLINE LOBBY\n1 / F1 Create Room\n2 / F2 Join Open Room\n3 / F3 Leave Room", new Vector3(0f, 0.35f, 6.1f), 0.38f);
        SetupLobbyCameras();
    }

    public Vector3 GetLobbyLocalPosition()
    {
        string localId = OnlineNetworkBootstrap.LocalClientId;
        if (!string.IsNullOrEmpty(localId) && lobbyAvatars.TryGetValue(localId, out GameObject avatar))
        {
            return avatar.transform.position;
        }

        return new Vector3(0f, 1f, -4f);
    }

    public void ApplyLobbyUsers(string payload)
    {
        if (!lobbyMode)
        {
            ShowLobby();
        }

        HashSet<string> seen = new HashSet<string>();
        if (!string.IsNullOrEmpty(payload))
        {
            string[] users = payload.Split(';');
            for (int i = 0; i < users.Length; i++)
            {
                if (string.IsNullOrEmpty(users[i]))
                {
                    continue;
                }

                string[] fields = users[i].Split(',');
                if (fields.Length < 6)
                {
                    continue;
                }

                string id = fields[0];
                string displayName = System.Uri.UnescapeDataString(fields[1]);
                DuelAbility ability = (DuelAbility)Mathf.Clamp(ParseLobbyInt(fields[2]), 0, System.Enum.GetValues(typeof(DuelAbility)).Length - 1);
                Vector3 position = new Vector3(ParseLobbyFloat(fields[3]), 1f, ParseLobbyFloat(fields[4]));
                string room = fields[5];

                seen.Add(id);
                UpdateLobbyAvatar(id, displayName, ability, position, room);
            }
        }

        List<string> removeIds = new List<string>();
        foreach (string id in lobbyAvatars.Keys)
        {
            if (!seen.Contains(id))
            {
                removeIds.Add(id);
            }
        }

        for (int i = 0; i < removeIds.Count; i++)
        {
            Destroy(lobbyAvatars[removeIds[i]]);
            lobbyAvatars.Remove(removeIds[i]);
        }
    }

    public void ShowLocalLobbyAvatar(string displayName, DuelAbility ability, string room)
    {
        if (!lobbyMode)
        {
            ShowLobby();
        }

        string id = string.IsNullOrEmpty(OnlineNetworkBootstrap.LocalClientId) ? "local-preview" : OnlineNetworkBootstrap.LocalClientId;
        UpdateLobbyAvatar(id, displayName, ability, new Vector3(0f, 1f, -4f), room);
    }

    public void SetOnlineRole(bool isOnline, bool isHost)
    {
        onlineMode = isOnline;
        onlineHost = isHost;
        ConfigurePlayerInputForCurrentMode();
        AssignSingleCameraTargets();
    }

    public void SetMatchRunning(bool running)
    {
        matchRunning = running;
    }

    public int CurrentMapIndex => currentMapIndex;
    public float TimeLeft => timeLeft;
    public string WinnerMessage => winnerMessage;
    public DuelPlayer Player1 => player1;
    public DuelPlayer Player2 => player2;

    public void SetTimeLeft(float value)
    {
        timeLeft = Mathf.Clamp(value, 0f, MatchTime);
    }

    public void ApplyRemoteInput(DuelInputState input)
    {
        if (player2 != null)
        {
            player2.SetExternalInput(input);
        }
    }

    public DuelInputState ReadPlayer2LocalInput()
    {
        return player2 != null ? player2.ReadLocalInput() : new DuelInputState();
    }

    public void ApplyNetworkSnapshot(Vector3 p1Position, Quaternion p1Rotation, int p1Health, Vector3 p2Position, Quaternion p2Rotation, int p2Health, float syncedTime, string syncedWinner)
    {
        if (player1 == null || player2 == null)
        {
            return;
        }

        player1.ApplyNetworkState(p1Position, p1Rotation, p1Health);
        if (onlineMode && !onlineHost)
        {
            player2.ApplyNetworkCorrection(p2Position, p2Rotation, p2Health);
        }
        else
        {
            player2.ApplyNetworkState(p2Position, p2Rotation, p2Health);
        }

        timeLeft = syncedTime;
        winnerMessage = syncedWinner ?? "";
    }

    private void ConfigurePlayerInputForCurrentMode()
    {
        if (player1 == null || player2 == null)
        {
            return;
        }

        if (!onlineMode)
        {
            player1.SetControls(KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space, KeyCode.Mouse0, KeyCode.E, KeyCode.R);
            player2.SetControls(KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space, KeyCode.Mouse0, KeyCode.E, KeyCode.R);
            player1.SetLocalInputEnabled(true);
            player1.SetExternalInputEnabled(false);
            player2.SetLocalInputEnabled(true);
            player2.SetExternalInputEnabled(false);
            return;
        }

        player1.SetControls(KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space, KeyCode.Mouse0, KeyCode.E, KeyCode.R);
        player2.SetControls(KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space, KeyCode.Mouse0, KeyCode.E, KeyCode.R);
        player1.SetLocalInputEnabled(onlineHost);
        player1.SetExternalInputEnabled(false);
        player2.SetLocalInputEnabled(!onlineHost);
        player2.SetExternalInputEnabled(onlineHost);
    }

    private void ClearOldRound()
    {
        foreach (DuelProjectile projectile in FindObjectsByType<DuelProjectile>())
        {
            Destroy(projectile.gameObject);
        }

        foreach (DuelPlayer player in FindObjectsByType<DuelPlayer>())
        {
            Destroy(player.gameObject);
        }

        if (mapRoot != null)
        {
            Destroy(mapRoot.gameObject);
        }
    }

    private void EndByTime()
    {
        if (player1.Health > player2.Health)
        {
            winnerMessage = "Time Up! " + BuildWinMessage(player1, "Player 1");
        }
        else if (player2.Health > player1.Health)
        {
            winnerMessage = "Time Up! " + BuildWinMessage(player2, "Player 2");
        }
        else
        {
            winnerMessage = "Time Up! Draw!";
        }
    }

    private string BuildWinMessage(DuelPlayer winner, string fallbackName)
    {
        if (winner == null)
        {
            return fallbackName + " Wins!";
        }

        return winner.name + " (" + winner.AbilityDisplayName + ") Wins!";
    }

    private DuelPlayer GetLocalPlayer()
    {
        if (!onlineMode)
        {
            return player1;
        }

        return onlineHost ? player1 : player2;
    }

    private DuelPlayer GetRemotePlayer()
    {
        if (!onlineMode)
        {
            return player2;
        }

        return onlineHost ? player2 : player1;
    }

    private void RestartCurrentRound()
    {
        if (onlineMode && !onlineHost)
        {
            return;
        }

        StartRound(lastMapIndex, lastPlayer1Ability, lastPlayer2Ability, lastPlayer1Name, lastPlayer2Name);
        if (onlineMode)
        {
            OnlineNetworkBootstrap network = FindAnyObjectByType<OnlineNetworkBootstrap>();
            if (network != null)
            {
                network.MarkMatchRestarted();
            }
        }
    }

    private void ReturnToMenu()
    {
        OnlineNetworkBootstrap network = FindAnyObjectByType<OnlineNetworkBootstrap>();
        if (network != null && network.IsInRoom)
        {
            network.LeaveToLobby();
            return;
        }

        ShowLobby();
    }

    private void CheckFallDeaths()
    {
        CheckFallDeath(player1);
        CheckFallDeath(player2);
    }

    private void CheckFallDeath(DuelPlayer player)
    {
        if (player == null || player.Health <= 0)
        {
            return;
        }

        Vector3 position = player.transform.position;
        bool fellDown = position.y < -2f;

        if (fellDown)
        {
            player.Kill();
        }
    }

    private DuelAbility GetRandomAbility()
    {
        int count = System.Enum.GetValues(typeof(DuelAbility)).Length;
        return (DuelAbility)Random.Range(0, count);
    }

    private Vector3 GetPlayerSpawnPosition(int playerNumber)
    {
        float side = playerNumber == 1 ? -1f : 1f;

        switch (currentMapIndex)
        {
            case 12:
                return new Vector3(side * 14f * MatchMapScale, 1f, 0f);
            case 13:
                return new Vector3(side * 6f * MatchMapScale, 1.8f, -5.5f * MatchMapScale);
            default:
                return new Vector3(side * 5.8f * MatchMapScale, 1f, 0f);
        }
    }

    private Vector2 GetMapSize(int mapIndex)
    {
        switch (mapIndex)
        {
            case 12:
                return new Vector2(40f, 28f);
            case 13:
                return new Vector2(24f, 24f);
            default:
                return new Vector2(18f, 12f);
        }
    }

    private DuelPlayer CreatePlayer(string playerName, Vector3 position, Color color, KeyCode up, KeyCode down, KeyCode left, KeyCode right, KeyCode jump, KeyCode attack, KeyCode skillOne, KeyCode skillTwo, DuelAbility ability)
    {
        GameObject playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerObject.name = playerName;
        playerObject.transform.position = position;
        Renderer capsuleRenderer = playerObject.GetComponent<Renderer>();
        ApplyVisibleColor(capsuleRenderer, color);
        capsuleRenderer.enabled = true;
        Collider playerCollider = playerObject.GetComponent<Collider>();
        if (playerCollider != null)
        {
            Destroy(playerCollider);
        }

        CharacterController controller = playerObject.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.43f;
        controller.skinWidth = 0.04f;
        controller.stepOffset = 0.25f;
        controller.slopeLimit = 45f;

        DuelPlayer player = playerObject.AddComponent<DuelPlayer>();
        player.Setup(playerName, up, down, left, right, jump, attack, skillOne, skillTwo, ability);
        CreateAlwaysVisibleBody(playerObject.transform, color);

        GameObject attackVisual = new GameObject("Attack Visual");
        attackVisual.transform.SetParent(playerObject.transform);
        attackVisual.transform.localPosition = Vector3.zero;
        attackVisual.transform.localRotation = Quaternion.identity;
        attackVisual.SetActive(false);
        player.SetAttackVisual(attackVisual);

        CreateNameTag(playerObject.transform, playerName);
        return player;
    }

    private void CreateAlwaysVisibleBody(Transform parent, Color color)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Always Visible Player Body";
        body.transform.SetParent(parent);
        body.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = new Vector3(0.82f, 0.88f, 0.82f);
        ApplyVisibleColor(body.GetComponent<Renderer>(), Color.Lerp(color, Color.white, 0.18f));
        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
        {
            Destroy(bodyCollider);
        }

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Always Visible Player Head";
        head.transform.SetParent(parent);
        head.transform.localPosition = new Vector3(0f, 1.08f, 0f);
        head.transform.localRotation = Quaternion.identity;
        head.transform.localScale = Vector3.one * 0.42f;
        ApplyVisibleColor(head.GetComponent<Renderer>(), Color.Lerp(color, Color.white, 0.45f));
        Collider headCollider = head.GetComponent<Collider>();
        if (headCollider != null)
        {
            Destroy(headCollider);
        }
    }

    private void UpdateLobbyAvatar(string id, string displayName, DuelAbility ability, Vector3 position, string room)
    {
        bool created = false;
        if (!lobbyAvatars.TryGetValue(id, out GameObject avatar))
        {
            avatar = new GameObject("Lobby Avatar " + id);
            avatar.transform.SetParent(lobbyRoot != null ? lobbyRoot : mapRoot);
            avatar.transform.position = position;
            CreateAlwaysVisibleBody(avatar.transform, Color.Lerp(Color.cyan, Color.white, 0.35f));
            CreateLobbyText("Name", displayName, new Vector3(0f, 2.2f, 0f), 0.18f).transform.SetParent(avatar.transform, false);
            lobbyAvatars[id] = avatar;
            created = true;
        }

        if (!created)
        {
            avatar.transform.position = Vector3.Lerp(avatar.transform.position, position, 16f * Time.deltaTime);
        }
        TextMesh label = avatar.GetComponentInChildren<TextMesh>();
        if (label != null)
        {
            label.text = string.IsNullOrEmpty(room) ? displayName : displayName + "\nRoom " + room;
        }
    }

    private GameObject CreateLobbyText(string objectName, string text, Vector3 position, float size)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(lobbyRoot != null ? lobbyRoot : mapRoot);
        textObject.transform.position = position;
        textObject.transform.rotation = Quaternion.Euler(65f, 0f, 0f);

        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.characterSize = size;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
        return textObject;
    }

    private void SetupLobbyCameras()
    {
        if (player1Camera != null)
        {
            player1Camera.GetComponent<Camera>().enabled = true;
            Camera camera = player1Camera.GetComponent<Camera>();
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            player1Camera.SetStaticView(new Vector3(0f, 13f, -10f), Quaternion.Euler(58f, 0f, 0f));
        }

        if (player2Camera != null)
        {
            player2Camera.GetComponent<Camera>().enabled = false;
        }
    }

    private void ClearLobby()
    {
        foreach (GameObject avatar in lobbyAvatars.Values)
        {
            Destroy(avatar);
        }

        lobbyAvatars.Clear();

        if (lobbyRoot != null)
        {
            Destroy(lobbyRoot.gameObject);
            lobbyRoot = null;
        }
    }

    private float ParseLobbyFloat(string value)
    {
        float parsed;
        return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed) ? parsed : 0f;
    }

    private int ParseLobbyInt(string value)
    {
        int parsed;
        return int.TryParse(value, out parsed) ? parsed : 0;
    }

    private void BuildMap(int mapIndex)
    {
        mapRoot = new GameObject("Map - " + mapNames[mapIndex]).transform;
        currentMapSize = GetMapSize(mapIndex);
        scaleMatchGeometry = true;

        BuildFloor(mapIndex);

        switch (mapIndex)
        {
            case 0:
                AddObstacle("Center Block", 0f, 0f, 2f, 2f);
                AddObstacle("Top Block", 0f, 3.3f, 4f, 0.7f);
                AddObstacle("Bottom Block", 0f, -3.3f, 4f, 0.7f);
                break;
            case 1:
                AddObstacle("Cross H", 0f, 0f, 5.5f, 0.65f);
                AddObstacle("Cross V", 0f, 0f, 0.65f, 5f);
                break;
            case 2:
                AddObstacle("Pillar 1", -3.5f, 2.7f, 1.2f, 1.2f);
                AddObstacle("Pillar 2", 3.5f, 2.7f, 1.2f, 1.2f);
                AddObstacle("Pillar 3", -3.5f, -2.7f, 1.2f, 1.2f);
                AddObstacle("Pillar 4", 3.5f, -2.7f, 1.2f, 1.2f);
                break;
            case 3:
                AddObstacle("Left Lane", -2.5f, 0f, 0.8f, 8f);
                AddObstacle("Right Lane", 2.5f, 0f, 0.8f, 8f);
                AddObstacle("Mid Cover", 0f, 0f, 1.4f, 1.4f);
                break;
            case 4:
                AddObstacle("Top Left", -5f, 3f, 3f, 1f);
                AddObstacle("Top Right", 5f, 3f, 3f, 1f);
                AddObstacle("Bottom Left", -5f, -3f, 3f, 1f);
                AddObstacle("Bottom Right", 5f, -3f, 3f, 1f);
                break;
            case 5:
                AddObstacle("Maze 1", -4f, 2.3f, 4f, 0.7f);
                AddObstacle("Maze 2", 4f, -2.3f, 4f, 0.7f);
                AddObstacle("Maze 3", -1.8f, -1.2f, 0.7f, 3.2f);
                AddObstacle("Maze 4", 1.8f, 1.2f, 0.7f, 3.2f);
                break;
            case 6:
                AddObstacle("Hole Map Cover 1", -5.2f, 3.5f, 2f, 0.7f);
                AddObstacle("Hole Map Cover 2", 5.2f, -3.5f, 2f, 0.7f);
                break;
            case 7:
                AddObstacle("Void Cross Cover", 0f, 0f, 1.2f, 1.2f);
                break;
            case 8:
                AddObstacle("Split Left Cover", -4.7f, 0f, 1.1f, 2.4f);
                AddObstacle("Split Right Cover", 4.7f, 0f, 1.1f, 2.4f);
                break;
            case 9:
                AddObstacle("Island Center Cover", 0f, 0f, 1.2f, 1.2f);
                break;
            case 10:
                AddObstacle("Step Cover Left", -3.3f, 0f, 0.9f, 0.9f);
                AddObstacle("Step Cover Right", 3.3f, 0f, 0.9f, 0.9f);
                break;
            case 11:
                AddObstacle("Sky Pad Cover", 0f, 0f, 0.9f, 0.9f);
                break;
            case 12:
                AddObstacle("Big Center Block", 0f, 0f, 4f, 4f);
                AddObstacle("Big North Cover", -8f, 8f, 6f, 1.2f);
                AddObstacle("Big South Cover", 8f, -8f, 6f, 1.2f);
                AddObstacle("Big West Pillar", -12f, -6f, 2.2f, 2.2f);
                AddObstacle("Big East Pillar", 12f, 6f, 2.2f, 2.2f);
                break;
            default:
                AddObstacle("Tower Center Cover", 0f, 0f, 1.2f, 1.2f);
                break;
        }

        AddExtraObstacles(mapIndex);
        scaleMatchGeometry = false;
    }

    private void BuildFloor(int mapIndex)
    {
        switch (mapIndex)
        {
            case 6:
                BuildHoleFloor();
                break;
            case 7:
                BuildVoidCrossFloor();
                break;
            case 8:
                BuildSplitVoidFloor();
                break;
            case 9:
                BuildIslandFloor();
                break;
            case 10:
                BuildJumpStepsFloor();
                break;
            case 11:
                BuildSkyPadsFloor();
                break;
            case 12:
                BuildBigArenaFloor();
                break;
            case 13:
                BuildTowerHeightsFloor();
                break;
            default:
                BuildPerforatedArenaFloor();
                break;
        }
    }

    private void BuildPerforatedArenaFloor()
    {
        Color floorColor = new Color(0.25f, 0.36f, 0.31f);
        const float tileSize = 2f;
        float halfWidth = 9f;
        float halfLength = 6f;

        for (float x = -halfWidth + tileSize * 0.5f; x < halfWidth; x += tileSize)
        {
            for (float z = -halfLength + tileSize * 0.5f; z < halfLength; z += tileSize)
            {
                if (IsFloorHoleTile(x, z))
                {
                    continue;
                }

                CreateBlock("Arena Floor Tile", new Vector3(x, 0f, z), new Vector3(tileSize, 0.25f, tileSize), floorColor, true);
            }
        }

        CreateBlock("Left Spawn Safe Floor", new Vector3(-5.8f, 0.02f, 0f), new Vector3(3.8f, 0.22f, 3.8f), floorColor, true);
        CreateBlock("Right Spawn Safe Floor", new Vector3(5.8f, 0.02f, 0f), new Vector3(3.8f, 0.22f, 3.8f), floorColor, true);
    }

    private bool IsFloorHoleTile(float x, float z)
    {
        return
            IsInsideRect(x, z, -2.8f, 2.8f, 1.8f, 1.4f) ||
            IsInsideRect(x, z, 2.8f, -2.8f, 1.8f, 1.4f) ||
            IsInsideRect(x, z, 0f, 0f, 1.4f, 1.4f) ||
            IsInsideRect(x, z, -6f, -3.8f, 1.6f, 1.2f) ||
            IsInsideRect(x, z, 6f, 3.8f, 1.6f, 1.2f);
    }

    private bool IsInsideRect(float x, float z, float centerX, float centerZ, float width, float length)
    {
        return Mathf.Abs(x - centerX) <= width * 0.5f && Mathf.Abs(z - centerZ) <= length * 0.5f;
    }

    private void AddExtraObstacles(int mapIndex)
    {
        AddObstacle("Extra North Left Cover", -6.5f, 4.4f, 2.2f, 0.9f);
        AddObstacle("Extra North Right Cover", 6.5f, 4.4f, 2.2f, 0.9f);
        AddObstacle("Extra South Left Cover", -6.5f, -4.4f, 2.2f, 0.9f);
        AddObstacle("Extra South Right Cover", 6.5f, -4.4f, 2.2f, 0.9f);
        AddObstacle("Extra Mid Pillar A", -3.2f, 0f, 1.1f, 1.1f);
        AddObstacle("Extra Mid Pillar B", 3.2f, 0f, 1.1f, 1.1f);

        if (mapIndex >= 10)
        {
            AddObstacle("Extra High Cover A", -7.5f, 7.5f, 2.4f, 1.2f);
            AddObstacle("Extra High Cover B", 7.5f, -7.5f, 2.4f, 1.2f);
        }
    }

    private void BuildHoleFloor()
    {
        CreateBlock("Floor Left", new Vector3(-6.75f, 0f, 0f), new Vector3(4.5f, 0.25f, 12f), new Color(0.23f, 0.34f, 0.29f), true);
        CreateBlock("Floor Right", new Vector3(6.75f, 0f, 0f), new Vector3(4.5f, 0.25f, 12f), new Color(0.23f, 0.34f, 0.29f), true);
        CreateBlock("Floor Top", new Vector3(0f, 0f, 4.3f), new Vector3(9f, 0.25f, 3.4f), new Color(0.23f, 0.34f, 0.29f), true);
        CreateBlock("Floor Bottom", new Vector3(0f, 0f, -4.3f), new Vector3(9f, 0.25f, 3.4f), new Color(0.23f, 0.34f, 0.29f), true);
        CreateBlock("Floor Center Strip H", new Vector3(0f, 0f, 0f), new Vector3(9f, 0.25f, 1.2f), new Color(0.23f, 0.34f, 0.29f), true);
        CreateBlock("Floor Center Strip V", new Vector3(0f, 0f, 0f), new Vector3(1.2f, 0.25f, 6.8f), new Color(0.23f, 0.34f, 0.29f), true);
    }

    private void BuildVoidCrossFloor()
    {
        Color floorColor = new Color(0.24f, 0.35f, 0.32f);
        CreateBlock("Cross Floor H", Vector3.zero, new Vector3(18f, 0.25f, 2.2f), floorColor, true);
        CreateBlock("Cross Floor V", Vector3.zero, new Vector3(2.4f, 0.25f, 12f), floorColor, true);
        CreateBlock("Spawn Pad Left", new Vector3(-5.8f, 0f, 0f), new Vector3(3.2f, 0.25f, 3.2f), floorColor, true);
        CreateBlock("Spawn Pad Right", new Vector3(5.8f, 0f, 0f), new Vector3(3.2f, 0.25f, 3.2f), floorColor, true);
    }

    private void BuildSplitVoidFloor()
    {
        Color floorColor = new Color(0.24f, 0.34f, 0.3f);
        CreateBlock("Left Platform", new Vector3(-5.1f, 0f, 0f), new Vector3(6.2f, 0.25f, 10.5f), floorColor, true);
        CreateBlock("Right Platform", new Vector3(5.1f, 0f, 0f), new Vector3(6.2f, 0.25f, 10.5f), floorColor, true);
        CreateBlock("Top Bridge", new Vector3(0f, 0f, 3.9f), new Vector3(4f, 0.25f, 1.1f), floorColor, true);
        CreateBlock("Bottom Bridge", new Vector3(0f, 0f, -3.9f), new Vector3(4f, 0.25f, 1.1f), floorColor, true);
    }

    private void BuildIslandFloor()
    {
        Color floorColor = new Color(0.25f, 0.36f, 0.31f);
        CreateBlock("Left Island", new Vector3(-5.8f, 0f, 0f), new Vector3(4.4f, 0.25f, 5.2f), floorColor, true);
        CreateBlock("Right Island", new Vector3(5.8f, 0f, 0f), new Vector3(4.4f, 0.25f, 5.2f), floorColor, true);
        CreateBlock("Center Island", Vector3.zero, new Vector3(3f, 0.25f, 3f), floorColor, true);
        CreateBlock("North Bridge", new Vector3(0f, 0f, 2.5f), new Vector3(9f, 0.25f, 0.9f), floorColor, true);
        CreateBlock("South Bridge", new Vector3(0f, 0f, -2.5f), new Vector3(9f, 0.25f, 0.9f), floorColor, true);
    }

    private void BuildJumpStepsFloor()
    {
        Color floorColor = new Color(0.24f, 0.35f, 0.33f);
        CreateBlock("Left Spawn Platform", new Vector3(-6.2f, 0f, 0f), new Vector3(3.8f, 0.25f, 4.8f), floorColor, true);
        CreateBlock("Right Spawn Platform", new Vector3(6.2f, 0f, 0f), new Vector3(3.8f, 0.25f, 4.8f), floorColor, true);
        CreatePlatform("Step 1", -3.3f, 0f, 0.45f, 2.2f, 2.6f);
        CreatePlatform("Step 2", 0f, 0f, 0.9f, 2.4f, 2.4f);
        CreatePlatform("Step 3", 3.3f, 0f, 0.45f, 2.2f, 2.6f);
        CreateBlock("Top Thin Bridge", new Vector3(0f, 0.1f, 3.7f), new Vector3(11f, 0.25f, 0.75f), floorColor, true);
        CreateBlock("Bottom Thin Bridge", new Vector3(0f, 0.1f, -3.7f), new Vector3(11f, 0.25f, 0.75f), floorColor, true);
    }

    private void BuildSkyPadsFloor()
    {
        Color floorColor = new Color(0.25f, 0.36f, 0.31f);
        CreateBlock("Left Sky Pad", new Vector3(-5.8f, 0f, 0f), new Vector3(4f, 0.25f, 4f), floorColor, true);
        CreateBlock("Right Sky Pad", new Vector3(5.8f, 0f, 0f), new Vector3(4f, 0.25f, 4f), floorColor, true);
        CreatePlatform("North Mid Pad", -2.2f, 2.8f, 0.55f, 2.2f, 2f);
        CreatePlatform("Center High Pad", 0f, 0f, 1.15f, 2.3f, 2.3f);
        CreatePlatform("South Mid Pad", 2.2f, -2.8f, 0.55f, 2.2f, 2f);
        CreateBlock("Small Left Bridge", new Vector3(-3.9f, 0.15f, 1.9f), new Vector3(2.2f, 0.25f, 0.65f), floorColor, true);
        CreateBlock("Small Right Bridge", new Vector3(3.9f, 0.15f, -1.9f), new Vector3(2.2f, 0.25f, 0.65f), floorColor, true);
    }

    private void BuildBigArenaFloor()
    {
        Color floorColor = new Color(0.23f, 0.34f, 0.3f);
        CreateBlock("Huge Arena Floor", Vector3.zero, new Vector3(40f, 0.25f, 28f), floorColor, true);
        CreateBlock("Huge Arena North Ridge", new Vector3(0f, 0.25f, 10f), new Vector3(24f, 0.45f, 1.1f), new Color(0.3f, 0.38f, 0.34f), true);
        CreateBlock("Huge Arena South Ridge", new Vector3(0f, 0.25f, -10f), new Vector3(24f, 0.45f, 1.1f), new Color(0.3f, 0.38f, 0.34f), true);
        CreateBlock("Huge Arena Mid Lane", Vector3.zero, new Vector3(2f, 0.4f, 22f), new Color(0.28f, 0.36f, 0.32f), true);
    }

    private void BuildTowerHeightsFloor()
    {
        Color floorColor = new Color(0.24f, 0.35f, 0.33f);
        CreateBlock("Lower Left Platform", new Vector3(-6f, 0f, -5.5f), new Vector3(4.5f, 0.25f, 4f), floorColor, true);
        CreateBlock("Lower Right Platform", new Vector3(6f, 0f, -5.5f), new Vector3(4.5f, 0.25f, 4f), floorColor, true);
        CreatePlatform("Mid Left Tower", -4f, -1.6f, 1.8f, 3.4f, 3.4f);
        CreatePlatform("Mid Right Tower", 4f, -1.6f, 1.8f, 3.4f, 3.4f);
        CreatePlatform("High Center Tower", 0f, 2f, 3.4f, 4f, 4f);
        CreatePlatform("Top North Tower", 0f, 6.3f, 5.2f, 4.5f, 3.2f);
        CreateBlock("Left High Bridge", new Vector3(-2.2f, 2.65f, 0.1f), new Vector3(4.4f, 0.3f, 0.75f), floorColor, true);
        CreateBlock("Right High Bridge", new Vector3(2.2f, 2.65f, 0.1f), new Vector3(4.4f, 0.3f, 0.75f), floorColor, true);
        CreateBlock("North High Bridge", new Vector3(0f, 4.25f, 4.2f), new Vector3(0.85f, 0.3f, 4.4f), floorColor, true);
    }

    private void CreatePlatform(string platformName, float x, float z, float height, float width, float length)
    {
        CreateBlock(platformName, new Vector3(x, height * 0.5f, z), new Vector3(width, height, length), new Color(0.32f, 0.4f, 0.36f), true);
    }

    private void AddObstacle(string blockName, float x, float z, float width, float length)
    {
        CreateBlock(blockName, new Vector3(x, OuterWallHeight * 0.5f, z), new Vector3(width, OuterWallHeight, length), new Color(0.45f, 0.42f, 0.32f), true);
    }

    private GameObject CreateBlock(string blockName, Vector3 position, Vector3 scale, Color color, bool hasCollider)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = blockName;
        block.transform.SetParent(mapRoot);
        if (scaleMatchGeometry)
        {
            position = new Vector3(position.x * MatchMapScale, position.y, position.z * MatchMapScale);
            scale = new Vector3(scale.x * MatchMapScale, scale.y, scale.z * MatchMapScale);
        }

        block.transform.position = position;
        block.transform.localScale = scale;
        ApplyVisibleColor(block.GetComponent<Renderer>(), color);

        return block;
    }

    private GameObject CreateVisualCube(string objectName, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = objectName;
        visual.transform.SetParent(parent);
        visual.transform.localPosition = localPosition;
        visual.transform.localScale = localScale;
        ApplyVisibleColor(visual.GetComponent<Renderer>(), color);
        return visual;
    }

    private void CreateCameraAndLight()
    {
        foreach (Camera camera in FindObjectsByType<Camera>())
        {
            Destroy(camera.gameObject);
        }

        player1Camera = CreateSplitCamera("Duel Camera", new Rect(0f, 0f, 1f, 1f), true);
        player2Camera = CreateSplitCamera("Unused Secondary Camera", new Rect(0f, 0f, 1f, 1f), false);
        player2Camera.GetComponent<Camera>().enabled = false;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.9f, 0.92f, 0.95f);
        RenderSettings.fog = false;

        foreach (Light existingLight in FindObjectsByType<Light>())
        {
            Destroy(existingLight.gameObject);
        }

        GameObject sunObject = new GameObject("Duel Sun Light");
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 2.4f;
        sun.color = Color.white;
        sunObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

        GameObject fillObject = new GameObject("Duel Fill Light");
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 1.2f;
        fill.color = new Color(0.72f, 0.82f, 1f);
        fillObject.transform.rotation = Quaternion.Euler(35f, 145f, 0f);
    }

    private SplitScreenCameraFollow CreateSplitCamera(string cameraName, Rect viewport, bool mainCamera)
    {
        GameObject cameraObject = new GameObject(cameraName);
        if (mainCamera)
        {
            cameraObject.tag = "MainCamera";
        }

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.rect = viewport;
        camera.orthographic = false;
        camera.fieldOfView = 70f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 300f;
        camera.cullingMask = ~0;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.16f, 0.2f, 0.24f);

        SplitScreenCameraFollow follow = cameraObject.AddComponent<SplitScreenCameraFollow>();
        follow.SetMapScale(currentMapIndex >= 12 ? 1.25f : 1f);
        return follow;
    }

    private void AssignSingleCameraTargets()
    {
        if (player1 == null || player2 == null)
        {
            return;
        }

        if (player1Camera != null)
        {
            player1Camera.GetComponent<Camera>().enabled = true;
            player1Camera.GetComponent<Camera>().rect = new Rect(0f, 0f, 1f, 1f);
            Transform localTarget = onlineMode && !onlineHost ? player2.transform : player1.transform;
            player1Camera.SetThirdPersonTarget(localTarget);
            player1Camera.SetMapScale(currentMapIndex >= 12 ? 1.25f : 1f);
        }

        if (player2Camera != null)
        {
            player2Camera.GetComponent<Camera>().enabled = false;
        }
    }

    private void OnGUI()
    {
        GUI.color = Color.white;

        if (lobbyMode)
        {
            DrawPanel(new Rect(16f, 16f, 330f, 132f), "Online Lobby\nF1: Create 1v1 Room\nF2: Join Open Room\nF3: Leave Room\nPlayers here are real relay clients.");
            return;
        }

        if (player1 == null || player2 == null)
        {
            return;
        }

        DuelPlayer localPlayer = GetLocalPlayer();
        DuelPlayer remotePlayer = GetRemotePlayer();
        float rightPanelX = Mathf.Max(16f, Screen.width - 376f);
        float rightPanelY = Screen.width < 760 ? 172f : 16f;
        DrawPanel(new Rect(16f, 16f, 360f, 130f), localPlayer != null ? localPlayer.GetHudText("You") : "You");
        DrawPanel(new Rect(rightPanelX, rightPanelY, 360f, 110f), remotePlayer != null ? remotePlayer.GetHudText("Enemy") : "Enemy");
        DrawPanel(new Rect(Mathf.Max(16f, (Screen.width - 180f) * 0.5f), Screen.width < 760 ? 250f : 16f, 180f, 48f), matchRunning ? FormatTime(timeLeft) : "Waiting");
        DrawPanel(new Rect(16f, Screen.width < 760 ? 310f : 154f, 260f, 54f), onlineMode && !onlineHost ? "M: Menu\nHost: R Restart" : "M: Menu\nWin: R Restart");

        if (!string.IsNullOrEmpty(winnerMessage))
        {
            DrawPanel(new Rect((Screen.width - 420f) * 0.5f, (Screen.height - 118f) * 0.5f, 420f, 118f), winnerMessage + "\nR: Restart\nM: Menu");
        }
    }

    private void DrawPanel(Rect rect, string text)
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f), text);
        GUI.color = oldColor;
    }

    private void CreateNameTag(Transform parent, string text)
    {
        GameObject tagObject = new GameObject(text + " Name");
        tagObject.transform.SetParent(parent);
        tagObject.transform.localPosition = new Vector3(0f, 1.4f, 0f);

        TextMesh textMesh = tagObject.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.characterSize = 0.25f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
        tagObject.AddComponent<NameTagOcclusion>().Setup(parent);
    }

    private Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }

    private Material GetVisibleMaterial(Color color)
    {
        Color32 key = color;
        if (visibleMaterials.TryGetValue(key, out Material material) && material != null)
        {
            return material;
        }

        material = CreateMaterial(color);
        visibleMaterials[key] = material;
        return material;
    }

    private void ApplyVisibleColor(Renderer renderer, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.enabled = true;
        renderer.sharedMaterial = GetVisibleMaterial(color);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(seconds);
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }
}

public class DuelPlayer : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5.4f;
    [SerializeField] private float acceleration = 28f;
    [SerializeField] private float inputSmoothTime = 0.08f;
    [SerializeField] private float rotationDegreesPerSecond = 720f;
    [SerializeField] private float mouseTurnDegreesPerSecond = 105f;
    [SerializeField] private float jumpForce = 8.7f;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int attackDamage = 16;
    [SerializeField] private float attackRange = 1.65f;
    [SerializeField] private float attackCooldown = 0.8f;

    private DuelPlayer opponent;
    private GameObject attackVisual;
    private Transform characterVisual;
    private Vector3 characterVisualBaseScale = Vector3.one;
    private float characterVisualAttackEndTime;
    private DuelAbility ability;
    private KeyCode upKey;
    private KeyCode downKey;
    private KeyCode leftKey;
    private KeyCode rightKey;
    private KeyCode jumpKey;
    private KeyCode attackKey;
    private KeyCode skillOneKey;
    private KeyCode skillTwoKey;
    private Vector3 smoothedInput;
    private Vector3 inputSmoothVelocity;
    private Vector3 moveVelocity;
    private Vector3 pushVelocity;
    private CharacterController characterController;
    private float verticalVelocity;
    private float nextAttackTime;
    private float nextSkillOneTime;
    private float nextSkillTwoTime;
    private float hideAttackVisualTime;
    private float speedBoostEndTime;
    private float slowEndTime;
    private float stunEndTime;
    private float shieldEndTime;
    private float poisonTickTime;
    private int poisonTicksLeft;
    private Vector3 networkCorrectionPosition;
    private float networkCorrectionYaw;
    private bool hasNetworkCorrection;
    private bool acceptsLocalInput = true;
    private bool acceptsExternalInput;
    private DuelInputState externalInput;
    private DuelInputState activeInput;
    private float aimYaw;

    public int Health { get; private set; }
    public DuelAbility Ability => ability;
    public string AbilityDisplayName => GetAbilityName();

    public void Setup(string playerName, KeyCode up, KeyCode down, KeyCode left, KeyCode right, KeyCode jump, KeyCode attack, KeyCode skillOne, KeyCode skillTwo, DuelAbility selectedAbility)
    {
        name = playerName;
        upKey = up;
        downKey = down;
        leftKey = left;
        rightKey = right;
        jumpKey = jump;
        attackKey = attack;
        skillOneKey = skillOne;
        skillTwoKey = skillTwo;
        ability = selectedAbility;
        Health = maxHealth;
        aimYaw = transform.eulerAngles.y;
    }

    public void SetOpponent(DuelPlayer target)
    {
        opponent = target;
    }

    public void SetAttackVisual(GameObject visual)
    {
        attackVisual = visual;
    }

    public void SetCharacterVisual(Transform visual)
    {
        characterVisual = visual;
        if (characterVisual != null)
        {
            characterVisualBaseScale = characterVisual.localScale;
        }
    }

    public void SetLocalInputEnabled(bool enabled)
    {
        acceptsLocalInput = enabled;
    }

    public void SetControls(KeyCode up, KeyCode down, KeyCode left, KeyCode right, KeyCode jump, KeyCode attack, KeyCode skillOne, KeyCode skillTwo)
    {
        upKey = up;
        downKey = down;
        leftKey = left;
        rightKey = right;
        jumpKey = jump;
        attackKey = attack;
        skillOneKey = skillOne;
        skillTwoKey = skillTwo;
    }

    public void SetExternalInputEnabled(bool enabled)
    {
        acceptsExternalInput = enabled;
    }

    public void SetExternalInput(DuelInputState input)
    {
        externalInput = input;
    }

    public DuelInputState ReadLocalInput()
    {
        DuelInputState input = new DuelInputState();

        if (Input.GetKey(upKey))
        {
            input.MoveZ += 1f;
        }

        if (Input.GetKey(downKey))
        {
            input.MoveZ -= 1f;
        }

        if (Input.GetKey(leftKey))
        {
            input.MoveX -= 1f;
        }

        if (Input.GetKey(rightKey))
        {
            input.MoveX += 1f;
        }

        input.AimYaw = aimYaw;
        input.Jump = Input.GetKeyDown(jumpKey);
        input.Attack = attackKey == KeyCode.Mouse0 ? Input.GetMouseButtonDown(0) : Input.GetKeyDown(attackKey);
        input.SkillOne = Input.GetKeyDown(skillOneKey);
        input.SkillTwo = Input.GetKeyDown(skillTwoKey);
        return input;
    }

    public void ApplyNetworkState(Vector3 position, Quaternion rotation, int health)
    {
        transform.position = position;
        transform.rotation = rotation;
        aimYaw = rotation.eulerAngles.y;
        Health = Mathf.Clamp(health, 0, maxHealth);
        moveVelocity = Vector3.zero;
        pushVelocity = Vector3.zero;
    }

    public void ApplyNetworkHealth(int health)
    {
        Health = Mathf.Clamp(health, 0, maxHealth);
    }

    public void ApplyNetworkCorrection(Vector3 position, Quaternion rotation, int health)
    {
        Health = Mathf.Clamp(health, 0, maxHealth);
        networkCorrectionPosition = position;
        networkCorrectionYaw = rotation.eulerAngles.y;
        hasNetworkCorrection = true;
    }

    public string GetHudText(string label)
    {
        return $"{label} HP: {Health}\nAbility: {GetAbilityName()}\nMove: WASD  Jump: Space\nAim: Mouse  Attack: Click\nE: {GetSkillName(1)}  R: {GetSkillName(2)}";
    }

    public void TakeDamage(int damage)
    {
        if (Time.time < shieldEndTime)
        {
            damage = Mathf.CeilToInt(damage * 0.45f);
        }

        Health = Mathf.Max(Health - damage, 0);
    }

    public void Kill()
    {
        Health = 0;
    }

    public void Heal(int amount)
    {
        Health = Mathf.Min(Health + amount, maxHealth);
    }

    public void AddPush(Vector3 force)
    {
        pushVelocity += force;
    }

    public void ApplySlow(float duration)
    {
        slowEndTime = Mathf.Max(slowEndTime, Time.time + duration);
    }

    public void ApplyStun(float duration)
    {
        stunEndTime = Mathf.Max(stunEndTime, Time.time + duration);
    }

    public void ApplyPoison()
    {
        poisonTicksLeft = 4;
        poisonTickTime = Time.time + 0.5f;
    }

    private void Awake()
    {
        Health = maxHealth;
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (Health <= 0)
        {
            return;
        }

        if (acceptsLocalInput)
        {
            UpdateMouseAim();
            activeInput = ReadLocalInput();
        }
        else if (acceptsExternalInput)
        {
            activeInput = externalInput;
            aimYaw = externalInput.AimYaw;
            externalInput.Attack = false;
            externalInput.SkillOne = false;
            externalInput.SkillTwo = false;
            externalInput.Jump = false;
        }
        else
        {
            HideExpiredAttackVisual();
            return;
        }

        UpdateStatusEffects();
        ApplyAimRotation();
        Move();
        ApplyPendingNetworkCorrection();
        HandleActions();
        HideExpiredAttackVisual();
    }

    private void ApplyPendingNetworkCorrection()
    {
        if (!hasNetworkCorrection)
        {
            return;
        }

        Vector3 offset = networkCorrectionPosition - transform.position;
        float distance = offset.magnitude;
        if (distance > 2.4f)
        {
            MoveToNetworkPosition(networkCorrectionPosition);
            moveVelocity = Vector3.zero;
            pushVelocity = Vector3.zero;
        }
        else if (distance > 0.04f)
        {
            MoveToNetworkPosition(Vector3.Lerp(transform.position, networkCorrectionPosition, 0.22f));
        }

        aimYaw = Mathf.MoveTowardsAngle(aimYaw, networkCorrectionYaw, 360f * Time.deltaTime);
        hasNetworkCorrection = false;
    }

    private void MoveToNetworkPosition(Vector3 position)
    {
        if (characterController != null && characterController.enabled)
        {
            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = true;
            return;
        }

        transform.position = position;
    }

    private void UpdateMouseAim()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        float mouseX = Input.GetAxisRaw("Mouse X");
        if (Mathf.Abs(mouseX) > 0.001f)
        {
            aimYaw += mouseX * mouseTurnDegreesPerSecond * Time.deltaTime;
        }
    }

    private void ApplyAimRotation()
    {
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.Euler(0f, aimYaw, 0f),
            rotationDegreesPerSecond * Time.deltaTime);
    }

    private void HideExpiredAttackVisual()
    {
        if (attackVisual != null && attackVisual.activeSelf && Time.time >= hideAttackVisualTime)
        {
            attackVisual.SetActive(false);
        }
    }

    private void UpdateStatusEffects()
    {
        if (poisonTicksLeft > 0 && Time.time >= poisonTickTime)
        {
            TakeDamage(4);
            poisonTicksLeft--;
            poisonTickTime = Time.time + 0.5f;
        }
    }

    private void Move()
    {
        Vector3 rawInput = Vector3.zero;

        if (Time.time >= stunEndTime)
        {
            rawInput.x = activeInput.MoveX;
            rawInput.z = activeInput.MoveZ;
        }

        if (rawInput.sqrMagnitude > 1f)
        {
            rawInput.Normalize();
        }

        if (rawInput.sqrMagnitude > 0.001f)
        {
            rawInput = GetAimRelativeMove(rawInput);
        }

        smoothedInput = Vector3.SmoothDamp(smoothedInput, rawInput, ref inputSmoothVelocity, inputSmoothTime);

        float speedMultiplier = 1f;
        if (Time.time < speedBoostEndTime)
        {
            speedMultiplier += 0.45f;
        }

        if (Time.time < slowEndTime)
        {
            speedMultiplier *= 0.55f;
        }

        Vector3 targetVelocity = smoothedInput * moveSpeed * speedMultiplier;
        moveVelocity = Vector3.MoveTowards(moveVelocity, targetVelocity, acceleration * Time.deltaTime);
        pushVelocity = Vector3.MoveTowards(pushVelocity, Vector3.zero, 10f * Time.deltaTime);

        bool grounded = characterController != null ? characterController.isGrounded : transform.position.y <= 1.01f;

        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (grounded && activeInput.Jump && Time.time >= stunEndTime)
        {
            verticalVelocity = jumpForce;
        }
        else
        {
            verticalVelocity += -20f * Time.deltaTime;
        }

        Vector3 frameMovement = (moveVelocity + pushVelocity) * Time.deltaTime;
        frameMovement.y = verticalVelocity * Time.deltaTime;
        if (characterController != null && characterController.enabled)
        {
            CollisionFlags flags = characterController.Move(frameMovement);
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Vector3 blockedNormalVelocity = moveVelocity + pushVelocity;
                blockedNormalVelocity.y = 0f;
                if (blockedNormalVelocity.sqrMagnitude > 0.01f)
                {
                    moveVelocity *= 0.35f;
                    pushVelocity *= 0.35f;
                }
            }

            if ((flags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
        }
        else
        {
            transform.position += frameMovement;
        }

        if (characterController == null && transform.position.y < 1f && verticalVelocity <= 0f)
        {
            Vector3 groundedPosition = transform.position;
            groundedPosition.y = 1f;
            transform.position = groundedPosition;
            verticalVelocity = -1f;
        }

        AnimateCharacterVisual(moveVelocity + pushVelocity);
    }

    private Vector3 GetAimRelativeMove(Vector3 input)
    {
        Quaternion aimRotation = Quaternion.Euler(0f, aimYaw, 0f);
        Vector3 forward = aimRotation * Vector3.forward;
        Vector3 right = aimRotation * Vector3.right;
        Vector3 move = right * input.x + forward * input.z;
        return move.sqrMagnitude > 1f ? move.normalized : move;
    }

    private void AnimateCharacterVisual(Vector3 lookVelocity)
    {
        if (characterVisual == null)
        {
            return;
        }

        float moveAmount = Mathf.Clamp01(lookVelocity.magnitude / moveSpeed);
        float bob = Mathf.Sin(Time.time * 13f) * 0.045f * moveAmount;
        float lean = Mathf.Sin(Time.time * 13f) * 4f * moveAmount;

        if (Time.time < characterVisualAttackEndTime)
        {
            float attackT = Mathf.InverseLerp(characterVisualAttackEndTime - 0.18f, characterVisualAttackEndTime, Time.time);
            float punch = Mathf.Sin(attackT * Mathf.PI);
            characterVisual.localPosition = new Vector3(0f, 0.05f + bob, 0.08f * punch);
            characterVisual.localRotation = Quaternion.Euler(0f, 0f, lean) * Quaternion.Euler(-10f * punch, 0f, 0f);
            characterVisual.localScale = characterVisualBaseScale * (1f + 0.05f * punch);
            return;
        }

        characterVisual.localPosition = new Vector3(0f, 0.05f + bob, 0f);
        characterVisual.localRotation = Quaternion.Euler(0f, 0f, lean);
        characterVisual.localScale = Vector3.Lerp(characterVisual.localScale, characterVisualBaseScale, 14f * Time.deltaTime);
    }

    private void HandleActions()
    {
        if (activeInput.Attack && Time.time >= nextAttackTime)
        {
            BasicAttack();
        }

        if (activeInput.SkillOne && Time.time >= nextSkillOneTime)
        {
            UseSkillOne();
        }

        if (activeInput.SkillTwo && Time.time >= nextSkillTwoTime)
        {
            UseSkillTwo();
        }
    }

    private void BasicAttack()
    {
        int damage = attackDamage;
        float knockback = 10f;
        float cooldownMultiplier = 1f;
        float projectileSpeed = 13f;
        int slowSeconds = 0;
        float stunSeconds = 0f;
        bool poison = false;
        Color hitColor = new Color(1f, 0.85f, 0.15f);

        switch (ability)
        {
            case DuelAbility.DashMaster:
                damage = 14;
                knockback = 12f;
                cooldownMultiplier = 0.85f;
                projectileSpeed = 15f;
                hitColor = Color.yellow;
                break;
            case DuelAbility.FireMage:
                damage = 20;
                knockback = 8f;
                cooldownMultiplier = 1.05f;
                projectileSpeed = 12.5f;
                hitColor = Color.red;
                break;
            case DuelAbility.IceMage:
                damage = 12;
                knockback = 6f;
                slowSeconds = 1;
                projectileSpeed = 12f;
                hitColor = Color.cyan;
                break;
            case DuelAbility.Healer:
                damage = 13;
                knockback = 5f;
                projectileSpeed = 12f;
                hitColor = Color.green;
                break;
            case DuelAbility.Thunder:
                damage = 15;
                knockback = 8f;
                cooldownMultiplier = 0.9f;
                projectileSpeed = 15f;
                stunSeconds = 0.12f;
                hitColor = Color.yellow;
                break;
            case DuelAbility.Wind:
                damage = 11;
                knockback = 16f;
                cooldownMultiplier = 0.9f;
                projectileSpeed = 16f;
                hitColor = Color.white;
                break;
            case DuelAbility.Stone:
                damage = 24;
                knockback = 18f;
                cooldownMultiplier = 1.25f;
                projectileSpeed = 9.5f;
                hitColor = Color.gray;
                break;
            case DuelAbility.Shadow:
                damage = 18;
                knockback = 5f;
                cooldownMultiplier = 0.85f;
                projectileSpeed = 17f;
                hitColor = Color.black;
                break;
            case DuelAbility.Poison:
                damage = 10;
                knockback = 5f;
                poison = true;
                projectileSpeed = 12.5f;
                hitColor = Color.magenta;
                break;
            case DuelAbility.Magnet:
                damage = 13;
                knockback = 3f;
                projectileSpeed = 13.5f;
                hitColor = new Color(0.8f, 0.8f, 1f);
                break;
        }

        nextAttackTime = Time.time + attackCooldown * cooldownMultiplier;
        characterVisualAttackEndTime = Time.time + 0.18f;
        ShootProjectile(hitColor, projectileSpeed, damage, slowSeconds, stunSeconds, poison, knockback);
    }

    private void UseSkillOne()
    {
        nextSkillOneTime = Time.time + 4.5f;

        switch (ability)
        {
            case DuelAbility.DashMaster:
                pushVelocity += transform.forward * 12f;
                break;
            case DuelAbility.FireMage:
                ShootProjectile(Color.red, 8f, 18, 0, 0f, false);
                break;
            case DuelAbility.IceMage:
                ShootProjectile(Color.cyan, 7f, 10, 2, 0f, false);
                break;
            case DuelAbility.Healer:
                Heal(18);
                SpawnEffect(Color.green, 1.4f);
                break;
            case DuelAbility.Thunder:
                AreaDamageAt(transform.position, 3f, 14, Color.yellow, false, true);
                break;
            case DuelAbility.Wind:
                AreaPush(transform.position, 3.2f, 13f, Color.white, false);
                break;
            case DuelAbility.Stone:
                shieldEndTime = Time.time + 3f;
                SpawnEffect(Color.gray, 1.6f);
                break;
            case DuelAbility.Shadow:
                transform.position += transform.forward * 2.7f;
                SpawnEffect(Color.black, 1.2f);
                break;
            case DuelAbility.Poison:
                AreaDamageAt(transform.position + transform.forward * 1.8f, 2f, 8, Color.magenta, false, false);
                if (opponent != null && Vector3.Distance(transform.position + transform.forward * 1.8f, opponent.transform.position) <= 2f)
                {
                    opponent.ApplyPoison();
                }
                break;
            case DuelAbility.Magnet:
                AreaPush(transform.position, 4f, 11f, new Color(0.8f, 0.8f, 1f), true);
                break;
        }
    }

    private void UseSkillTwo()
    {
        nextSkillTwoTime = Time.time + 7.5f;

        switch (ability)
        {
            case DuelAbility.DashMaster:
                AreaDamageAt(transform.position + transform.forward * 1.5f, 2f, 24, Color.yellow, false, false);
                if (opponent != null && Vector3.Distance(transform.position + transform.forward * 1.5f, opponent.transform.position) <= 2f)
                {
                    opponent.AddPush(transform.forward * 8f);
                }
                break;
            case DuelAbility.FireMage:
                AreaDamage(2.5f, 24, Color.red, false, false);
                break;
            case DuelAbility.IceMage:
                AreaDamage(2.4f, 14, Color.cyan, true, false);
                break;
            case DuelAbility.Healer:
                shieldEndTime = Time.time + 4f;
                Heal(10);
                SpawnEffect(Color.green, 1.8f);
                break;
            case DuelAbility.Thunder:
                speedBoostEndTime = Time.time + 4f;
                AreaDamageAt(transform.position, 2.8f, 10, Color.yellow, false, true);
                break;
            case DuelAbility.Wind:
                transform.position += transform.forward * 3.8f;
                AreaPush(transform.position, 2.8f, 10f, Color.white, false);
                break;
            case DuelAbility.Stone:
                AreaDamage(2.3f, 18, Color.gray, false, true);
                break;
            case DuelAbility.Shadow:
                if (opponent != null)
                {
                    transform.position = opponent.transform.position - opponent.transform.forward * 1.4f;
                    AreaDamageAt(transform.position, 2.2f, 18, Color.black, false, false);
                }
                break;
            case DuelAbility.Poison:
                AreaDamage(2.6f, 10, Color.magenta, false, false);
                if (opponent != null && Vector3.Distance(transform.position, opponent.transform.position) <= 2.6f)
                {
                    opponent.ApplyPoison();
                }
                break;
            case DuelAbility.Magnet:
                shieldEndTime = Time.time + 2.5f;
                AreaPush(transform.position, 3.2f, 9f, new Color(0.8f, 0.8f, 1f), false);
                break;
        }
    }

    private bool IsOpponentInFront(float range)
    {
        return IsOpponentInFront(range, 0.25f);
    }

    private bool IsOpponentInFront(float range, float minimumDot)
    {
        if (opponent == null || opponent.Health <= 0)
        {
            return false;
        }

        Vector3 toOpponent = opponent.transform.position - transform.position;
        return toOpponent.magnitude <= range && Vector3.Dot(transform.forward, toOpponent.normalized) > minimumDot;
    }

    private bool IsAttackBlockedByWall()
    {
        if (opponent == null)
        {
            return true;
        }

        return false;
    }

    private void AreaDamage(float radius, int damage, Color color, bool slow, bool stun)
    {
        AreaDamageAt(transform.position, radius, damage, color, slow, stun);
    }

    private void AreaDamageAt(Vector3 center, float radius, int damage, Color color, bool slow, bool stun)
    {
        SpawnEffectAt(center, color, radius);

        if (opponent == null || Vector3.Distance(center, opponent.transform.position) > radius)
        {
            return;
        }

        opponent.TakeDamage(damage);

        if (slow)
        {
            opponent.ApplySlow(2.5f);
        }

        if (stun)
        {
            opponent.ApplyStun(0.55f);
        }
    }

    private void AreaPush(Vector3 center, float radius, float force, Color color, bool pull)
    {
        SpawnEffectAt(center, color, radius);

        if (opponent == null || Vector3.Distance(center, opponent.transform.position) > radius)
        {
            return;
        }

        Vector3 direction = pull ? center - opponent.transform.position : opponent.transform.position - center;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = transform.forward;
        }

        opponent.AddPush(direction.normalized * force);
    }

    private void ShootProjectile(Color color, float speed, int damage, int slowSeconds, float stunSeconds, bool poison, float knockback = 0f)
    {
        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = GetAbilityName() + " Projectile";
        projectileObject.transform.position = transform.position + transform.forward * 0.9f + Vector3.up * 0.2f;
        projectileObject.transform.localScale = Vector3.one * 0.45f;
        projectileObject.GetComponent<Renderer>().material = CreateMaterial(color);
        Collider projectileCollider = projectileObject.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            Destroy(projectileCollider);
        }

        BuildProjectileVisual(projectileObject.transform, color);

        DuelProjectile projectile = projectileObject.AddComponent<DuelProjectile>();
        projectile.Setup(this, opponent, transform.forward, speed, damage, slowSeconds, stunSeconds, poison, knockback);
    }

    private void BuildProjectileVisual(Transform projectileRoot, Color color)
    {
        switch (ability)
        {
            case DuelAbility.FireMage:
                AddEffectShape(projectileRoot, PrimitiveType.Sphere, "Flame Tail", new Vector3(0f, 0f, -0.65f), Vector3.one * 0.55f, Quaternion.identity, new Color(1f, 0.45f, 0.05f));
                AddEffectShape(projectileRoot, PrimitiveType.Sphere, "Flame Spark", new Vector3(0.28f, 0.14f, -0.35f), Vector3.one * 0.28f, Quaternion.identity, new Color(1f, 0.85f, 0.05f));
                break;
            case DuelAbility.IceMage:
                projectileRoot.localScale = new Vector3(0.22f, 0.22f, 0.85f);
                projectileRoot.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
                AddEffectShape(projectileRoot, PrimitiveType.Cube, "Ice Tip", new Vector3(0f, 0f, 0.55f), Vector3.one * 0.35f, Quaternion.Euler(45f, 45f, 45f), Color.white);
                break;
            case DuelAbility.Poison:
                AddEffectShape(projectileRoot, PrimitiveType.Sphere, "Poison Puff", new Vector3(-0.25f, 0.08f, -0.28f), Vector3.one * 0.38f, Quaternion.identity, new Color(0.55f, 0f, 0.55f));
                AddEffectShape(projectileRoot, PrimitiveType.Sphere, "Poison Bubble", new Vector3(0.23f, -0.08f, -0.48f), Vector3.one * 0.3f, Quaternion.identity, new Color(0.85f, 0.25f, 0.9f));
                break;
        }
    }

    private void ShowAttackVisual(Color color, float duration)
    {
        ShowAttackVisual(color, duration, attackRange);
    }

    private void ShowAttackVisual(Color color, float duration, float range)
    {
        ShowAttackVisual(color, duration, range, 0.28f);
    }

    private void ShowAttackVisual(Color color, float duration, float range, float width)
    {
        ShowAttackVisual(color, duration, range, width, 0.32f);
    }

    private void ShowAttackVisual(Color color, float duration, float range, float width, float height)
    {
        if (attackVisual == null)
        {
            return;
        }

        foreach (Transform child in attackVisual.transform)
        {
            Destroy(child.gameObject);
        }

        attackVisual.transform.localPosition = Vector3.zero;
        attackVisual.transform.localRotation = Quaternion.identity;
        attackVisual.transform.localScale = Vector3.one;
        BuildAttackVisual(color, range, width, height);
        ConfigureAttackMotion(duration, range);
        attackVisual.SetActive(true);
        hideAttackVisualTime = Time.time + duration;
    }

    private void ConfigureAttackMotion(float duration, float range)
    {
        AttackVisualMotion motion = attackVisual.GetComponent<AttackVisualMotion>();
        if (motion == null)
        {
            motion = attackVisual.AddComponent<AttackVisualMotion>();
        }

        switch (ability)
        {
            case DuelAbility.DashMaster:
                motion.Setup(duration, new Vector3(0f, 0f, -0.65f), new Vector3(0f, 0f, range * 0.75f), new Vector3(1f, 1f, 0.35f), new Vector3(1f, 1f, 1.2f), 0f, 0f, 0f);
                break;
            case DuelAbility.FireMage:
                motion.Setup(duration, Vector3.zero, new Vector3(0f, 0.25f, 0.25f), Vector3.one * 0.45f, Vector3.one * 1.25f, 0f, 55f, 0.08f);
                break;
            case DuelAbility.IceMage:
                motion.Setup(duration, new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.2f, 0.25f), Vector3.one * 0.75f, Vector3.one * 1.05f, 0f, -180f, 0f);
                break;
            case DuelAbility.Healer:
                motion.Setup(duration, Vector3.zero, Vector3.zero, Vector3.one * 0.35f, Vector3.one * 1.35f, 0f, 0f, 0f);
                break;
            case DuelAbility.Thunder:
                motion.Setup(duration, new Vector3(-0.12f, 0.18f, 0f), new Vector3(0.12f, -0.08f, 0.35f), Vector3.one * 0.65f, Vector3.one * 1.15f, 0f, 260f, 0.18f);
                break;
            case DuelAbility.Wind:
                motion.Setup(duration, Vector3.zero, new Vector3(0f, 0.15f, 0.1f), Vector3.one * 0.55f, Vector3.one * 1.35f, 720f, 0f, 0f);
                break;
            case DuelAbility.Stone:
                motion.Setup(duration, new Vector3(0f, 0.65f, 0.25f), new Vector3(0f, -0.12f, 0.08f), Vector3.one * 1.15f, Vector3.one * 0.95f, 0f, -95f, 0f);
                break;
            case DuelAbility.Shadow:
                motion.Setup(duration, new Vector3(-0.55f, 0f, -0.25f), new Vector3(0.45f, 0f, range * 0.65f), new Vector3(0.7f, 1f, 0.45f), new Vector3(1.05f, 1f, 1.35f), 0f, 0f, 0.05f);
                break;
            case DuelAbility.Poison:
                motion.Setup(duration, Vector3.zero, new Vector3(0f, 0.18f, 0.15f), Vector3.one * 0.55f, Vector3.one * 1.45f, 70f, 0f, 0.12f);
                break;
            default:
                motion.Setup(duration, Vector3.zero, new Vector3(0f, 0.05f, 0.05f), Vector3.one * 0.7f, Vector3.one * 1.2f, -540f, 0f, 0f);
                break;
        }
    }

    private void BuildAttackVisual(Color color, float range, float width, float height)
    {
        switch (ability)
        {
            case DuelAbility.DashMaster:
                AddAttackShape(PrimitiveType.Cube, "Dash Blade", new Vector3(0f, 0.1f, range * 0.55f), new Vector3(width, height, range), Quaternion.Euler(0f, 0f, 18f), color);
                AddAttackShape(PrimitiveType.Cube, "Dash Afterimage", new Vector3(-0.22f, 0.05f, range * 0.35f), new Vector3(width * 0.45f, height * 0.7f, range * 0.55f), Quaternion.Euler(0f, 0f, -18f), new Color(1f, 1f, 0.4f));
                break;
            case DuelAbility.FireMage:
                AddAttackShape(PrimitiveType.Sphere, "Fire Core", new Vector3(0f, 0.15f, range * 0.55f), new Vector3(width, height, range * 0.45f), Quaternion.identity, color);
                AddAttackShape(PrimitiveType.Sphere, "Fire Spark Left", new Vector3(-width * 0.45f, 0.28f, range * 0.45f), Vector3.one * 0.35f, Quaternion.identity, new Color(1f, 0.45f, 0.05f));
                AddAttackShape(PrimitiveType.Sphere, "Fire Spark Right", new Vector3(width * 0.45f, 0.02f, range * 0.68f), Vector3.one * 0.28f, Quaternion.identity, new Color(1f, 0.75f, 0.1f));
                break;
            case DuelAbility.IceMage:
                AddAttackShape(PrimitiveType.Cube, "Ice Spear", new Vector3(0f, 0.12f, range * 0.58f), new Vector3(width * 0.35f, height, range), Quaternion.Euler(0f, 0f, 45f), color);
                AddAttackShape(PrimitiveType.Cube, "Ice Crystal", new Vector3(width * 0.35f, 0.08f, range * 0.42f), Vector3.one * 0.35f, Quaternion.Euler(0f, 45f, 45f), Color.white);
                break;
            case DuelAbility.Healer:
                AddAttackShape(PrimitiveType.Sphere, "Heal Pulse", new Vector3(0f, 0.18f, range * 0.48f), new Vector3(width * 1.2f, height * 1.2f, range * 0.5f), Quaternion.identity, color);
                AddAttackShape(PrimitiveType.Cube, "Heal Cross V", new Vector3(0f, 0.2f, range * 0.5f), new Vector3(0.18f, 0.7f, 0.18f), Quaternion.identity, Color.white);
                AddAttackShape(PrimitiveType.Cube, "Heal Cross H", new Vector3(0f, 0.2f, range * 0.5f), new Vector3(0.7f, 0.18f, 0.18f), Quaternion.identity, Color.white);
                break;
            case DuelAbility.Thunder:
                AddAttackShape(PrimitiveType.Cube, "Thunder Bolt A", new Vector3(-0.15f, 0.18f, range * 0.45f), new Vector3(width * 0.35f, height, range * 0.55f), Quaternion.Euler(0f, 0f, 30f), color);
                AddAttackShape(PrimitiveType.Cube, "Thunder Bolt B", new Vector3(0.18f, 0.05f, range * 0.65f), new Vector3(width * 0.3f, height, range * 0.45f), Quaternion.Euler(0f, 0f, -35f), Color.white);
                break;
            case DuelAbility.Wind:
                AddAttackShape(PrimitiveType.Cylinder, "Wind Arc Wide", new Vector3(0f, 0.08f, range * 0.5f), new Vector3(width, 0.08f, range * 0.35f), Quaternion.Euler(90f, 0f, 0f), color);
                AddAttackShape(PrimitiveType.Cylinder, "Wind Arc Thin", new Vector3(0f, 0.32f, range * 0.68f), new Vector3(width * 0.7f, 0.06f, range * 0.25f), Quaternion.Euler(90f, 0f, 0f), new Color(0.75f, 0.95f, 1f));
                break;
            case DuelAbility.Stone:
                AddAttackShape(PrimitiveType.Cube, "Stone Hammer", new Vector3(0f, 0.08f, range * 0.45f), new Vector3(width, height, range * 0.42f), Quaternion.Euler(0f, 0f, -8f), color);
                AddAttackShape(PrimitiveType.Cube, "Stone Handle", new Vector3(0f, -0.05f, range * 0.25f), new Vector3(0.22f, 0.22f, range * 0.6f), Quaternion.identity, new Color(0.25f, 0.25f, 0.25f));
                break;
            case DuelAbility.Shadow:
                AddAttackShape(PrimitiveType.Cube, "Shadow Needle", new Vector3(0f, 0.12f, range * 0.6f), new Vector3(width, height, range), Quaternion.Euler(0f, 0f, -24f), color);
                AddAttackShape(PrimitiveType.Cube, "Shadow Trail", new Vector3(0.18f, 0.05f, range * 0.38f), new Vector3(width * 0.45f, height * 0.7f, range * 0.6f), Quaternion.Euler(0f, 0f, 24f), new Color(0.18f, 0.18f, 0.22f));
                break;
            case DuelAbility.Poison:
                AddAttackShape(PrimitiveType.Sphere, "Poison Cloud", new Vector3(0f, 0.12f, range * 0.5f), new Vector3(width, height, range * 0.42f), Quaternion.identity, color);
                AddAttackShape(PrimitiveType.Sphere, "Poison Puff A", new Vector3(-width * 0.35f, 0.2f, range * 0.62f), Vector3.one * 0.32f, Quaternion.identity, new Color(0.55f, 0f, 0.55f));
                AddAttackShape(PrimitiveType.Sphere, "Poison Puff B", new Vector3(width * 0.4f, 0.04f, range * 0.35f), Vector3.one * 0.26f, Quaternion.identity, new Color(0.8f, 0.2f, 0.9f));
                break;
            default:
                AddAttackShape(PrimitiveType.Cylinder, "Magnet Field", new Vector3(0f, 0.12f, range * 0.48f), new Vector3(width, 0.08f, range * 0.35f), Quaternion.Euler(90f, 0f, 0f), color);
                AddAttackShape(PrimitiveType.Sphere, "Magnet Core", new Vector3(0f, 0.12f, range * 0.48f), Vector3.one * 0.34f, Quaternion.identity, Color.white);
                break;
        }
    }

    private void AddAttackShape(PrimitiveType type, string shapeName, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Color color)
    {
        GameObject shape = GameObject.CreatePrimitive(type);
        shape.name = shapeName;
        shape.transform.SetParent(attackVisual.transform);
        shape.transform.localPosition = localPosition;
        shape.transform.localRotation = localRotation;
        shape.transform.localScale = localScale;
        shape.GetComponent<Renderer>().material = CreateMaterial(color);
        Collider shapeCollider = shape.GetComponent<Collider>();
        if (shapeCollider != null)
        {
            Destroy(shapeCollider);
        }
    }

    private void SpawnEffect(Color color, float size)
    {
        SpawnEffectAt(transform.position + Vector3.up * 0.1f, color, size);
    }

    private void SpawnEffectAt(Vector3 position, Color color, float size)
    {
        GameObject effect = new GameObject(GetAbilityName() + " Skill Effect");
        effect.transform.position = position + Vector3.up * 0.12f;

        GameObject visualRoot = new GameObject("Visual");
        visualRoot.transform.SetParent(effect.transform);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;
        visualRoot.transform.localScale = Vector3.one;

        BuildSkillEffect(visualRoot.transform, color, size);

        AttackVisualMotion motion = visualRoot.AddComponent<AttackVisualMotion>();
        ConfigureSkillEffectMotion(motion, size);
        Destroy(effect, 0.45f);
    }

    private void BuildSkillEffect(Transform root, Color color, float size)
    {
        switch (ability)
        {
            case DuelAbility.DashMaster:
                AddEffectShape(root, PrimitiveType.Cube, "Dash Shock Line", new Vector3(0f, 0.08f, 0f), new Vector3(size * 0.32f, 0.16f, size * 1.35f), Quaternion.Euler(0f, transform.eulerAngles.y, 0f), color);
                AddEffectShape(root, PrimitiveType.Cube, "Dash Slash Cross", new Vector3(0f, 0.18f, 0f), new Vector3(size * 0.9f, 0.1f, size * 0.2f), Quaternion.Euler(0f, transform.eulerAngles.y + 28f, 0f), Color.white);
                break;
            case DuelAbility.FireMage:
                AddEffectShape(root, PrimitiveType.Sphere, "Fire Blast Core", Vector3.zero, Vector3.one * size, Quaternion.identity, color);
                AddEffectShape(root, PrimitiveType.Sphere, "Fire Blast Hot", new Vector3(size * 0.18f, 0.18f, 0f), Vector3.one * size * 0.55f, Quaternion.identity, new Color(1f, 0.8f, 0.05f));
                AddEffectShape(root, PrimitiveType.Sphere, "Fire Blast Ember", new Vector3(-size * 0.25f, 0.05f, size * 0.2f), Vector3.one * size * 0.35f, Quaternion.identity, new Color(1f, 0.35f, 0f));
                break;
            case DuelAbility.IceMage:
                AddEffectShape(root, PrimitiveType.Cube, "Ice Field", Vector3.zero, new Vector3(size * 0.8f, 0.12f, size * 0.8f), Quaternion.Euler(0f, 45f, 0f), color);
                AddEffectShape(root, PrimitiveType.Cube, "Ice Spike A", new Vector3(size * 0.25f, 0.35f, 0f), new Vector3(0.18f, size * 0.8f, 0.18f), Quaternion.Euler(20f, 30f, 12f), Color.white);
                AddEffectShape(root, PrimitiveType.Cube, "Ice Spike B", new Vector3(-size * 0.2f, 0.25f, size * 0.18f), new Vector3(0.16f, size * 0.6f, 0.16f), Quaternion.Euler(-18f, 70f, -10f), color);
                break;
            case DuelAbility.Healer:
                AddEffectShape(root, PrimitiveType.Sphere, "Heal Dome", Vector3.zero, Vector3.one * size, Quaternion.identity, color);
                AddEffectShape(root, PrimitiveType.Cube, "Heal Cross Vertical", new Vector3(0f, 0.2f, 0f), new Vector3(0.22f, size * 0.9f, 0.22f), Quaternion.identity, Color.white);
                AddEffectShape(root, PrimitiveType.Cube, "Heal Cross Horizontal", new Vector3(0f, 0.2f, 0f), new Vector3(size * 0.9f, 0.22f, 0.22f), Quaternion.identity, Color.white);
                break;
            case DuelAbility.Thunder:
                AddEffectShape(root, PrimitiveType.Cube, "Lightning Strike A", new Vector3(-0.2f, size * 0.25f, 0f), new Vector3(0.22f, size * 1.25f, 0.22f), Quaternion.Euler(0f, 0f, 28f), color);
                AddEffectShape(root, PrimitiveType.Cube, "Lightning Strike B", new Vector3(0.22f, size * 0.05f, 0f), new Vector3(0.18f, size * 0.95f, 0.18f), Quaternion.Euler(0f, 0f, -32f), Color.white);
                AddEffectShape(root, PrimitiveType.Cylinder, "Thunder Ring", Vector3.zero, new Vector3(size * 0.75f, 0.05f, size * 0.75f), Quaternion.Euler(90f, 0f, 0f), color);
                break;
            case DuelAbility.Wind:
                AddEffectShape(root, PrimitiveType.Cylinder, "Wind Ring Outer", Vector3.zero, new Vector3(size, 0.06f, size), Quaternion.Euler(90f, 0f, 0f), color);
                AddEffectShape(root, PrimitiveType.Cylinder, "Wind Ring Inner", new Vector3(0f, 0.2f, 0f), new Vector3(size * 0.65f, 0.05f, size * 0.65f), Quaternion.Euler(90f, 0f, 0f), new Color(0.75f, 0.95f, 1f));
                AddEffectShape(root, PrimitiveType.Cube, "Wind Streak", new Vector3(0f, 0.25f, size * 0.35f), new Vector3(size * 0.12f, 0.08f, size * 0.7f), Quaternion.Euler(0f, 35f, 0f), Color.white);
                break;
            case DuelAbility.Stone:
                AddEffectShape(root, PrimitiveType.Cube, "Stone Plate", Vector3.zero, new Vector3(size * 0.8f, 0.22f, size * 0.8f), Quaternion.Euler(0f, 18f, 0f), color);
                AddEffectShape(root, PrimitiveType.Cube, "Stone Chunk A", new Vector3(size * 0.25f, 0.35f, 0f), Vector3.one * size * 0.28f, Quaternion.Euler(25f, 45f, 12f), color);
                AddEffectShape(root, PrimitiveType.Cube, "Stone Chunk B", new Vector3(-size * 0.25f, 0.22f, -size * 0.2f), Vector3.one * size * 0.22f, Quaternion.Euler(-20f, 10f, 40f), new Color(0.25f, 0.25f, 0.25f));
                break;
            case DuelAbility.Shadow:
                AddEffectShape(root, PrimitiveType.Cube, "Shadow Rift", Vector3.zero, new Vector3(size * 0.18f, 0.12f, size * 1.2f), Quaternion.Euler(0f, transform.eulerAngles.y + 25f, 0f), color);
                AddEffectShape(root, PrimitiveType.Cube, "Shadow Echo", new Vector3(0.22f, 0.08f, 0f), new Vector3(size * 0.12f, 0.08f, size * 0.9f), Quaternion.Euler(0f, transform.eulerAngles.y - 20f, 0f), new Color(0.18f, 0.18f, 0.22f));
                break;
            case DuelAbility.Poison:
                AddEffectShape(root, PrimitiveType.Sphere, "Poison Cloud Main", Vector3.zero, Vector3.one * size, Quaternion.identity, color);
                AddEffectShape(root, PrimitiveType.Sphere, "Poison Cloud A", new Vector3(size * 0.35f, 0.12f, 0f), Vector3.one * size * 0.55f, Quaternion.identity, new Color(0.55f, 0f, 0.55f));
                AddEffectShape(root, PrimitiveType.Sphere, "Poison Cloud B", new Vector3(-size * 0.25f, 0.22f, size * 0.22f), Vector3.one * size * 0.42f, Quaternion.identity, new Color(0.8f, 0.2f, 0.9f));
                break;
            default:
                AddEffectShape(root, PrimitiveType.Cylinder, "Magnet Outer Ring", Vector3.zero, new Vector3(size, 0.08f, size), Quaternion.Euler(90f, 0f, 0f), color);
                AddEffectShape(root, PrimitiveType.Cylinder, "Magnet Inner Ring", new Vector3(0f, 0.18f, 0f), new Vector3(size * 0.55f, 0.06f, size * 0.55f), Quaternion.Euler(90f, 0f, 0f), Color.white);
                AddEffectShape(root, PrimitiveType.Sphere, "Magnet Core", new Vector3(0f, 0.22f, 0f), Vector3.one * size * 0.18f, Quaternion.identity, color);
                break;
        }
    }

    private void ConfigureSkillEffectMotion(AttackVisualMotion motion, float size)
    {
        switch (ability)
        {
            case DuelAbility.Thunder:
                motion.Setup(0.32f, new Vector3(0f, 0.6f, 0f), Vector3.zero, Vector3.one * 0.45f, Vector3.one * 1.15f, 0f, 260f, 0.16f);
                break;
            case DuelAbility.Wind:
            case DuelAbility.Magnet:
                motion.Setup(0.42f, Vector3.zero, new Vector3(0f, 0.12f, 0f), Vector3.one * 0.25f, Vector3.one * 1.25f, ability == DuelAbility.Wind ? 900f : -720f, 0f, 0f);
                break;
            case DuelAbility.Stone:
                motion.Setup(0.36f, new Vector3(0f, 0.75f, 0f), Vector3.zero, Vector3.one * 1.15f, Vector3.one, 0f, -60f, 0.08f);
                break;
            case DuelAbility.Healer:
            case DuelAbility.Poison:
            case DuelAbility.FireMage:
                motion.Setup(0.42f, Vector3.zero, new Vector3(0f, 0.18f, 0f), Vector3.one * 0.2f, Vector3.one * 1.25f, ability == DuelAbility.Poison ? 80f : 0f, 0f, 0.04f);
                break;
            case DuelAbility.Shadow:
            case DuelAbility.DashMaster:
                motion.Setup(0.28f, new Vector3(0f, 0f, -0.25f), new Vector3(0f, 0f, 0.3f), Vector3.one * 0.6f, Vector3.one * 1.15f, 0f, 0f, 0.06f);
                break;
            default:
                motion.Setup(0.36f, Vector3.zero, new Vector3(0f, 0.16f, 0f), Vector3.one * 0.35f, Vector3.one * 1.15f, 0f, 0f, 0f);
                break;
        }
    }

    private void AddEffectShape(Transform parent, PrimitiveType type, string shapeName, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Color color)
    {
        GameObject shape = GameObject.CreatePrimitive(type);
        shape.name = shapeName;
        shape.transform.SetParent(parent);
        shape.transform.localPosition = localPosition;
        shape.transform.localScale = localScale;
        shape.transform.localRotation = localRotation;
        shape.GetComponent<Renderer>().material = CreateMaterial(color);
        Collider shapeCollider = shape.GetComponent<Collider>();
        if (shapeCollider != null)
        {
            Destroy(shapeCollider);
        }
    }

    private void SpawnHitEffect(Vector3 position, Color color)
    {
        GameObject root = new GameObject("Hit Effect");
        root.transform.position = position;

        switch (ability)
        {
            case DuelAbility.Stone:
                AddHitShape(root.transform, PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.7f, Quaternion.Euler(12f, 35f, 8f), color);
                AddHitShape(root.transform, PrimitiveType.Cube, new Vector3(0.35f, 0.15f, 0f), Vector3.one * 0.35f, Quaternion.Euler(40f, 0f, 25f), color);
                break;
            case DuelAbility.Wind:
            case DuelAbility.Magnet:
                AddHitShape(root.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.9f, 0.06f, 0.9f), Quaternion.Euler(90f, 0f, 0f), color);
                AddHitShape(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.18f, 0f), new Vector3(0.55f, 0.05f, 0.55f), Quaternion.Euler(90f, 0f, 0f), Color.white);
                break;
            case DuelAbility.Thunder:
                AddHitShape(root.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.22f, 0.9f, 0.22f), Quaternion.Euler(0f, 0f, 35f), color);
                AddHitShape(root.transform, PrimitiveType.Cube, new Vector3(0.12f, 0.15f, 0f), new Vector3(0.18f, 0.65f, 0.18f), Quaternion.Euler(0f, 0f, -35f), Color.white);
                break;
            case DuelAbility.FireMage:
            case DuelAbility.Poison:
            case DuelAbility.Healer:
                AddHitShape(root.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.75f, Quaternion.identity, color);
                AddHitShape(root.transform, PrimitiveType.Sphere, new Vector3(0.28f, 0.18f, 0f), Vector3.one * 0.32f, Quaternion.identity, Color.white);
                break;
            default:
                AddHitShape(root.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.55f, Quaternion.identity, color);
                AddHitShape(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.9f, 0.12f, 0.12f), Quaternion.Euler(0f, 0f, 35f), color);
                break;
        }

        Destroy(root, 0.16f);
    }

    private void AddHitShape(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Color color)
    {
        GameObject shape = GameObject.CreatePrimitive(type);
        shape.transform.SetParent(parent);
        shape.transform.localPosition = localPosition;
        shape.transform.localScale = localScale;
        shape.transform.localRotation = localRotation;
        shape.GetComponent<Renderer>().material = CreateMaterial(color);
        Collider shapeCollider = shape.GetComponent<Collider>();
        if (shapeCollider != null)
        {
            Destroy(shapeCollider);
        }
    }

    private Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }

    private string GetAbilityName()
    {
        switch (ability)
        {
            case DuelAbility.DashMaster:
                return "Dash";
            case DuelAbility.FireMage:
                return "Fire";
            case DuelAbility.IceMage:
                return "Ice";
            case DuelAbility.Healer:
                return "Heal";
            case DuelAbility.Thunder:
                return "Thunder";
            case DuelAbility.Wind:
                return "Wind";
            case DuelAbility.Stone:
                return "Stone";
            case DuelAbility.Shadow:
                return "Shadow";
            case DuelAbility.Poison:
                return "Poison";
            default:
                return "Magnet";
        }
    }

    private string GetSkillName(int skillNumber)
    {
        switch (ability)
        {
            case DuelAbility.DashMaster:
                return skillNumber == 1 ? "Dash" : "Slash";
            case DuelAbility.FireMage:
                return skillNumber == 1 ? "Fireball" : "Burst";
            case DuelAbility.IceMage:
                return skillNumber == 1 ? "Ice Shot" : "Freeze";
            case DuelAbility.Healer:
                return skillNumber == 1 ? "Heal" : "Shield";
            case DuelAbility.Thunder:
                return skillNumber == 1 ? "Bolt" : "Speed";
            case DuelAbility.Wind:
                return skillNumber == 1 ? "Push" : "Blink";
            case DuelAbility.Stone:
                return skillNumber == 1 ? "Armor" : "Quake";
            case DuelAbility.Shadow:
                return skillNumber == 1 ? "Step" : "Backstab";
            case DuelAbility.Poison:
                return skillNumber == 1 ? "Dart" : "Cloud";
            default:
                return skillNumber == 1 ? "Pull" : "Repel";
        }
    }
}

public class DuelProjectile : MonoBehaviour
{
    private DuelPlayer owner;
    private DuelPlayer target;
    private Vector3 direction;
    private float speed;
    private int damage;
    private int slowSeconds;
    private float stunSeconds;
    private bool poison;
    private float knockback;
    private float destroyTime;

    public void Setup(DuelPlayer projectileOwner, DuelPlayer projectileTarget, Vector3 moveDirection, float projectileSpeed, int projectileDamage, int projectileSlowSeconds, float projectileStunSeconds, bool projectilePoison, float projectileKnockback)
    {
        owner = projectileOwner;
        target = projectileTarget;
        direction = moveDirection.normalized;
        speed = projectileSpeed;
        damage = projectileDamage;
        slowSeconds = projectileSlowSeconds;
        stunSeconds = projectileStunSeconds;
        poison = projectilePoison;
        knockback = projectileKnockback;
        destroyTime = Time.time + 2.2f;
    }

    private void Update()
    {
        Vector3 startPosition = transform.position;
        Vector3 frameMove = direction * speed * Time.deltaTime;
        float frameDistance = frameMove.magnitude;

        if (frameDistance > 0.001f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(startPosition, 0.22f, direction, frameDistance);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.isTrigger || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                DuelPlayer hitPlayer = hitCollider.GetComponentInParent<DuelPlayer>();
                if (hitPlayer == owner)
                {
                    continue;
                }

                if (hitPlayer != null)
                {
                    if (hitPlayer == target && hitPlayer.Health > 0)
                    {
                        ApplyHit(hitPlayer);
                    }

                    Destroy(gameObject);
                    return;
                }

                transform.position = startPosition + direction * Mathf.Max(0f, hits[i].distance - 0.05f);
                Destroy(gameObject);
                return;
            }
        }

        transform.position += frameMove;

        if (target != null && target.Health > 0 && Vector3.Distance(transform.position, target.transform.position + Vector3.up) < 0.85f)
        {
            ApplyHit(target);
            Destroy(gameObject);
            return;
        }

        if (Time.time >= destroyTime)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyHit(DuelPlayer hitPlayer)
    {
        hitPlayer.TakeDamage(damage);

        if (knockback > 0f)
        {
            hitPlayer.AddPush(direction * knockback);
        }

        if (slowSeconds > 0)
        {
            hitPlayer.ApplySlow(slowSeconds);
        }

        if (stunSeconds > 0f)
        {
            hitPlayer.ApplyStun(stunSeconds);
        }

        if (poison)
        {
            hitPlayer.ApplyPoison();
        }
    }
}

public class NameTagOcclusion : MonoBehaviour
{
    private Transform owner;
    private Renderer tagRenderer;

    public void Setup(Transform ownerTransform)
    {
        owner = ownerTransform;
        tagRenderer = GetComponent<Renderer>();
    }

    private void Awake()
    {
        tagRenderer = GetComponent<Renderer>();
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null || tagRenderer == null)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);

        Vector3 cameraPosition = camera.transform.position;
        Vector3 toTag = transform.position - cameraPosition;
        float distance = toTag.magnitude;
        if (distance < 0.01f)
        {
            tagRenderer.enabled = true;
            return;
        }

        RaycastHit[] hits = Physics.RaycastAll(cameraPosition, toTag.normalized, distance - 0.05f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger)
            {
                continue;
            }

            if (owner != null && hitCollider.transform.IsChildOf(owner))
            {
                continue;
            }

            tagRenderer.enabled = false;
            return;
        }

        tagRenderer.enabled = true;
    }
}

public class AttackVisualMotion : MonoBehaviour
{
    private Vector3 startPosition;
    private Vector3 endPosition;
    private Vector3 startScale;
    private Vector3 endScale;
    private float duration;
    private float startTime;
    private float yawSpinDegrees;
    private float rollDegrees;
    private float shakeAmount;

    public void Setup(float motionDuration, Vector3 fromLocalPosition, Vector3 toLocalPosition, Vector3 fromLocalScale, Vector3 toLocalScale, float spinYaw, float roll, float shake)
    {
        duration = Mathf.Max(0.01f, motionDuration);
        startPosition = fromLocalPosition;
        endPosition = toLocalPosition;
        startScale = fromLocalScale;
        endScale = toLocalScale;
        yawSpinDegrees = spinYaw;
        rollDegrees = roll;
        shakeAmount = shake;
        startTime = Time.time;

        transform.localPosition = startPosition;
        transform.localScale = startScale;
        transform.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        float t = Mathf.Clamp01((Time.time - startTime) / duration);
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        Vector3 shake = Vector3.zero;

        if (shakeAmount > 0f)
        {
            float wave = Mathf.Sin(t * Mathf.PI * 8f);
            shake = new Vector3(wave * shakeAmount, -wave * shakeAmount * 0.5f, 0f);
        }

        transform.localPosition = Vector3.Lerp(startPosition, endPosition, eased) + shake;
        transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.Sin(t * Mathf.PI * 0.5f));
        transform.localRotation = Quaternion.Euler(0f, yawSpinDegrees * t, rollDegrees * Mathf.Sin(t * Mathf.PI));
    }
}

public class SplitScreenCameraFollow : MonoBehaviour
{
    private Transform target;
    private Transform secondaryTarget;
    private Vector3 velocity;
    private float mapScale = 1f;
    private float distanceScale = 1f;
    private bool thirdPersonMode;

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
        secondaryTarget = null;
        thirdPersonMode = false;
        SnapToTarget();
    }

    public void SetTargets(Transform firstTarget, Transform secondTarget)
    {
        target = firstTarget;
        secondaryTarget = secondTarget;
        thirdPersonMode = false;
        SnapToTarget();
    }

    public void SetThirdPersonTarget(Transform followTarget)
    {
        target = followTarget;
        secondaryTarget = null;
        thirdPersonMode = true;
        SnapToTarget();
    }

    public void SetMapScale(float scale)
    {
        mapScale = scale;
        UpdateCameraSize();
    }

    public void SetStaticView(Vector3 position, Quaternion rotation)
    {
        target = null;
        secondaryTarget = null;
        thirdPersonMode = false;
        velocity = Vector3.zero;
        transform.position = position;
        transform.rotation = rotation;
        distanceScale = 1f;
        UpdateCameraSize();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = thirdPersonMode ? GetThirdPersonPosition() : GetDesiredPosition();
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, 0.12f);
        transform.rotation = thirdPersonMode ? GetThirdPersonRotation() : Quaternion.Euler(58f, 0f, 0f);
        UpdateCameraSize();
    }

    private void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        transform.position = thirdPersonMode ? GetThirdPersonPosition() : GetDesiredPosition();
        transform.rotation = thirdPersonMode ? GetThirdPersonRotation() : Quaternion.Euler(58f, 0f, 0f);
        UpdateCameraSize();
    }

    private Vector3 GetDesiredPosition()
    {
        Vector3 focus = target.position;
        distanceScale = 1f;

        if (secondaryTarget != null)
        {
            focus = (target.position + secondaryTarget.position) * 0.5f;
            float distance = Vector3.Distance(target.position, secondaryTarget.position);
            distanceScale = Mathf.Clamp(distance / 8f, 1f, 1.75f);
        }

        return focus + new Vector3(0f, 11f * mapScale * distanceScale, -8.5f * mapScale * distanceScale);
    }

    private Vector3 GetThirdPersonPosition()
    {
        Vector3 forward = target.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        return target.position - forward * 7.6f * mapScale + Vector3.up * 3.25f * mapScale;
    }

    private Quaternion GetThirdPersonRotation()
    {
        Vector3 forward = target.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }

        Vector3 lookPoint = target.position + Vector3.up * 0.85f + forward.normalized * 1.55f;
        Vector3 direction = lookPoint - transform.position;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = forward;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void UpdateCameraSize()
    {
        Camera camera = GetComponent<Camera>();
        if (camera != null && camera.orthographic)
        {
            camera.orthographicSize = 8.5f * mapScale * distanceScale;
        }
    }
}
