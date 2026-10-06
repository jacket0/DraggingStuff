using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ShelfFiltersController : MonoBehaviour
{
    [SerializeField] private LevelSession _session;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private ShelfItemDragController _dragController;
    [SerializeField] private ShelfColumnRaycaster _columnRaycaster;
    [SerializeField] private List<ShelfFilterSignView> _signs = new List<ShelfFilterSignView>();
    [SerializeField] private ShelfFilterAudioPlayer _audio;

    private readonly Dictionary<Shelf, ShelfFilterSignView> _filterSigns = new Dictionary<Shelf, ShelfFilterSignView>();
    private ItemType? _draggedType;

    private void Awake()
    {
        if (_session == null || _shelfBoard == null || _dragController == null || _columnRaycaster == null || _audio == null || _signs.Contains(null))
            throw new InvalidOperationException($"{name}: invalid shelf filters wiring.");
    }

    private void OnEnable()
    {
        _session.SessionStarted += HandleSessionStarted;
        _dragController.DragStarting += HandleDragStarting;
        _dragController.HoveredShelfChanged += HandleHoveredShelfChanged;
        _dragController.DropRejected += HandleDropRejected;
        _dragController.DragEnded += HandleDragEnded;
    }

    private void OnDisable()
    {
        _session.SessionStarted -= HandleSessionStarted;
        _dragController.DragStarting -= HandleDragStarting;
        _dragController.HoveredShelfChanged -= HandleHoveredShelfChanged;
        _dragController.DropRejected -= HandleDropRejected;
        _dragController.DragEnded -= HandleDragEnded;
    }

    private void HandleSessionStarted()
    {
        _filterSigns.Clear();
        ShelfItemCatalog itemCatalog = _session.CurrentLevel.Definition.ItemCatalog;

        foreach (ShelfFilterSignView sign in _signs)
            sign.Hide();

        foreach (Shelf shelf in _shelfBoard.Shelves.Where(_shelfBoard.IsFiltered))
        {
            ShelfFilterSignView[] shelfSigns = _signs.Where(sign => sign.Shelf == shelf).ToArray();

            if (shelfSigns.Length != 1)
                throw new InvalidOperationException($"{name}: filtered shelf {shelf.name} needs exactly one sign, found {shelfSigns.Length}.");

            shelfSigns[0].Show(_shelfBoard.GetAcceptedTypes(shelf).Select(itemCatalog.GetIcon).ToArray());
            _filterSigns.Add(shelf, shelfSigns[0]);
        }
    }

    private void HandleDragStarting(ShelfItem item)
    {
        _draggedType = item.Type;

        foreach (KeyValuePair<Shelf, ShelfFilterSignView> filterSign in _filterSigns)
            filterSign.Value.SetDimmed(!_shelfBoard.Accepts(filterSign.Key, item.Type));
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
            sign.SetDimmed(false);
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
