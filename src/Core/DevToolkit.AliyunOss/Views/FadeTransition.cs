using System.Windows;
using System.Windows.Media.Animation;

namespace DevToolkit.AliyunOss.Views;

/// <summary>
/// 元素的淡入淡出显隐过渡。
/// <see cref="Visibility"/> 不能参与动画，因此隐藏要等淡出播完再折叠；
/// 淡出途中若又要求显示，靠代次号让那次折叠失效，否则元素会被卡在隐藏态。
/// </summary>
internal static class FadeTransition
{
    /// <summary>默认时长。太短显得生硬，太长会跟不上鼠标移动。</summary>
    public static readonly Duration Default = new(TimeSpan.FromMilliseconds(170));

    private static readonly DependencyProperty GenerationProperty =
        DependencyProperty.RegisterAttached(
            "Generation", typeof(int), typeof(FadeTransition), new PropertyMetadata(0));

    /// <summary>淡入并参与命中测试。</summary>
    public static void Show(UIElement element, Duration? duration = null)
    {
        var generation = Bump(element);

        element.Visibility = Visibility.Visible;
        element.IsHitTestVisible = true;
        element.BeginAnimation(UIElement.OpacityProperty, Ease(1, EasingMode.EaseOut, duration));

        _ = generation;
    }

    /// <summary>淡出，播完后折叠并退出命中测试（避免点击到正在消失的按钮）。</summary>
    public static void Hide(UIElement element, Duration? duration = null)
    {
        if (element.Visibility != Visibility.Visible)
        {
            element.Opacity = 0;
            element.Visibility = Visibility.Collapsed;
            return;
        }

        // 立刻停止响应点击：淡出期间按钮已不可用，误点会触发取消这类破坏性操作
        element.IsHitTestVisible = false;

        var generation = Bump(element);

        var animation = Ease(0, EasingMode.EaseIn, duration);
        animation.Completed += (_, _) =>
        {
            // 淡出期间又被要求显示时代次已变，此时折叠会把元素卡住
            if ((int)element.GetValue(GenerationProperty) != generation) return;

            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 0;
            element.Visibility = Visibility.Collapsed;
        };

        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    /// <summary>
    /// 只改透明度、不动 <see cref="Visibility"/>，适合始终占位（如铺在底层的遮罩）的元素。
    /// </summary>
    public static void To(UIElement element, double target, Duration? duration = null)
    {
        element.Visibility = Visibility.Visible;
        element.BeginAnimation(UIElement.OpacityProperty, Ease(target, EasingMode.EaseInOut, duration));
    }

    private static int Bump(UIElement element)
    {
        var generation = (int)element.GetValue(GenerationProperty) + 1;
        element.SetValue(GenerationProperty, generation);
        return generation;
    }

    private static DoubleAnimation Ease(double target, EasingMode mode, Duration? duration) =>
        new(target, duration ?? Default)
        {
            EasingFunction = new CubicEase { EasingMode = mode },
            FillBehavior = FillBehavior.HoldEnd,
        };
}
