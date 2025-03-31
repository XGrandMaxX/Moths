using Mirror;
using Steamworks;
using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class SteamLobbyNetwork : MonoBehaviour
{
    public event Action<CSteamID> OnLobbyCreatedEvent;
    public event Action<CSteamID> OnLobbyEnteredEvent;

    internal NetworkManager NetworkManager;
    internal const string HostAddressKey = "HostAddress";
    public CSteamID CurrentLobby { get; private set; }

    private Callback<LobbyCreated_t> lobbyCreatedCallback;
    private Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequestedCallback;
    private Callback<LobbyEnter_t> lobbyEnteredCallback;

    private void Awake() => Init();
    public void Init()
    {
        NetworkManager = FindObjectOfType<NetworkManager>();
    }
    private void Start()
    {
        if (!SteamManager.Initialized) return;

        lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    private void OnDestroy()
    {
        OnLobbyCreatedEvent = null;
        OnLobbyEnteredEvent = null;
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK) return;

        //NetworkManager.StartHost();
        CurrentLobby = new CSteamID(callback.m_ulSteamIDLobby);

        SteamMatchmaking.SetLobbyData(CurrentLobby, HostAddressKey, SteamUser.GetSteamID().ToString());
        OnLobbyCreatedEvent?.Invoke(CurrentLobby);
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        CurrentLobby = new CSteamID(callback.m_ulSteamIDLobby);

        string hostAddress = SteamMatchmaking.GetLobbyData(CurrentLobby, HostAddressKey);
        CSteamID hostID = new CSteamID(ulong.Parse(hostAddress));

        Debug.Log($"<color=yellow>[SteamLobbyNetwork]</color>: Entered lobby. Host ID: {hostID}, My ID: {SteamUser.GetSteamID()}");

        if (hostAddress == SteamUser.GetSteamID().ToString())
        {
            Debug.Log($"<color=yellow>[SteamLobbyNetwork]</color>: This is the host, not connecting as client.");
            //Явно указываем, что мы хост
            return;
        }

        Debug.Log($"<color=yellow>[SteamLobbyNetwork]</color>: Connecting to server at address: {hostAddress}");

        NetworkManager.networkAddress = hostAddress;
        NetworkManager.StartClient();

        OnLobbyEnteredEvent?.Invoke(CurrentLobby);
    }



    public void StartGame()
    {
        if (NetworkServer.active)
        {
            Debug.Log($"<color=yellow>[SteamLobbyNetwork]</color>: Server already active, stopping first...");
            NetworkManager.StopHost();
        }

        if (NetworkClient.active)
        {
            Debug.Log($"<color=yellow>[SteamLobbyNetwork]</color>: Client already active, stopping first...");
            NetworkManager.StopClient();
        }

        Debug.Log($"<color=yellow>[SteamLobbyNetwork]</color>: Server started...");
        NetworkManager.StartHost();

        if (SceneManager.GetActiveScene().path == NetworkManager.onlineScene)
        {
            Debug.Log("Вы уже находитесь на этой сцене");
            return;
        }

        if (NetworkManager.loadingSceneAsync != null)
        {
            Debug.LogWarning("Смена сцены уже выполняется!");
            return;
        }

        Debug.Log("Запускаем смену сцены...");
        NetworkManager.ServerChangeScene(NetworkManager.onlineScene);
    }

    public void StopServer() => NetworkManager.StopServer();
    public void StopHost() => NetworkManager.StopHost();
    public void StopClient() => NetworkManager.StopClient();
    public bool IsHost()
    {
        if (!SteamManager.Initialized || CurrentLobby == CSteamID.Nil)
            return NetworkServer.active; //Для офлайн-режима

        return SteamMatchmaking.GetLobbyOwner(CurrentLobby) == SteamUser.GetSteamID();
    }


    public bool IsOfflineScene() => SceneManager.GetActiveScene().path == NetworkManager.offlineScene;
    public bool IsOnlineScene() => SceneManager.GetActiveScene().path == NetworkManager.onlineScene;
    public bool IsMainMenuScene() => SceneManager.GetActiveScene().name == "MainMenu";
    public void LeaveLobby()
    {
        if (CurrentLobby != CSteamID.Nil)
        {
            SteamMatchmaking.LeaveLobby(CurrentLobby);
            CurrentLobby = CSteamID.Nil;
        }
    }
}

[System.Serializable]
public class PlayerSlot
{
    public CSteamID PlayerID { get; private set; }
    [field: SerializeField] public RawImage Avatar { get; private set; }
    [field: SerializeField] public TMP_Text Name { get; private set; }

    public void SetActive(bool state)
    {
        Avatar.gameObject.SetActive(state);
        Name.gameObject.SetActive(state);
    }

    public void SetPlayer(CSteamID playerID, Color nameColor, Texture avatarTexture)
    {
        PlayerID = playerID;
        Name.text = SteamFriends.GetFriendPersonaName(playerID);
        Name.color = nameColor;

        Avatar.texture = avatarTexture;

        SetActive(true);
    }

    public void SetInvite(Texture defaultTexture, Color nameColor, string name = "Invite Friend")
    {
        Name.text = name;
        Name.color = nameColor;
        Avatar.texture = defaultTexture;

        SetActive(true);
    }

    public Button GetActionButton() => Avatar.GetComponent<Button>();
    public bool HasPlayer() => PlayerID != CSteamID.Nil; //Nil означает, что слота нет
    public void UpdateName(string newName) => Name.text = newName;
    public void UpdateAvatar(Texture newTexture) => Avatar.texture = newTexture;
}
