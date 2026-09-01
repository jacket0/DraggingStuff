using System;
using UnityEngine;

public abstract class LeaderboardScoreSource : MonoBehaviour
{
    public abstract long Value { get; }

    public event Action ValueChanged;

    protected void NotifyValueChanged()
    {
        ValueChanged?.Invoke();
    }
}