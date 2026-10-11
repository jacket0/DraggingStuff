using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class KeyLocksController : MonoBehaviour
{
    [SerializeField] private LevelSession _session;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private List<SafeDoorView> _doors = new List<SafeDoorView>();
    [SerializeField] private List<KeyLockMark> _marks = new List<KeyLockMark>();

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

        if (_doors.Contains(null) || _doors.Select(door => door.Shelf).Distinct().Count() != _doors.Count)
            throw new InvalidOperationException($"{name}: every safe door must belong to its own shelf.");

        if (_marks.Count < TimedLevelLayoutRules.MaximumKeyLockedShelfCount || _marks.Any(mark => mark == null || mark.ShapePrefab == null))
            throw new InvalidOperationException($"{name}: {TimedLevelLayoutRules.MaximumKeyLockedShelfCount} lock marks with shapes are required.");
    }

    private void OnEnable()
    {
        _session.SessionStarted += HandleSessionStarted;
        _session.ShelfResolved += HandleShelfResolved;
    }

    private void OnDisable()
    {
        _session.SessionStarted -= HandleSessionStarted;
        _session.ShelfResolved -= HandleShelfResolved;
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

            _session.RequestBoardChange(new[] { shelf, key.LockedShelf }, completed => PlayCollection(shelf, column, completed));
        }
    }

    private void PlayCollection(Shelf shelf, ShelfColumn column, Action completed)
    {
        if (column.FrontKey.View == null)
            throw new InvalidOperationException($"{column.FrontKey.name}: the key has no view.");

        ShelfKey key = column.TakeFrontKey();
        Shelf safe = key.LockedShelf;
        _pendingKeys.Remove(key);
        _shelfBoard.OpenKeyLockedShelf(safe);
        SafeDoorView door = GetDoor(safe);
        _collectionCount++;
        int pendingAnimationCount = 2;

        void CompleteAnimation()
        {
            pendingAnimationCount--;

            if (pendingAnimationCount > 0)
                return;

            key.Delete();
            _collectionCount--;
            SafeOpened?.Invoke(safe);
            completed();
        }

        key.View.PlayCollection(door.LockPoint, () => door.PlayOpen(CompleteAnimation));
        shelf.View.Advance(CompleteAnimation);
    }

    private SafeDoorView GetDoor(Shelf safe)
    {
        SafeDoorView[] doors = _doors.Where(door => door.Shelf == safe).ToArray();

        if (doors.Length != 1)
            throw new InvalidOperationException($"{safe.name}: a key-locked shelf needs exactly one safe door.");

        return doors[0];
    }
}
