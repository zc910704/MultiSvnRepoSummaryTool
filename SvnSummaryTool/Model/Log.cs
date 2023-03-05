using Serilog.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SvnSummaryTool
{
    [XmlRoot(ElementName = "log")]
    public class Log
    {
        public static Logger Logger { get; internal set; }
        [XmlElement(ElementName = "logentry")]
        public List<Logentry> Logentry { get; set; }

        public static Log Create(string logXml)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Log));
                using (TextReader reader = new StringReader(logXml))
                {
                    var log = (Log)serializer.Deserialize(reader);
                    return log;
                }
            }
            catch(Exception e) 
            {
                LogHelper.Error("Create Log Error", e);
            }
            return new Log();
        }
    }

    [XmlRoot(ElementName = "logentry")]
    public class Logentry
    {
        [XmlAttribute(AttributeName = "revision")]
        public int ReVision { get; set; }

        [XmlElement(ElementName = "author")]
        public Author Author { get; set; }

        [XmlElement(ElementName = "date")]
        public Date Date { get; set; }

        [XmlElement(ElementName = "paths")]
        public Paths Paths { get; set; }

        [XmlElement(ElementName = "msg")]
        public Msg Msg { get; set; }
    }

    [XmlRoot(ElementName = "author")]
    public class Author
    {
        [XmlText]
        public string Value { get; set; }
    }

    [XmlRoot(ElementName = "date")]
    public class Date
    {
        [XmlText]
        public DateTime Value { get; set; }
    }

    [XmlRoot(ElementName = "paths")]
    public class Paths
    {
        [XmlElement(ElementName = "path")]
        public List<PathChanged> Path { get; set; }
    }

    [XmlRoot(ElementName = "path")]
    public class PathChanged
    {
        [XmlAttribute(AttributeName = "text-mods")]
        public string TextMods { get; set; }
        [XmlAttribute(AttributeName = "kind")]
        public string Kind { get; set; }
        [XmlAttribute(AttributeName = "action")]
        public string Action { get; set; }
        [XmlAttribute(AttributeName = "prop-mods")]
        public string PropMods { get; set; }
        [XmlText]
        public string Value { get; set; }

    }

    [XmlRoot(ElementName = "msg")]
    public class Msg
    {
        [XmlText]
        public string Value { get; set; }
    }


    public class LogSummary
    {
        [DisplayName("作者")]
        public string author { get; set; }
        [DisplayName("增加的行数")]
        public int appendLines { get; set; }
        [DisplayName("删除的行数")]
        public int removeLines { get; set; }
        [DisplayName("总行数")]
        public int totalLines { get; set; }
    }
}
