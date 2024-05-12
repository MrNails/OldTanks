using System.Collections.ObjectModel;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Texture;

namespace CoolEngine.Services.Interfaces;

public interface IDrawable : ITransformable
{
    Scene Scene { get; }
    
    ObservableCollection<TexturedObjectInfo> TexturedObjectInfos { get; }
    
    bool Visible { get; set; }
    
    string Name { get; set; }
}