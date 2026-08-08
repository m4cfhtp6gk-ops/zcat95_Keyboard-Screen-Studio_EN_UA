using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KeyboardScreen.App.Avalonia.Behaviors;

internal sealed class EdgeFadeBehavior : AvaloniaObject
{
    private const double EdgeTolerance = 0.5;
    private static readonly ConditionalWeakTable<ScrollViewer, FadeState> States = new();

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<EdgeFadeBehavior, ScrollViewer, bool>("IsEnabled");

    static EdgeFadeBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>(OnIsEnabledChanged);
    }

    public static void SetIsEnabled(AvaloniaObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(AvaloniaObject element) =>
        element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(ScrollViewer viewer, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            viewer.ScrollChanged += Viewer_OnChanged;
            viewer.LayoutUpdated += Viewer_OnLayoutUpdated;
            States.GetValue(viewer, static _ => new FadeState());
            Update(viewer);
            return;
        }

        viewer.ScrollChanged -= Viewer_OnChanged;
        viewer.LayoutUpdated -= Viewer_OnLayoutUpdated;
        viewer.OpacityMask = null;
        States.Remove(viewer);
    }

    private static void Viewer_OnChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            Update(viewer);
        }
    }

    private static void Viewer_OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            Update(viewer);
        }
    }

    private static void Update(ScrollViewer viewer)
    {
        double scrollableHeight = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
        FadeMode mode = scrollableHeight <= EdgeTolerance
            ? FadeMode.None
            : (viewer.Offset.Y > EdgeTolerance, viewer.Offset.Y < scrollableHeight - EdgeTolerance) switch
            {
                (true, true) => FadeMode.Both,
                (true, false) => FadeMode.Top,
                (false, true) => FadeMode.Bottom,
                _ => FadeMode.None
            };

        FadeState state = States.GetValue(viewer, static _ => new FadeState());
        if (state.Mode == mode)
        {
            return;
        }

        state.Mode = mode;
        viewer.OpacityMask = mode switch
        {
            FadeMode.Top => FindBrush(viewer, "KssEdgeFadeTopBrush"),
            FadeMode.Bottom => FindBrush(viewer, "KssEdgeFadeBottomBrush"),
            FadeMode.Both => FindBrush(viewer, "KssEdgeFadeBothBrush"),
            _ => null
        };
    }

    private static IBrush? FindBrush(ScrollViewer viewer, string key) =>
        viewer.TryFindResource(key, out object? value) ? value as IBrush : null;

    private enum FadeMode
    {
        Unknown,
        None,
        Top,
        Bottom,
        Both
    }

    private sealed class FadeState
    {
        public FadeMode Mode { get; set; } = FadeMode.Unknown;
    }
}
