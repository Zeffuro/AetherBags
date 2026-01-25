using System.Diagnostics;
using Dalamud.Plugin.Services;

namespace AetherBags.Extensions;

public static class LoggerExtensions
{
    extension(IPluginLog logger)
    {
        [Conditional("DEBUG")]
        public void DebugOnly(string message)
        {
            if (System.Config?.General?.DebugEnabled == true)
            {
                logger.Debug(message);
            }
        }

        [Conditional("DEBUG")]
        public void DebugOnly(string message, params object[] args)
        {
            if (System.Config?.General?.DebugEnabled == true)
            {
                logger.Debug(message, args);
            }
        }
    }
}