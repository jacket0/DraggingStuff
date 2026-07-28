using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSession : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;

    public LevelState State { get; private set; }
    public bool IsPlaying => State == LevelState.Playing;

    public event Action LevelCompleted;

    private void Start()
    {
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

        MoveOutcome moveOutcome = _shelfBoard.TryMove(source, target);

        if (moveOutcome.IsLevelCompleted)
        {
            State = LevelState.Won;
            LevelCompleted?.Invoke();
        }

        return moveOutcome;
    }

    public void RestartLevel()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }
}
