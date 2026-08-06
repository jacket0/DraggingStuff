using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSession : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private LevelBuilder _levelBuilder;

    private bool _shouldCompleteLevel;

    public LevelState State { get; private set; }
    public bool IsPlaying => State == LevelState.Playing;

    public event Action LevelCompleted;

    private void Start()
    {
        _levelBuilder.Build();
        StartLevel();
    }

    public void StartLevel()
    {
        State = LevelState.Playing;
    }

    public MoveOutcome TryMove(ShelfSlot source, ShelfSlot target)
    {
        if (!IsPlaying)
            return MoveOutcome.Rejected();

        State = LevelState.Resolving;

        MoveOutcome moveOutcome = _shelfBoard.TryMove(source, target, CompleteResolution);

        if (!moveOutcome.IsSuccessful)
        {
            State = LevelState.Playing;
            return moveOutcome;
        }

        _shouldCompleteLevel = moveOutcome.IsLevelCompleted;

        if (!moveOutcome.HasLayerTransition)
            CompleteResolution();

        return moveOutcome;
    }

    public void RestartLevel()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    private void CompleteResolution()
    {
        bool isLevelCompleted = _shouldCompleteLevel;

        _shouldCompleteLevel = false;

        if (isLevelCompleted)
        {
            State = LevelState.Won;
            LevelCompleted?.Invoke();
            return;
        }

        State = LevelState.Playing;
    }
}
