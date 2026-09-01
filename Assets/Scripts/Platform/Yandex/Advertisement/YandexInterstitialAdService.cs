using System;
using UnityEngine;
using YG;

public sealed class YandexInterstitialAdService : MonoBehaviour, IInterstitialAdService
{
    private Action _completed;

    private void OnEnable()
    {
        YG2.onCloseInterAdv += HandleCompleted;
        YG2.onErrorInterAdv += HandleCompleted;
    }

    private void OnDisable()
    {
        YG2.onCloseInterAdv -= HandleCompleted;
        YG2.onErrorInterAdv -= HandleCompleted;
    }

    public void Show(Action completed)
    {
        if (completed == null)
            throw new ArgumentNullException(nameof(completed));

        if (_completed != null)
            throw new InvalidOperationException(nameof(YandexInterstitialAdService));

        if (!CanRequestAdvertisement())
        {
            completed();
            return;
        }

        _completed = completed;

        try
        {
            YG2.InterstitialAdvShow();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            CompleteRequest();
        }
    }

    private static bool CanRequestAdvertisement()
    {
        return YG2.isSDKEnabled && YG2.isTimerAdvCompleted && !YG2.nowAdsShow;
    }

    private void HandleCompleted()
    {
        CompleteRequest();
    }

    private void CompleteRequest()
    {
        Action completed = _completed;
        _completed = null;
        completed?.Invoke();
    }
}