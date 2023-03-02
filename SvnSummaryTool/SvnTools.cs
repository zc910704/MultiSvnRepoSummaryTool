using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace SvnSummaryTool
{
    public static class SvnTools
    {
        /// <summary>
        /// 映射svn本地仓库地址的路径到相对repo的路径
        /// e.g. /branches/2.10.0.0
        /// </summary>
        private static ConcurrentDictionary<string , string> _svnDirToUrlDictCache = new ConcurrentDictionary<string, string> ();
        /// <summary>
        /// 下载Log文件
        /// </summary>
        /// <param name="logPath">文件保存路径</param>
        /// <param name="fileName">文件名</param>
        /// <param name="dirPath">svn本地check路径</param>
        /// <param name="from">开始时间</param>
        /// <param name="to">结束时间</param>
        /// <returns>日志是否下载成功</returns>
        public static async Task<bool> DownloadLogFile(string logPath, string fileName, string dirPath, DateTime from, DateTime to)
        {
            var startTime = from.ToString("yyyy-MM-dd");
            var endTime = to.AddDays(1).ToString("yyyy-MM-dd");
            var cmd = $"svn log -v --xml -r {{\"{startTime}\"}}:{{\"{endTime}\"}} {dirPath} > {logPath}\\{fileName}";
            var logResult = await Util.ExecuteCommandAsync(cmd);
            return !logResult.Contains("E200007") ? true : false;
        }

        /// <summary>
        /// 获取svn远程仓库相对svn远程根目录的地址 <br/>
        /// e.g. /branches/2.10.0.0
        /// </summary>
        /// <returns></returns>
        public static async Task<string> GetSvnRoot(string svnDir)
        {
            if (_svnDirToUrlDictCache.ContainsKey(svnDir))
            {
                return _svnDirToUrlDictCache[svnDir];
            }
            else
            {
                var cmd = $"svn info --xml {svnDir}";
                var info = await Util.ExecuteCommandAsync(cmd);
                XmlDocument infoXML = new XmlDocument();
                infoXML.LoadXml(info);
                var root = infoXML.SelectSingleNode("info/entry/relative-url")!.InnerXml;
                // ^/branches/2.10.0.0 去掉最开始的^开始符号
                var rootUrl = HttpUtility.UrlDecode(root).Substring(1);
                _svnDirToUrlDictCache[svnDir] = rootUrl;
                return rootUrl;
            }
        }

        /// <summary>
        /// 计算单个文件单次提交中的修改行数
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="revision"></param>
        /// <param name="localSvnDir"></param>
        public static async Task<LineChange> GetLineDiff(string fileName, string localSvnDir, int revision)
        {
            var appendLines = 0;
            var removeLines = 0;
            // 是否到达变更区域
            var reachLineChange = false;
            //svn diff 命令返回的解释 https://blog.csdn.net/weiwangchao_/article/details/19117191
            string diffBuffer = await SvnTools.CallSvnDiff(fileName, localSvnDir, revision);
            var lines = diffBuffer.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();
            foreach (var line in lines)
            {
                if(line.Trim().StartsWith("@"))
                {
                    reachLineChange = true;
                }
                if (reachLineChange)
                {
                    if (line.StartsWith("+"))
                    {
                        appendLines++;
                    }
                    if (line.StartsWith("-"))
                    {
                        removeLines++;
                    }
                }               
            }
            return new LineChange(appendLines, removeLines);
        }

        /// <summary>
        /// 在vscode中比较版本差别
        /// </summary>
        /// <returns></returns>
        public static async Task ShowDiffInVscode(LogFormat logFormat)
        {
            var localfilePath = await ConvertUrlToLocalFilePath(logFormat.LocalSvnDir, logFormat.fileName);
            // https://stackoverflow.com/questions/74100260/how-to-compare-files-between-two-svn-revisions-in-vs-code
            // 两种都可以
            // 1. svn diff --old CCP6600.cs@5817 --new CCP6600.cs@5818 --diff-cmd "C:\Program Files\Microsoft VS Code\Code.exe" - x "--wait --diff"
            // 2. svn diff -r 5818 CCP6600.cs --diff-cmd  Code -x "--wait --diff"
            var cmd = $"svn diff -r {logFormat.version} {localfilePath} --diff-cmd Code -x \"--wait --diff\"";
            await Util.ExecuteCommandAsync(cmd);
        }


        /// <summary>
        ///  调用svn diff操作，返回diff结果
        /// </summary>
        /// <param name="urlFileName">svn的log返回的修改文件的url路径 <br/>
        /// e.g. /branches/2.10.0.0/Code/Demo.cs</param>
        /// <param name="localSvnDir">本地check的svn目录位置</param>
        /// <param name="revision">版本号</param>
        /// <returns></returns>
        private static async Task<string> CallSvnDiff(string urlFileName, string localSvnDir, int revision)
        {

            var localfilePath = await ConvertUrlToLocalFilePath(localSvnDir, urlFileName);
            var cmd = $"svn diff --old {localfilePath}@{revision - 1} --new {localfilePath}@{revision}";
            var fileDiff = await Util.ExecuteCommandAsync(cmd);
            return fileDiff;
        }

        /// <summary>
        /// 将svn路径转为本地路径
        /// </summary>
        /// <returns></returns>
        private static async Task<string> ConvertUrlToLocalFilePath(string localSvnDir, string urlFileName)
        {
            localSvnDir = localSvnDir.Trim().Replace("\r\n", "");
            // 获取svn库check的相对根地址，用来替换文件目录 e.g. /branches/2.10.0.0
            var rootUrl = await SvnTools.GetSvnRoot(localSvnDir);
            // /Code/Demo.cs
            var localfileName = urlFileName.Substring(urlFileName.IndexOf(rootUrl) + rootUrl.Length);
            if (!localfileName.StartsWith("/"))
            {
                localfileName = "/" + localfileName;
            }
            // 替换"/" 为系统路径分隔符 windows下为"\"
            localfileName = localfileName.Replace('/', System.IO.Path.DirectorySeparatorChar);

            return $"{localSvnDir}{localfileName}";
        }
    }

    /// <summary>
    /// svn变更
    /// </summary>
    public struct LineChange
    {
        /// <summary>
        /// 新增行
        /// </summary>
        public int AppendLine { get; set; }
        /// <summary>
        /// 删除行
        /// </summary>
        public int RemoveLine { get; set; }
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="append"></param>
        /// <param name="remove"></param>
        public LineChange(int append, int remove)
        {
            AppendLine = append;
            RemoveLine = remove;
        }
    }
}
