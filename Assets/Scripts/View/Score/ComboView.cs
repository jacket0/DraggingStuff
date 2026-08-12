using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ComboView : MonoBehaviour
{
    [SerializeField] private ComboSystem _comboSystem;
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private Image _timerFill;

    private int _displayedCount = 0;

    private void OnEnable()
    {
        _comboSystem.StateChanged += Refresh;
        Refresh(_comboSystem.ComboState);
    }

    private void OnDisable()
    {
        _comboSystem.StateChanged -= Refresh;
    }

    private void Refresh(ComboState comboState)
    {
        if (_root.activeSelf != comboState.IsActive)
            _root.SetActive(comboState.IsActive);

        if (!comboState.IsActive)
        {
            _displayedCount = 0;
            _timerFill.fillAmount = 0f;
            return;
        }

        if (_displayedCount != comboState.Count)
        {
            _text.SetText("x{0}", comboState.Count);
            _displayedCount = comboState.Count;
        }

        _timerFill.fillAmount = comboState.NormalizedTime;
    }
}
