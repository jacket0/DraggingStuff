using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class DefeatResultView : MonoBehaviour
{
    private const float RaysTurnDuration = 24f;
    private const float IconShakeDuration = 0.7f;
    private const float IconShakeAngle = 16f;
    private const int IconShakeVibrato = 9;
    private const float ProgressFillDelay = 0.25f;
    private const float ProgressFillDuration = 0.6f;

    [Serializable]
    private sealed class ReasonCaption
    {
        [SerializeField] private LevelDefeatReason _reason;
        [SerializeField] private GameObject _caption;

        public LevelDefeatReason Reason => _reason;
        public GameObject Caption => _caption;
    }

    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _menuButton;
    [SerializeField] private TMP_Text _collectedResult;
    [SerializeField] private RectTransform _collectedFill;
    [SerializeField] private TMP_Text _scoreResult;
    [SerializeField] private TMP_Text _bestScoreResult;
    [SerializeField] private RectTransform _lightRays;
    [FormerlySerializedAs("_clock")]
    [SerializeField] private RectTransform _icon;
    [SerializeField] private AudioClip _defeatClip;
    [SerializeField] private AudioMixerGroup _outputAudioMixerGroup;
    [SerializeField, Range(0f, 1f)] private float _defeatVolume = 0.7f;
    [SerializeField] private ReasonCaption[] _reasonCaptions = Array.Empty<ReasonCaption>();

    private Sequence _showSequence;
    private Tween _raysTween;
    private Quaternion _iconRotation;

    public event Action RestartRequested;
    public event Action MenuRequested;

    private void Awake()
    {
        ValidateDependencies();
        _iconRotation = _icon.localRotation;
    }

    private void OnEnable()
    {
        _restartButton.onClick.AddListener(RestartButtonClicked);
        _menuButton.onClick.AddListener(MenuButtonClicked);
    }

    private void OnDisable()
    {
        _restartButton.onClick.RemoveListener(RestartButtonClicked);
        _menuButton.onClick.RemoveListener(MenuButtonClicked);
        KillTweens();
    }

    public void Show(LevelRunResult result, long bestScore)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        if (bestScore < 0)
            throw new ArgumentOutOfRangeException(nameof(bestScore));

        _collectedResult.SetText($"{result.CollectedItemCount} / {result.TotalItemCount}");
        _scoreResult.SetText(result.Score.ToString());
        _bestScoreResult.SetText(bestScore.ToString());
        gameObject.SetActive(true);
        ShowReasonCaption(result.DefeatReason);
        PlayShowAnimation(GetCollectedShare(result));
        UiAudioPlayer.Play(_defeatClip, _outputAudioMixerGroup, _defeatVolume);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetInteractable(bool interactable)
    {
        _restartButton.interactable = interactable;
        _menuButton.interactable = interactable;
    }

    private void ShowReasonCaption(LevelDefeatReason reason)
    {
        foreach (ReasonCaption caption in _reasonCaptions)
            caption.Caption.SetActive(caption.Reason == reason);
    }

    private static float GetCollectedShare(LevelRunResult result)
    {
        return result.TotalItemCount > 0 ? (float)result.CollectedItemCount / result.TotalItemCount : 0f;
    }

    private void PlayShowAnimation(float collectedShare)
    {
        KillTweens();
        _icon.localRotation = _iconRotation;
        _collectedFill.anchorMax = new Vector2(0f, 1f);
        _lightRays.localRotation = Quaternion.identity;

        _raysTween = _lightRays
            .DORotate(new Vector3(0f, 0f, 360f), RaysTurnDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        _showSequence = DOTween.Sequence()
            .Append(_icon.DOShakeRotation(IconShakeDuration, new Vector3(0f, 0f, IconShakeAngle), IconShakeVibrato, 0f))
            .Insert(ProgressFillDelay, _collectedFill.DOAnchorMax(new Vector2(collectedShare, 1f), ProgressFillDuration).SetEase(Ease.OutCubic))
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void KillTweens()
    {
        _raysTween?.Kill();
        _raysTween = null;
        _showSequence?.Kill(true);
        _showSequence = null;
    }

    private void RestartButtonClicked()
    {
        RestartRequested?.Invoke();
    }

    private void MenuButtonClicked()
    {
        MenuRequested?.Invoke();
    }

    private void ValidateDependencies()
    {
        if (_restartButton == null || _menuButton == null)
            throw new InvalidOperationException(nameof(Button));

        if (_collectedResult == null || _scoreResult == null || _bestScoreResult == null)
            throw new InvalidOperationException(nameof(TMP_Text));

        if (_collectedFill == null || _lightRays == null || _icon == null)
            throw new InvalidOperationException(nameof(DefeatResultView));

        foreach (ReasonCaption caption in _reasonCaptions)
        {
            if (caption.Caption == null)
                throw new InvalidOperationException(nameof(_reasonCaptions));
        }
    }
}
