using System;
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

    private Sequence _presentationSequence;
    private Action _completed;

    public bool IsPlaying => _presentationSequence != null;

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

        if (suggestion.TargetMatchingItems.Count > _matchingHighlights.Length)
            throw new InvalidOperationException("Недостаточно систем подсветки совпадающих предметов.");

        StopParticleSystems();

        Vector3 sourcePosition = suggestion.SourceSlot.Item.transform.position;
        Vector3 targetPosition = suggestion.TargetSlot.transform.position;

        PlayAtPosition(_sourceHighlight, sourcePosition);

        for (int index = 0; index < suggestion.TargetMatchingItems.Count; index++)
            PlayAtPosition(_matchingHighlights[index], suggestion.TargetMatchingItems[index].transform.position);

        PlayAtPosition(_targetHighlight, targetPosition);

        float movementDuration = _movementParticles.main.duration;
        float presentationDuration = GetPresentationDuration();
        float remainingDuration = presentationDuration - movementDuration;

        _completed = completed;
        _movementParticles.transform.position = sourcePosition;
        _movementParticles.Play(false);

        _presentationSequence = DOTween.Sequence();
        _presentationSequence.Append(_movementParticles.transform.DOMove(targetPosition, movementDuration).SetEase(_movementEase));
        _presentationSequence.AppendInterval(remainingDuration);
        _presentationSequence.OnComplete(CompletePresentation);
        _presentationSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void Stop()
    {
        _presentationSequence?.Kill();
        CompletePresentation();
    }

    private void PlayAtPosition(ParticleSystem particleSystem, Vector3 position)
    {
        particleSystem.transform.position = position;
        particleSystem.Play(false);
    }

    private float GetPresentationDuration()
    {
        float duration = Mathf.Max(_sourceHighlight.main.duration, _targetHighlight.main.duration);
        duration = Mathf.Max(duration, _movementParticles.main.duration);

        foreach (ParticleSystem matchingHighlight in _matchingHighlights)
            duration = Mathf.Max(duration, matchingHighlight.main.duration);

        return Mathf.Max(MinimumPresentationDuration, duration);
    }

    private void CompletePresentation()
    {
        Action completed = _completed;

        _completed = null;
        _presentationSequence = null;

        StopParticleSystems();
        completed?.Invoke();
    }

    private void StopParticleSystems()
    {
        _sourceHighlight.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        _targetHighlight.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        _movementParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

        foreach (ParticleSystem matchingHighlight in _matchingHighlights)
            matchingHighlight.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}