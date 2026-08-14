using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSession : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private LevelBuilder _levelBuilder;
    [SerializeField] private MoveResolutionPlayer _moveResolutionPlayer;

    public LevelState State { get; private set; }
    public bool IsPlaying => State == LevelState.Playing;

    public event Action LevelCompleted;
    public event Action<MatchResolution> MatchSucceeded;

    private void Start()
    {
        _levelBuilder.Build();
        _shelfBoard.InitializeViews();
        StartLevel();
    }

    public void StartLevel()
    {
        Time.timeScale = 1;
        State = LevelState.Playing;
    }

    public MoveOutcome TryMove(ShelfSlot source, ShelfSlot target)
    {
        if (!IsPlaying)
            return MoveOutcome.Rejected();

        MoveOutcome moveOutcome = _shelfBoard.TryMove(source, target);

        if (!moveOutcome.IsSuccessful)
            return moveOutcome;

        if (moveOutcome.HasMatch)
            MatchSucceeded?.Invoke(moveOutcome.Match);

        PlayMoveResolution(moveOutcome);

        return moveOutcome;
    }

    public void RestartLevel()
    {
        Time.timeScale = 1;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public bool TryPauseLevel()
    {
        if (State != LevelState.Playing)
            return false;

        State = LevelState.Paused;
        Time.timeScale = 0;
        return true;
    }

    public void ResumeLevel()
    {
        if (State != LevelState.Paused)
            return;

        Time.timeScale = 1;
        State = LevelState.Playing;
    }

    private void PlayMoveResolution(MoveOutcome moveOutcome)
    {
        if (!moveOutcome.HasMatch)
        {
            AdvanceLayers(moveOutcome);
            return;
        }

        _moveResolutionPlayer.Play(moveOutcome.Match, () => AdvanceLayers(moveOutcome));
        _shelfBoard.HideLayersToAdvice(moveOutcome.ShelvesToAdvance);
    }

    private void AdvanceLayers(MoveOutcome moveOutcome)
    {
        _shelfBoard.AdvanceLayers(moveOutcome.ShelvesToAdvance, () => HandleMoveResolutionCompleted(moveOutcome.IsLevelCompleted));
    }

    private void HandleMoveResolutionCompleted(bool isLevelCompleted)
    {
        if (!isLevelCompleted)
            return;

        if (State != LevelState.Playing)
            return;

        State = LevelState.Won;
        LevelCompleted?.Invoke();
    }
}
