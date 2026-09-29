using FlowLauncher.Plugin.AliyunOss.Utils;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FlowLauncher.Plugin.AliyunOss.Views.Controls;

/// <summary>
/// 圆环进度条：以正上方为起点顺时针绘制弧线，用于气泡窗口的圆形边框。
/// 不确定进度时改为一段旋转的短弧。
/// </summary>
public class RingProgress : Control
{
    private const double IndeterminateSweep = 90;

    private readonly RotateTransform _spin = new();

    public RingProgress()
    {
        // 不确定态靠整体旋转表现；轨道是整圆，旋转它不可见，只有弧线在动
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = _spin;
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(RingProgress),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>进度百分比（0–100）。</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(RingProgress),
            new FrameworkPropertyMetadata(
                6d,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(
            nameof(TrackBrush),
            typeof(Brush),
            typeof(RingProgress),
            new FrameworkPropertyMetadata(
                new SolidColorBrush(Color.FromRgb(0xE8, 0xEC, 0xF0)),
                FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>轨道（未完成部分）画刷。</summary>
    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public static readonly DependencyProperty IsIndeterminateProperty =
        DependencyProperty.Register(
            nameof(IsIndeterminate),
            typeof(bool),
            typeof(RingProgress),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnIsIndeterminateChanged));

    /// <summary>长度未知时置为 true，环上显示循环旋转的短弧。</summary>
    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    private static void OnIsIndeterminateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((RingProgress)d).UpdateSpin();

    private void UpdateSpin()
    {
        if (IsIndeterminate)
        {
            var animation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.1))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            };
            _spin.BeginAnimation(RotateTransform.AngleProperty, animation);
            return;
        }

        _spin.BeginAnimation(RotateTransform.AngleProperty, null);
        _spin.Angle = 0;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var side = double.IsNaN(constraint.Width) || double.IsInfinity(constraint.Width)
            ? 88
            : constraint.Width;
        return new Size(side, side);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var thickness = Math.Max(1, StrokeThickness);
        // 描边以路径为中心向两侧扩展，半径内缩半个线宽才不会在边缘被裁掉
        var radius = (Math.Min(ActualWidth, ActualHeight) - thickness) / 2;
        if (radius <= 0) return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);

        if (TrackBrush is { } track)
        {
            dc.DrawEllipse(null, new Pen(track, thickness), center, radius, radius);
        }

        var fraction = Value / 100;
        var sweep = IsIndeterminate ? IndeterminateSweep : RingArc.SweepFor(fraction);
        if (sweep <= 0) return;

        var pen = new Pen(Foreground, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };

        if (!IsIndeterminate && RingArc.IsFullTurn(fraction))
        {
            // 整圆用 Ellipse 直接画：360° 的 ArcSegment 起终点重合会退化成一个点
            dc.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        var start = new Point(RingArc.PointX(center.X, radius, 0), RingArc.PointY(center.Y, radius, 0));
        var end = new Point(
            RingArc.PointX(center.X, radius, sweep),
            RingArc.PointY(center.Y, radius, sweep));

        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment(
            end,
            new Size(radius, radius),
            0,
            RingArc.IsLargeArc(sweep),
            SweepDirection.Clockwise,
            true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        dc.DrawGeometry(null, pen, geometry);
    }
}
