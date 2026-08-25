public interface IMoveSuggestionProvider
{
    public bool TryGetSuggestion(out MoveSuggestion suggestion);
}