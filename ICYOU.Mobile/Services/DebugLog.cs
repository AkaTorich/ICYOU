namespace ICYOU.Mobile.Services;

public static class DebugLog
{
    public static void Write(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
        Console.WriteLine(message);
    }
}
