using System.Collections.Generic;
using UnityEngine;

public sealed class LevelChapterSectionView : MonoBehaviour
{
    [SerializeField] private LevelChapterCoverView _cover;
    [SerializeField] private List<LevelCardView> _cards = new List<LevelCardView>();

    public LevelChapterCoverView Cover => _cover;
    public IReadOnlyList<LevelCardView> Cards => _cards;
    public RectTransform RectTransform => (RectTransform)transform;
}
