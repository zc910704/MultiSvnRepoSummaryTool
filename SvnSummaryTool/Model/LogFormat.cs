using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.VisualBasic.Logging;
using System;
using System.ComponentModel;
using System.Web;

namespace SvnSummaryTool.Model
{
    /// <summary>
    /// 格式化后的SVN提交信息
    /// </summary>
    public partial class LogFormat : ObservableObject
    {
        [DisplayName("版本")]
        public int Revision { get; set; }
        [DisplayName("作者")]
        public string Author { get; set; }
        /// <summary>
        /// 文件路径 <br/>
        /// e.g. /trunk/Folder1
        /// </summary>
        [DisplayName("文件路径")]
        public string FileUrlPath { get; set; }
        [DisplayName("增加的行数")]
        public int AppendLines { get; set; }
        [DisplayName("删除的行数")]
        public int RemoveLines { get; set; }
        [DisplayName("总行数")]
        public int TotalLines { get; set; }
        [DisplayName("签入时间")]
        public DateTime CheckTime { get; set; }
        [DisplayName("修改信息")]
        public string Msg { get; set; }
        /// <summary>
        /// SVN信息
        /// </summary>
        public SVNInfo SvnInfo { get; set; }
        /// <summary>
        /// 是否需要缓存
        /// </summary>
        [ObservableProperty]
        private bool _IsNeedCache = false;
        /// <summary>
        /// 文件完整路径
        /// </summary>
        public string FileFullUrl
        {
            get
            {
                // 含有/的网址前半部分
                var baseUrl = SvnInfo.Repository.Root.EndsWith('/') ? SvnInfo.Repository.Root : SvnInfo.Repository.Root + "/";
                // 不含/的后半部分
                var filePath = this.FileUrlPath.StartsWith('/') ? FileUrlPath.Substring(1) : FileUrlPath;                
                return baseUrl + filePath;
            }
        }
    }
}
