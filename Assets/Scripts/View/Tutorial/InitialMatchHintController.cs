using UnityEngine;

public sealed class InitialMatchHintController : MonoBehaviour
{
    [SerializeField] private LevelSession _session;
    [SerializeField] private MoveSuggestionProvider _suggestionProvider;
    [SerializeField] private HintPresenter _presenter;
    [SerializeField] private ShelfItemDragController _dragController;
    [SerializeField] private IdleMovePrompt _idleMovePrompt;
    [SerializeField, Min(1)] private int _levelNumber = 5;

    private bool _isEligible;
    private bool _wasShown;

    private void OnEnable()
    {
        _session.SessionStarted += HandleSessionStarted;
        _session.StateChanged += HandleStateChanged;
        _dragController.DragStarting += HandleDragStarting;
    }

    private void OnDisable()
    {
        _session.SessionStarted -= HandleSessionStarted;
        _session.StateChanged -= HandleStateChanged;
        _dragController.DragStarting -= HandleDragStarting;
        _idleMovePrompt.enabled = true;
    }

    private void Update()
    {
        if (!_isEligible || _wasShown || !_session.CanInteract || !_session.IsBoardSettled || Time.timeScale <= 0f || _presenter.IsPlaying)
            return;

        if (!_suggestionProvider.TryGetSuggestion(out MoveSuggestion suggestion))
            return;

        if (!suggestion.TargetColumn.IsEmpty || suggestion.TargetMatchingItems.Count != suggestion.TargetColumn.Shelf.Capacity - 1)
            return;

        _wasShown = true;
        _presenter.Play(suggestion, HandlePresentationCompleted);
    }

    private void HandleSessionStarted()
    {
        _isEligible = _session.CurrentLevel.Number == _levelNumber;
        _wasShown = false;

        if (_isEligible)
            _idleMovePrompt.enabled = false;
    }

    private void HandlePresentationCompleted()
    {
        _isEligible = false;
        _idleMovePrompt.enabled = isActiveAndEnabled;
    }

    private void HandleDragStarting(ShelfItem item)
    {
        if (!_isEligible)
            return;

        _wasShown = true;
        _isEligible = false;
        _presenter.Stop();
        _idleMovePrompt.enabled = isActiveAndEnabled;
    }

    private void HandleStateChanged(LevelState state)
    {
        if (state == LevelState.Playing || !_isEligible)
            return;

        _wasShown = true;
        _isEligible = false;
        _presenter.Stop();
        _idleMovePrompt.enabled = isActiveAndEnabled;
    }
}
