namespace FlowLauncher.Plugin.AliyunOss.Utils;

/// <summary>
/// 圆环进度弧的几何计算：角度以正上方（12 点方向）为 0°，顺时针增大。
/// 与 WPF 无关，便于单独验证边界（0%、100%、越界值）。
/// </summary>
public static class RingArc
{
    /// <summary>
    /// 整圆扫角上限。360° 的 ArcSegment 起终点重合会退化成一个点而什么都不画，
    /// 因此满分时用略小于 360° 的角度代替，视觉上等价。
    /// </summary>
    public const double FullSweep = 359.999;

    /// <summary>把进度比例（0–1）换算成扫过角度（0–359.999）。</summary>
    public static double SweepFor(double fraction)
    {
        if (double.IsNaN(fraction) || fraction <= 0) return 0;
        if (fraction >= 1) return FullSweep;
        return fraction * 360;
    }

    /// <summary>是否达到整圆（此时应直接画完整圆，避免弧退化）。</summary>
    public static bool IsFullTurn(double fraction) =>
        !double.IsNaN(fraction) && fraction >= 1;

    /// <summary>扫角超过半圆时需要设置 ArcSegment 的 large-arc 标志，否则会走短边。</summary>
    public static bool IsLargeArc(double sweep) => sweep > 180;

    /// <summary>圆的横坐标。<paramref name="clockwiseDegrees"/> 从正上方起算，顺时针为正。</summary>
    public static double PointX(double centerX, double radius, double clockwiseDegrees) =>
        centerX + radius * Math.Sin(clockwiseDegrees * Math.PI / 180);

    /// <summary>圆的纵坐标。</summary>
    public static double PointY(double centerY, double radius, double clockwiseDegrees) =>
        centerY - radius * Math.Cos(clockwiseDegrees * Math.PI / 180);
}
