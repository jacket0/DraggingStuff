using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TutorialController : MonoBehaviour
{
    private const string TutorialSceneName = "TutorialLevel";

    [SerializeField] private Button _openButton;

    private bool _isOpening;

    private void Awake()
    {
        if (_openButton == null)
            throw new InvalidOperationException(nameof(_openButton));

        _openButton.onClick.AddListener(Open);
    }

    private void OnDestroy()
    {
        if (_openButton != null)
            _openButton.onClick.RemoveListener(Open);
    }

    private void Open()
    {
        if (_isOpening)
            return;

        _isOpening = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(TutorialSceneName);
    }
}
