using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TutorialSession : GameSession
{
    private const string MainMenuSceneName = "MainMenu";

    [SerializeField] private LevelBuilder _levelBuilder;
    [SerializeField] private LevelDefinition _levelDefinition;
    [SerializeField, Min(0f)] private float _completionDelay = 1f;

    private void Start()
    {
        _levelBuilder.Build(_levelDefinition);
        StartSession();
    }

    protected override void HandleBoardSettled(bool isBoardCleared)
    {
        if (!isBoardCleared || !IsPlaying)
            return;

        CompleteAsWon();
        TutorialProgress.MarkCompleted();
        StartCoroutine(ReturnToMenu());
    }

    private IEnumerator ReturnToMenu()
    {
        yield return new WaitForSeconds(_completionDelay);

        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenuSceneName);
    }
}
