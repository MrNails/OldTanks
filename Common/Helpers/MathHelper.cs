namespace Common.Helpers;

public static class MathHelper
{
    public static bool QuadraticEquation(float a, float b, float c, out float x1, out float x2)
    {
        x1 = float.NaN;
        x2 = float.NaN;
        var discr = b * b - 4 * a * c;

        if (discr < 0)
            return false;
        
        if (discr == 0)
        {
            x1 = x2 = -0.5f * b / a;
            return true;
        }

        var q = (float)(b > 0
            ? -0.5f * (b + Math.Sqrt(discr))
            : -0.5f * (b - Math.Sqrt(discr)));

        x1 = q / a;
        x2 = c / q;

        if (x1 > x2)
            (x1, x2) = (x2, x1);

        return true;
    }
}