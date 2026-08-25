using System;
using UnityEngine;
using YG;

public sealed class YandexRewardedAdService : MonoBehaviour, IRewardedAdService
{
    private bool _isShowing;

    public event Action<string> RewardGranted;
    public event Action ShowingStateChanged;

    public bool IsSupported => true;
    public bool IsShowing => _isShowing;

    private void OnEnable()
    {
        YG2.onOpenRewardedAdv += HandleOpened;
        YG2.onCloseRewardedAdv += HandleClosed;
        YG2.onErrorRewardedAdv += HandleFailed;
        YG2.onRewardAdv += HandleRewardGranted;

        SetShowing(YG2.nowRewardAdv);
    }

    private void OnDisable()
    {
        YG2.onOpenRewardedAdv -= HandleOpened;
        YG2.onCloseRewardedAdv -= HandleClosed;
        YG2.onErrorRewardedAdv -= HandleFailed;
        YG2.onRewardAdv -= HandleRewardGranted;
    }

    public bool TryShow(string rewardId)
    {
        if (string.IsNullOrWhiteSpace(rewardId))
            throw new ArgumentException(nameof(rewardId));

        if (_isShowing || YG2.nowRewardAdv || YG2.nowInterAdv)
            return false;

        SetShowing(true);

        try
        {
            YG2.RewardedAdvShow(rewardId);
            return true;
        }
        catch
        {
            SetShowing(false);
            throw;
        }
    }

    private void HandleOpened()
    {
        SetShowing(true);
    }

    private void HandleClosed()
    {
        SetShowing(false);
    }

    private void HandleFailed()
    {
        SetShowing(false);
    }

    private void HandleRewardGranted(string rewardId)
    {
        RewardGranted?.Invoke(rewardId);
    }

    private void SetShowing(bool isShowing)
    {
        if (_isShowing == isShowing)
            return;

        _isShowing = isShowing;
        ShowingStateChanged?.Invoke();
    }
}