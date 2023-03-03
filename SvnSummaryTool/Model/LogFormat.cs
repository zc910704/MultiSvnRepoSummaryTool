using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvnSummaryTool.Model
{
    /// <summary>
    /// 格式化后的SVN提交信息
    /// </summary>
    public class LogFormat
    {
        [DisplayName("版本")]
        public int version { get; set; }
        [DisplayName("作者")]
        public string author { get; set; }
        [DisplayName("文件路径")]
        public string fileName { get; set; }
        [DisplayName("增加的行数")]
        public int appendLines { get; set; }
        [DisplayName("删除的行数")]
        public int removeLines { get; set; }
        [DisplayName("总行数")]
        public int totalLines { get; set; }
        [DisplayName("签入时间")]
        public DateTime checkTime { get; set; }
        [DisplayName("修改信息")]
        public string msg { get; set; }
        /// <summary>
        /// 本地svn路径
        /// </summary>
        public string LocalSvnDir { get; set; }
    }
}
