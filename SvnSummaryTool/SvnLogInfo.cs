using Microsoft.VisualBasic.Logging;
using SvnSummaryTool.Model;
using SvnSummaryTool.Utils;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SvnSummaryTool
{
    internal class SvnLogInfo
    {
        /// <summary>
        /// 日志保存目录 <br/>
        /// D:\SVNLog\
        /// </summary>
        public string? LogDir { get; set; }
        /// <summary>
        /// 日志文件名<br/>
        /// repo1.log
        /// </summary>
        public string? LogFileName { get; set; }
        /// <summary>
        /// 日志
        /// </summary>
        public Log? Log { get; set; }
        /// <summary>
        /// svn库信息
        /// </summary>
        public SVNInfo? SvnInfo { get; set; }
        /// <summary>
        /// 起始时间
        /// </summary>
        public DateTime Start { get; set; }
        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        /// 将XML格式的Log文件读取为对象
        /// </summary>
        /// <param name="logDir"></param>
        /// <param name="filePath"></param>
        /// <param name="svnDir"></param>
        /// <returns></returns>
        public static SvnLogInfo Create(string logDir, string filePath, SVNInfo? svnInfo = null)
        {
            try
            {
                var filenameWithDir = Path.GetFileName(filePath);
                var logfileName = Path.ChangeExtension(filePath, "log");
                Log? log = null;

                if (svnInfo == null)
                {
                    var svnInfoXml = File.ReadAllText(Path.Combine(logDir, Path.ChangeExtension(filePath, "svnInfo")));
                    svnInfo = SvnInfoResponse.Create(svnInfoXml)?.Value;
                }

                var logXml = File.ReadAllText($"{logDir}\\{logfileName}");
                log = Log.Create(logXml);
                var end = log.Logentry.Max(lg => lg.Date.Value);
                var start = log.Logentry.Min(lg => lg.Date.Value);

                return new SvnLogInfo 
                {
                    LogDir = logDir,
                    Log = log,
                    Start = start,
                    End = end,
                    SvnInfo = svnInfo,
                    LogFileName = logfileName 
                };
            }
            catch (Exception e)
            {

            }
            return new SvnLogInfo();
        }
    }
}