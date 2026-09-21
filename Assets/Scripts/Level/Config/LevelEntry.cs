using UnityEngine;

[CreateAssetMenu(fileName = "LevelEntry", menuName = "Game/Level/Level Entry")]
public class LevelEntry : ScriptableObject
{
    [SerializeField, Min(1)] private int _number = 1;
    [SerializeField] private string _sceneName;
    [SerializeField] private TimedLevelDefinition _definition;
    [SerializeField] private bool _available;

    public int Number => _number;
    public string SceneName => _sceneName;
    public TimedLevelDefinition Definition => _definition;
    public bool CanBeStarted => _available && _definition != null && !string.IsNullOrWhiteSpace(_sceneName);
}
