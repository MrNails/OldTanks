using CoolEngine.Services.Misc;

namespace CoolEngine.Services.Interfaces;

public interface IModelLoader
{
    Task<LoaderData> LoadAsync(string path);
}