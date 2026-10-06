using System.Collections.Generic;
using UnityEngine;

public sealed class LevelChapterSectionView : MonoBehaviour
{
    [SerializeField] private LevelChapterHeaderView _header;
    [SerializeField] private List<LevelCardView> _cards = new List<LevelCardView>();
    [SerializeField] private GameObject _openFrame;
    [SerializeField] private GameObject _lockedFrame;

    public LevelChapterHeaderView Header => _header;
    public IReadOnlyList<LevelCardView> Cards => _cards;
    public RectTransform RectTransform => (RectTransform)transform;

    public void Bind(LevelChapterHeaderState state)
    {
        _header.Bind(state);
        _openFrame.SetActive(state.IsUnlocked);
        _lockedFrame.SetActive(!state.IsUnlocked);
    }
}
