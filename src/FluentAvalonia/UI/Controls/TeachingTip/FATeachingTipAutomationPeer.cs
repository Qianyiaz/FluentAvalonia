using System.Runtime.CompilerServices;
using Avalonia.Automation.Peers;

namespace FluentAvalonia.UI.Controls;

/// <summary>
///     AutomationPeer for a <see cref="TeachingTip" />
/// </summary>
public class FATeachingTipAutomationPeer : ContentControlAutomationPeer
{
    internal FATeachingTipAutomationPeer(FATeachingTip owner)
        : base(owner)
    {
    }

    private FATeachingTip TeachingTip => Unsafe.As<FATeachingTip>(Owner);

    public static bool Maximizable => false;

    public static bool Minimizable => false;

    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        if (TeachingTip.IsLightDismissEnabled) return AutomationControlType.Window;

        return AutomationControlType.Pane;
    }

    protected override string GetClassNameCore() => nameof(TeachingTip);

    // public WindowInteractionState InteractionState();

    public bool IsModal() => TeachingTip.IsLightDismissEnabled;

    public bool IsTopmost() => TeachingTip.IsOpen;

    // public WindowVisualState VisualState();

    public void Close() => TeachingTip.IsOpen = false;

    // public void SetVisualState(WindowVisualState state);

    public static bool WaitForInputIdle(int milliseconds) => true;

    public static void RaiseWindowClosedEvent()
    {
        // Don't have automation events
    }

    public static void RaiseWindowOpenedEvent()
    {
        // Don't have automation events
    }
}