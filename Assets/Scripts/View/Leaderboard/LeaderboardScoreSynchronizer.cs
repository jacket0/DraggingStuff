using System;
using System.Collections;
using UnityEngine;
using YG;
using YG.Utils.LB;

public class LeaderboardScoreSynchronizer : MonoBehaviour
{
    [SerializeField] private TotalScoreService _totalScore;
    [SerializeField] private LeaderboardYG _leaderboard;
    [SerializeField, Min(1f)] private float _refreshDelay = 1.1f;

    private Coroutine _refreshCoroutine;
    private int? _submittedScore;

    private void OnEnable()
    {
        YG2.onGetSDKData += HandleSdkDataReceived;
        YG2.onGetLeaderboard += HandleLeaderboardReceived;

        if (YG2.isSDKEnabled)
            RequestLeaderboard();
    }

    private void OnDisable()
    {
        YG2.onGetSDKData -= HandleSdkDataReceived;
        YG2.onGetLeaderboard -= HandleLeaderboardReceived;

        if (_refreshCoroutine != null)
            StopCoroutine(_refreshCoroutine);

        _refreshCoroutine = null;
    }

    private void RequestLeaderboard()
    {
        if (!YG2.isSDKEnabled)
            return;

        _leaderboard.UpdateLB();
    }

    private void HandleSdkDataReceived()
    {
        RequestLeaderboard();
    }

    private void HandleLeaderboardReceived(LBData leaderboardData)
    {
        if (leaderboardData == null || leaderboardData.technoName != _leaderboard.nameLB)
            return;

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
        long totalBestScore = _totalScore.Value;

        if (totalBestScore < 0 || totalBestScore > int.MaxValue)
        {
            Debug.LogError($"Leaderboard score is outside Int32 range: {totalBestScore}");
            score = 0;
            return false;
        }

        score = (int)totalBestScore;
        return score > 0;
    }

    private IEnumerator RefreshAfterSubmission()
    {
        yield return new WaitForSecondsRealtime(_refreshDelay);
        _refreshCoroutine = null;
        RequestLeaderboard();
    }
}
