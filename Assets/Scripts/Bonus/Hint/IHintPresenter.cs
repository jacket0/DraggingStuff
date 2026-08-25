using System;

public interface IHintPresenter
{
    public bool IsPlaying { get; }

    public void Play(MoveSuggestion suggestion, Action completed);
    public void Stop();
}