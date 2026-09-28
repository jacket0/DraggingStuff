using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ConditionStarEffect : MonoBehaviour
{
    private static readonly float[] LateralOffsets = { 0.28f, -0.22f, 0.10f };

    [SerializeField] private MoveResolutionPlayer _resolutionPlayer;
    [SerializeField] private ConditionStarView _starPrefab;
    [SerializeField] private Camera _camera;
    [SerializeField, Min(0f)] private float _starInterval = 0.09f;

    private readonly Dictionary<MatchResolution, List<StarRequest>> _pending = new Dictionary<MatchResolution, List<StarRequest>>();
    private readonly Stack<ConditionStarView> _pool = new Stack<ConditionStarView>();

    private void Awake()
    {
        if (_resolutionPlayer == null || _starPrefab == null || _camera == null)
            throw new InvalidOperationException("Invalid condition star effect wiring.");
    }

    private void OnEnable() => _resolutionPlayer.Exploded += HandleExploded;

    private void OnDisable() => _resolutionPlayer.Exploded -= HandleExploded;

    public void Enqueue(MatchResolution match, ClosedShelfCoverView cover, ConditionCredit credit, ConditionUnit unit)
    {
        if (credit.StarCount <= 0)
            return;

        if (!_pending.TryGetValue(match, out List<StarRequest> requests))
        {
            requests = new List<StarRequest>();
            _pending.Add(match, requests);
        }

        cover.ExpectStars(credit.StarCount);
        requests.Add(new StarRequest(cover, credit, unit));
    }

    private void HandleExploded(MatchResolution match, Vector3 position)
    {
        if (!_pending.TryGetValue(match, out List<StarRequest> requests))
            return;

        _pending.Remove(match);
        int launchIndex = 0;

        foreach (StarRequest request in requests)
        {
            ClosedShelfCoverView cover = request.Cover;
            int slotIndex = request.Credit.SlotIndex;
            int starCount = request.Credit.StarCount;

            for (int star = 0; star < starCount; star++)
            {
                int amount = request.Unit == ConditionUnit.Items ? 1 : star == starCount - 1 ? request.Credit.Amount : 0;
                float lateral = LateralOffsets[launchIndex % LateralOffsets.Length];

                GetStar().Fly(
                    position,
                    () => cover != null ? cover.GetSlotTarget(slotIndex) : position,
                    lateral,
                    launchIndex * _starInterval,
                    _camera,
                    () =>
                    {
                        if (cover != null)
                            cover.ReceiveStar(slotIndex, amount);
                    },
                    _pool.Push);

                launchIndex++;
            }
        }
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public void DebugBurst(ClosedShelfCoverView cover, Vector3 position, int starCount)
    {
        cover.ExpectStars(starCount);

        for (int star = 0; star < starCount; star++)
        {
            GetStar().Fly(
                position,
                () => cover != null ? cover.GetSlotTarget(0) : position,
                LateralOffsets[star % LateralOffsets.Length],
                star * _starInterval,
                _camera,
                () =>
                {
                    if (cover != null)
                        cover.ReceiveStar(0, 0);
                },
                _pool.Push);
        }
    }
#endif

    private ConditionStarView GetStar()
    {
        return _pool.Count > 0 ? _pool.Pop() : Instantiate(_starPrefab, transform);
    }

    private readonly struct StarRequest
    {
        public ClosedShelfCoverView Cover { get; }
        public ConditionCredit Credit { get; }
        public ConditionUnit Unit { get; }

        public StarRequest(ClosedShelfCoverView cover, ConditionCredit credit, ConditionUnit unit)
        {
            Cover = cover;
            Credit = credit;
            Unit = unit;
        }
    }
}
