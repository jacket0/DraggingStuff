#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[UnityEngine.Scripting.Preserve]
public sealed class ClosedShelfDebugProbe : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject probe = new GameObject("ClosedShelvesDebug");
        DontDestroyOnLoad(probe);
        probe.AddComponent<ClosedShelfDebugProbe>();
    }

    [UnityEngine.Scripting.Preserve]
    public void OpenLevel(int levelNumber)
    {
        LevelSelectionState selection = Resources.FindObjectsOfTypeAll<LevelSelectionState>().FirstOrDefault();
        LevelEntry level = Resources.FindObjectsOfTypeAll<LevelEntry>().FirstOrDefault(entry => entry.Number == levelNumber);

        if (selection == null || level == null)
        {
            Debug.LogWarning($"ClosedShelvesDebug: level {levelNumber} is not available from this scene.");
            return;
        }

        selection.Select(level);
        SceneManager.LoadScene(level.SceneName);
    }

    [UnityEngine.Scripting.Preserve]
    public void LoadScene(string sceneName) => SceneManager.LoadScene(sceneName);

    [UnityEngine.Scripting.Preserve]
    public void RevealAll() => FindObjectOfType<ClosedShelvesController>()?.DebugRevealAll();

    [UnityEngine.Scripting.Preserve]
    public void StarBurst(int starCount) => FindObjectOfType<ClosedShelvesController>()?.DebugStarBurst(starCount);
}
#endif
