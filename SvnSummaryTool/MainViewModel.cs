using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog.Filters;
using SvnSummaryTool.Model;
using SvnSummaryTool.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SvnSummaryTool
{
    internal partial class MainViewModel : ObservableObject
    {
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
        private ObservableCollection<LogFormat> _DataTableSource = new ObservableCollection<LogFormat>();
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
        /// 需要过滤的提交条件
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<CanCheckedItem<string>> _Filters = new ObservableCollection<CanCheckedItem<string>>();
        /// <summary>
        /// 待添加的条件
        /// </summary>
        [ObservableProperty]
        private string _AboutToAddCondition = string.Empty;
        /// <summary>
        /// 当前选择的条件
        /// </summary>
        [ObservableProperty]
        private CanCheckedItem<string> _SelectedFilterCondition = null;
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
        /// 本地diff的缓存保存路径
        /// </summary>
        private string _DiffSaveDir = Path.Combine(Application.StartupPath, "DiffCache");

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
            try
            { 
            
            }
            catch(Exception ex) 
            {
                LogHelper.Error("MainViewModel::RemoveSvnDir |Exception.", ex);
            }
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
            try
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
                    // 用来保存svn信息的文件名
                    string svnInfoFilename = $"{svnDir.Split('\\').ToList().LastOrDefault()}.svnInfo";
                    var svnInfoResponse = await SvnTools.GetSvnInfo(svnDir);
                    // 需要把svn的信息保存下来
                    svnInfoResponse.Save(@$"{logSaveDir}\{svnInfoFilename}");
                    // 读取log文件
                    var log = SvnLogInfo.Create(logSaveDir, logFileName, svnInfoResponse.Value);
                    if (log != null)
                    {
                        ProjectSvnLogInfo.Add(log);
                    }
                }
                //await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                LogHelper.Error("MainViewModel::FetchSvnLogAsync |Exception", ex);
            }           
        }
        /// <summary>
        /// 删除要分析的日志
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRemoveSvnLogInfo))]
        private void RemoveLog()
        {
            try
            {
                var next = GetNext(ProjectSvnLogInfo, SelectedSvnLogInfo);
                ProjectSvnLogInfo.Remove(SelectedSvnLogInfo);
                SelectedSvnLogInfo = next;
            }
            catch (Exception ex)
            { 
                LogHelper.Error("MainViewModel::RemoveLog |Exception", ex);
            }
        }

        /// <summary>
        /// 新增要分析的日志
        /// </summary>
        [RelayCommand]
        private void AddLog()
        {
            try
            {
                var fileDialog = new OpenFileDialog();
                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    if (!ProjectSvnLogInfo.Any(p => PathChanged.Equals(p.LogDir, fileDialog.FileName)))
                    {
                        var filename = fileDialog.FileName;
                        var fileInfo = new FileInfo(filename);
                        var dir = fileInfo.DirectoryName;
                        SvnLogInfo? svnInfo = SvnLogInfo.Create(dir, fileInfo.Name, null);
                        if (svnInfo != null && ProjectSvnLogInfo.All(p => p.SvnInfo?.Url != svnInfo.SvnInfo?.Url))
                        {
                            ProjectSvnLogInfo.Add(svnInfo);
                        }
                    }
                }
            }
            catch(Exception ex) 
            { 
                LogHelper.Error("MainViewModel::AddLog |Exception", ex);
            }

        }

        /// <summary>
        /// 分析日志
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private void Convert()
        {
            try
            {
                _LogFormats.Clear();
                Authors.Clear();

                // 获取所有提交的集合
                var logEntrywithInfo = new List<Tuple<SvnLogInfo, Logentry>>();
                foreach (var svnlogInfo in ProjectSvnLogInfo)
                {
                    foreach (var entry in svnlogInfo.Log!.Logentry)
                    {
                        // 格式化Log对象
                        var current = ConvertLogEntry(svnlogInfo.SvnInfo, entry);
                        foreach (var c in current)
                        {
                            _LogFormats.Add(c);
                        }
                    }
                }

                foreach (var author in _LogFormats.Select(f => f.Author).Distinct())
                {
                    Authors.Add(new CanCheckedItem<string>(author));
                }
            }
            catch( Exception ex )
            {
                LogHelper.Error("MainViewModel::Convert |Exception", ex);
            }
        }

        /// <summary>
        /// 开始计算变更行数
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task StartCalculateDiffAsync()
        {
            try
            {
                LogHelper.Debug("MainViewModel::StartCalculateDiffAsync |Enter");
                Progress = 0;
                var progress = new Progress<int>(value =>
                {
                    Progress = value;
                });
                await DoCalculateDiffAsync(progress);
                GenerateDataSource();
            }
            catch ( Exception ex )
            {
                LogHelper.Error("MainViewModel::StartCalculateDiffAsync |Exception", ex);
            }
        }
        /// <summary>
        /// 切换是否需要全选提交记录来缓存
        /// </summary>
        [RelayCommand]
        private void ToggleSelectedAllLogForCache(bool isChecked)
        {
            isChecked = !isChecked;
            foreach (var item in DataTableSource) { item.IsNeedCache = isChecked; }
        }
        /// <summary>
        /// 下载变更
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task DownloadDiffAsync()
        {
            try
            {
                var progress = new Progress<int>(value =>
                {
                    Progress = value;
                });

                if (!Directory.Exists(_DiffSaveDir))
                {
                    Directory.CreateDirectory(_DiffSaveDir);
                }

                await DoDownloadDiffAsync(progress, _DiffSaveDir);
            }
            catch(Exception ex) 
            {
                LogHelper.Error("MainViewModel::DownloadDiffAsync |Exception", ex);
            }           
        }

        /// <summary>
        /// 清空本地缓存文件
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task ClearLocalCache()
        {
            try
            {
                var saveDir = Path.Combine(Application.StartupPath, "DiffCache");
                if (Directory.Exists(saveDir))
                {
                    Directory.Delete(saveDir, true);
                }
            }
            catch (Exception ex) 
            {
                LogHelper.Error("MainViewModel::ClearLocalCache |Exception", ex);
            }

        }

        /// <summary>
        /// 作者选择状态变更
        /// </summary>
        [RelayCommand]
        private void AuthorCheckedChanged()
        {
            try
            {
                GenerateDataSource();
            }
            catch (Exception ex) 
            {
                LogHelper.Error("MainViewModel::AuthorCheckedChanged |Exception", ex);
            }
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
                await SvnTools.ShowDiffInVscode(SelectedLogFormat, _DiffSaveDir);
            }
        }

        /// <summary>
        /// 关闭时保存配置
        /// </summary>
        [RelayCommand]
        private void SaveConfig()
        {
            Settings.Default.SvnDirSaved = new System.Collections.Specialized.StringCollection();
            Settings.Default.SvnDirSaved.AddRange(ProjectsPath.ToArray());

            Settings.Default.FilterSaved = new System.Collections.Specialized.StringCollection();
            foreach (var filter in Filters) 
            {
                Settings.Default.FilterSaved.Add($"{filter.Item};{filter.IsChecked}");
            }            
            Settings.Default.Save();
            LogHelper.Close();
        }

        /// <summary>
        /// 关闭时保存配置
        /// </summary>
        [RelayCommand]
        private void Load()
        {
            LogHelper.Debug("Start!");
            var svnDirSaved = Settings.Default.SvnDirSaved;
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
            var ignore = ConfigTools.GetConfig("Ignore").Split(';')
                    .Where(s => !string.IsNullOrEmpty(s));
            var userFilters = Settings.Default.FilterSaved;
            if (userFilters != null)
            {
                foreach(var filter in userFilters) 
                {
                    var array = filter.Split(";");
                    if (array.Length > 1 && bool.TryParse(array[1], out bool isChecked))
                    {
                        var item = new CanCheckedItem<string>(array[0], isChecked);
                        Filters.Add(item);
                    }
                    
                }
            }
            else
            {
                foreach (var item in ignore)
                {
                    Filters.Add(new CanCheckedItem<string>(item, true));
                }
            }
        }

    /// <summary>
    /// 测试
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
        private async Task TestAsync()
        {
            await CommandTools.ExecuteCommandAsync("explorer .\\Logs\\");
        }

        /// <summary>
        /// 新增过滤条件
        /// </summary>
        [RelayCommand]
        private void AddNewFilter()
        {
            try
            {
                var @new = new CanCheckedItem<string>(AboutToAddCondition, true);
                Filters.Add(@new);
                SelectedFilterCondition = @new;
            }
            catch (Exception ex)
            {
                LogHelper.Error("MainViewModel::RemoveFilter |Exception", ex);
            }
        }

        /// <summary>
        /// 移除过滤条件
        /// </summary>
        [RelayCommand]
        private void RemoveFilter()
        {
            try
            {
                var current = Filters.FirstOrDefault( f => f.Item == AboutToAddCondition);
                if (current != null && Filters.Any(f => f == current))
                { 
                    Filters.Remove(current);
                }
            }
            catch(Exception ex) 
            {
                LogHelper.Error("MainViewModel::RemoveFilter |Exception", ex);
            }
        }

        /// <summary>
        /// 能否移除待分析的svn日志
        /// </summary>
        private bool CanRemoveSvnLogInfo => CanRemoveSvnLogInfoEnalbe;

        /// <summary>
        /// 下载所有变更
        /// </summary>
        /// <param name="progress"></param>
        /// <returns></returns>
        private async Task DoDownloadDiffAsync(IProgress<int> progress, string saveDir)
        {
            Progress = 0;

            var source = DataTableSource.Where(d => d.IsNeedCache);
            var total = source.Count();
            var pos = 0;
            LogHelper.Debug($"MainViewModel::DoDownloadDiffAsync |start for total {total}");

            await Parallel.ForEachAsync(source, async (log, CancellationToken) =>
            {
                await SvnTools.DownloadFile(log.FileFullUrl, saveDir, log.Revision - 1);
                await SvnTools.DownloadFile(log.FileFullUrl, saveDir, log.Revision);

                log.IsCached = true;
                Interlocked.Increment(ref pos);

                LogHelper.Debug($"MainViewModel::DoCalculateDiffAsync |pos/total = {pos}/{total}");
                //await Task.Delay(1000);
                progress.Report((int)(((float)pos / (float)total) * 100));
            });
        }

        private async Task DoCalculateDiffAsync(IProgress<int> progress)
        {
            try
            {
                var total = _LogFormats.Count;
                var pos = 0;

                LogHelper.Debug($"MainViewModel::DoCalculateDiffAsync |start for total {total}");
                await Parallel.ForEachAsync(_LogFormats, new ParallelOptions() { MaxDegreeOfParallelism = 30 },
                                async (entry, cancellationToken) =>
                                {
                                    // 格式化Log对象，并计算修改行数
                                    await SvnTools.GetLineDiff(entry);
                                    Interlocked.Increment(ref pos);
                                    LogHelper.Debug($"MainViewModel::DoCalculateDiffAsync |pos/total = {pos}/{total}");
                                    //await Task.Delay(1000);
                                    progress.Report((int)(((float)pos / (float)total) * 100));
                                });
            }
            catch (Exception ex)
            {
                LogHelper.Error("MainViewModel::DoCalculateDiffAsync |Exception", ex);
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
        private List<LogFormat> ConvertLogEntry(SVNInfo svnInfo, Logentry logentry)
        {
            int revision = logentry.ReVision;
            string author = logentry.Author.Value;
            DateTime checkDate = logentry.Date.Value;
            var msg = logentry.Msg.Value;
            var reversion = logentry.ReVision;

            List<PathChanged> paths = logentry.Paths.Path.Where(x =>
                                                    x.Kind == "file" &&
                                                    !IsIgnoreFile(x.Value)
                                                    ).ToList();

            var formats = new List<LogFormat>();
            foreach (var path in paths)
            {
                var logFormat = new LogFormat();
                logFormat.Author = author;
                logFormat.FileUrlPath = path.Value;
                logFormat.AppendLines = 0;
                logFormat.RemoveLines = 0;
                logFormat.Msg = msg;
                logFormat.Revision = reversion;
                logFormat.TotalLines = 0;
                logFormat.CheckTime = checkDate.ToLocalTime();
                logFormat.SvnInfo = svnInfo;
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
            foreach (var filter in Filters)
            {
                if (filter.IsChecked &&
                    filePath.Contains(filter.Item, StringComparison.OrdinalIgnoreCase))
                { return true; }
            }
            return false;
            
        }

        /// <summary>
        /// 创建数据源
        /// </summary>
        private void GenerateDataSource()
        {
            var selectedAuthors = Authors
                .Where(a => a.IsChecked)
                .Select(a => a.Item).ToList();
            var selectedUserLog = _LogFormats
                .Where(log => selectedAuthors.Contains(log.Author) && !IsIgnoreFile(log.FileFullUrl));
            if (selectedUserLog != null && selectedUserLog.Any())
            {
                AppendLineCount = selectedUserLog
                    .Select(s => s.AppendLines)
                    .Aggregate((l1, l2) => l1 + l2);
                DeleteLineCount = selectedUserLog
                    .Select(s => s.RemoveLines)
                    .Aggregate((l1, l2) => l1 + l2);
                foreach (var item in selectedUserLog
                    .OrderBy(x => x.CheckTime)
                    .ThenBy(x => x.Author))
                {
                    // 检查本地是否有该文件缓存
                    item.IsCached = SvnTools.CheckFileExistInCache(item.FileFullUrl, _DiffSaveDir, item.Revision)
                        && SvnTools.CheckFileExistInCache(item.FileFullUrl, _DiffSaveDir, item.Revision - 1);
                    DataTableSource.Add(item);
                }
            }
            else
            {
                AppendLineCount = 0;
                DeleteLineCount = 0;
                DataTableSource.Clear();
            }
        }

        /// <summary>
        /// 获取下个焦点
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourece"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private T GetNext<T>(IEnumerable<T> sourece, T target)
            where T : class
        {
            bool isCurrent = false;
            foreach(var t in sourece) 
            {
                if (isCurrent)
                { 
                    return t;
                }
                // 如果目标是当前这个， 则标记下一个返回                
                if (target.Equals(t))
                {
                    isCurrent = true;
                }
            }
            // 如果当前这个是最后一个, 就返回最后一个
            return sourece.Last();
        }
    }
}
