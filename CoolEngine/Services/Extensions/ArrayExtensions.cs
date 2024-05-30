using OpenTK.Mathematics;

namespace CoolEngine.Services.Extensions;

public static class ArrayExtensions
{
    public static int ClosestPointIndex(this Vector3[] vertices, in Vector3 to)
    {
        var length = float.MaxValue;
        var idx = -1;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            var tmpLength = Vector3.Distance(vertices[i], to);
            if (length > tmpLength)
            {
                length = tmpLength;
                idx = i;
            }
        }

        return idx;
    }
}