using UnityEngine;
using UnityEngine.UI;

public class MainMenuSettingsController : MonoBehaviour
{
    [SerializeField] private Button _settingsButton;
    [SerializeField] private SettingsWindowView _settingsWindow;

    private void Awake()
    {
        _settingsWindow.Hide();
    }

    private void OnEnable()
    {
        _settingsButton.onClick.AddListener(OpenSettings);
        _settingsWindow.CloseRequested += CloseSettings;
    }

    private void OnDisable()
    {
        _settingsButton.onClick.RemoveListener(OpenSettings);
        _settingsWindow.CloseRequested -= CloseSettings;
    }

    private void OpenSettings()
    {
        _settingsWindow.Show();
    }

    private void CloseSettings()
    {
        _settingsWindow.Hide();
    }
}
