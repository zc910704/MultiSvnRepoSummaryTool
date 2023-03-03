using System;
using System.IO;
using System.Web;
using System.Xml;
using System.Xml.Serialization;

namespace SvnSummaryTool.Model
{
    [XmlRoot(ElementName = "info")]
    public class Info
    {
        /// <summary>
        /// 信息内容
        /// </summary>
        [XmlElement( ElementName = "entry")]
        public SVNInfo? Value { get; set; }
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
        /// 读取实例
        /// </summary>
        /// <param name="svnInfo"></param>
        /// <returns></returns>
        public static SVNInfo? Create(string svnInfo)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Info));
                using (var reader = new StringReader(svnInfo))
                { 
                    var info = (Info)serializer.Deserialize(reader);
                    return info?.Value;
                }
                
            }
            catch (Exception e)
            {

            }
            return null;
        }
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

    public enum SVNType 
    {
        [XmlEnum(Name = "dir")]
        DIR,
        [XmlEnum(Name = "file")]
        FILE
    }
}
