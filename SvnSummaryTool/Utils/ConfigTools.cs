using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvnSummaryTool.Utils
{
    public class ConfigTools
    {
        public static string GetConfig(string key)
        {
            return ConfigurationManager.AppSettings.Get(key);
        }
    }
}
