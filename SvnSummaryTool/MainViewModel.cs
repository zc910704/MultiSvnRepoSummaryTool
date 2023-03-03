using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.Logging;
using SvnSummaryTool.Model;
using SvnSummaryTool.Utils;
using static System.Windows.Forms.DataFormats;

namespace SvnSummaryTool
{
    internal partial class MainViewModel : ObservableObject
    {
        private static string[] _ignoreFile = ConfigTools.GetConfig("IgnoreFile").Split(';').Where(s => !string.IsNullOrEmpty(s)).ToArray();
        private static string[] _ignoreFolder = ConfigTools.GetConfig("IgnoreFolder").Split(';').Where(s => !string.IsNullOrEmpty(s)).ToArray();

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
        /// 能否移除待分析的svn日志
        /// </summary>
        [ObservableProperty]
        private bool _CanRemoveSvnLogInfoEnalbe = false;
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
        /// 当前统计信息表数据源
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<LogFormat> _DataTableSourece = new ObservableCollection<LogFormat>();
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
        /// 当前选择的日志
        /// </summary>
        [ObservableProperty]
        private LogFormat _SelectedLogFormat = null;
        /// <summary>
        /// 处理进度
        /// </summary>
        [ObservableProperty]
        private int _Progress = 0;
        /// <summary>
        /// 调试模式
        /// </summary>
        [ObservableProperty]
# if DEBUG
        private bool _DebugMode = true;
#else
        private bool _DebugMode = false;
#endif
        /// <summary>
        /// 当前修改总行数
        /// </summary>
        public int ModifyLineCount => AppendLineCount + DeleteLineCount;
        /// <summary>
        /// 当前未筛选的日志
        /// </summary>
        private ConcurrentBag<LogFormat> _LogFormats = new ConcurrentBag<LogFormat>();

        /// <summary>
        /// 作者选择状态变更
        /// </summary>
        [RelayCommand]
        private void AuthorCheckedChanged()
        {
            var selectedAuthors = Authors
                .Where(a => a.IsChecked)
                .Select(a => a.Item).ToList();
            var selectedUserLog = _LogFormats
                .Where(log => selectedAuthors.Contains(log.author));

            if (selectedUserLog != null && selectedUserLog.Any())
            {
                AppendLineCount = selectedUserLog
                    .Select(s => s.appendLines)
                    .Aggregate((l1, l2) => l1 + l2);

                DeleteLineCount = selectedUserLog
                    .Select(s => s.removeLines)
                    .Aggregate((l1, l2) => l1 + l2);
                DataTableSourece.Clear();
                foreach (var item in selectedUserLog
                    .OrderBy(x => x.checkTime)
                    .ThenBy(x => x.author))
                {
                    DataTableSourece.Add(item);
                }
            }
            else
            {
                AppendLineCount = 0;
                DeleteLineCount = 0;
                DataTableSourece.Clear();
            }
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

        /// <summary>
        /// 异步获取日志
        /// </summary>
        /// <returns></returns>
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
                // 检测是否下载成功
                if (!await SvnTools.DownloadLogFile(logSaveDir, logFileName, svnDir, StartTime, EndTime))
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
        [RelayCommand(CanExecute = nameof(CanRemoveSvnLogInfo))]
        private void RemoveLog()
        {
            ProjectSvnLogInfo.Remove(SelectedSvnLogInfo);
        }
        /// <summary>
        /// 能否移除待分析的svn日志
        /// </summary>
        private bool CanRemoveSvnLogInfo => CanRemoveSvnLogInfoEnalbe;

        /// <summary>
        /// 新增要分析的日志
        /// </summary>
        [RelayCommand]
        private void AddLog()
        {
            var fileDialog = new OpenFileDialog();
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                if (!ProjectSvnLogInfo.Any(p => Path.Equals(p.LogDir, fileDialog.FileName)))
                {
                    var filename = fileDialog.FileName;
                    var fileInfo = new FileInfo(filename);
                    var dir = fileInfo.DirectoryName;
                    var svnInfo = SvnLogInfo.Create(dir, fileInfo.Name, null);
                    if (svnInfo != null && ProjectSvnLogInfo.All(p => !Path.Equals(p.SvnDir, svnInfo.SvnDir)))
                    {                        
                        ProjectSvnLogInfo.Add(svnInfo);
                    }
                }
            }
        }

