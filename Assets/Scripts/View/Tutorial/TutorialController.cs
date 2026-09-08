using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialController : MonoBehaviour
{
    private const string TutorialVersionKey = "tutorial.seen.version";
    private const int TutorialVersion = 1;

    [SerializeField] private TutorialView _tutorialView;
    [SerializeField] private Button _openButton;
    [SerializeField] private List<TutorialPage> _pages = new List<TutorialPage>();

    private bool _isInitialized;

    private void Awake()
    {
        ValidateDependencies();
        _tutorialView.Initialize();
        _tutorialView.Hide();
        _openButton.onClick.AddListener(Open);
        _tutorialView.Closed += HandleTutorialClosed;
        _isInitialized = true;
    }

    private void Start()
    {
        if (PlayerPrefs.GetInt(TutorialVersionKey, 0) < TutorialVersion)
            Open();
    }

    private void OnDestroy()
    {
        if (!_isInitialized)
            return;

        _openButton.onClick.RemoveListener(Open);
        _tutorialView.Closed -= HandleTutorialClosed;
    }

    private void Open()
    {
        _tutorialView.Show(_pages);
    }

    private void HandleTutorialClosed()
    {
        PlayerPrefs.SetInt(TutorialVersionKey, TutorialVersion);
        PlayerPrefs.Save();
    }

    private void ValidateDependencies()
    {
        if (_tutorialView == null)
            throw new InvalidOperationException(nameof(_tutorialView));

        if (_openButton == null)
            throw new InvalidOperationException(nameof(_openButton));

        if (_pages == null || _pages.Count != 3 || _pages.Exists(page => page == null))
            throw new InvalidOperationException(nameof(_pages));
    }
}
