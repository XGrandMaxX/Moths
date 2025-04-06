using UnityEngine;
using NaughtyAttributes;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using System;

public class Bootstrap : MonoBehaviour
{
    [SerializeField, Scene] private string loadingScene;
    [SerializeField, Min(0)] private float initDelay;

    public async void Start() => await Init();

    private async UniTask Init()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(initDelay), cancellationToken: this.GetCancellationTokenOnDestroy());

        G.SteamLobbyManager.CreateLobby();
        await SceneManager.LoadSceneAsync(loadingScene);
    }
}
