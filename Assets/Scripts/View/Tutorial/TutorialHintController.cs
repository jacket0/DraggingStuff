using UnityEngine;

public sealed class TutorialHintController : MonoBehaviour
{
    [SerializeField] private GameSession _session;
    [SerializeField] private MoveSuggestionProvider _suggestionProvider;
    [SerializeField] private HintPresenter _presenter;
    [SerializeField] private ShelfItemDragController _dragController;
    [SerializeField] private IdleMovePrompt _idleMovePrompt;
    [SerializeField, Min(1)] private int _guidedMatchCount = 2;
    [SerializeField, Min(0f)] private float _repeatDelay = 1f;

    private int _completedMatchCount;
    private float _remainingDelay;

    private void OnEnable()
    {
        _session.MatchSucceeded += HandleMatchSucceeded;
        _session.StateChanged += HandleStateChanged;
        _dragController.DragStarting += HandleDragStarting;
        _idleMovePrompt.enabled = true;
    }

    private void OnDisable()
    {
        _session.MatchSucceeded -= HandleMatchSucceeded;
        _session.StateChanged -= HandleStateChanged;
        _dragController.DragStarting -= HandleDragStarting;
        _presenter.Stop();
        _idleMovePrompt.enabled = false;
    }

    private void Update()
    {
        if (_completedMatchCount >= _guidedMatchCount || !_session.CanInteract
            || !_session.IsBoardSettled || Time.timeScale <= 0f || _presenter.IsPlaying)
            return;

        _remainingDelay -= Time.deltaTime;

        if (_remainingDelay > 0f)
            return;

        if (_suggestionProvider.TryGetSuggestion(out MoveSuggestion suggestion))
        {
            _idleMovePrompt.enabled = false;
            _presenter.Play(suggestion, HandlePresentationCompleted);
        }
    }

    private void HandlePresentationCompleted()
    {
        _remainingDelay = _repeatDelay;
        _idleMovePrompt.enabled = isActiveAndEnabled;
    }

    private void HandleDragStarting(ShelfItem item)
    {
        _presenter.Stop();
    }

    private void HandleMatchSucceeded(MatchResolution match)
    {
        _completedMatchCount++;
        _presenter.Stop();
        _remainingDelay = 0f;
    }

    private void HandleStateChanged(LevelState state)
    {
        if (state != LevelState.Playing)
            _presenter.Stop();

        _remainingDelay = 0f;
    }
}
