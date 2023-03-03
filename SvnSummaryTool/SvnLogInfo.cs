using Microsoft.VisualBasic.Logging;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace SvnSummaryTool
{
    internal class SvnLogInfo
    {
        /// <summary>
        /// 日志保存目录 <br/>
        /// D:\SVNLog\
        /// </summary>
        public string LogDir { get; set; }
        /// <summary>
        /// 日志文件名<br/>
        /// repo1.log
        /// </summary>
        public string LogFileName { get; set; }
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
        /// <param name="svnDir"></param>
        /// <returns></returns>
        public static SvnLogInfo Create(string logDir, string fileName, string svnDir)
        {
            try
            {
                // aaa.bbb.text
                var pureName = fileName;
                var dotIndex = pureName.LastIndexOf('.');
                // 不包含文件类型.后的文件名
                var name = pureName.Substring(0, dotIndex);
                var svnDirFilePath = System.IO.Path.Combine(logDir, $"{name}.path");
                var logfileName = $"{name}.log";

                if (string.IsNullOrEmpty(svnDir))
                {
                    using (StreamReader sr = new StreamReader(svnDirFilePath))
                    {
                        svnDir = sr.ReadToEnd();
                    }
                }

                XmlSerializer serializer = new XmlSerializer(typeof(Log));
                using (TextReader reader = new StringReader(File.ReadAllText($"{logDir}\\{logfileName}")))
                {
                    var log = (Log)serializer.Deserialize(reader);
                    var end = log.Logentry.Max(lg => lg.Date.Value);
                    var start = log.Logentry.Min(lg => lg.Date.Value);
                    return new SvnLogInfo { LogDir = logDir, Log = log, Start = start, End = end, SvnDir = svnDir, LogFileName = logfileName };
                }
            }
            catch (Exception e)
            {

            }
            return null;
        }
    }
}