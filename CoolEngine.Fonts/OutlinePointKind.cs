namespace CoolEngine.Fonts;

/// <summary>The role of an <see cref="OutlinePoint"/> in its contour, as FreeType's point tags.</summary>
public enum OutlinePointKind : byte
{
    /// <summary>A point on the curve.</summary>
    OnCurve,

    /// <summary>The control point of a quadratic Bézier arc.</summary>
    Quadratic,

    /// <summary>One of the two control points of a cubic Bézier arc.</summary>
    Cubic,
}
