using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EndlessResultView : MonoBehaviour
{
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _menuButton;
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _bestScoreText;

    public event Action RestartRequested;
    public event Action MenuRequested;

    private void OnEnable()
    {
        _restartButton.onClick.AddListener(HandleRestartClicked);
        _menuButton.onClick.AddListener(HandleMenuClicked);
    }

    private void OnDisable()
    {
        _restartButton?.onClick.RemoveListener(HandleRestartClicked);
        _menuButton?.onClick.RemoveListener(HandleMenuClicked);
    }

    public void Show(long score, long bestScore)
    {
        _scoreText.SetText(score.ToString());
        _bestScoreText.SetText(bestScore.ToString());
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void HandleRestartClicked()
    {
        RestartRequested?.Invoke();
    }

    private void HandleMenuClicked()
    {
        MenuRequested?.Invoke();
    }
}
