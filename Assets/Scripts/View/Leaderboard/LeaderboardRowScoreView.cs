using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using YG;

public sealed class LeaderboardRowScoreView : MonoBehaviour
{
    [SerializeField] private LBPlayerDataYG _playerData;
    [SerializeField] private TMP_Text _scoreText;

    private long _score;
    private bool _hasScore;

    private void Awake()
    {
        if (_playerData == null)
            throw new InvalidOperationException(nameof(_playerData));

        if (_scoreText == null)
            throw new InvalidOperationException(nameof(_scoreText));
    }

    private void OnEnable()
    {
        YG2.onSwitchLang += Refresh;
    }

    private void Start()
    {
        _hasScore = long.TryParse(_playerData.data.score, NumberStyles.Integer, CultureInfo.InvariantCulture, out _score) && _score >= 0;
        Refresh(YG2.lang);
    }

    private void OnDisable()
    {
        YG2.onSwitchLang -= Refresh;
    }

    private void Refresh(string language)
    {
        _scoreText.SetText(_hasScore ? ScoreTextFormatter.FormatCompact(_score, language) : string.Empty);
    }
}
