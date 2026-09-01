using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class HintPresenter : MonoBehaviour, IHintPresenter
{
    private const float MinimumPresentationDuration = 1f;

    [SerializeField] private ParticleSystem _sourceHighlight;
    [SerializeField] private ParticleSystem[] _matchingHighlights;
    [SerializeField] private ParticleSystem _targetHighlight;
    [SerializeField] private ParticleSystem _movementParticles;
    [SerializeField] private Ease _movementEase = Ease.InOutSine;
    [SerializeField, Min(0.01f)] private float _itemMoveDuration = 0.35f;
    [SerializeField, Min(0f)] private float _targetHoldDuration = 0.2f;

    private Sequence _presentationSequence;
    private Action _completed;
    private ShelfItem _animatedItem;
    private Transform _originalParent;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;
    private Vector3 _originalLocalScale;
    private readonly List<ParticleSystem> _matchingHighlightPool = new List<ParticleSystem>();

    public bool IsPlaying => _presentationSequence != null;

    private void Awake()
    {
        foreach (ParticleSystem matchingHighlight in _matchingHighlights)
        {
            if (matchingHighlight == null || _matchingHighlightPool.Contains(matchingHighlight))
                throw new InvalidOperationException(nameof(_matchingHighlights));

            _matchingHighlightPool.Add(matchingHighlight);
        }

        if (_matchingHighlightPool.Count == 0)
            throw new InvalidOperationException(nameof(_matchingHighlights));
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play(MoveSuggestion suggestion, Action completed)
    {
        if (suggestion == null)
            throw new ArgumentNullException(nameof(suggestion), "Подсказка не задана.");

        if (completed == null)
            throw new ArgumentNullException(nameof(completed), "Обработчик завершения подсказки не задан.");

        if (IsPlaying)
            throw new InvalidOperationException("Отображение подсказки уже запущено.");

        if (suggestion.SourceSlot.IsEmpty)
            throw new InvalidOperationException("Исходный слот подсказки больше не содержит предмет.");

        if (!suggestion.TargetSlot.IsEmpty)
            throw new InvalidOperationException("Целевой слот подсказки уже занят.");

        EnsureMatchingHighlightCapacity(suggestion.TargetMatchingItems.Count);

        StopParticleSystems();

        _animatedItem = suggestion.SourceSlot.Item;
        _originalParent = _animatedItem.transform.parent;
        _originalLocalPosition = _animatedItem.transform.localPosition;
        _originalLocalRotation = _animatedItem.transform.localRotation;
        _originalLocalScale = _animatedItem.transform.localScale;

        Vector3 sourcePosition = _animatedItem.transform.position;
        Vector3 targetPosition = suggestion.TargetSlot.transform.TransformPoint(_originalLocalPosition);

        PlayAtPosition(_sourceHighlight, sourcePosition);

        for (int index = 0; index < suggestion.TargetMatchingItems.Count; index++)
            PlayAtPosition(_matchingHighlightPool[index], suggestion.TargetMatchingItems[index].transform.position);

        PlayAtPosition(_targetHighlight, targetPosition);

        _completed = completed;
        _movementParticles.transform.position = sourcePosition;
        _movementParticles.Play(false);

        _presentationSequence = DOTween.Sequence();
        _presentationSequence.Append(_animatedItem.transform.DOMove(targetPosition, _itemMoveDuration).SetEase(_movementEase));
        _presentationSequence.Join(_movementParticles.transform.DOMove(targetPosition, _itemMoveDuration).SetEase(_movementEase));
        _presentationSequence.AppendInterval(Mathf.Max(_targetHoldDuration, GetHighlightRemainingDuration()));
        _presentationSequence.Append(_animatedItem.transform.DOMove(sourcePosition, _itemMoveDuration).SetEase(_movementEase));
        _presentationSequence.OnComplete(CompletePresentation);
        _presentationSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void Stop()
    {
        _presentationSequence?.Kill();
        RestoreAnimatedItem();
        CompletePresentation();
    }

    private void PlayAtPosition(ParticleSystem particleSystem, Vector3 position)
    {
        particleSystem.transform.position = position;
        particleSystem.Play(false);
    }

    private float GetHighlightRemainingDuration()
    {
        float duration = Mathf.Max(_sourceHighlight.main.duration, _targetHighlight.main.duration);
        duration = Mathf.Max(duration, _movementParticles.main.duration);

        foreach (ParticleSystem matchingHighlight in _matchingHighlightPool)
            duration = Mathf.Max(duration, matchingHighlight.main.duration);

        float remainingDuration = Mathf.Max(MinimumPresentationDuration, duration) - _itemMoveDuration;
        return Mathf.Max(0f, remainingDuration);
    }

    private void CompletePresentation()
    {
        Action completed = _completed;

        RestoreAnimatedItem();
        _completed = null;
        _presentationSequence = null;

        StopParticleSystems();
        completed?.Invoke();
    }

    private void RestoreAnimatedItem()
    {
        if (_animatedItem == null)
            return;

        Transform itemTransform = _animatedItem.transform;
        itemTransform.SetParent(_originalParent, false);
        itemTransform.localPosition = _originalLocalPosition;
        itemTransform.localRotation = _originalLocalRotation;
        itemTransform.localScale = _originalLocalScale;

        _animatedItem = null;
        _originalParent = null;
    }

    private void StopParticleSystems()
    {
        _sourceHighlight.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        _targetHighlight.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        _movementParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

        foreach (ParticleSystem matchingHighlight in _matchingHighlightPool)
            matchingHighlight.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void EnsureMatchingHighlightCapacity(int count)
    {
        ParticleSystem template = _matchingHighlightPool[0];

        while (_matchingHighlightPool.Count < count)
        {
            ParticleSystem matchingHighlight = Instantiate(template, template.transform.parent);
            matchingHighlight.name = $"MatchingHighlight{_matchingHighlightPool.Count + 1}";
            _matchingHighlightPool.Add(matchingHighlight);
        }
    }
}
