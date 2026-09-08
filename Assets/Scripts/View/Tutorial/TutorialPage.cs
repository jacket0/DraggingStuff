using System;
using UnityEngine;

[Serializable]
public sealed class TutorialPage
{
    [SerializeField] private Sprite _image;

    public Sprite Image => _image;
}
