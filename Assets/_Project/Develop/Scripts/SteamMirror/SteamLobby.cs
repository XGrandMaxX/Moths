using Mirror;
using NaughtyAttributes;
using Steamworks;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class PlayerSlot
{
    public Button SlotButton;
    public RawImage PlayerAvatar;
    public TMP_Text PlayerNameText;
    public CSteamID SteamID;
    public bool IsOccupied;

    public string PlayerName
    {
        get => PlayerNameText != null ? PlayerNameText.text : "";
        set
        {
            if (PlayerNameText != null)
                PlayerNameText.text = value;
        }
    }
}

[RequireComponent(typeof(NetworkManager))]
public class SteamLobby : MonoBehaviour
{
    [Header("Lobby settings")]
    public ulong current_lobbyID;

    [Header("Player Slots")]
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField, MinValue(1), MaxValue(10)] private int maxPlayers = 4;
    [SerializeField, MinValue(0)] private float slotSpacing = 10f;

    [Space(10)]
    public List<PlayerSlot> PlayerList = new List<PlayerSlot>();
    [HideInInspector] public NetworkManager NetworkManager;

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;
    protected Callback<LobbyDataUpdate_t> Callback_lobbyInfo;
    protected Callback<LobbyChatUpdate_t> lobbyChatUpdate;

    private const string HOST_ADDRESS_KEY = "HostAddress";
    private const string GAME_STARTED = "GameStarted";
    private const string INVITE_FRIEND_TEXT = "Invite Friend";
    private const string BOOTSTRAP_SCENE = "Bootstrap";

    private Dictionary<CSteamID, Texture2D> cachedAvatars = new Dictionary<CSteamID, Texture2D>();

    #region Main logic
    public void CreateNewLobby(ELobbyType lobbyType) => SteamMatchmaking.CreateLobby(lobbyType, NetworkManager.maxConnections);
    public void ExitGame()
    {
        if (current_lobbyID != 0)
        {
            SteamMatchmaking.LeaveLobby(new CSteamID(current_lobbyID));
        }

        NetworkManager.StopClient();
        Application.Quit();
    }
    public void GoToMenu()
    {
        if (current_lobbyID != 0)
        {
            if (IsLobbyHost())
            {
                HostCloseLobby();
                return;
            }
            SteamMatchmaking.LeaveLobby(new CSteamID(current_lobbyID));
        }

        NetworkManager.StopClient();
        SceneManager.LoadScene(NetworkManager.offlineScene);
    }

    public void HostCloseLobby()
    {
        if (!IsLobbyHost())
        {
            Debug.LogWarning("Only the lobby owner can close it!");
            return;
        }

        if (current_lobbyID != 0)
        {
            SteamMatchmaking.SetLobbyData(
                new CSteamID(current_lobbyID),
                "LobbyClosing",
                "true"
            );

            Invoke(nameof(FinishCloseLobby), 0.5f);
        }
        else
        {
            FinishCloseLobby();
        }
    }
    private void FinishCloseLobby()
    {
        if (current_lobbyID != 0)
        {
            SteamMatchmaking.LeaveLobby(new CSteamID(current_lobbyID));
            current_lobbyID = 0;
        }

        if (NetworkServer.active)
        {
            NetworkManager.StopHost();
        }
        else if (NetworkClient.active)
        {
            NetworkManager.StopClient();
        }

        // Возвращаемся на bootstrap сцену и создаем новое пустое лобби
        SceneManager.LoadScene(BOOTSTRAP_SCENE);
    }


    public void StartGame()
    {
        Debug.Log("Trying to start game. NetworkClient.active: " + NetworkClient.active + ", NetworkServer.active: " + NetworkServer.active);

        if (!IsLobbyHost())
        {
            Debug.LogWarning("Only the lobby owner can start the game!");
            return;
        }

        if (NetworkServer.active)
        {
            Debug.Log("Server already active, just loading scene");
            SteamMatchmaking.SetLobbyData(
                new CSteamID(current_lobbyID),
                GAME_STARTED,
                "true"
            );
            SceneManager.LoadScene(NetworkManager.onlineScene);
            return;
        }

        if (NetworkClient.active)
        {
            Debug.Log("Client active, stopping it first");
            NetworkManager.StopClient();
        }

        SteamMatchmaking.SetLobbyData(
            new CSteamID(current_lobbyID),
            GAME_STARTED,
            "true"
        );

        Debug.Log("Starting host");
        NetworkManager.StartHost();

        SceneManager.LoadScene(NetworkManager.onlineScene);
    }

    #endregion

    #region Initialize
    private void Awake()
    {
        if (G.SteamLobby != null)
        {
            Destroy(gameObject);
            return;
        }

        G.SteamLobby = this;
        NetworkManager = GetComponent<NetworkManager>();
    }

    private void Start() => Initialize();

    public void Initialize()
    {
        if (!SteamManager.Initialized) return;

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        Callback_lobbyInfo = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);

        SceneManager.sceneLoaded += OnSceneLoaded;

        InitializePlayerSlots();
    }
    private void InitializePlayerSlots()
    {
        PlayerList.Clear();
        if (slotsContainer == null)
        {
            Debug.LogError("Slot container is not assigned!");
            return;
        }

        foreach (Transform child in slotsContainer)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < maxPlayers; i++)
        {
            GameObject slotObject = Instantiate(slotPrefab, slotsContainer);
            Button slotButton = slotObject.GetComponentInChildren<Button>();
            RawImage playerAvatar = slotObject.GetComponentInChildren<RawImage>();
            TMP_Text playerNameText = slotObject.GetComponentInChildren<TMP_Text>();

            PlayerSlot slot = new()
            {
                SlotButton = slotButton,
                PlayerAvatar = playerAvatar,
                PlayerNameText = playerNameText,
                IsOccupied = false
            };

            PlayerList.Add(slot);

            bool isActive = i == 0 || i == 1;
            string defaultText = i == 0 ? SteamFriends.GetPersonaName() : INVITE_FRIEND_TEXT;

            if (i == 0)
            {
                LoadPlayerAvatar(SteamUser.GetSteamID(), playerAvatar);
                slotButton.interactable = false;
                slot.IsOccupied = true;
                slot.SteamID = SteamUser.GetSteamID();
            }

            slotObject.SetActive(isActive);
            playerNameText.text = defaultText;
            playerNameText.color = defaultText == INVITE_FRIEND_TEXT ? Color.cyan : Color.green;
            playerNameText.gameObject.SetActive(true);

            if (slotObject.TryGetComponent<RectTransform>(out var rectTransform))
            {
                float positionY = -(i * (rectTransform.rect.height + slotSpacing));
                rectTransform.localPosition = new Vector3(0, positionY, 0);
            }
        }
    }
    #endregion

    #region Lobby Handlers
    private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
    {
        if (current_lobbyID != callback.m_ulSteamIDLobby)
            return;

        string lobbyClosing = SteamMatchmaking.GetLobbyData(
            new CSteamID(callback.m_ulSteamIDLobby),
            "LobbyClosing");

        if (lobbyClosing == "true")
        {
            Debug.Log("Lobby is closing by host. Returning to bootstrap scene.");
            if (NetworkClient.active)
            {
                NetworkManager.StopClient();
            }

            // Возвращаемся на bootstrap сцену
            SceneManager.LoadScene(BOOTSTRAP_SCENE);
            return;
        }

        if (NetworkClient.active || NetworkServer.active)
            return;

        string gameStarted = SteamMatchmaking.GetLobbyData(
            new CSteamID(callback.m_ulSteamIDLobby),
            GAME_STARTED);

        if (gameStarted == "true")
        {
            ConnectToHost(callback.m_ulSteamIDLobby);
            SceneManager.LoadScene(NetworkManager.onlineScene);
        }
    }
    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        current_lobbyID = callback.m_ulSteamIDLobby;

        Debug.Log("OnLobbyEntered for lobby with id: " + current_lobbyID.ToString());


        //Проверяем, если мы хост, не нужно запускать клиент
        if (NetworkServer.active)
        {
            Debug.Log("OnLobbyEntered: Server already active, skipping client connect");
            return;
        }

        string gameStarted = SteamMatchmaking.GetLobbyData(
            new CSteamID(callback.m_ulSteamIDLobby),
            GAME_STARTED);

        if (gameStarted == "true")
        {
            //Если игра уже начата, подключаемся к хосту
            Debug.Log("OnLobbyEntered: Game already started, connecting to host");
            ConnectToHost(callback.m_ulSteamIDLobby);
            SceneManager.LoadScene(NetworkManager.onlineScene);
        }
        else
        {
            //Если мы не хост и игра не начата, то НЕ подключаемся к хосту
            //Подключение произойдет только когда хост нажмет "Start Game"
            Debug.Log("OnLobbyEntered: Game not started yet, waiting");
        }

        UpdatePlayerList();
    }

    private void ConnectToHost(ulong lobbyID)
    {
        if (NetworkClient.active)
        {
            Debug.Log("ConnectToHost: Client already active, skipping connection");
            return;
        }

        string hostAddress = SteamMatchmaking.GetLobbyData(
            new CSteamID(lobbyID),
            HOST_ADDRESS_KEY);

        if (!string.IsNullOrEmpty(hostAddress))
        {
            Debug.Log("ConnectToHost: Connecting to host at " + hostAddress);
            NetworkManager.networkAddress = hostAddress;
            NetworkManager.StartClient();
        }
        else
        {
            Debug.LogError("Error: hostAddress is empty!");
        }

        UpdatePlayerList();
    }
    private void OnLobbyChatUpdate(LobbyChatUpdate_t callback) => UpdatePlayerList();
    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("OnGameLobbyJoinRequested");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }
    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        Debug.Log("OnLobbyCreated");
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            return;
        }

        current_lobbyID = callback.m_ulSteamIDLobby;

        SteamMatchmaking.SetLobbyData(new CSteamID(current_lobbyID), "name", SteamFriends.GetPersonaName() + "'s lobby");
        SteamMatchmaking.SetLobbyData(new CSteamID(current_lobbyID), HOST_ADDRESS_KEY, SteamUser.GetSteamID().ToString());

        UpdatePlayerList();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if(scene.path == NetworkManager.offlineScene)
        {
            G.GameMenu.ShowMenu();
            UpdatePlayerList();
        }
        else if(scene.path == NetworkManager.onlineScene)
        {
            G.GameMenu.HideMenu();
        }
    }

    #endregion

    #region Player List Management
    public void UpdatePlayerList()
    {
        if (current_lobbyID == 0) return;

        CSteamID lobbyID = new CSteamID(current_lobbyID);
        int playerCount = SteamMatchmaking.GetNumLobbyMembers(lobbyID);

        for (int i = 1; i < PlayerList.Count; i++)
        {
            PlayerSlot slot = PlayerList[i];
            slot.IsOccupied = false;

            if (slot.PlayerAvatar != null)
                slot.PlayerAvatar.gameObject.SetActive(false);

            if (slot.PlayerNameText != null)
            {
                slot.PlayerNameText.text = INVITE_FRIEND_TEXT;
            }

            bool shouldBeActive = i <= playerCount;

            if (i == 1) shouldBeActive = true;

            if (slot.SlotButton != null)
            {
                GameObject slotObject = slot.SlotButton.gameObject;
                slotObject.SetActive(shouldBeActive);
            }
        }

        for (int i = 0; i < playerCount; i++)
        {
            CSteamID playerID = SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);
            string playerName = SteamFriends.GetFriendPersonaName(playerID);

            bool isLocalPlayer = playerID == SteamUser.GetSteamID();

            int slotIndex = isLocalPlayer ? 0 : i;

            if (slotIndex >= PlayerList.Count)
                continue;

            PlayerList[slotIndex].SteamID = playerID;
            PlayerList[slotIndex].IsOccupied = true;
            PlayerList[slotIndex].PlayerName = playerName;

            if (PlayerList[slotIndex].SlotButton != null)
            {
                GameObject slotObject = PlayerList[slotIndex].SlotButton.gameObject;
                slotObject.SetActive(true);

                PlayerList[slotIndex].SlotButton.interactable = !isLocalPlayer && !PlayerList[slotIndex].IsOccupied;
            }

            if (PlayerList[slotIndex].PlayerAvatar != null)
            {
                PlayerList[slotIndex].PlayerAvatar.gameObject.SetActive(true);
                LoadPlayerAvatar(playerID, PlayerList[slotIndex].PlayerAvatar);
            }
        }

        if (playerCount < PlayerList.Count - 1)
        {
            int nextSlotIndex = playerCount + 1;

            if (nextSlotIndex < PlayerList.Count && nextSlotIndex > 1) 
            {
                if (PlayerList[nextSlotIndex].SlotButton != null)
                {
                    GameObject slotObject = PlayerList[nextSlotIndex].SlotButton.gameObject;
                    slotObject.SetActive(true);
                    PlayerList[nextSlotIndex].SlotButton.interactable = true;
                }
            }
        }
    }

    private void LoadPlayerAvatar(CSteamID playerID, RawImage avatarImage)
    {
        if (cachedAvatars.TryGetValue(playerID, out Texture2D avatarTexture))
        {
            avatarImage.texture = avatarTexture;
            return;
        }

        int imageHandle = SteamFriends.GetMediumFriendAvatar(playerID);
        if (imageHandle == 0) return;

        bool success = SteamUtils.GetImageSize(imageHandle, out uint width, out uint height);
        if (!success) return;

        avatarTexture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);

        byte[] imageData = new byte[width * height * 4];
        success = SteamUtils.GetImageRGBA(imageHandle, imageData, (int)(width * height * 4));
        if (!success) return;

        avatarTexture.LoadRawTextureData(imageData);
        avatarTexture.Apply();

        cachedAvatars[playerID] = avatarTexture;
        avatarImage.texture = avatarTexture;
    }

    public void ClearAllSlots()
    {
        foreach (var slot in PlayerList)
        {
            slot.IsOccupied = false;
            slot.SteamID = CSteamID.Nil;

            if (slot.PlayerAvatar != null)
            {
                slot.PlayerAvatar.gameObject.SetActive(false);
                slot.PlayerAvatar.texture = null;
            }

            if (slot.PlayerNameText != null)
            {
                slot.PlayerNameText.text = INVITE_FRIEND_TEXT;
            }
        }
    }
    #endregion

    #region Utilities

    public bool IsOfflineScene() => SceneManager.GetActiveScene().path == NetworkManager.offlineScene;
    public bool IsLobbyHost()
    {
        if (current_lobbyID == 0) return false;

        CSteamID owner = SteamMatchmaking.GetLobbyOwner(new CSteamID(current_lobbyID));
        return owner == SteamUser.GetSteamID();
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        foreach (var avatar in cachedAvatars.Values)
        {
            Destroy(avatar);
        }
        cachedAvatars.Clear();
    }
    #endregion

}


