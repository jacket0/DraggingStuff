using System;

public interface IInterstitialAdService
{
    public void Show(Action completed);
}