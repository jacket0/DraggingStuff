using System;

public interface IRewardedAdService
{
    public event Action<string> RewardGranted;
    public event Action ShowingStateChanged;

    public bool IsSupported { get; }
    public bool IsShowing { get; }

    public bool TryShow(string rewardId);
}
