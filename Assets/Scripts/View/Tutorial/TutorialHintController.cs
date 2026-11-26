using System;
using UnityEngine;

public sealed class TutorialHintController : MonoBehaviour
{
    [SerializeField] private GameSession _session;
    [SerializeField] private MoveSuggestionProvider _suggestionProvider;
    [SerializeField] private HintPresenter _presenter;
    [SerializeField] private ShelfPointerInput _pointerInput;
    [SerializeField] private ShelfItemDragController _dragController;
    [SerializeField] private TutorialStepHint _stepHint;
    [SerializeField, Min(0f)] private float _firstHintDelay = 2f;
    [SerializeField, Min(0f)] private float _repeatDelay = 4f;

    private float _remainingDelay;

    private void Awake()
    {
        if (_session == null)
            throw new InvalidOperationException(nameof(_session));

        if (_suggestionProvider == null)
            throw new InvalidOperationException(nameof(_suggestionProvider));

        if (_presenter == null)
            throw new InvalidOperationException(nameof(_presenter));

        if (_pointerInput == null)
            throw new InvalidOperationException(nameof(_pointerInput));

        if (_dragController == null)
            throw new InvalidOperationException(nameof(_dragController));

        if (_stepHint == null)
            throw new InvalidOperationException(nameof(_stepHint));

        _remainingDelay = _firstHintDelay;
    }

    private void OnEnable()
    {
        _session.StateChanged += HandleStateChanged;
        _pointerInput.Interacted += HandleInteraction;
        _dragController.DragStarting += HandleDragStarting;
        _dragController.DragEnded += HandleInteraction;
    }

    private void OnDisable()
    {
        _session.StateChanged -= HandleStateChanged;
        _pointerInput.Interacted -= HandleInteraction;
        _dragController.DragStarting -= HandleDragStarting;
        _dragController.DragEnded -= HandleInteraction;
        _presenter.Stop();
    }

    private void Update()
    {
        if (!_session.CanInteract || !_session.IsBoardSettled || Time.timeScale <= 0f || _presenter.IsPlaying)
            return;

        _remainingDelay -= Time.deltaTime;

        if (_remainingDelay > 0f)
            return;

        if (!_suggestionProvider.TryGetSuggestion(out MoveSuggestion suggestion))
            return;

        _stepHint.Show(suggestion.IsSwap);
        _presenter.Play(suggestion, HandlePresentationCompleted);
    }

    private void HandlePresentationCompleted()
    {
        _remainingDelay = _repeatDelay;
    }

    private void HandleDragStarting(ShelfItem item)
    {
        HandleInteraction();
    }

    private void HandleInteraction()
    {
        _presenter.Stop();
        _remainingDelay = _firstHintDelay;
    }

    private void HandleStateChanged(LevelState state)
    {
        if (state == LevelState.Playing || state == LevelState.Paused)
            return;

        _presenter.Stop();
        _stepHint.Hide();
    }
}
