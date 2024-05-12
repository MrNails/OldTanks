namespace OldTanks.UI.Services.Interfaces;

public interface IControl : IEqualityComparer<IControl>
{
    string Name { get; set; }
    
    float? Width { get; set; }
    
    bool IsVisible { get; set; }
    
    public void Draw();
}