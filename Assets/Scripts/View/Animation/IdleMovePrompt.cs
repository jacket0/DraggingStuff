using DG.Tweening;
using UnityEngine;

public sealed class IdleMovePrompt : MonoBehaviour
{
    [SerializeField] private GameSession _levelSession;
    [SerializeField] private MoveSuggestionProvider _suggestionProvider;
    [SerializeField] private ShelfPointerInput _pointerInput;
    [SerializeField] private ShelfItemDragController _dragController;
    [SerializeField, Min(0f)] private float _minimumIdleDuration = 3f;
    [SerializeField, Min(0f)] private float _maximumIdleDuration = 5f;
    [SerializeField, Min(0.01f)] private float _promptDuration = 0.5f;
    [SerializeField, Min(0f)] private float _rotationStrength = 7f;

    private ShelfItem _promptedItem;
    private ShelfItemOutlineView _outlineView;
    private Quaternion _initialRotation;
    private Tween _promptTween;
    private float _elapsedTime;
    private float _idleDuration;
    private Vector3 _lastPointerPosition;
    private bool _wasInteractable;

    private void Awake()
    {
        _lastPointerPosition = Input.mousePosition;
        ResetTimer();
    }

    private void OnEnable()
    {
        _levelSession.StateChanged += HandleStateChanged;
        _pointerInput.Interacted += HandleInteraction;
        _dragController.InteractionOccurred += HandleInteraction;
        _dragController.DragStarting += HandleDragStarting;
    }

    private void OnDisable()
    {
        _levelSession.StateChanged -= HandleStateChanged;
        _pointerInput.Interacted -= HandleInteraction;
        _dragController.InteractionOccurred -= HandleInteraction;
        _dragController.DragStarting -= HandleDragStarting;
        StopPrompt();
    }

    private void Update()
    {
        bool canInteract = _levelSession.CanInteract && Time.timeScale > 0f;
        bool hasInput = Input.anyKey || Input.touchCount > 0 || Input.mousePosition != _lastPointerPosition;
        _lastPointerPosition = Input.mousePosition;

        if (hasInput || canInteract != _wasInteractable)
            StopPrompt();

        _wasInteractable = canInteract;

        if (!canInteract || hasInput || _promptedItem != null)
            return;

        _elapsedTime += Time.unscaledDeltaTime;

        if (_elapsedTime >= _idleDuration)
            ShowPrompt();
    }

    private void ShowPrompt()
    {
        if (!_suggestionProvider.TryGetSuggestion(out MoveSuggestion suggestion) || suggestion.SourceSlot.IsEmpty)
        {
            ResetTimer();
            return;
        }

        _promptedItem = suggestion.SourceSlot.Item;
        _outlineView = ShelfItemOutlineView.GetRequired(_promptedItem);
        _initialRotation = _promptedItem.transform.localRotation;
        _outlineView.Show();

        _promptTween = _promptedItem.transform
            .DOPunchRotation(new Vector3(0f, 0f, _rotationStrength), _promptDuration, 6, 0.6f)
            .SetUpdate(true)
            .OnComplete(CompletePrompt);
    }

    private void CompletePrompt()
    {
        if (_promptedItem != null)
            _promptedItem.transform.localRotation = _initialRotation;

        _outlineView?.Hide();
        _promptedItem = null;
        _outlineView = null;
        _promptTween = null;
        ResetTimer();
    }

    private void StopPrompt()
    {
        _promptTween?.Kill();
        _promptTween = null;

        if (_promptedItem != null)
            _promptedItem.transform.localRotation = _initialRotation;

        _outlineView?.Hide();
        _promptedItem = null;
        _outlineView = null;
        ResetTimer();
    }

    private void ResetTimer()
    {
        _elapsedTime = 0f;
        _idleDuration = Random.Range(_minimumIdleDuration, Mathf.Max(_minimumIdleDuration, _maximumIdleDuration));
    }

    private void HandleInteraction()
    {
        StopPrompt();
    }

    private void HandleDragStarting(ShelfItem item)
    {
        StopPrompt();
    }

    private void HandleStateChanged(LevelState state)
    {
        StopPrompt();
    }
}
