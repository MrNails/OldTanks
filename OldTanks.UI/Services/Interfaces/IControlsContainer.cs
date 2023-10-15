namespace OldTanks.UI.Services.Interfaces;

public interface IControlsContainer : IControl
{
    ControlCollection Children { get; }
}