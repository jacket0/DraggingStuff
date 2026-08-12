using System;
using UnityEngine;

public class ComboSystem : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _comboDuration = 4f;

    private int _currentCount;
    private float _remainingTime;
    
    public ComboState ComboState => CreateState();

    public event Action<ComboState> StateChanged;
    public event Action<ComboState> ComboIncreased;

    private void Update()
    {
        float comboDeltaTime = Time.deltaTime;
        bool stateChanged = false;

        if (_currentCount > 0 &&  comboDeltaTime > 0f)
        {
            _remainingTime = Mathf.Max(0f, _remainingTime - comboDeltaTime);

            stateChanged = true;

            if (_remainingTime <= 0f)
            {
                _currentCount = 0;
                _remainingTime = 0f;
            }
        }

        if (stateChanged)
            StateChanged?.Invoke(CreateState());
    }

    private void OnValidate()
    {
        _comboDuration = Mathf.Max(0.01f, _comboDuration);
    }

    public int RegisterMatch()
    {
        _currentCount++;
        _remainingTime = _comboDuration;

        ComboState comboState = CreateState();

        StateChanged?.Invoke(comboState);
        ComboIncreased?.Invoke(comboState);

        return _currentCount;
    }

    public void ResetState()
    {
        _currentCount = 0;
        _remainingTime = 0f;
        StateChanged?.Invoke(CreateState());
    }

    private ComboState CreateState()
    {
        float normalizedTime = _currentCount > 0 ? Mathf.Clamp01(_remainingTime / _comboDuration) : 0f;
        return new ComboState(_currentCount, _remainingTime, normalizedTime);
    }
}
