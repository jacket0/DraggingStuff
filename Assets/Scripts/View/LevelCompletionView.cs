using System;
using UnityEngine;
using UnityEngine.UI;

public class LevelCompletionView : MonoBehaviour
{
    [SerializeField] private Button _restartButton;

    public event Action RestartLevel;

    private void OnEnable()
    {
        _restartButton.onClick.AddListener(RestartButtonClicked);
    }

    private void OnDisable()
    {
        _restartButton.onClick.RemoveListener(RestartButtonClicked);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void RestartButtonClicked()
    {
        RestartLevel?.Invoke();   
    }
}
