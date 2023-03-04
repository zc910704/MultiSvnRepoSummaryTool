using Microsoft.Win32;
using System;
using System.IO;
using System.Web;
using System.Xml;
using System.Xml.Serialization;

namespace SvnSummaryTool.Model
{
    [XmlRoot(ElementName = "info")]
    public class SvnInfoResponse
    {
        /// <summary>
        /// 信息内容
        /// </summary>
        [XmlElement( ElementName = "entry")]
        public SVNInfo? Value { get; set; }

        /// <summary>
        /// 读取实例
        /// </summary>
        /// <param name="svnInfo"></param>
        /// <returns></returns>
        public static SvnInfoResponse? Create(string svnInfoXml)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SvnInfoResponse));
                using (var reader = new StringReader(svnInfoXml))
                {
                    var info = (SvnInfoResponse)serializer.Deserialize(reader);
                    return info;
                }
            }
            catch (Exception e)
            {

            }
            return null;
        }

        /// <summary>
        /// 序列化自身保存到指定文件
        /// </summary>
        /// <param name="targetFilePath"></param>
        public void Save(string targetFilePath)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SvnInfoResponse));
                using (TextWriter writer = new StreamWriter(targetFilePath))
                {
                    serializer.Serialize(writer, this);
                    writer.Flush();
                }
            }
            catch (Exception e)
            { 
            }            
        }
    }

    [XmlRoot(ElementName = "entry")]
    public class SVNInfo
    {
        /// <summary>
        /// 提交版本号
        /// </summary>
        [XmlAttribute(AttributeName = "revision")]
        public string? Revision { get; set; }
        /// <summary>
        /// 当前查询svn信息返回的类型
        /// </summary>
        [XmlAttribute(AttributeName = "kind")]
        public SVNType SVNType { get; set; }
        /// <summary>
        /// 当前（目录或文件）的远程地址 <br/>
        /// e.g. http://127.0.0.1:80/svn/repo1/branches/2.10.0.0/src/Example
        /// </summary>
        [XmlElement(ElementName = "url")]
        public string? Url { get; set; }
        /// <summary>
        /// 当前相对库根目录的路径,注意以^开头 <br/>
        /// e.g. ^/branches/2.10.0.0/src/Example
        /// </summary>
        [XmlElement(ElementName = "relative-url")]
        public string? DecorateDRelativeUrl { get; set; }
        /// <summary>
        /// 当前相对库根目录的路径 <br/>
        /// /branches/2.10.0.0/src/Example
        /// </summary>
        public string? RelativeUrl => HttpUtility.UrlDecode(DecorateDRelativeUrl)?.Substring(1);
        /// <summary>
        /// 当前库信息
        /// </summary>
        [XmlElement(ElementName = "repository")]
        public Repository? Repository { get; set; }
        /// <summary>
        /// 本地svn信息
        /// </summary>
        [XmlElement(ElementName = "wc-info")]
        public WorkCopyInfo WorkCopyInfo { get; set; }
    }

    [XmlRoot(ElementName = "repository")]
    public class Repository
    {
        /// <summary>
        /// 库根地址 <br/>
        /// e.g. http://127.0.0.1:80/svn/repo1
        /// </summary>
        [XmlElement(ElementName = "root")]
        public string Root { get; set; }
        /// <summary>
        /// uuid
        /// </summary>
        [XmlElement(ElementName = "uuid")]
        public string UUID { get; set; }
    }

    [XmlRoot(ElementName = "wc-info")]
    public class WorkCopyInfo 
    {
        /// <summary>
        /// 工作拷贝的绝对目录根地址 <br/>
        /// e.g. D:/Desktop/svn/demoRepo
        /// </summary>
        [XmlElement("wcroot-abspath")]
        public string RootPath { get; set; }
    }

    public enum SVNType 
    {
        [XmlEnum(Name = "dir")]
        DIR,
        [XmlEnum(Name = "file")]
        FILE
    }
}
