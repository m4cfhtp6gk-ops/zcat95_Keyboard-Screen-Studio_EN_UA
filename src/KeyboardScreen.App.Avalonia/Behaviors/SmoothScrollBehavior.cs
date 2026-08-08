using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace KeyboardScreen.App.Avalonia.Behaviors;

internal sealed class SmoothScrollBehavior : AvaloniaObject
{
    private const double WheelDistance = 64;
    private const double DurationMilliseconds = 190;
    private static readonly ConditionalWeakTable<ScrollViewer, ScrollState> States = new();

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<SmoothScrollBehavior, ScrollViewer, bool>("IsEnabled");

    static SmoothScrollBehavior()
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
            viewer.AddHandler(
                InputElement.PointerWheelChangedEvent,
                Viewer_OnPointerWheelChanged,
                RoutingStrategies.Tunnel);
            viewer.ScrollChanged += Viewer_OnScrollChanged;
            States.GetValue(viewer, static item => new ScrollState(item));
            return;
        }

        viewer.RemoveHandler(InputElement.PointerWheelChangedEvent, Viewer_OnPointerWheelChanged);
        viewer.ScrollChanged -= Viewer_OnScrollChanged;
        if (States.TryGetValue(viewer, out ScrollState? state))
        {
            state.Stop();
            States.Remove(viewer);
        }
    }

    private static void Viewer_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer)
        {
            return;
        }

        double scrollableHeight = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
        if (scrollableHeight <= 0 || Math.Abs(e.Delta.Y) < 0.001)
        {
            return;
        }

        ScrollState state = States.GetValue(viewer, static item => new ScrollState(item));
        double origin = state.IsAnimating ? state.TargetOffset : viewer.Offset.Y;
        double target = Math.Clamp(origin - (e.Delta.Y * WheelDistance), 0, scrollableHeight);
        if (Math.Abs(target - viewer.Offset.Y) < 0.1)
        {
            return;
        }

        state.Start(target);
        e.Handled = true;
    }

    private static void Viewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer viewer &&
            States.TryGetValue(viewer, out ScrollState? state) &&
            !state.IsAnimating)
        {
            state.TargetOffset = viewer.Offset.Y;
        }
    }

    private sealed class ScrollState
    {
        private readonly ScrollViewer _viewer;
        private readonly DispatcherTimer _timer;
        private long _startedAt;
        private double _startOffset;

        public ScrollState(ScrollViewer viewer)
        {
            _viewer = viewer;
            TargetOffset = viewer.Offset.Y;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(8)
            };
            _timer.Tick += Timer_OnTick;
        }

        public bool IsAnimating => _timer.IsEnabled;

        public double TargetOffset { get; set; }

        public void Start(double target)
        {
            _startOffset = _viewer.Offset.Y;
            TargetOffset = target;
            _startedAt = Stopwatch.GetTimestamp();
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        public void Stop() => _timer.Stop();

        private void Timer_OnTick(object? sender, EventArgs e)
        {
            double progress = Math.Clamp(
                Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds / DurationMilliseconds,
                0,
                1);
            double eased = 1 - Math.Pow(1 - progress, 3);
            double current = _startOffset + ((TargetOffset - _startOffset) * eased);
            _viewer.Offset = new Vector(_viewer.Offset.X, current);

            if (progress >= 1)
            {
                _viewer.Offset = new Vector(_viewer.Offset.X, TargetOffset);
                _timer.Stop();
            }
        }
    }
}
