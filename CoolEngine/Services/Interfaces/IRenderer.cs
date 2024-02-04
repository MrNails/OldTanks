using System.Collections.ObjectModel;
using CoolEngine.GraphicalEngine.Core;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Interfaces;

public interface IRenderer<T>
{
    bool IsActive { get; set; }
    
    ObservableCollection<T>? DrawableItems { get; set; }
    
    void Render(Camera camera, ref Matrix4 projection);
}