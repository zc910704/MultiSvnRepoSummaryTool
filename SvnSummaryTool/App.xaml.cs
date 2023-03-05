using System.Windows;

namespace SvnSummaryTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            LogHelper.InitLog();
        }
    }
}
