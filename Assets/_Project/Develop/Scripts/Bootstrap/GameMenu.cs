using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameMenu : MonoBehaviour
{
    [SerializeField, ReadOnly] private bool dontDestroyOnLoad = true;
    [field: SerializeField] public Button StartGameButton { get; private set; }
    [field: SerializeField] public Button SettingsGameButton { get; private set; }
    [field: SerializeField] public Button ExitGameButton { get; private set; }
    [field: SerializeField] public Button GoToMenuButton { get; private set; }
    #region Initialzie
    private void Awake()
    {
        if (G.MainMenu != null)
        {
            Destroy(gameObject);
        }
        else
        {
            G.MainMenu = this;
            if(dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }
    }
    void Start()
    {
        SubscribeEvents();
    }
    private void OnDestroy()
    {
        UnsubscribeEvents();
    }
    private void SubscribeEvents()
    {
        //ExitGameButton.onClick.AddListener(G.SteamLobbyManager.ExitGame);
        //StartGameButton.onClick.AddListener(G.SteamLobbyManager.StartGame);
        //GoToMenuButton.onClick.AddListener(G.SteamLobbyManager.ExitToMenu);
        //SettingsGameButton.onClick.AddListener();
    }
    private void UnsubscribeEvents()
    {
        ExitGameButton.onClick.RemoveAllListeners();
        StartGameButton.onClick.RemoveAllListeners();
        GoToMenuButton.onClick.RemoveAllListeners();
        SettingsGameButton.onClick.RemoveAllListeners();
    }
    #endregion
    public void HideMenu()
    {
        if (MenuIsNull())
            return;

        ExitGameButton.gameObject.SetActive(false);
        StartGameButton.gameObject.SetActive(false);
        SettingsGameButton.gameObject.SetActive(false);
        GoToMenuButton.gameObject.SetActive(false);
    }
    public void ShowMenu()
    {
        if (MenuIsNull())
            return;

        //SettingsGameButton.gameObject.SetActive(true);

        //if (G.SteamLobbyManager.SteamLobbyNetwork.IsOfflineScene() || G.SteamLobbyManager.SteamLobbyNetwork.IsMainMenuScene())
        //{
        //    ExitGameButton.gameObject.SetActive(true);
        //    GoToMenuButton.gameObject.SetActive(false);
        //    StartGameButton.gameObject.SetActive(true);
        //}
        //else
        //{
        //    ExitGameButton.gameObject.SetActive(false);
        //    GoToMenuButton.gameObject.SetActive(true);
        //    StartGameButton.gameObject.SetActive(false);
        //}
    }
    public bool MenuIsOpen() => SettingsGameButton.gameObject.activeInHierarchy || GoToMenuButton.gameObject.activeInHierarchy;
    private bool MenuIsNull() => SettingsGameButton == null || ExitGameButton == null || StartGameButton == null || GoToMenuButton == null;
}
