using System;
using UnityEngine;
using UnityEngine.UI;

public class SettingsWindowView : MonoBehaviour
{
    [SerializeField] private Button _dimmerButton;
    [SerializeField] private Button _closeButton;

    public event Action CloseRequested;

    private void OnEnable()
    {
        _dimmerButton.onClick.AddListener(RequestClose);
        _closeButton.onClick.AddListener(RequestClose);
    }

    private void OnDisable()
    {
        _dimmerButton.onClick.RemoveListener(RequestClose);
        _closeButton.onClick.RemoveListener(RequestClose);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void RequestClose()
    {
        CloseRequested?.Invoke();
    }

}
