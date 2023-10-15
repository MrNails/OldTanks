namespace OldTanks.UI.Services.Interfaces;

public interface IControlContainer : IControl
{
    IControl? Child { get; set; }
}