using System.Collections.ObjectModel;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services.Interfaces;

namespace CoolEngine.Services;

public sealed class Level
{
    public Level()
    {
        Textures = new ObservableCollection<Texture>();
        Drawables = new ObservableCollection<IDrawable>();
        Collisions = new ObservableCollection<Collision>();
        Shaders = new ObservableCollection<Shader>();
    }
    
    public ObservableCollection<IDrawable> Drawables { get; }
    public ObservableCollection<Texture> Textures { get; }
    public ObservableCollection<Collision> Collisions { get; }
    public ObservableCollection<Shader> Shaders { get; }

    public string Name { get; set; }
    
    //Triggers, Cinematics e.t.c. in future
}