using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(SteamLobbyNetwork))]
public class SteamLobbyManager : MonoBehaviour
{
    [SerializeField, ReadOnly] private bool dontDestroyOnLoad = true;
    [Header("Other settings")]
    [Tooltip("Дефолтная текстура для пустого слота игрока")]
    [SerializeField] private Texture defaultTexture;

    [Header("Lobby settings")]
    [SerializeField] private List<PlayerSlot> playerSlots;


    private CSteamID currentLobby;
    private const string START_GAME_KEY = "StartGame";
    private const string RETURN_TO_MENU_KEY = "ReturnToMenu";
    public SteamLobbyNetwork SteamLobbyNetwork { get; private set; }

    private Callback<LobbyDataUpdate_t> lobbyDataUpdated;
    private Callback<LobbyChatUpdate_t> lobbyChatUpdated;

    #region Initialize
    private void Awake()
    {
        SteamLobbyNetwork = GetComponent<SteamLobbyNetwork>();

        if(G.SteamLobbyManager != null)
        {
            Destroy(gameObject);
        }
        else
        {
            G.SteamLobbyManager = this;
            if(dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }
    }

    public void Init()
    {
        SubscribeEvents();
        SteamLobbyNetwork.Init();
    }
    private void OnDestroy() => UnsubscribeEvents();

    private void SubscribeEvents()
    {
        SteamLobbyNetwork.OnLobbyCreatedEvent += OnLobbyCreated;
        SteamLobbyNetwork.OnLobbyEnteredEvent += OnLobbyEntered;

        SceneManager.sceneLoaded += OnSceneLoaded;

        lobbyDataUpdated = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdated);
        lobbyChatUpdated = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdated);
    }
    private void UnsubscribeEvents()
    {
        SteamLobbyNetwork.OnLobbyCreatedEvent -= OnLobbyCreated;
        SteamLobbyNetwork.OnLobbyEnteredEvent -= OnLobbyEntered;
        lobbyDataUpdated?.Dispose();
        lobbyChatUpdated?.Dispose();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        //G.SteamLobbyManager = null;
    }
    #endregion

    #region LobbyHandlers
    private async void OnLobbyCreated(CSteamID lobbyID)
    {
        Debug.Log($"<color=yellow>[SteamLobbyManager]</color>: <color=green>Lobby created!</color>");
        currentLobby = lobbyID;

        SteamMatchmaking.SetLobbyData(currentLobby, SteamLobbyNetwork.HostAddressKey, SteamUser.GetSteamID().ToString());

        await UpdatePlayerListAsync();
    }
    //private async void OnLobbyEntered(CSteamID lobbyID)
    //{
    //    Debug.Log($"<color=yellow>[SteamLobbyManager]</color>: Lobby entered");
    //    currentLobby = lobbyID;

    //    CSteamID myID = SteamUser.GetSteamID();

    //    int playerCount = SteamMatchmaking.GetNumLobbyMembers(currentLobby);

    //    for (int i = 0; i < playerCount; i++)
    //    {
    //        if (SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i) == myID)
    //        {
    //            Debug.Log($"<color=yellow>[SteamLobbyManager]</color>: Player <color=cyan>{myID}</color> is already in the lobby, skipping rejoining.");
    //            return;
    //        }
    //    }

    //    Debug.Log($"<color=yellow>[SteamLobbyManager]</color>: Connecting to the server at address: {SteamLobbyNetwork.HostAddressKey}");

    //    SteamMatchmaking.SetLobbyData(currentLobby, SteamLobbyNetwork.HostAddressKey, SteamUser.GetSteamID().ToString());

    //    await UpdatePlayerListAsync();
    //}
    private async void OnLobbyEntered(CSteamID lobbyID)
    {
        currentLobby = lobbyID;
        await UpdatePlayerListAsync();
    }
    private void OnLobbyDataUpdated(LobbyDataUpdate_t callback)
    {
        if (callback.m_ulSteamIDLobby != currentLobby.m_SteamID) return;

        string returnToMenu = SteamMatchmaking.GetLobbyData(currentLobby, RETURN_TO_MENU_KEY);
        if (returnToMenu == "1")
        {
            Debug.Log("<color=yellow>[SteamLobbyManager]</color>: The host returned to the menu. Moving everyone to offline scene.");
            SceneManager.LoadScene(SteamLobbyNetwork.NetworkManager.offlineScene);
        }
    }
    private void OnLobbyChatUpdated(LobbyChatUpdate_t callback)
    {
        G.MainMenu.ShowMenu();
        UpdatePlayerListAsync().Forget();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
        {
            G.MainMenu.ShowMenu();
            PlayerListSetActive(true);
        }
        else if(scene.path == SteamLobbyNetwork.NetworkManager.onlineScene)
        {
            G.MainMenu.HideMenu();
            PlayerListSetActive(false);
        }
    }
    #endregion

    #region Player List

    public void PlayerListSetActive(bool value)
    {
        foreach (var slot in playerSlots)
        {
            if (slot == null)
                continue;

            slot.SetActive(value);
        }
    }
    private async UniTask UpdatePlayerListAsync(bool enableListAfterUpdate = false)
    {
        int playerCount = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
        int maxPlayers = playerSlots.Count;

        for (int i = 0; i < maxPlayers; i++)
        {
            if (i < playerCount)
            {
                CSteamID playerID = SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i);
                Texture avatar = await GetSteamAvatarAsync(playerID);
                playerSlots[i].SetPlayer(playerID, Color.green, avatar);

                Button actionButton = playerSlots[i].GetActionButton();
                actionButton.onClick.RemoveAllListeners();
                actionButton.interactable = false;
            }
            else
            {
                playerSlots[i].SetInvite(defaultTexture, Color.gray);
                Button inviteButton = playerSlots[i].GetActionButton();

                inviteButton.gameObject.SetActive(true);
                inviteButton.interactable = true;
                inviteButton.onClick.RemoveAllListeners();
                inviteButton.onClick.AddListener(InviteFriend);
            }
        }

        if(enableListAfterUpdate)
            PlayerListSetActive(true);
    }
    private async UniTask<Texture2D> GetSteamAvatarAsync(CSteamID playerID)
    {
        int imageID = SteamFriends.GetLargeFriendAvatar(playerID);

        if (imageID <= 0)
        {
            Debug.Log($"<color=yellow>[SteamLobbyManager]</color>: Avatar for <color=cyan>{playerID}</color> is not loaded yet. <color=yellow>Waiting</color>...");
            imageID = await WaitForAvatarAsync(playerID);
        }

        Texture2D avatar = LoadSteamAvatar(imageID);
        return avatar;
    }
    private async UniTask<int> WaitForAvatarAsync(CSteamID playerID)
    {
        int imageID = 0;

        while (imageID <= 0)
        {
            Debug.Log($"<color=yellow>[SteamLobbyManager]</color>: Waiting for avatar for <color=cyan>{playerID}</color>...");
            await UniTask.Delay(1000);
            imageID = SteamFriends.GetLargeFriendAvatar(playerID);
        }

        //await UpdatePlayerListAsync(); //Перерисовываем список игроков
        return imageID;
    }
    private Texture2D LoadSteamAvatar(int imageID)
    {
        SteamUtils.GetImageSize(imageID, out uint width, out uint height);
        if (width == 0 || height == 0) return null;

        byte[] imageData = new byte[width * height * 4];
        if (!SteamUtils.GetImageRGBA(imageID, imageData, imageData.Length)) return null;

        Texture2D avatar = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
        avatar.LoadRawTextureData(imageData);
        avatar.Apply();

        return avatar;
    }
    #endregion

    #region Lobby Managment
    public void CreateLobby()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam не инициализирован! Лобби не создано");
            return;
        }

        Debug.Log("<color=yellow>[SteamLobbyManager]</color>: Создание нового лобби...");
        Init();

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, SteamLobbyNetwork.NetworkManager.maxConnections);
    }

    private void InviteFriend()
    {
#if UNITY_EDITOR
        Debug.Log("<color=yellow>[SteamLobbyManager]</color>: Opened friend list for testing");
        SteamFriends.ActivateGameOverlay("Friends");
#else

        SteamFriends.ActivateGameOverlayInviteDialog(currentLobby);
#endif
    }
    #endregion

    #region Game Flow
    public void StartGame()
    {
        //Проверка на инициализацию Steam и наличие лобби
        if (!SteamManager.Initialized || currentLobby == CSteamID.Nil)
        {
            Debug.Log("<color=yellow>[SteamLobbyManager]</color>: Starting game in offline mode or creating a new lobby");
            SteamLobbyNetwork.StartGame();
            return;
        }

        bool isHost = SteamLobbyNetwork.IsHost();

        Debug.Log($"<color=yellow>[SteamLobbyManager]</color>: Am I host: {isHost}");

        if (isHost)
        {
            Debug.Log("<color=yellow>[SteamLobbyManager]</color>: Starting game as lobby owner");
            SteamMatchmaking.SetLobbyData(currentLobby, RETURN_TO_MENU_KEY, "0");
            SteamMatchmaking.SetLobbyData(currentLobby, START_GAME_KEY, "1");
            SteamLobbyNetwork.StartGame();
        }
        else
        {
            Debug.Log("<color=yellow>[SteamLobbyManager]</color>: Only the host can start the game.");
        }
    }
    public void ExitGame()
    {
        Debug.Log("<color=yellow>[SteamLobbyManager]</color>: Exiting game...");

        if (SteamLobbyNetwork.IsHost())
        {
            int playerCount = SteamMatchmaking.GetNumLobbyMembers(currentLobby);

            if (playerCount > 1)
            {
                //Назначаем нового хоста
                CSteamID newHost = GetNextHost();
                SteamMatchmaking.SetLobbyOwner(currentLobby, newHost);
                Debug.Log($"<color=yellow>[SteamLobbyManager]</color>: Transferring lobby ownership to {newHost}.");
            }
            else
            {
                //Если игроков больше нет — удаляем лобби
                Debug.Log("<color=yellow>[SteamLobbyManager]</color>: No players left, deleting lobby.");
                SteamMatchmaking.LeaveLobby(currentLobby);
            }
        }
        else
        {
            SteamMatchmaking.LeaveLobby(currentLobby);
        }

        Application.Quit();
    }
    public async void ExitToMenu()
    {
        if (SteamLobbyNetwork.IsHost())
        {
            Debug.Log("<color=yellow>[SteamLobbyManager]</color>: Хост инициирует возврат в меню");
            Debug.Log("<color=yellow>[SteamLobbyManager]</color>: <color=magenta>Пересоздание лобби</color>...");

            SteamMatchmaking.SetLobbyData(currentLobby, RETURN_TO_MENU_KEY, "1");
            SteamMatchmaking.LeaveLobby(currentLobby);

            if (SteamLobbyNetwork.IsHost())
            {
                SteamLobbyNetwork.StopHost();
            }
            else
            {
                SteamLobbyNetwork.StopClient();
            }

            await SceneManager.LoadSceneAsync(SteamLobbyNetwork.NetworkManager.offlineScene);

            //SteamMatchmaking.SetLobbyData(currentLobby, START_GAME_KEY, "0");
        }

        //SteamLobbyNetwork.NetworkManager.ServerChangeScene(SteamLobbyNetwork.NetworkManager.offlineScene);
        //Использование SceneManager.sceneLoaded уже обрабатывает показ меню через OnSceneLoaded);
    }

    private CSteamID GetNextHost()
    {
        int playerCount = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
        for (int i = 0; i < playerCount; i++)
        {
            CSteamID playerID = SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i);
            if (playerID != SteamUser.GetSteamID())
            {
                return playerID;
            }
        }
        return CSteamID.Nil; //Если никого нет
    }
    #endregion
}
