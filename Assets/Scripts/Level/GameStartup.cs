using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameStartup : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        yield return null;

        string sceneName = TutorialProgress.IsCompleted ? "MainMenu" : "TutorialLevel";
        yield return SceneManager.LoadSceneAsync(sceneName);
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;

        Destroy(gameObject);
    }
}
