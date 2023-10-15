namespace OldTanks.UI;

public static class GlobalSettings
{
    private static readonly object s_locker = new object();

    private static float s_floatComparisonTolerance;

    public static float FloatComparisonTolerance
    {
        get => s_floatComparisonTolerance;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value),"Tolerance cannot be less than zero");

            lock (s_locker)
            {
                s_floatComparisonTolerance = value;
            }
        }
    }
}