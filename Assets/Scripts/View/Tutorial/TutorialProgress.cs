using UnityEngine;

public static class TutorialProgress
{
    private const string CompletedKey = "tutorial.completed";

    public static bool IsCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;

    public static void MarkCompleted()
    {
        PlayerPrefs.SetInt(CompletedKey, 1);
        PlayerPrefs.Save();
    }
}
