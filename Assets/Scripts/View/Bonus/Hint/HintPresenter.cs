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
    [SerializeField] private HintHandView _hand;
    [SerializeField] private Ease _movementEase = Ease.InOutSine;
    [SerializeField, Min(0.01f)] private float _itemMoveDuration = 0.35f;
    [SerializeField, Min(0f)] private float _targetHoldDuration = 0.2f;

    private Sequence _presentationSequence;
    private Action _completed;
    private ItemPose _animatedItem;
    private ItemPose _displacedItem;
    private readonly List<ParticleSystem> _matchingHighlightPool = new List<ParticleSystem>();

    public bool IsPlaying => _presentationSequence != null;

    private void Awake()
    {
        if (_hand == null)
            throw new InvalidOperationException(nameof(_hand));

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

        if (suggestion.SourceColumn.IsEmpty)
            throw new InvalidOperationException("Исходный слот подсказки больше не содержит предмет.");

        if (suggestion.IsSwap == suggestion.TargetColumn.IsEmpty)
            throw new InvalidOperationException("Целевой слот подсказки изменился.");

        EnsureMatchingHighlightCapacity(suggestion.TargetMatchingItems.Count);

        StopParticleSystems();

        _animatedItem = new ItemPose(suggestion.SourceColumn.FrontItem);
        _displacedItem = suggestion.IsSwap ? new ItemPose(suggestion.TargetColumn.FrontItem) : null;

        Vector3 sourcePosition = _animatedItem.Item.transform.position;
        Vector3 targetPosition = suggestion.TargetColumn.ItemAnchor.TransformPoint(_animatedItem.LocalPosition);

        PlayAtPosition(_sourceHighlight, sourcePosition);

        for (int index = 0; index < suggestion.TargetMatchingItems.Count; index++)
            PlayAtPosition(_matchingHighlightPool[index], suggestion.TargetMatchingItems[index].transform.position);

        PlayAtPosition(_targetHighlight, targetPosition);

        _completed = completed;
        _movementParticles.transform.position = sourcePosition;
        _movementParticles.Play(false);
        _hand.Show(_animatedItem.Item.transform);

        _presentationSequence = DOTween.Sequence();
        _presentationSequence.AppendInterval(_hand.AppearDuration);
        _presentationSequence.AppendCallback(_hand.Grab);
        _presentationSequence.AppendInterval(_hand.PressDuration);
        _presentationSequence.Append(_animatedItem.Item.transform.DOMove(targetPosition, _itemMoveDuration).SetEase(_movementEase));
        _presentationSequence.Join(_movementParticles.transform.DOMove(targetPosition, _itemMoveDuration).SetEase(_movementEase));

        Vector3 displacedPosition = Vector3.zero;

        if (_displacedItem != null)
        {
            displacedPosition = _displacedItem.Item.transform.position;
            Vector3 displacedTargetPosition = suggestion.SourceColumn.ItemAnchor.TransformPoint(_displacedItem.LocalPosition);
            _presentationSequence.Join(_displacedItem.Item.transform.DOMove(displacedTargetPosition, _itemMoveDuration).SetEase(_movementEase));
        }

        _presentationSequence.AppendCallback(_hand.Release);
        _presentationSequence.AppendInterval(Mathf.Max(_targetHoldDuration, GetHighlightRemainingDuration()));
        _presentationSequence.AppendCallback(_hand.Hide);
        _presentationSequence.AppendInterval(_hand.AppearDuration);
        _presentationSequence.Append(_animatedItem.Item.transform.DOMove(sourcePosition, _itemMoveDuration).SetEase(_movementEase));

        if (_displacedItem != null)
            _presentationSequence.Join(_displacedItem.Item.transform.DOMove(displacedPosition, _itemMoveDuration).SetEase(_movementEase));

        _presentationSequence.OnComplete(CompletePresentation);
        _presentationSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void Stop()
    {
        _presentationSequence?.Kill();
        RestoreItems();
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

        float elapsedDuration = _hand.AppearDuration + _hand.PressDuration + _itemMoveDuration;
        float remainingDuration = Mathf.Max(MinimumPresentationDuration, duration) - elapsedDuration;
        return Mathf.Max(0f, remainingDuration);
    }

    private void CompletePresentation()
    {
        Action completed = _completed;

        RestoreItems();
        _completed = null;
        _presentationSequence = null;

        StopParticleSystems();

        if (_hand != null)
            _hand.HideImmediately();

        completed?.Invoke();
    }

    private void RestoreItems()
    {
        _animatedItem?.Restore();
        _displacedItem?.Restore();
        _animatedItem = null;
        _displacedItem = null;
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

    private sealed class ItemPose
    {
        private readonly Transform _parent;
        private readonly Quaternion _localRotation;
        private readonly Vector3 _localScale;

        public ShelfItem Item { get; }
        public Vector3 LocalPosition { get; }

        public ItemPose(ShelfItem item)
        {
            Item = item;
            _parent = item.transform.parent;
            LocalPosition = item.transform.localPosition;
            _localRotation = item.transform.localRotation;
            _localScale = item.transform.localScale;
        }

        public void Restore()
        {
            if (Item == null)
                return;

            Transform itemTransform = Item.transform;
            itemTransform.SetParent(_parent, false);
            itemTransform.localPosition = LocalPosition;
            itemTransform.localRotation = _localRotation;
            itemTransform.localScale = _localScale;
        }
    }
}
