using System;
using Serilog;

namespace SvnSummaryTool
{
    public static class LogHelper
    {
        public static void InitLog()
        {
            Log.Logger = new LoggerConfiguration()
# if DEBUG
                .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
#endif
                .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        public static void Info(string info) => Serilog.Log.Information(info);

        public static void Debug(string info) => Serilog.Log.Debug(info);

        public static void Error(string msg, Exception e) => Serilog.Log.Error(msg, e);

        public static void Close()
        {
            Serilog.Log.CloseAndFlush();
        }
    }
}
