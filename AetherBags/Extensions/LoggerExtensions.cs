namespace AetherBags.Extensions;

public static class LoggerExtensions
{
    public static void DebugOnly(this object logger, string message)
    {
        if(System.Config.General.DebugEnabled) Services.Logger.Debug(message);
    }

    public static void DebugOnly(this object logger, string message, params object[] args)
    {
        if(System.Config.General.DebugEnabled) Services.Logger.Debug(message);
    }
}