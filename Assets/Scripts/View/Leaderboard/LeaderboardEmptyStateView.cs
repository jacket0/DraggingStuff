using System;
using System.Collections;
using TMPro;
using UnityEngine;
using YG;
using YG.LanguageLegacy;
using YG.Utils.LB;

[DefaultExecutionOrder(-100)]
public sealed class LeaderboardEmptyStateView : MonoBehaviour
{
    private enum State
    {
        Loading,
        Empty,
        Error,
        Content
    }

    [SerializeField] private LeaderboardYG _leaderboard;
    [SerializeField, Min(1f)] private float _requestTimeout = 8f;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private LanguageYG _loadingLocalization;
    [SerializeField] private LanguageYG _emptyLocalization;
    [SerializeField] private LanguageYG _errorLocalization;
    private Coroutine _timeoutCoroutine;

    private void Awake()
    {
        if (_leaderboard == null)
            throw new InvalidOperationException(nameof(_leaderboard));

        if (_leaderboard.rootSpawnPlayersData == null)
            throw new InvalidOperationException(nameof(_leaderboard.rootSpawnPlayersData));

        if (_statusText == null)
            throw new InvalidOperationException(nameof(_statusText));

        if (_loadingLocalization == null || _emptyLocalization == null || _errorLocalization == null)
            throw new InvalidOperationException(nameof(LanguageYG));
    }

    private void OnEnable()
    {
        YG2.onGetLeaderboard += HandleLeaderboardReceived;
        ShowLoading();
    }

    private void OnDisable()
    {
        YG2.onGetLeaderboard -= HandleLeaderboardReceived;
        StopTimeout();
    }

    private void HandleLeaderboardReceived(LBData leaderboardData)
    {
        if (leaderboardData == null || leaderboardData.technoName != _leaderboard.nameLB)
            return;

        StopTimeout();

        if (leaderboardData.entries == InfoYG.NO_DATA)
        {
            ShowState(State.Empty);
            return;
        }

        ShowState(State.Content);
    }

    private void ShowLoading()
    {
        ShowState(State.Loading);
        StopTimeout();
        _timeoutCoroutine = StartCoroutine(ShowErrorAfterTimeout());
    }

    private IEnumerator ShowErrorAfterTimeout()
    {
        yield return new WaitForSecondsRealtime(_requestTimeout);
        _timeoutCoroutine = null;
        ShowState(State.Error);
    }

    private void ShowState(State state)
    {
        bool showsContent = state == State.Content;
        _leaderboard.rootSpawnPlayersData.gameObject.SetActive(showsContent);
        _statusText.gameObject.SetActive(!showsContent);
        SelectLocalization(state);
    }

    private void SelectLocalization(State state)
    {
        if (state == State.Content)
            return;

        LanguageYG selectedLocalization = state switch
        {
            State.Loading => _loadingLocalization,
            State.Empty => _emptyLocalization,
            State.Error => _errorLocalization,
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };

        _loadingLocalization.enabled = selectedLocalization == _loadingLocalization;
        _emptyLocalization.enabled = selectedLocalization == _emptyLocalization;
        _errorLocalization.enabled = selectedLocalization == _errorLocalization;
        selectedLocalization.SwitchLanguage();
    }

    private void StopTimeout()
    {
        if (_timeoutCoroutine == null)
            return;

        StopCoroutine(_timeoutCoroutine);
        _timeoutCoroutine = null;
    }

}
