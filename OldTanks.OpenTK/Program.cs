using OldTanks.Windows;

namespace OldTanks;

public static class Program
{
    public static int Main(string[] args)
    {
        using var mainWindow = new MainWindow("Test");

        mainWindow.Run();
        
        return 0;
    }
}
