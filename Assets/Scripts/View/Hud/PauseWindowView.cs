using System;
using UnityEngine;
using UnityEngine.UI;

public class PauseWindowView : MonoBehaviour
{
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _restartButton;

    public event Action ResumeRequested;
    public event Action RestartRequested;

    private void OnEnable()
    {
        _resumeButton.onClick.AddListener(ResumeClicked);
        _restartButton.onClick.AddListener(RestartClicked);
    }

    private void OnDisable()
    {
        _resumeButton.onClick.RemoveListener(ResumeClicked);
        _restartButton.onClick.RemoveListener(RestartClicked);
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
}
