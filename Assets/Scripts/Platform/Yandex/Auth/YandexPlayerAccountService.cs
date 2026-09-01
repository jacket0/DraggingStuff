using System;
using YG;

public enum PlayerAccountState
{
    Guest,
    AuthorizedNamed,
    AuthorizedAnonymous
}

public sealed class YandexPlayerAccountService
{
    public event Action StateChanged;

    public PlayerAccountState State
    {
        get
        {
            if (!YG2.player.auth)
                return PlayerAccountState.Guest;

            return string.IsNullOrWhiteSpace(YG2.player.name) || YG2.player.name == InfoYG.ANONYMOUS
                ? PlayerAccountState.AuthorizedAnonymous
                : PlayerAccountState.AuthorizedNamed;
        }
    }

    public string PlayerName => State == PlayerAccountState.AuthorizedNamed ? YG2.player.name : string.Empty;

    public void Enable()
    {
        YG2.onGetSDKData += HandlePlayerDataChanged;
    }

    public void Disable()
    {
        YG2.onGetSDKData -= HandlePlayerDataChanged;
    }

    public bool TryRequestAuthorization()
    {
        if (State != PlayerAccountState.Guest || !YG2.isSDKEnabled)
            return false;

        YG2.OpenAuthDialog();
        return true;
    }

    private void HandlePlayerDataChanged()
    {
        StateChanged?.Invoke();
    }
}
