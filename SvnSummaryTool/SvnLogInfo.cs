using Microsoft.VisualBasic.Logging;
using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace SvnSummaryTool
{
    internal class SvnLogInfo
    {
        /// <summary>
        /// 日志路径
        /// </summary>
        public string LogPath { get; set; }
        /// <summary>
        /// 本地check的SVN文件夹地址
        /// </summary>
        public string SvnDir { get; set; }
        /// <summary>
        /// 日志
        /// </summary>
        public Log Log { get; set; }
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
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static SvnLogInfo Create(string logDir, string fileName, string svnDir)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Log));
                using (TextReader reader = new StringReader(File.ReadAllText($"{logDir}\\{fileName}")))
                {
                    var log = (Log)serializer.Deserialize(reader);
                    var end = log.Logentry.Max(lg => lg.Date.Value);
                    var start = log.Logentry.Min(lg => lg.Date.Value);
                    return new SvnLogInfo { LogPath = logDir, Log = log, Start = start, End = end,SvnDir = svnDir };
                }
            }
            catch (Exception)
            {

            }
            return null;
        }
    }
}