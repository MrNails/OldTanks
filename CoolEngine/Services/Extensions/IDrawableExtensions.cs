using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Services.Interfaces;

namespace CoolEngine.Services.Extensions;

public static class IDrawableExtensions
{
    public static IEnumerable<Texture> GetUniqueTexturesFromDrawable(this IDrawable drawable)
    {
        return drawable.TexturedObjectInfos
            .SelectMany(d => d.TexturedMeshes.Select(tm => tm.Value.Texture))
            .Distinct();
    }
}