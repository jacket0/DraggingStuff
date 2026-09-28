using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ClosedShelvesController : MonoBehaviour
{
    [SerializeField] private ClosedShelfLevelSession _session;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private TripleBoardRefiller _refiller;
    [SerializeField] private ClosedShelfCoverView _coverPrefab;
    [SerializeField] private TargetShelfMarkerView _markerPrefab;
    [SerializeField] private ConditionStarEffect _starEffect;
    [SerializeField] private ClosedShelfAudioPlayer _audio;
    [SerializeField] private ShelfItemCatalog _itemCatalog;
    [SerializeField] private Camera _camera;
    [SerializeField] private Sprite _shelfIcon;
    [SerializeField] private Color[] _markerColors =
    {
        new Color(0.25f, 0.82f, 1f),
        new Color(0.49f, 1f, 0.42f),
        new Color(1f, 0.37f, 0.64f),
        new Color(0.72f, 0.49f, 1f)
    };
    [SerializeField, Min(0f)] private float _simultaneousRevealDelay = 0.3f;

    private readonly Dictionary<int, ClosedShelfCoverView> _covers = new Dictionary<int, ClosedShelfCoverView>();
    private readonly Dictionary<int, TargetShelfMarkerView> _markers = new Dictionary<int, TargetShelfMarkerView>();
    private readonly Dictionary<int, Color> _markerColorsByShelf = new Dictionary<int, Color>();
    private ClosedShelfProgress _progress;
    private TimedLevelDefinition _definition;
    private float _nextRevealTime;
    private int _startedReveals;

    public event Action<int> ShelfOpened;
    public event Action AllShelvesOpened;
    public event Action<IReadOnlyList<ClosedShelfCoverView>> CoversCreated;

    private void Awake()
    {
        if (_session == null || _shelfBoard == null || _refiller == null || _coverPrefab == null || _markerPrefab == null
            || _starEffect == null || _audio == null || _itemCatalog == null || _camera == null || _shelfIcon == null)
            throw new InvalidOperationException("Invalid closed shelves controller wiring.");

        _session.SessionStarted += HandleSessionStarted;
        _session.StateChanged += HandleStateChanged;
    }

    private void OnDestroy()
    {
        if (_session == null)
            return;

        _session.SessionStarted -= HandleSessionStarted;
        _session.StateChanged -= HandleStateChanged;
        _session.MatchSucceeded -= HandleMatchSucceeded;
    }

    public void PrepareBoard()
    {
        if (_progress == null)
            return;

        IReadOnlyList<Shelf> touched = _refiller.RefillIfNeeded(_progress.IsRefillActive, _progress.PriorityTypes, _definition.EmptyColumnCount);

        foreach (Shelf shelf in touched)
            shelf.View.Refresh();
    }

    public bool TryGetCoverAt(Camera camera, Vector2 screenPosition, out ClosedShelfCoverView cover)
    {
        cover = _covers.Values.FirstOrDefault(candidate => candidate.ContainsScreenPoint(camera, screenPosition));
        return cover != null;
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public void DebugRevealAll()
    {
        foreach (ClosedShelfCoverView cover in _covers.Values.ToArray())
            HandleRevealReady(cover);
    }

    public void DebugStarBurst(int starCount)
    {
        ClosedShelfCoverView cover = _covers.Values.FirstOrDefault();

        if (cover != null)
            _starEffect.DebugBurst(cover, _shelfBoard.Shelves[_shelfBoard.Shelves.Count / 2].transform.position, starCount);
    }
#endif

    private void HandleSessionStarted()
    {
        _definition = _session.CurrentLevel.Definition;

        if (!_definition.HasClosedShelves)
            throw new InvalidOperationException($"{nameof(ClosedShelvesController)} requires a level with closed shelves.");

        _progress = new ClosedShelfProgress(_definition.ClosedShelves);

        foreach (ClosedShelfDefinition definition in _definition.ClosedShelves)
        {
            int shelfIndex = definition.ShelfIndex;
            Shelf shelf = _shelfBoard.Shelves[shelfIndex];

            if (!_shelfBoard.IsShelfClosed(shelf))
                throw new InvalidOperationException($"Shelf {shelfIndex} must already be closed by the level builder.");

            if (_progress.GetCondition(shelfIndex) is ShelfMatchesCondition shelfMatches)
                CreateMarker(shelfIndex, shelfMatches.TargetShelfIndex);

            _covers[shelfIndex] = CreateCover(shelfIndex, shelf);
        }

        IReadOnlyList<ItemType> levelTypes = _definition.ItemGroups.Select(group => group.Type).Distinct().ToArray();
        _refiller.Initialize(_session.CurrentSeed, _definition.RefillSettings, levelTypes);
        _session.MatchSucceeded += HandleMatchSucceeded;
        CoversCreated?.Invoke(_definition.ClosedShelves.Select(definition => _covers[definition.ShelfIndex]).ToArray());
    }

    private ClosedShelfCoverView CreateCover(int shelfIndex, Shelf shelf)
    {
        ClosedShelfCoverView cover = Instantiate(_coverPrefab);
        cover.name = $"ClosedShelfCover_{shelf.name}";
        cover.Initialize(shelfIndex, GetAnchor(shelf), _camera, _progress.GetCondition(shelfIndex).Slots, GetIcon, GetAccent, _audio);
        cover.RevealReady += HandleRevealReady;
        return cover;
    }

    private void CreateMarker(int closedShelfIndex, int targetShelfIndex)
    {
        Color color = _markerColors[_markers.Count % _markerColors.Length];
        Shelf target = _shelfBoard.Shelves[targetShelfIndex];
        TargetShelfMarkerView marker = Instantiate(_markerPrefab);
        marker.name = $"TargetShelfMarker_{target.name}";
        marker.Show(GetAnchor(target), color);
        _markers[closedShelfIndex] = marker;
        _markerColorsByShelf[targetShelfIndex] = color;
    }

    private void HandleMatchSucceeded(MatchResolution match)
    {
        LevelState state = _session.State;

        if (state != LevelState.Playing && state != LevelState.Ending)
            return;

        MatchInfo info = new MatchInfo(IndexOf(match.Shelf), match.Items[0].Type, match.Items.Count);
        IReadOnlyList<ShelfProgressCredit> credits = _progress.RegisterMatch(info);

        foreach (ShelfProgressCredit credit in credits)
        {
            int shelfIndex = credit.ClosedShelfIndex;
            ClosedShelfCoverView cover = _covers[shelfIndex];
            ShelfUnlockCondition condition = _progress.GetCondition(shelfIndex);

            foreach (ConditionCredit conditionCredit in credit.Credits)
                _starEffect.Enqueue(match, cover, conditionCredit, condition.Slots[conditionCredit.SlotIndex].Unit);

            if (_markers.TryGetValue(shelfIndex, out TargetShelfMarkerView marker))
            {
                if (condition.IsCompleted)
                {
                    marker.Hide();
                    _markers.Remove(shelfIndex);
                }
                else
                {
                    marker.Bounce();
                }
            }

            if (state == LevelState.Playing && _progress.GetState(shelfIndex) == ClosedShelfState.Revealing)
                cover.MarkCompleted();
        }
    }

    private void HandleRevealReady(ClosedShelfCoverView cover)
    {
        if (_session.State != LevelState.Playing && _session.State != LevelState.Paused)
            return;

        int shelfIndex = cover.ShelfIndex;
        float delay = Mathf.Max(0f, _nextRevealTime - Time.time);
        _nextRevealTime = Time.time + delay + _simultaneousRevealDelay;
        _startedReveals++;
        bool isFinal = _startedReveals == _progress.ClosedShelfIndexes.Count;

        cover.PlayReveal(delay, isFinal, () => Reveal(shelfIndex), () => Open(shelfIndex));
    }

    private Shelf Reveal(int shelfIndex)
    {
        Shelf shelf = _shelfBoard.Shelves[shelfIndex];
        ClosedShelfDefinition definition = _definition.ClosedShelves.First(candidate => candidate.ShelfIndex == shelfIndex);

        _refiller.RevealShelf(shelf, definition.RevealedGroupCount);
        shelf.View.Refresh();
        return shelf;
    }

    private void Open(int shelfIndex)
    {
        _shelfBoard.OpenShelf(_shelfBoard.Shelves[shelfIndex]);
        _progress.MarkOpen(shelfIndex);
        _covers.Remove(shelfIndex);

        ShelfOpened?.Invoke(shelfIndex);

        if (!_shelfBoard.HasClosedShelves)
            AllShelvesOpened?.Invoke();
    }

    private void HandleStateChanged(LevelState state)
    {
        if (state != LevelState.Ending && state != LevelState.Won && state != LevelState.Lost)
            return;

        foreach (ClosedShelfCoverView cover in _covers.Values)
            cover.StopReveal();
    }

    private Sprite GetIcon(ConditionSlot slot)
    {
        return slot.Kind switch
        {
            ConditionSlotKind.Item => _itemCatalog.Entries.First(entry => entry.Type == slot.ItemType.Value).Icon,
            ConditionSlotKind.Shelf => _shelfIcon,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot.Kind, "Unknown condition slot kind.")
        };
    }

    private Color? GetAccent(ConditionSlot slot)
    {
        if (slot.Kind == ConditionSlotKind.Shelf && _markerColorsByShelf.TryGetValue(slot.TargetShelfIndex.Value, out Color color))
            return color;

        return null;
    }

    private static ClosedShelfCoverAnchor GetAnchor(Shelf shelf)
    {
        if (!shelf.TryGetComponent(out ClosedShelfCoverAnchor anchor))
            throw new MissingComponentException($"{shelf.name}: {nameof(ClosedShelfCoverAnchor)}");

        return anchor;
    }

    private int IndexOf(Shelf shelf)
    {
        IReadOnlyList<Shelf> shelves = _shelfBoard.Shelves;

        for (int index = 0; index < shelves.Count; index++)
        {
            if (shelves[index] == shelf)
                return index;
        }

        throw new ArgumentException("The shelf does not belong to this board.", nameof(shelf));
    }
}
