using UnityEngine;
using YG;

public static class YandexGameProgressSynchronizer
{
    private static bool _isInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _isInitialized = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        YG2.onGetSDKData += Synchronize;

        if (YG2.isSDKEnabled)
            Synchronize();
    }

    private static void Synchronize()
    {
        GameProgressRepository.Synchronize(YG2.saves.GameProgress);
    }
}
