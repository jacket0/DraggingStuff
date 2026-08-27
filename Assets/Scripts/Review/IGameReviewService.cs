public interface IGameReviewService
{
    bool CanRequest { get; }

    bool TryRequest();
}