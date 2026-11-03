using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class ItemAudioPlayer : MonoBehaviour
{
    private const int VoiceCount = 4;

    [SerializeField] private GameSession _session;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private ShelfItemDragController _dragController;
    [SerializeField] private ShelfColumnRaycaster _columnRaycaster;
    [SerializeField] private List<AudioClip> _pickUpClips = new List<AudioClip>();
    [SerializeField] private List<AudioClip> _placeClips = new List<AudioClip>();
    [SerializeField] private AudioClip _swapDoubleMatchClip;
    [SerializeField] private AudioClip _rejectClip;
    [SerializeField, Range(0f, 1f)] private float _pickUpVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _placeVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _swapDoubleMatchVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float _rejectVolume = 0.35f;
    [SerializeField, Range(0f, 0.2f)] private float _pitchVariation = 0.05f;
    [SerializeField, Min(0f)] private float _placeDelay = 0.12f;

    private AudioSource[] _voices;
    private int _nextVoice;
    private ShelfColumn _dragSourceColumn;

    private void Awake()
    {
        if (_session == null || _shelfBoard == null || _dragController == null || _columnRaycaster == null
            || _pickUpClips.Count == 0 || _pickUpClips.Contains(null) || _placeClips.Count == 0 || _placeClips.Contains(null)
            || _swapDoubleMatchClip == null || _rejectClip == null)
        {
            throw new InvalidOperationException($"{name}: invalid item audio wiring.");
        }

        AudioSource template = GetComponent<AudioSource>();
        _voices = new AudioSource[VoiceCount];
        _voices[0] = template;

        for (int index = 1; index < VoiceCount; index++)
        {
            AudioSource voice = gameObject.AddComponent<AudioSource>();
            voice.outputAudioMixerGroup = template.outputAudioMixerGroup;
            voice.playOnAwake = false;
            voice.spatialBlend = template.spatialBlend;
            _voices[index] = voice;
        }
    }

    private void OnEnable()
    {
        _dragController.DragStarting += HandleDragStarting;
        _dragController.DropRejected += HandleDropRejected;
        _session.MoveStarted += HandleMoveStarted;
    }

    private void OnDisable()
    {
        _dragController.DragStarting -= HandleDragStarting;
        _dragController.DropRejected -= HandleDropRejected;
        _session.MoveStarted -= HandleMoveStarted;
    }

    private void HandleDragStarting(ShelfItem item)
    {
        _dragSourceColumn = item.Column;
        Play(PickRandom(_pickUpClips), _pickUpVolume, 0f);
    }

    private void HandleMoveStarted(MoveOutcome outcome)
    {
        int matchingShelfCount = outcome.AffectedShelves.Distinct().Count(shelf => shelf.HasMatch());

        if (matchingShelfCount == 0)
            Play(PickRandom(_placeClips), _placeVolume, _placeDelay);
        else if (outcome.IsSwap && matchingShelfCount >= 2)
            Play(_swapDoubleMatchClip, _swapDoubleMatchVolume, _placeDelay);
    }

    private void HandleDropRejected(Vector2 pointerPosition)
    {
        if (!_columnRaycaster.TryGetShelf(pointerPosition, out Shelf shelf) || _shelfBoard.IsShelfClosed(shelf) || _shelfBoard.IsFiltered(shelf))
            return;

        if (_columnRaycaster.TryGetColumnUnderPointer(pointerPosition, out ShelfColumnView column)
            && (column.Column == _dragSourceColumn || _shelfBoard.IsSticky(column.Column)))
        {
            return;
        }

        Play(_rejectClip, _rejectVolume, 0f);
    }

    private static AudioClip PickRandom(List<AudioClip> clips) => clips[UnityEngine.Random.Range(0, clips.Count)];

    private void Play(AudioClip clip, float volume, float delay)
    {
        AudioSource voice = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _voices.Length;
        voice.Stop();
        voice.clip = clip;
        voice.volume = volume;
        voice.pitch = 1f + UnityEngine.Random.Range(-_pitchVariation, _pitchVariation);
        voice.PlayDelayed(delay);
    }
}
