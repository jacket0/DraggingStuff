using System;
using UnityEngine;

public sealed class HintBonusEffect : BonusEffect
{
    [SerializeField] private MoveSuggestionProvider _suggestionProvider;
    [SerializeField] private HintPresenter _presenter;
    [SerializeField] private ShelfItemDragController _dragController;

    private MoveSuggestion _preparedSuggestion;

    private IMoveSuggestionProvider SuggestionProvider => _suggestionProvider;
    private IHintPresenter Presenter => _presenter;

    private void OnEnable()
    {
        if (_dragController != null)
            _dragController.DragStarting += HandleDragStarting;
    }

    private void OnDisable()
    {
        if (_dragController != null)
            _dragController.DragStarting -= HandleDragStarting;

        _preparedSuggestion = null;

        if (_presenter != null)
            Presenter.Stop();

        CompleteEffect();
    }

    protected override bool CanActivateEffect()
    {
        _preparedSuggestion = null;

        if (!isActiveAndEnabled)
            return false;

        if (_suggestionProvider == null || !_suggestionProvider.isActiveAndEnabled)
            return false;

        if (_presenter == null || !_presenter.isActiveAndEnabled || Presenter.IsPlaying)
            return false;

        return SuggestionProvider.TryGetSuggestion(out _preparedSuggestion);
    }

    protected override void ActivateEffect()
    {
        if (_preparedSuggestion == null)
            throw new InvalidOperationException("Для запуска бонуса не подготовлена подсказка.");

        MoveSuggestion suggestion = _preparedSuggestion;
        _preparedSuggestion = null;

        Presenter.Play(suggestion, HandlePresentationCompleted);
    }

    private void HandlePresentationCompleted()
    {
        CompleteEffect();
    }

    private void HandleDragStarting(ShelfItem item)
    {
        if (Presenter.IsPlaying)
            Presenter.Stop();
    }
}
