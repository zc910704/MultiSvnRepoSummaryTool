using NLog;
using System;

namespace SvnSummaryTool
{
    public static class LogHelper
    {
        private static Logger _Logger = null;
        public static void InitLog()
        {
            _Logger = LogManager.GetCurrentClassLogger(); 
        }

        public static void Info(string info) => _Logger.Info(info);

        public static void Debug(string info) => _Logger.Debug(info);

        public static void Error(string msg, Exception e) => _Logger.Error(e, msg);

        public static void Close()
        {

        }
    }
}
