using TMPro;
using UnityEngine;

public sealed class EndlessDebugView : MonoBehaviour
{
    [SerializeField] private EndlessSession _session;
    [SerializeField] private EndlessTimer _timer;
    [SerializeField] private EndlessBoardRefiller _refiller;
    [SerializeField] private TMP_Text _text;

    private void Start()
    {
        if (!Debug.isDebugBuild)
            gameObject.SetActive(false);
    }

    private void Update()
    {
        _text.SetText(
            $"Seed: {_session.Seed}\n" +
            $"Matches: {_session.MatchCount}\n" +
            $"Drain: {_timer.DrainMultiplier:0.00}\n" +
            $"Buffer: {_refiller.HiddenItemCount}\n" +
            $"Generation: {_refiller.LastGenerationMilliseconds:0.0} ms");
    }
}
