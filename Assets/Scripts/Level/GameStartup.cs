using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameStartup : MonoBehaviour
{
    private const float CloudProgressWaitSeconds = 3f;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        yield return null;

        float progressWaitEndTime = Time.realtimeSinceStartup + CloudProgressWaitSeconds;

        while (!TutorialProgress.IsCompleted && !GameProgressRepository.IsSynchronized && Time.realtimeSinceStartup < progressWaitEndTime)
            yield return null;

        LanguagePreference.ApplySaved();
        string sceneName = TutorialProgress.IsCompleted ? "MainMenu" : "TutorialLevel";
        yield return SceneManager.LoadSceneAsync(sceneName);
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;

        Destroy(gameObject);
    }
}
