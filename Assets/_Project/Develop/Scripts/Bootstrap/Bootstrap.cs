using UnityEngine;
using NaughtyAttributes;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

public class Bootstrap : MonoBehaviour
{
    [SerializeField, Scene] private string _loadingScene;

    public async void Start() => await Init();

    private async UniTask Init()
    {
        await SceneManager.LoadSceneAsync(_loadingScene);
    }
}
