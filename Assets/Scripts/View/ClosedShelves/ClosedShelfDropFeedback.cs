using System;
using UnityEngine;

public sealed class ClosedShelfDropFeedback : MonoBehaviour
{
    [SerializeField] private ShelfItemDragController _dragController;
    [SerializeField] private ClosedShelvesController _closedShelves;
    [SerializeField] private ClosedShelfAudioPlayer _audio;
    [SerializeField] private Camera _camera;

    private void Awake()
    {
        if (_dragController == null || _closedShelves == null || _audio == null || _camera == null)
            throw new InvalidOperationException("Invalid closed shelf drop feedback wiring.");
    }

    private void OnEnable() => _dragController.DropRejected += HandleDropRejected;

    private void OnDisable() => _dragController.DropRejected -= HandleDropRejected;

    private void HandleDropRejected(Vector2 screenPosition)
    {
        if (!_closedShelves.TryGetCoverAt(_camera, screenPosition, out ClosedShelfCoverView cover))
            return;

        cover.PlayRejected(_camera.transform.right);
        _audio.PlayReject();
    }
}
