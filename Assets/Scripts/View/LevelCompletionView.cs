using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelCompletionView : MonoBehaviour
{
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _menuButton;
    [SerializeField] private TMP_Text _finalScoreResult;

    public event Action MenuRequested;
    public event Action RestartRequested;

    private void OnEnable()
    {
        _restartButton.onClick.AddListener(RestartButtonClicked);
        _menuButton.onClick.AddListener(MenuButtonClicked);
    }

    private void OnDisable()
    {
        _restartButton?.onClick.RemoveListener(RestartButtonClicked);
        _menuButton?.onClick.RemoveListener(MenuButtonClicked);
    }

    public void Show(long finalScore)
    {
        _finalScoreResult.SetText(finalScore.ToString());
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void RestartButtonClicked()
    {
        RestartRequested?.Invoke();   
    }

    private void MenuButtonClicked()
    {
        MenuRequested?.Invoke();
    }
}
