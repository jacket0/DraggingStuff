using System;
using UnityEngine;
using UnityEngine.UI;

public class PauseWindowView : MonoBehaviour
{
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _mainMenuButton;

    public event Action ResumeRequested;
    public event Action RestartRequested;
    public event Action MenuRequested;

    private void OnEnable()
    {
        _resumeButton.onClick.AddListener(ResumeClicked);
        _restartButton.onClick.AddListener(RestartClicked);
        _mainMenuButton.onClick.AddListener(HandleMenuClick);
    }

    private void OnDisable()
    {
        _resumeButton.onClick.RemoveListener(ResumeClicked);
        _restartButton.onClick.RemoveListener(RestartClicked);
        _mainMenuButton.onClick.RemoveListener(HandleMenuClick);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void RestartClicked()
    {
        RestartRequested?.Invoke();
    }

    private void ResumeClicked()
    {
        ResumeRequested?.Invoke();
    }

    private void HandleMenuClick()
    {
        MenuRequested?.Invoke();
    }
}
