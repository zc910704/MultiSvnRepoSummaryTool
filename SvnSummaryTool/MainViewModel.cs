using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.Logging;

namespace SvnSummaryTool
{
    internal partial class MainViewModel : ObservableObject
    {
        private static string[] _ignoreFile = Util.GetConfig("IgnoreFile").Split(';').Where(s => !string.IsNullOrEmpty(s)).ToArray();
        private static string[] _ignoreFolder = Util.GetConfig("IgnoreFolder").Split(';').Where(s => !string.IsNullOrEmpty(s)).ToArray();

        /// <summary>
        /// 选择的项目路径集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _ProjectsPath = new ObservableCollection<string>();
        /// <summary>
        /// 当前选择的项目路径
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveSvnDirCommand))]
        private string _SelectedProjectPath = string.Empty;
        /// <summary>
        /// 能否删除项目路径
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveSvnDirCommand))]
        private bool _CanRemoveSvnDirEnabled = false;
        /// <summary>
        /// 已经拉取的日志列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<SvnLogInfo> _ProjectSvnLogInfo = new ObservableCollection<SvnLogInfo>();
        /// <summary>
        /// 当前选择的svn日志信息
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveLogCommand))]
        private SvnLogInfo _SelectedSvnLogInfo = null;
        /// <summary>
        /// 当前SVN所有提交的作者
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<CanCheckedItem<string>> _Authors = new ObservableCollection<CanCheckedItem<string>>();
        /// <summary>
        /// 日志拉取的起始时间
        /// </summary>
        [ObservableProperty]
        private DateTime _StartTime = DateTime.Now;
        /// <summary>
        /// 日志拉取的结束时间
        /// </summary>
        [ObservableProperty]
        private DateTime _EndTime = DateTime.Now;
        /// <summary>
        /// 当前选择用户的新增行数
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ModifyLineCount))]
        private int _AppendLineCount;
        /// <summary>
        /// 当前选择用户的删除行数
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ModifyLineCount))]
        private int _DeleteLineCount;
        /// <summary>
        /// 当前修改总行数
        /// </summary>
        public int ModifyLineCount => AppendLineCount + DeleteLineCount;
        /// <summary>
        /// 当前未筛选的日志
        /// </summary>
        private List<LogFormat> _LogFormats = new List<LogFormat>();

        public MainViewModel()
        {

        }

        [RelayCommand]
        private void AuthorCheckedChanged()
        {
            var selectedAuthors = _Authors
                .Where(a => a.IsChecked)
                .Select(a => a.Item).ToList();
            var selectedUserLog = _LogFormats
                .Where(log => selectedAuthors.Contains(log.author));

            AppendLineCount = selectedUserLog
                .Select(s => s.appendLines)
                .Aggregate((l1, l2) => l1 + l2);

            DeleteLineCount = selectedUserLog
                .Select(s => s.removeLines)
                .Aggregate((l1, l2) => l1 + l2);
        }


        /// <summary>
        /// 添加已经下载好的svn日志记录
        /// </summary>
        [RelayCommand]
        private void AddSvnDir()
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = "请选择需要统计svn日志xml";
            folderBrowserDialog.RootFolder = Environment.SpecialFolder.Desktop;
            folderBrowserDialog.ShowNewFolderButton = false;
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                if (!ProjectsPath.Contains(folderBrowserDialog.SelectedPath))
                {
                    ProjectsPath.Add(folderBrowserDialog.SelectedPath);
                    SelectedProjectPath = folderBrowserDialog.SelectedPath;
                }
                else
                {

                }
            }            
        }

        [RelayCommand(CanExecute = nameof(CanRemoveSvnDir))]        
        private void RemoveSvnDir() 
        {
            ProjectsPath.Remove(SelectedProjectPath);
        }

        private bool CanRemoveSvnDir() => CanRemoveSvnDirEnabled;

        
        [RelayCommand]
        private void SelectedSvnDirChanged()
        {
            CanRemoveSvnDirEnabled =!string.IsNullOrEmpty(SelectedProjectPath);
        }

        [RelayCommand]
        private async Task FetchSvnLogAsync()
        {
            // 检查SVNLog文件夹是否存在，不存在创建
            var logSaveDir = $@"{Application.StartupPath}\SVNLog";
            if (Directory.Exists(logSaveDir))
            {
                Directory.Delete(logSaveDir, true);
            }
            Directory.CreateDirectory(logSaveDir);
            foreach (var svnDir in ProjectsPath)
            {
                // 下载svn的log文件名
                string logFileName = $"{svnDir.Split('\\').ToList().LastOrDefault()}.log";                
                string logResult = await DownloadLogFile(logSaveDir, logFileName, svnDir);
                // 检测是否下载成功
                if (logResult.Contains("E200007"))
                {
                    MessageBox.Show("请输入正确的项目地址", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                // 用来保存本地check的目录的文件名
                string originSvnDirSaveName = $"{svnDir.Split('\\').ToList().LastOrDefault()}.path";
                // 需要把本地check的目录svnDir保存下来，这样才能知道日志对应的目录在哪里
                using (TextWriter writer = new StreamWriter(@$"{logSaveDir}\{originSvnDirSaveName}"))
                {
                    writer.WriteLine(svnDir);
                    writer.Flush();
                }
                // 读取log文件
                var log = SvnLogInfo.Create(logSaveDir, logFileName, svnDir);
                if (log != null)
                {
                    ProjectSvnLogInfo.Add(log);
                }
            }
            //await Task.Delay(2000);
        }
        /// <summary>
        /// 删除要分析的日志
        /// </summary>
        [RelayCommand]
        private void RemoveLog()
        {
            ProjectSvnLogInfo.Remove(SelectedSvnLogInfo);
        }
        /// <summary>
        /// 新增要分析的日志
        /// </summary>
        [RelayCommand]
        private void AddLog()
        {
            var fileDialog = new OpenFileDialog();
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                if (!ProjectSvnLogInfo.Any(p => Path.Equals(p.LogPath, fileDialog.FileName)))
                {
                    var filename = fileDialog.FileName;
                    var fileInfo = new FileInfo(filename);
                    var dir = fileInfo.DirectoryName;
                    // 不包含文件类型.后的文件名
                    var name = fileInfo.Name.Split('.').FirstOrDefault();
                    var svnDirFilePath = System.IO.Path.Combine(dir, $"{name}.path");
                    var logFilePath = $"{name}.log";
                    using (StreamReader sr = new StreamReader(svnDirFilePath))
                    {
                        var svnDir = sr.ReadToEnd();
                        var svnInfo = SvnLogInfo.Create(dir, logFilePath, svnDir);
                        ProjectSvnLogInfo.Add(svnInfo);
                    }
                }
            }
        }

        partial void OnSelectedSvnLogInfoChanged(SvnLogInfo value)
        {

        }

        [RelayCommand]
        private async Task ConvertAndAnalysis()
        {
            _LogFormats.Clear();
            foreach (var svnLogInfo in ProjectSvnLogInfo) 
            {                
                // 格式化Log对象，并计算修改行数
                var current = await GetLogFormats(svnLogInfo.Log, svnLogInfo.SvnDir);
                _LogFormats.AddRange(current);
            }
            Authors.Clear();
            foreach (var author in _LogFormats.Select(f => f.author).Distinct())
            { 
                Authors.Add(new CanCheckedItem<string>(author));
            }
            
        }

        /// <summary>
        /// 下载Log文件
        /// </summary>
        /// <param name="logPath"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private async Task<string> DownloadLogFile(string logPath, string fileName, string dirPath)
        {
            var startTime = StartTime.ToString("yyyy-MM-dd");
            var endTime = EndTime.AddDays(1).ToString("yyyy-MM-dd");
            var cmd = $"svn log -v --xml -r {{\"{startTime}\"}}:{{\"{endTime}\"}} {dirPath} > {logPath}\\{fileName}";
            return await Util.ExecuteCommandAsync(cmd);
        }


        /// <summary>
        /// 将Log对象转为格式化数组
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        private async Task<List<LogFormat>> GetLogFormats(Log log, string localSvnDir)
        {
            List<LogFormat> formats = new List<LogFormat>();
            foreach (var logentry in log.Logentry)
            {
                int revision = logentry.ReVision;
                string author = logentry.Author.Value;
                DateTime checkDate = logentry.Date.Value;
                var msg = logentry.Msg.Value;

                List<Path> paths = logentry.Paths.Path.Where(x =>
                                                        x.Kind == "file" &&
                                                        !IsIgnoreFile(x.Value)
                                                        ).ToList();

                foreach (var path in paths)
                {
                    var val = await GetLineDiff(path.Value, localSvnDir, revision);
                    if (val.Item1 == 0 && val.Item2 == 0)
                    {
                        continue;
                    }

                    var logFormat = new LogFormat();
                    logFormat.author = author;
                    logFormat.fileName = path.Value;
                    logFormat.appendLines = val.Item1;
                    logFormat.removeLines = val.Item2;
                    logFormat.msg = msg;
                    logFormat.totalLines = val.Item1 - val.Item2;
                    logFormat.checkTime = checkDate.ToLocalTime();
                    formats.Add(logFormat);
                }
            }
            formats = formats.OrderBy(x => x.checkTime).ThenBy(x => x.author).ToList();

            return formats;
        }

        /// <summary>
        /// 排除不需要计算的文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private bool IsIgnoreFile(string filePath)
        {
            filePath = filePath.ToLower();
            foreach (var ignoreFile in _ignoreFile)
            {
                if (filePath.EndsWith(ignoreFile.ToLower()))
                    return true;
            }

            foreach (var ignoreFfolder in _ignoreFolder)
            {
                if (filePath.Contains(ignoreFfolder.ToLower()))
                    return true;
            }

            return filePath.Contains("bin/") ||
                   filePath.Contains("obj/") ||
                   filePath.Contains("AutoConfigs/") ||
                   filePath.EndsWith(".cache") ||
                   filePath.EndsWith(".csproj") ||
                   filePath.EndsWith(".sln") ||
                   //filePath.EndsWith(".txt") ||
                   filePath.EndsWith(".md") ||
                   filePath.EndsWith(".xlsx") ||
                   filePath.EndsWith(".xls") ||
                   filePath.EndsWith(".doc") ||
                   filePath.EndsWith(".docs") ||
                   filePath.EndsWith(".pdf") ||
                   filePath.EndsWith(".jpg") ||
                   filePath.EndsWith(".jpge") ||
                   filePath.EndsWith(".png") ||
                   filePath.EndsWith(".Designer.cs") ||
                   filePath.EndsWith(".resx") ||
                   filePath.EndsWith(".ico");
        }

        /// <summary>
        /// 计算单个文件单次提交中的修改行数
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="revision"></param>
        /// <param name="appendLines"></param>
        /// <param name="removeLines"></param>
        private async Task<ValueTuple<int, int>> GetLineDiff(string fileName, string localSvnDir, int revision)
        {
            var appendLines = 0;
            var removeLines = 0;
            //svn diff 命令返回的解释 https://blog.csdn.net/weiwangchao_/article/details/19117191
            string diffBuffer = await CallSvnDiff(fileName, localSvnDir, revision);
            var lines = diffBuffer.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();

            foreach (var line in lines)
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
            var rt = new ValueTuple<int, int>(appendLines, removeLines);
            return rt;
        }

        /// <summary>
        ///  调用svn diff操作，返回diff结果
        /// </summary>
        /// <param name="urlFileName">svn的log返回的修改文件的url路径 <br/>
        /// e.g. /branches/2.10.0.0/Code/Demo.cs</param>
        /// <param name="localSvnDir">本地check的svn目录位置</param>
        /// <param name="revision">版本号</param>
        /// <returns></returns>
        private async Task<string> CallSvnDiff(string urlFileName, string localSvnDir, int revision)
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

            var cmd = $"svn diff --old {localSvnDir}{localfileName}@{revision - 1} --new {localSvnDir}{localfileName}@{revision}";
            var fileDiff = await Util.ExecuteCommandAsync(cmd);
            return fileDiff;
        }

        /// <summary>
        /// 获取svn远程仓库相对svn远程根目录的地址 <br/>
        /// e.g. /branches/2.10.0.0
        /// </summary>
        /// <returns></returns>
        private async Task<string> GetSvnRoot(string svnDir)
        {
            var cmd = $"svn info --xml {svnDir}";
            var info = await Util.ExecuteCommandAsync(cmd);
            XmlDocument infoXML = new XmlDocument();
            infoXML.LoadXml(info);
            var root = infoXML.SelectSingleNode("info/entry/relative-url").InnerXml;
            // ^/branches/2.10.0.0 去掉最开始的^开始符号
            var rootUrl = HttpUtility.UrlDecode(root).Substring(1);
            return rootUrl;
        }
    }


    internal class CanCheckedItem<T>
    {
        public bool IsChecked { get; set; }

        public T Item { get; set; }

        public CanCheckedItem(T item)
        {
            this.IsChecked = false;
            this.Item = item;
        }
    }
}
