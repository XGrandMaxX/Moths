using UnityEngine;
using NaughtyAttributes;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using System;
using Steamworks;

public class Bootstrap : MonoBehaviour
{
    [Header("Bootstrap")]
    [SerializeField, Scene] private string loadingScene;
    [SerializeField, MinValue(0), MaxValue(3)] private float sceneLoadDelay = 0;

    [Header("Lobby")]
    [SerializeField] private ELobbyType LobbyTypeOnCreate;

    [Header("Localization")]
    [SerializeField] private ExistingLanguages baseLang;
    private enum ExistingLanguages { ru, en }

    public async void Start() => await Init();

    private async UniTask Init()
    {
        G.Localization = LocalizationManager.Instance;

        await G.Localization.SetLanguageAsync(baseLang.ToString());

        await LoadGameAsync();
    }

    private async UniTask LoadGameAsync()
    {
        G.SteamLobby.CreateNewLobby(LobbyTypeOnCreate);

        await UniTask.Delay(TimeSpan.FromSeconds(sceneLoadDelay), cancellationToken: this.GetCancellationTokenOnDestroy());
        await SceneManager.LoadSceneAsync(loadingScene);
    }
}
