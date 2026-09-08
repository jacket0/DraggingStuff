using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuAdaptiveLayout : MonoBehaviour
{
    [SerializeField] private RectTransform _contentRoot;
    [SerializeField] private MenuPanelLayout _panelLayout;
    [SerializeField] private LayoutElement _levelSelectionLayout;
    [SerializeField] private LayoutElement _leaderboardLayout;
    [SerializeField, Min(0.1f)] private float _compactAspectThreshold = 1.6f;
    [SerializeField, Min(0.1f)] private float _wideAspectThreshold = 2.2f;
    [SerializeField, Min(1f)] private float _wideContentWidth = 1728f;

    private Vector2Int _screenSize;

    private void Awake()
    {
        ValidateDependencies();
        ApplyLayout();
    }

    private void Update()
    {
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

        if (screenSize != _screenSize)
            ApplyLayout();
    }

    private void ApplyLayout()
    {
        _screenSize = new Vector2Int(Screen.width, Screen.height);

        if (Screen.height <= 0)
            return;

        float aspect = (float)Screen.width / Screen.height;

        if (aspect <= _compactAspectThreshold)
            ApplyCompactLayout();
        else
            ApplyHorizontalLayout(aspect > _wideAspectThreshold);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRoot);
    }

    private void ApplyCompactLayout()
    {
        _panelLayout.SetVertical(true);
        _contentRoot.anchorMin = new Vector2(0.04f, 0.08f);
        _contentRoot.anchorMax = new Vector2(0.96f, 0.84f);
        _contentRoot.sizeDelta = Vector2.zero;
        _levelSelectionLayout.minWidth = 0f;
        _levelSelectionLayout.flexibleHeight = 3f;
        _leaderboardLayout.minWidth = 0f;
        _leaderboardLayout.flexibleHeight = 2f;
    }

    private void ApplyHorizontalLayout(bool useFixedWidth)
    {
        _panelLayout.SetVertical(false);
        _contentRoot.anchorMin = new Vector2(useFixedWidth ? 0.5f : 0.05f, 0.225f);
        _contentRoot.anchorMax = new Vector2(useFixedWidth ? 0.5f : 0.95f, 0.85f);
        _contentRoot.sizeDelta = new Vector2(useFixedWidth ? _wideContentWidth : 0f, 0f);
        _levelSelectionLayout.minWidth = 800f;
        _levelSelectionLayout.flexibleHeight = 1f;
        _leaderboardLayout.minWidth = 340f;
        _leaderboardLayout.flexibleHeight = 1f;
    }

    private void ValidateDependencies()
    {
        if (_contentRoot == null)
            throw new InvalidOperationException(nameof(_contentRoot));

        if (_panelLayout == null)
            throw new InvalidOperationException(nameof(_panelLayout));

        if (_levelSelectionLayout == null)
            throw new InvalidOperationException(nameof(_levelSelectionLayout));

        if (_leaderboardLayout == null)
            throw new InvalidOperationException(nameof(_leaderboardLayout));
    }
}
