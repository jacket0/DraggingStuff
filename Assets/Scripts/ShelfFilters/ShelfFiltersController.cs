using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public sealed class ShelfFiltersController : MonoBehaviour
{
    [SerializeField] private LevelSession _session;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private ShelfItemDragController _dragController;
    [SerializeField] private ShelfColumnRaycaster _columnRaycaster;
    [SerializeField] private List<ShelfFilterSignView> _signs = new List<ShelfFilterSignView>();
    [SerializeField] private ShelfFilterAudioPlayer _audio;
    [SerializeField, Min(0)] private int _itemShakeLimit = 3;
    [SerializeField, Min(0f)] private float _itemShakeDistance = 0.035f;
    [SerializeField, Min(0.01f)] private float _itemShakeDuration = 0.4f;
    [SerializeField, Min(1)] private int _itemShakeVibrato = 20;

    private readonly Dictionary<Shelf, ShelfFilterSignView> _filterSigns = new Dictionary<Shelf, ShelfFilterSignView>();
    private readonly HashSet<Shelf> _stuckShelves = new HashSet<Shelf>();
    private readonly Dictionary<ShelfItem, Tween> _itemShakes = new Dictionary<ShelfItem, Tween>();
    private ItemType? _draggedType;
    private int _itemShakeCount;

    public event Action<IReadOnlyList<ShelfFilterSignView>> SignsShown;

    private void Awake()
    {
        if (_session == null || _shelfBoard == null || _dragController == null || _columnRaycaster == null || _audio == null || _signs.Contains(null))
            throw new InvalidOperationException($"{name}: invalid shelf filters wiring.");
    }

    private void OnEnable()
    {
        _session.SessionStarted += HandleSessionStarted;
        _session.BoardSettled += HandleBoardSettled;
        _dragController.DragStarting += HandleDragStarting;
        _dragController.HoveredShelfChanged += HandleHoveredShelfChanged;
        _dragController.DropRejected += HandleDropRejected;
        _dragController.DragEnded += HandleDragEnded;
    }

    private void OnDisable()
    {
        _session.SessionStarted -= HandleSessionStarted;
        _session.BoardSettled -= HandleBoardSettled;
        _dragController.DragStarting -= HandleDragStarting;
        _dragController.HoveredShelfChanged -= HandleHoveredShelfChanged;
        _dragController.DropRejected -= HandleDropRejected;
        _dragController.DragEnded -= HandleDragEnded;
        StopItemShakes();
    }

    private void HandleSessionStarted()
    {
        _filterSigns.Clear();
        _stuckShelves.Clear();
        _itemShakeCount = 0;
        StopItemShakes();
        ShelfItemCatalog itemCatalog = _session.CurrentLevel.Definition.ItemCatalog;

        foreach (ShelfFilterSignView sign in _signs)
            sign.Hide();

        foreach (Shelf shelf in _shelfBoard.Shelves.Where(_shelfBoard.IsFiltered))
        {
            ShelfFilterSignView[] shelfSigns = _signs.Where(sign => sign.Shelf == shelf).ToArray();

            if (shelfSigns.Length != 1)
                throw new InvalidOperationException($"{name}: filtered shelf {shelf.name} needs exactly one sign, found {shelfSigns.Length}.");

            shelfSigns[0].Show(_shelfBoard.GetAcceptedTypes(shelf).Select(itemCatalog.GetSignIcon).ToArray());
            _filterSigns.Add(shelf, shelfSigns[0]);
        }

        SignsShown?.Invoke(_filterSigns.Values.ToArray());
    }

    private void HandleBoardSettled()
    {
        if (!_session.IsPlaying || _filterSigns.Count == 0)
            return;

        Shelf[] stuckShelves = _shelfBoard.Shelves.Where(shelf => shelf.HasMatch() && !_shelfBoard.CanMatch(shelf)).ToArray();

        foreach (Shelf shelf in stuckShelves.Where(shelf => !_stuckShelves.Contains(shelf)))
            PlayBoxReminder(shelf);

        _stuckShelves.Clear();
        _stuckShelves.UnionWith(stuckShelves);
    }

    private void PlayBoxReminder(Shelf shelf)
    {
        ItemType type = shelf.Columns[0].FrontItem.Type;

        foreach (KeyValuePair<Shelf, ShelfFilterSignView> filterSign in _filterSigns.Where(filterSign => _shelfBoard.GetAcceptedTypes(filterSign.Key).Contains(type)))
            filterSign.Value.PlayPulse();

        _audio.PlayBoxReminder();

        if (_itemShakeCount >= _itemShakeLimit)
            return;

        _itemShakeCount++;

        foreach (ShelfColumn column in shelf.Columns)
            ShakeItem(column.FrontItem);
    }

    private void ShakeItem(ShelfItem item)
    {
        StopItemShake(item);
        Transform itemTransform = item.transform;
        Vector3 position = itemTransform.localPosition;
        Vector3 offset = itemTransform.parent != null ? itemTransform.parent.InverseTransformVector(Vector3.right * _itemShakeDistance) : Vector3.right * _itemShakeDistance;
        _itemShakes[item] = itemTransform
            .DOPunchPosition(offset, _itemShakeDuration, _itemShakeVibrato, 1f)
            .SetLink(item.gameObject)
            .OnKill(() =>
            {
                if (item != null)
                    itemTransform.localPosition = position;

                _itemShakes.Remove(item);
            });
    }

    private void StopItemShake(ShelfItem item)
    {
        if (_itemShakes.TryGetValue(item, out Tween shake))
            shake.Kill();
    }

    private void StopItemShakes()
    {
        foreach (ShelfItem item in _itemShakes.Keys.ToArray())
            StopItemShake(item);
    }

    private void HandleDragStarting(ShelfItem item)
    {
        _draggedType = item.Type;
        StopItemShake(item);

        foreach (KeyValuePair<Shelf, ShelfFilterSignView> filterSign in _filterSigns)
        {
            filterSign.Value.SetDimmed(!_shelfBoard.Accepts(filterSign.Key, item.Type));
            filterSign.Value.SetHighlighted(_shelfBoard.GetAcceptedTypes(filterSign.Key).Contains(item.Type));
        }
    }

    private void HandleHoveredShelfChanged(Shelf shelf)
    {
        if (TryGetRefusingSign(shelf, out ShelfFilterSignView sign))
            sign.PlayRefusalHint();
    }

    private void HandleDropRejected(Vector2 screenPosition)
    {
        if (!_columnRaycaster.TryGetShelf(screenPosition, out Shelf shelf) || !TryGetRefusingSign(shelf, out ShelfFilterSignView sign))
            return;

        sign.PlayRejected();
        _audio.PlayReject();
    }

    private void HandleDragEnded()
    {
        foreach (ShelfFilterSignView sign in _filterSigns.Values)
        {
            sign.SetDimmed(false);
            sign.SetHighlighted(false);
        }
    }

    private bool TryGetRefusingSign(Shelf shelf, out ShelfFilterSignView sign)
    {
        sign = null;
        return shelf != null
            && _draggedType.HasValue
            && _filterSigns.TryGetValue(shelf, out sign)
            && !_shelfBoard.Accepts(shelf, _draggedType.Value);
    }
}
