using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class KeyLocksController : MonoBehaviour
{
    [SerializeField] private LevelSession _session;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private ShelfItemDragController _dragController;
    [SerializeField] private List<SafeDoorView> _doors = new List<SafeDoorView>();
    [SerializeField] private List<KeyLockMark> _marks = new List<KeyLockMark>();
    [SerializeField] private KeyLockAudioPlayer _audio;

    private readonly HashSet<ShelfKey> _pendingKeys = new HashSet<ShelfKey>();
    private readonly Dictionary<Shelf, KeyLockMark> _safeMarks = new Dictionary<Shelf, KeyLockMark>();
    private int _collectionCount;

    public bool IsCollecting => _collectionCount > 0;

    public event Action<Shelf> SafeOpened;

    private void Awake()
    {
        if (_session == null)
            throw new InvalidOperationException(nameof(_session));

        if (_shelfBoard == null)
            throw new InvalidOperationException(nameof(_shelfBoard));

        if (_dragController == null)
            throw new InvalidOperationException(nameof(_dragController));

        if (_audio == null)
            throw new InvalidOperationException(nameof(_audio));

        if (_doors.Contains(null) || _doors.Select(door => door.Shelf).Distinct().Count() != _doors.Count)
            throw new InvalidOperationException($"{name}: every safe door must belong to its own shelf.");

        if (_marks.Count < TimedLevelLayoutRules.MaximumKeyLockedShelfCount || _marks.Any(mark => mark == null || mark.ShapePrefab == null))
            throw new InvalidOperationException($"{name}: {TimedLevelLayoutRules.MaximumKeyLockedShelfCount} lock marks with shapes are required.");
    }

    private void OnEnable()
    {
        _session.SessionStarted += HandleSessionStarted;
        _session.ShelfResolved += HandleShelfResolved;
        _dragController.HoveredShelfChanged += HandleHoveredShelfChanged;
    }

    private void OnDisable()
    {
        _session.SessionStarted -= HandleSessionStarted;
        _session.ShelfResolved -= HandleShelfResolved;
        _dragController.HoveredShelfChanged -= HandleHoveredShelfChanged;
    }

    private void HandleSessionStarted()
    {
        _pendingKeys.Clear();
        _safeMarks.Clear();
        IReadOnlyList<int> safeIndices = _session.CurrentLevel.Definition.KeyLockedShelfIndices;

        for (int markIndex = 0; markIndex < safeIndices.Count; markIndex++)
        {
            Shelf safe = _shelfBoard.Shelves[safeIndices[markIndex]];
            _safeMarks.Add(safe, _marks[markIndex]);
            GetDoor(safe).ShowClosed(_marks[markIndex]);
        }

        foreach (SafeDoorView door in _doors.Where(door => !_safeMarks.ContainsKey(door.Shelf)))
            door.HideForLevel();

        foreach (ShelfKey key in _shelfBoard.Shelves.SelectMany(shelf => shelf.Columns).SelectMany(column => column.Keys))
            _safeMarks[key.LockedShelf].Attach(key.MarkPoint);
    }

    private void HandleShelfResolved(Shelf shelf)
    {
        foreach (ShelfColumn column in shelf.Columns)
        {
            ShelfKey key = column.FrontKey;

            if (key == null || !_pendingKeys.Add(key))
                continue;

            _session.RequestBoardChange(new[] { shelf }, completed => PlayCollection(shelf, column, completed));
        }
    }

    private void HandleHoveredShelfChanged(Shelf shelf)
    {
        if (shelf != null && _safeMarks.ContainsKey(shelf) && _shelfBoard.IsKeyLocked(shelf))
            GetDoor(shelf).PlayShudder();
    }

    private void PlayCollection(Shelf shelf, ShelfColumn column, Action keyShelfCompleted)
    {
        if (column.FrontKey.View == null)
            throw new InvalidOperationException($"{column.FrontKey.name}: the key has no view.");

        ShelfKey key = column.TakeFrontKey();
        Shelf safe = key.LockedShelf;
        _pendingKeys.Remove(key);
        SafeDoorView door = GetDoor(safe);
        _collectionCount++;
        bool isDoorOpened = false;
        Action safeCompleted = null;

        void TryFinishSafe()
        {
            if (!isDoorOpened || safeCompleted == null)
                return;

            key.Delete();
            _collectionCount--;
            SafeOpened?.Invoke(safe);
            safeCompleted();
        }

        _session.RequestBoardChange(new[] { safe }, completed =>
        {
            _shelfBoard.OpenKeyLockedShelf(safe);
            safeCompleted = completed;
            TryFinishSafe();
        });

        _audio.PlayKeyPickup();
        key.View.PlayCollection(door.LockPoint, () => door.PlayOpen(() =>
        {
            isDoorOpened = true;
            TryFinishSafe();
        }));
        shelf.View.Advance(keyShelfCompleted);
    }

    private SafeDoorView GetDoor(Shelf safe)
    {
        SafeDoorView[] doors = _doors.Where(door => door.Shelf == safe).ToArray();

        if (doors.Length != 1)
            throw new InvalidOperationException($"{safe.name}: a key-locked shelf needs exactly one safe door.");

        return doors[0];
    }
}
