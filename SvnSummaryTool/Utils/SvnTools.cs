using Microsoft.VisualBasic;
using SvnSummaryTool.Model;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace SvnSummaryTool.Utils
{
    public static class SvnTools
    {
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
            var logResult = await CommandTools.ExecuteCommandAsync(cmd);
            return !logResult.Contains("E200007") ? true : false;
        }
        /// <summary>
        /// 获取指定url文件内容保存到本地
        /// </summary>
        /// <param name="fileUrl">文件的完整url路径</param>
        /// <param name="saveDir">保存目录</param>
        /// <param name="revision">版本</param>
        /// <returns></returns>
        public static async Task DownloadFile(string fileUrl, string saveDir, int revision)
        {
            string saveFilePath = GetSaveFilePath(fileUrl, saveDir, revision);

            if (!File.Exists(saveFilePath))
            {
                var cmd = $"svn export {fileUrl}@{revision} {saveFilePath}";
                var response = await CommandTools.ExecuteCommandAsync(cmd);
                // 失败：svn: E170000: URL 'http://home/svn/Demo/trunk/Folder1/a.cs' doesn't exist
                // 文件不存在就写空                
                // 成功：A    DiffCache\a.42CCE7919597553FFF8D72E3E629D478.r4.cs Export complete.
                if (!response.Contains("A"))
                {
                    await Task.Delay(100);
                    if (!File.Exists(saveFilePath))
                    {
                        LogHelper.Info($"SvnTools::DownloadFile |failed for {saveFilePath}");
                        using (var ts = File.CreateText(saveFilePath)) { }
                    }
                }
            }
        }

        /// <summary>
        /// 获取保存路径
        /// </summary>
        /// <param name="fileUrl"></param>
        /// <param name="saveDir"></param>
        /// <param name="revision"></param>
        /// <returns></returns>
        private static string GetSaveFilePath(string fileUrl, string saveDir, int revision)
        {
            var fileName = Path.GetFileName(fileUrl);
            var extension = Path.GetExtension(fileUrl);
            // 使用md5作为路径唯一编码
            var md5 = GetStrMd5(fileUrl);
            var saveFileName = Path.ChangeExtension(fileName, $".{md5}.r{revision}{extension}");
            var saveFilePath = Path.Combine(saveDir, saveFileName);
            return saveFilePath;
        }

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        /// <param name="fileUrl"></param>
        /// <param name="saveDir"></param>
        /// <param name="revision"></param>
        /// <returns></returns>
        public static bool CheckFileExistInCache(string fileUrl, string saveDir, int revision)
        {
            string saveFilePath = GetSaveFilePath(fileUrl, saveDir, revision);
            return File.Exists(saveFilePath);
        }

        /// <summary>
        /// 获取svn信息
        /// </summary>
        /// <param name="svnDir"></param>
        /// <returns></returns>
        public static async Task<SvnInfoResponse> GetSvnInfo(string svnDir)
        {
            var cmd = $"svn info --xml {svnDir}";
            var infoXml = await CommandTools.ExecuteCommandAsync(cmd);
            return SvnInfoResponse.Create(infoXml);
        }

        /// <summary>
        /// 获取svn远程仓库相对svn远程根目录的地址 <br/>
        /// e.g. /branches/2.10.0.0
        /// </summary>
        /// <returns></returns>
        public static async Task<string> GetSvnRoot(string svnDir)
        {
            var cmd = $"svn info --xml {svnDir}";
            var info = await CommandTools.ExecuteCommandAsync(cmd);
            XmlDocument infoXML = new XmlDocument();
            infoXML.LoadXml(info);
            var root = infoXML.SelectSingleNode("info/entry/relative-url")!.InnerXml;
            // ^/branches/2.10.0.0 去掉最开始的^开始符号
            var rootUrl = HttpUtility.UrlDecode(root).Substring(1);
            return rootUrl;
        }

        /// <summary>
        /// 计算单个文件单次提交中的修改行数
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="revision"></param>
        /// <param name="localSvnDir"></param>
        public static async Task<LineChange> GetLineDiff(string fileName,SVNInfo svnInfo, int revision, DiffCheckMode diffCheckMode)
        {
            var appendLines = 0;
            var removeLines = 0;
            var localSvnDir = svnInfo.WorkCopyInfo.RootPath;
            // 是否到达变更区域
            var reachLineChange = false;
            //svn diff 命令返回的解释 https://blog.csdn.net/weiwangchao_/article/details/19117191
            string diffBuffer = await CallSvnDiff(fileName, localSvnDir, revision);
            var lines = diffBuffer.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("@"))
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
        public static async Task ShowDiffInVscode(LogFormat logFormat, string saveDir)
        {
            if (logFormat.IsCached) 
            {
                var oldVersion = GetSaveFilePath(logFormat.FileFullUrl, saveDir, logFormat.Revision - 1);
                var newVersion = GetSaveFilePath(logFormat.FileFullUrl, saveDir, logFormat.Revision);

                var cmd = $"Code --diff --wait {oldVersion} {newVersion}";
                await CommandTools.ExecuteCommandAsync(cmd);
            }
            else
            {
                var localfilePath = await ConvertUrlToLocalFilePath(logFormat.SvnInfo.WorkCopyInfo.RootPath, logFormat.FileUrlPath);
                // https://stackoverflow.com/questions/74100260/how-to-compare-files-between-two-svn-revisions-in-vs-code
                // 第二种方式有的有问题
                // 1. svn diff --old CCP6600.cs@5817 --new CCP6600.cs@5818 --diff-cmd "C:\Program Files\Microsoft VS Code\Code.exe" -x "--wait --diff"
                // 2. svn diff -r 5818 CCP6600.cs --diff-cmd  Code -x "--wait --diff"
                var oldVersion = logFormat.Revision > 1 ? logFormat.Revision - 1 : 0;
                var cmd = $"svn diff --old {localfilePath}@{oldVersion} --new {localfilePath}@{logFormat.Revision} --diff-cmd Code -x \"--wait --diff\"";
                await CommandTools.ExecuteCommandAsync(cmd);
            }
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
            var fileDiff = await CommandTools.ExecuteCommandAsync(cmd);
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
            var rootUrl = await GetSvnRoot(localSvnDir);
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

        private static string GetStrMd5(string str)
        { 
            MD5 md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(UTF8Encoding.UTF8.GetBytes(str))).Replace("-", "");
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

    /// <summary>
    /// svn文件版本差异检查模式
    /// </summary>
    public enum DiffCheckMode
    {
        /// <summary>
        /// 本地文件
        /// </summary>
        LocalFile,
        /// <summary>
        /// 远程url
        /// </summary>
        Remote,
        /// <summary>
        /// 本地缓存
        /// </summary>
        Cached
    }
}
