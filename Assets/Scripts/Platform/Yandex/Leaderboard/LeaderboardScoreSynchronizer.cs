using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YG;
using YG.Utils.LB;

public class LeaderboardScoreSynchronizer : MonoBehaviour
{
    private static readonly Dictionary<string, float> RequestTimes = new Dictionary<string, float>();
    private static readonly Dictionary<string, LBData> ReceivedLeaderboards = new Dictionary<string, LBData>();

    [SerializeField] private LeaderboardScoreSource _scoreSource;
    [SerializeField] private LeaderboardYG _leaderboard;
    [SerializeField, Min(1f)] private float _refreshDelay = 1.1f;
    [SerializeField, Min(0f)] private float _minRequestInterval = 60f;

    private Coroutine _refreshCoroutine;
    private int? _submittedScore;

    private void OnEnable()
    {
        YG2.onGetSDKData += HandleSdkDataReceived;
        YG2.onGetLeaderboard += HandleLeaderboardReceived;
        _scoreSource.ValueChanged += HandleScoreChanged;
    }

    private void OnDisable()
    {
        YG2.onGetSDKData -= HandleSdkDataReceived;
        YG2.onGetLeaderboard -= HandleLeaderboardReceived;
        _scoreSource.ValueChanged -= HandleScoreChanged;

        if (_refreshCoroutine != null)
            StopCoroutine(_refreshCoroutine);

        _refreshCoroutine = null;
    }

    private void Start()
    {
        if (YG2.isSDKEnabled)
            RequestLeaderboard();
    }

    private void RequestLeaderboard()
    {
        if (!YG2.isSDKEnabled)
            return;

        if (RequestTimes.TryGetValue(_leaderboard.nameLB, out float requestTime) &&
            Time.realtimeSinceStartup - requestTime < _minRequestInterval)
        {
            if (ReceivedLeaderboards.TryGetValue(_leaderboard.nameLB, out LBData receivedLeaderboard))
                YG2.onGetLeaderboard?.Invoke(receivedLeaderboard);

            return;
        }

        SendLeaderboardRequest();
    }

    private void SendLeaderboardRequest()
    {
        RequestTimes[_leaderboard.nameLB] = Time.realtimeSinceStartup;
        _leaderboard.UpdateLB();
    }

    private void HandleSdkDataReceived()
    {
        RequestLeaderboard();
    }

    private void HandleScoreChanged()
    {
        if (ReceivedLeaderboards.TryGetValue(_leaderboard.nameLB, out LBData receivedLeaderboard))
            TrySubmitScore(receivedLeaderboard);
        else if (YG2.isSDKEnabled)
            SendLeaderboardRequest();
    }

    private void HandleLeaderboardReceived(LBData leaderboardData)
    {
        if (leaderboardData == null || leaderboardData.technoName != _leaderboard.nameLB)
            return;

        ReceivedLeaderboards[_leaderboard.nameLB] = leaderboardData;
        TrySubmitScore(leaderboardData);
    }

    private void TrySubmitScore(LBData leaderboardData)
    {
        if (!YG2.player.auth || !TryGetPlatformScore(out int localScore))
            return;

        int remoteScore = leaderboardData.currentPlayer?.score ?? 0;
        int effectiveRemoteScore = Math.Max(remoteScore, _submittedScore ?? 0);

        if (localScore <= effectiveRemoteScore)
            return;

        _submittedScore = localScore;
        _leaderboard.SetLeaderboard(localScore);

        if (_refreshCoroutine != null)
            StopCoroutine(_refreshCoroutine);

        _refreshCoroutine = StartCoroutine(RefreshAfterSubmission());
    }

    private bool TryGetPlatformScore(out int score)
    {
        long scoreValue = _scoreSource.Value;

        if (scoreValue < 0 || scoreValue > int.MaxValue)
        {
            Debug.LogError($"Leaderboard score is outside Int32 range: {scoreValue}");
            score = 0;
            return false;
        }

        score = (int)scoreValue;
        return score > 0;
    }

    private IEnumerator RefreshAfterSubmission()
    {
        yield return new WaitForSecondsRealtime(_refreshDelay);
        _refreshCoroutine = null;

        if (YG2.isSDKEnabled)
            SendLeaderboardRequest();
    }
}
