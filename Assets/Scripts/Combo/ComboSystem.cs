using System;
using UnityEngine;
using UnityEngine.Serialization;

public class ComboSystem : TimerSpeedModifierTarget
{
    [FormerlySerializedAs("_comboDuration")]
    [SerializeField, Min(0.01f)] private float _comboStepDuration = 3f;
    [SerializeField, Min(1)] private int _maxComboMultiplier = 10;

    private int _currentCount;
    private float _remainingTime;
    
    public ComboState ComboState => CreateState();

    public event Action<ComboState> StateChanged;
    public event Action<ComboState> ComboIncreased;

    private void Update()
    {
        float comboDeltaTime = Time.deltaTime * TimerSpeedMultiplier;

        AdvanceTimer(comboDeltaTime);
    }

    private void OnValidate()
    {
        _comboStepDuration = Mathf.Max(0.01f, _comboStepDuration);
        _maxComboMultiplier = Mathf.Max(1, _maxComboMultiplier);
    }

    public int RegisterMatch()
    {
        _currentCount = Mathf.Min(_currentCount + 1, _maxComboMultiplier);
        _remainingTime = _comboStepDuration;

        ComboState comboState = CreateState();

        StateChanged?.Invoke(comboState);
        ComboIncreased?.Invoke(comboState);

        return _currentCount;
    }

    private ComboState CreateState()
    {
        float normalizedTime = _currentCount > 0 ? Mathf.Clamp01(_remainingTime / _comboStepDuration) : 0f;
        return new ComboState(_currentCount, _remainingTime, normalizedTime);
    }

    private void AdvanceTimer(float elapsedTime)
    {
        if (_currentCount <= 0 || elapsedTime <= 0f)
            return;

        _remainingTime -= elapsedTime;

        while (_currentCount > 0 && _remainingTime <= 0f)
        {
            _currentCount--;

            if (_currentCount > 0)
                _remainingTime += _comboStepDuration;
            else
                _remainingTime = 0f;
        }

        StateChanged?.Invoke(CreateState());
    }
}
