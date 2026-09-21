using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class TimedLevelVariant
{
    [SerializeField] private int _seed;
    [SerializeField, Min(1)] private int _generatorVersion = TimedLevelLayoutRules.GeneratorVersion;
    [SerializeField, Min(1)] private int _moveCount = 1;
    [SerializeField] private string _layoutHash;
    [SerializeField] private List<TimedLevelShelfLayout> _shelves = new List<TimedLevelShelfLayout>();
    [SerializeField] private List<TimedLevelMoveData> _solutionMoves = new List<TimedLevelMoveData>();

    public int Seed => _seed;
    public int GeneratorVersion => _generatorVersion;
    public int MoveCount => _moveCount;
    public string LayoutHash => _layoutHash;
    public bool HasLayout => _shelves != null && _shelves.Count > 0;
    public bool HasSolution => _solutionMoves != null && _solutionMoves.Count == _moveCount;

    public TimedLevelVariant(int seed, int generatorVersion, int moveCount, string layoutHash)
        : this(seed, generatorVersion, moveCount, layoutHash, null, null)
    {
    }

    public TimedLevelVariant(
        int seed,
        int generatorVersion,
        int moveCount,
        string layoutHash,
        BoardStateSnapshot layout,
        IReadOnlyList<TimedLevelMove> solutionMoves)
    {
        if (generatorVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(generatorVersion));

        if (moveCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(moveCount));

        if (string.IsNullOrWhiteSpace(layoutHash))
            throw new ArgumentException("A layout hash is required.", nameof(layoutHash));

        _seed = seed;
        _generatorVersion = generatorVersion;
        _moveCount = moveCount;
        _layoutHash = layoutHash;

        if (layout != null)
        {
            foreach (ShelfStateSnapshot shelf in layout.Shelves)
                _shelves.Add(new TimedLevelShelfLayout(shelf.Columns));
        }

        if (solutionMoves != null)
        {
            foreach (TimedLevelMove move in solutionMoves)
                _solutionMoves.Add(new TimedLevelMoveData(move));
        }
    }

    public BoardStateSnapshot CreateLayout()
    {
        if (!HasLayout)
            throw new InvalidOperationException($"Seed {_seed}: layout is missing.");

        ShelfStateSnapshot[] shelves = _shelves
            .Select(shelf => new ShelfStateSnapshot(
                shelf.Columns.Select(column => new ColumnStateSnapshot(column.Items.ToArray())).ToArray()))
            .ToArray();
        return new BoardStateSnapshot(shelves);
    }

    public IReadOnlyList<TimedLevelMove> CreateSolution()
    {
        if (!HasSolution)
            throw new InvalidOperationException($"Seed {_seed}: solution is missing.");

        return Array.AsReadOnly(_solutionMoves.Select(move => move.CreateMove()).ToArray());
    }
}
