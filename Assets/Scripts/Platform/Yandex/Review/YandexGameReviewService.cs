using UnityEngine;
using YG;

public sealed class YandexGameReviewService : MonoBehaviour, IGameReviewService
{
    public bool CanRequest => YG2.reviewCanShow;

    public bool TryRequest()
    {
        if (!CanRequest)
            return false;

        YG2.ReviewShow();
        return true;
    }
}