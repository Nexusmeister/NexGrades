namespace NexGrades.App.Tests.TestSupport;

/// <summary>
/// Tests that go through <c>PageNavigator.NavigateWithHierarchy</c> or a view model's <c>OnNavigatedToAsync</c> share
/// the static <c>NavigationParameters</c> hand-over, which is keyed by view model type. xUnit runs test classes in
/// parallel, so classes that use the same view model types run one at a time in this collection.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NavigationParametersState
{
    public const string Name = "NavigationParameters";
}
