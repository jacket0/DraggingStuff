using System;
using UnityEngine;
using UnityEngine.UI;

public class LevelHudView : MonoBehaviour
{
    [SerializeField] private Button _pauseButton;

    public event Action PauseRequested;

    private void OnEnable()
    {
        _pauseButton.onClick.AddListener(PauseButtonClicked);
    }

    private void OnDisable()
    {
        _pauseButton.onClick.RemoveListener(PauseButtonClicked);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void PauseButtonClicked()
    {
        PauseRequested?.Invoke();
    }
}
