using Avalonia.Controls;

namespace FluentAvalonia.UI.Controls;

/// <summary>
///     Specifies factory methods a <see cref="FAFrame" /> uses to resolve pages.
///     Pages are created exclusively via this factory; reflection-based creation is not supported.
/// </summary>
public interface IFANavigationPageFactory
{
    /// <summary>
    ///     Returns a page instance for the given <see cref="Type" />.
    ///     Must return a non-null instance; returning null will fail the navigation.
    /// </summary>
    Control GetPage(Type srcType);
}