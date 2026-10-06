using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelChapter", menuName = "Game/Level/Level Chapter")]
public sealed class LevelChapter : ScriptableObject
{
    [SerializeField] private LocalizedHintText _title = new LocalizedHintText();
    [SerializeField] private LocalizedHintText _description = new LocalizedHintText();
    [SerializeField] private LevelMechanic _mechanic;
    [SerializeField] private Sprite _mechanicIcon;
    [SerializeField] private List<LevelEntry> _levels = new List<LevelEntry>();

    public LocalizedHintText Title => _title;
    public LocalizedHintText Description => _description;
    public LevelMechanic Mechanic => _mechanic;
    public Sprite MechanicIcon => _mechanicIcon;
    public IReadOnlyList<LevelEntry> Levels => _levels;
}
