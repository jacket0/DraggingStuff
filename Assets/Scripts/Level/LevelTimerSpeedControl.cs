using System;
using UnityEngine;

public sealed class LevelTimerSpeedControl : TimerSpeedModifierTarget
{
    [SerializeField] private CountdownTimer _timer;

    private void Awake()
    {
        if (_timer == null)
            throw new InvalidOperationException(nameof(_timer));
    }

    private void Update()
    {
        _timer.SetCountdownRate(TimerSpeedMultiplier);
    }
}