        /// <summary>
        /// 分析日志
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private void Convert()
        {
            _LogFormats.Clear();
            Authors.Clear();

            // 获取所有提交的集合
            var logEntrywithInfo = new List<Tuple<SvnLogInfo, Logentry>>();
            foreach (var svnInfo in ProjectSvnLogInfo)
            {
                foreach (var entry in svnInfo.Log.Logentry)
                {
                    // 格式化Log对象
                    var current = ConvertLogEntry(svnInfo.SvnDir, entry);
                    foreach (var c in current)
                    {
                        _LogFormats.Add(c);
                    }
                }                
            }

            foreach (var author in _LogFormats.Select(f => f.author).Distinct())
            {
                Authors.Add(new CanCheckedItem<string>(author));
            }

        }

        [RelayCommand]
        private async Task StartCalculateDiffAsync()
        {
            var progress = new Progress<int>(value =>
            {
                Progress = value;
            });
            await DoCalculateDiffAsync(progress);

        }

        /// <summary>
        /// 下载变更
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task DownloadDiffAsync()
        { 
        
        }

        /// <summary>
        /// 清空本地缓存文件
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task ClearLocalCache()
        { 
        
        }

        private async Task DoCalculateDiffAsync(IProgress<int> progress)
        {
            var total = _LogFormats.Count;
            var pos = 0;

            await Parallel.ForEachAsync(_LogFormats, new ParallelOptions() { MaxDegreeOfParallelism = 20 },
                            async (entry, cancellationToken) =>
                            {
                                // 格式化Log对象，并计算修改行数
                                var val = await SvnTools.GetLineDiff(entry.fileName, entry.LocalSvnDir, entry.version);

                                entry.appendLines = val.AppendLine;
                                entry.removeLines = val.RemoveLine;
                                entry.totalLines = val.AppendLine + val.RemoveLine;
                                Interlocked.Increment(ref pos);

                                progress.Report((int)(((float)pos / (float)total) * 100));
                            });
        }

        /// <summary>
        /// 使用vscode查看提交
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task ShowDiffInVsCodeAsync()
        {
            if (SelectedLogFormat != null)
            {
                await SvnTools.ShowDiffInVscode(SelectedLogFormat);
            }
        }

        /// <summary>
        /// 关闭时保存配置
        /// </summary>
        [RelayCommand]
        private void SaveConfig()
        {
            Settings.Default.svnDirSaved = new System.Collections.Specialized.StringCollection();
            Settings.Default.svnDirSaved.AddRange(ProjectsPath.ToArray());
            Settings.Default.Save();
        }

        /// <summary>
        /// 关闭时保存配置
        /// </summary>
        [RelayCommand]
        private void Load()
        {
            var svnDirSaved = Settings.Default.svnDirSaved;
            if (svnDirSaved != null && svnDirSaved.Count != 0)
            {
                foreach (var dir in svnDirSaved)
                {
                    if (!string.IsNullOrEmpty(dir))
                    {
                        ProjectsPath.Add(dir);
                    }
                }
            }
        }

        partial void OnSelectedSvnLogInfoChanged(SvnLogInfo value) => CanRemoveSvnLogInfoEnalbe = value != null;


        /// <summary>
        /// 将log对象转换为
        /// </summary>
        /// <param name="localSvnDir"></param>
        /// <param name="formats"></param>
        /// <param name="logentry"></param>
        /// <returns></returns>
        private List<LogFormat> ConvertLogEntry(string localSvnDir, Logentry logentry)
        {
            int revision = logentry.ReVision;
            string author = logentry.Author.Value;
            DateTime checkDate = logentry.Date.Value;
            var msg = logentry.Msg.Value;
            var reversion = logentry.ReVision;

            List<Path> paths = logentry.Paths.Path.Where(x =>
                                                    x.Kind == "file" &&
                                                    !IsIgnoreFile(x.Value)
                                                    ).ToList();

            var formats = new List<LogFormat>();
            foreach (var path in paths)
            {
                var logFormat = new LogFormat();
                logFormat.author = author;
                logFormat.fileName = path.Value;
                logFormat.appendLines = 0;
                logFormat.removeLines = 0;
                logFormat.msg = msg;
                logFormat.version = reversion;
                logFormat.totalLines = 0;
                logFormat.checkTime = checkDate.ToLocalTime();
                logFormat.LocalSvnDir = localSvnDir;
                formats.Add(logFormat);
            }
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
        /// 测试
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task TestAsync()
        {
            var re = await SvnTools.GetSvnInfo(SelectedProjectPath);   
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
