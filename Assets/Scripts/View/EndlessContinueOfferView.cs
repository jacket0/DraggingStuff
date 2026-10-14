using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EndlessContinueOfferView : MonoBehaviour
{
    [SerializeField] private Button _watchAdButton;
    [SerializeField] private Button _declineButton;
    [SerializeField] private TMP_Text _countdownText;
    [SerializeField] private Image _countdownFill;
    [SerializeField, Min(1f)] private float _countdownDuration = 5f;

    private Coroutine _countdown;

    public event Action WatchAdRequested;
    public event Action DeclineRequested;
    public event Action CountdownFinished;

    private void Awake()
    {
        if (_watchAdButton == null)
            throw new InvalidOperationException(nameof(_watchAdButton));

        if (_declineButton == null)
            throw new InvalidOperationException(nameof(_declineButton));

        if (_countdownText == null)
            throw new InvalidOperationException(nameof(_countdownText));

        if (_countdownFill == null)
            throw new InvalidOperationException(nameof(_countdownFill));
    }

    private void OnEnable()
    {
        _watchAdButton.onClick.AddListener(HandleWatchAdClicked);
        _declineButton.onClick.AddListener(HandleDeclineClicked);
    }

    private void OnDisable()
    {
        _watchAdButton.onClick.RemoveListener(HandleWatchAdClicked);
        _declineButton.onClick.RemoveListener(HandleDeclineClicked);
        StopCountdown();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        RestartCountdown();
    }

    public void Hide()
    {
        StopCountdown();
        gameObject.SetActive(false);
    }

    public void PauseCountdown()
    {
        StopCountdown();
        SetButtonsInteractable(false);
    }

    public void RestartCountdown()
    {
        StopCountdown();
        SetButtonsInteractable(true);
        _countdown = StartCoroutine(RunCountdown());
    }

    private IEnumerator RunCountdown()
    {
        float remainingTime = _countdownDuration;

        while (remainingTime > 0f)
        {
            ApplyCountdown(remainingTime);
            yield return null;
            remainingTime -= Time.deltaTime;
        }

        ApplyCountdown(0f);
        _countdown = null;
        SetButtonsInteractable(false);
        CountdownFinished?.Invoke();
    }

    private void ApplyCountdown(float remainingTime)
    {
        _countdownText.SetText(Mathf.CeilToInt(remainingTime).ToString());
        _countdownFill.fillAmount = remainingTime / _countdownDuration;
    }

    private void StopCountdown()
    {
        if (_countdown == null)
            return;

        StopCoroutine(_countdown);
        _countdown = null;
    }

    private void SetButtonsInteractable(bool isInteractable)
    {
        _watchAdButton.interactable = isInteractable;
        _declineButton.interactable = isInteractable;
    }

    private void HandleWatchAdClicked()
    {
        WatchAdRequested?.Invoke();
    }

    private void HandleDeclineClicked()
    {
        StopCountdown();
        SetButtonsInteractable(false);
        DeclineRequested?.Invoke();
    }
}
