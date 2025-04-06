using NaughtyAttributes;
using UnityEngine;
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
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }
    }
    void Start() => SubscribeEvents();
    private void OnDestroy() => UnsubscribeEvents();
    private void SubscribeEvents()
    {
        ExitGameButton.onClick.AddListener(G.SteamLobbyManager.ExitGame);
        StartGameButton.onClick.AddListener(G.SteamLobbyManager.StartGame);
        GoToMenuButton.onClick.AddListener(G.SteamLobbyManager.ExitToMenu);
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

    #region Menu Logic
    public void HideMenu(bool cursorState = false)
    {
        if (MenuIsNull())
            return;

        SetButtonsActive(false);
        CursorSetActive(cursorState);
    }
    public void ShowMenu(bool cursorState = true)
    {
        if (MenuIsNull())
            return;

        SetButtonsActive(true);

        if (G.SteamLobbyManager.SteamLobbyNetwork.IsOfflineScene() || G.SteamLobbyManager.SteamLobbyNetwork.IsMainMenuScene())
        {
            SetMainMenuButtonsActive(true);
            GoToMenuButton.gameObject.SetActive(false);
        }
        else
        {
            SetMainMenuButtonsActive(false);
            GoToMenuButton.gameObject.SetActive(true);
        }

        CursorSetActive(cursorState);
    }
    #endregion

    #region Handlers
    public void CursorSetActive(bool value)
    {
        Cursor.visible = value;
        Cursor.lockState = value ? CursorLockMode.Confined : CursorLockMode.Locked;
    }
    private void SetButtonsActive(bool active)
    {
        StartGameButton.gameObject.SetActive(active);
        SettingsGameButton.gameObject.SetActive(active);
        ExitGameButton.gameObject.SetActive(active);
        GoToMenuButton.gameObject.SetActive(active);
    }
    private void SetMainMenuButtonsActive(bool active)
    {
        StartGameButton.gameObject.SetActive(active);
        ExitGameButton.gameObject.SetActive(active);
    }
    public bool MenuIsOpen() => SettingsGameButton.gameObject.activeInHierarchy || GoToMenuButton.gameObject.activeInHierarchy;
    private bool MenuIsNull() => SettingsGameButton == null || ExitGameButton == null || StartGameButton == null || GoToMenuButton == null;

    #endregion
}
