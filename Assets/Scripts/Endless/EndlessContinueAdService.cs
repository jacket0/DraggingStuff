using System;
using UnityEngine;

public sealed class EndlessContinueAdService : MonoBehaviour
{
    private const string RewardId = "endless.continue";

    [SerializeField] private EndlessSession _session;
    [SerializeField] private YandexRewardedAdService _rewardedAdService;

    private bool _isWaitingForAd;
    private bool _isRewardGranted;

    public bool HasShownAd { get; private set; }

    public event Action<bool> AdFinished;

    private IRewardedAdService RewardedAdService => _rewardedAdService;

    private void Awake()
    {
        if (_session == null)
            throw new InvalidOperationException(nameof(_session));

        if (_rewardedAdService == null)
            throw new InvalidOperationException(nameof(_rewardedAdService));
    }

    private void OnEnable()
    {
        RewardedAdService.RewardGranted += HandleRewardGranted;
        RewardedAdService.ShowingStateChanged += HandleShowingStateChanged;
    }

    private void OnDisable()
    {
        RewardedAdService.RewardGranted -= HandleRewardGranted;
        RewardedAdService.ShowingStateChanged -= HandleShowingStateChanged;
    }

    public bool CanShow()
    {
        return _session.IsContinueOffered
            && !_isWaitingForAd
            && RewardedAdService.IsSupported
            && !RewardedAdService.IsShowing;
    }

    public bool TryShow()
    {
        if (!CanShow())
            return false;

        _isWaitingForAd = true;
        _isRewardGranted = false;

        if (!RewardedAdService.TryShow(RewardId))
        {
            _isWaitingForAd = false;
            return false;
        }

        return true;
    }

    private void HandleRewardGranted(string rewardId)
    {
        if (rewardId != RewardId)
            return;

        _isRewardGranted = true;

        if (!RewardedAdService.IsShowing)
            CompleteAd();
    }

    private void HandleShowingStateChanged()
    {
        if (_isWaitingForAd && !RewardedAdService.IsShowing)
            CompleteAd();
    }

    private void CompleteAd()
    {
        bool isRewarded = _isRewardGranted;
        _isWaitingForAd = false;
        _isRewardGranted = false;

        if (isRewarded)
        {
            HasShownAd = true;
            _session.Continue();
        }

        AdFinished?.Invoke(isRewarded);
    }
}
