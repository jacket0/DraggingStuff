using System;
using UnityEngine;

[Serializable]
public sealed class LocalizedHintText
{
    [SerializeField] private string _ru;
    [SerializeField] private string _en;
    [SerializeField] private string _tr;

    public LocalizedHintText()
    {
    }

    public LocalizedHintText(string ru, string en, string tr)
    {
        _ru = ru;
        _en = en;
        _tr = tr;
    }

    public string Get(string language) => language switch
    {
        "ru" => _ru,
        "tr" => _tr,
        _ => _en
    };
}
