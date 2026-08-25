using System;

public class ModifierOwner : IDisposable
{
    private Action _releaseAction;

    public ModifierOwner(Action releaseAction)
    {
        _releaseAction = releaseAction ?? throw new ArgumentNullException(nameof(releaseAction));
    }

    public void Dispose()
    {
        if (_releaseAction == null)
            return;

        _releaseAction.Invoke();
        _releaseAction = null;
    }
}