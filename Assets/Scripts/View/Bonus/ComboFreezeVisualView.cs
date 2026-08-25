using DG.Tweening;
using UnityEngine;

public sealed class ComboFreezeVisualView : MonoBehaviour
{
    [SerializeField] private BonusDefinition _definition;
    [SerializeField] private BonusUseController _useController;

    [SerializeField] private CanvasGroup _screenTintCanvasGroup;
    [SerializeField] private CanvasGroup _comboFrostCanvasGroup;
    [SerializeField] private RectTransform _comboFrostRectTransform;
    [SerializeField] private ParticleSystem[] _edgeParticleSystems;

    [SerializeField, Range(0f, 1f)] private float _screenTintOpacity = 0.18f;
    [SerializeField, Range(0.75f, 1f)] private float _hiddenFrostScale = 0.88f;
    [SerializeField, Min(0f)] private float _showDuration = 0.25f;
    [SerializeField, Min(0f)] private float _hideDuration = 0.2f;

    private Tween _screenTintTween;
    private Tween _comboFrostTween;

    private bool _hasStarted;
    private bool _isFreezeVisible;

    private IBonusRuntimeStateSource RuntimeStateSource => _useController;

    private void Awake()
    {
        _screenTintCanvasGroup.interactable = false;
        _screenTintCanvasGroup.blocksRaycasts = false;

        _comboFrostCanvasGroup.interactable = false;
        _comboFrostCanvasGroup.blocksRaycasts = false;

        ApplyHiddenState();
    }

    private void OnEnable()
    {
        RuntimeStateSource.RuntimeStateChanged += HandleRuntimeStateChanged;

        if (_hasStarted)
            SynchronizeState();
    }

    private void OnDisable()
    {
        RuntimeStateSource.RuntimeStateChanged -= HandleRuntimeStateChanged;

        StopAnimations();
        ApplyHiddenState();

        _isFreezeVisible = false;
    }

    private void Start()
    {
        _hasStarted = true;
        SynchronizeState();
    }

    private void SynchronizeState()
    {
        BonusRuntimeState state = RuntimeStateSource.GetState(_definition);
        SetFreezeVisible(state.Phase == BonusRuntimePhase.Active);
    }

    private void HandleRuntimeStateChanged(BonusRuntimeState state)
    {
        if (state.Definition != _definition)
            return;

        SetFreezeVisible(state.Phase == BonusRuntimePhase.Active);
    }

    private void SetFreezeVisible(bool isVisible)
    {
        if (_isFreezeVisible == isVisible)
            return;

        _isFreezeVisible = isVisible;
        StopAnimations();

        if (isVisible)
            PlayShowAnimation();
        else
            PlayHideAnimation();
    }

    private void PlayShowAnimation()
    {
        PlayEdgeParticles();
        _screenTintTween = _screenTintCanvasGroup.DOFade(_screenTintOpacity, _showDuration).SetEase(Ease.OutCubic);

        _comboFrostTween = DOTween.Sequence().Join(_comboFrostCanvasGroup.DOFade(1f, _showDuration).SetEase(Ease.OutCubic))
            .Join(_comboFrostRectTransform.DOScale(Vector3.one, _showDuration).SetEase(Ease.OutCubic));
    }

    private void PlayHideAnimation()
    {
        StopEdgeParticles();
        _screenTintTween = _screenTintCanvasGroup.DOFade(0f, _hideDuration).SetEase(Ease.InCubic);

        _comboFrostTween = DOTween.Sequence().Join(_comboFrostCanvasGroup.DOFade(0f, _hideDuration).SetEase(Ease.InCubic))
            .Join(_comboFrostRectTransform.DOScale(Vector3.one * _hiddenFrostScale, _hideDuration).SetEase(Ease.InCubic));
    }

    private void StopAnimations()
    {
        _screenTintTween?.Kill();
        _comboFrostTween?.Kill();
        _screenTintTween = null;
        _comboFrostTween = null;
    }

    private void ApplyHiddenState()
    {
        _screenTintCanvasGroup.alpha = 0f;
        _comboFrostCanvasGroup.alpha = 0f;

        _comboFrostRectTransform.localScale = Vector3.one * _hiddenFrostScale;
        StopEdgeParticles();
    }

    private void PlayEdgeParticles()
    {
        foreach (ParticleSystem particleSystem in _edgeParticleSystems)
        {
            particleSystem.Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear);

            particleSystem.Play(false);
        }
    }

    private void StopEdgeParticles()
    {
        foreach (ParticleSystem particleSystem in _edgeParticleSystems)
        {
            particleSystem.Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
