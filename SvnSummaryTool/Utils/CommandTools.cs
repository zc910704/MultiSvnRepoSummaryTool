using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SvnSummaryTool.Utils
{
    public static class CommandTools
    {
        /// <summary>
        /// 执行CMD命令，并返回结果
        /// </summary>
        /// <param name="command">命令</param>
        /// <returns></returns>
        public static string ExecuteCommand(string command)
        {
            LogHelper.Debug($"CommandTools::ExecuteCommand |cmd = {command}");
            Process pro = new Process();
            pro.StartInfo.FileName = "cmd.exe";
            pro.StartInfo.UseShellExecute = false;
            pro.StartInfo.RedirectStandardError = true;
            pro.StartInfo.RedirectStandardInput = true;
            pro.StartInfo.RedirectStandardOutput = true;
            pro.StartInfo.CreateNoWindow = true;
            pro.Start();
            pro.StandardInput.AutoFlush = true;
            pro.StandardInput.WriteLine("@echo off");
            pro.StandardInput.WriteLine(command + " & exit");
            //获取cmd窗口的输出信息
            string output = pro.StandardOutput.ReadToEnd();
            pro.WaitForExit();//等待程序执行完退出进程
            pro.Close();
            return output.Substring(output.IndexOf("& exit") + 8);
        }

        /// <summary>
        /// 异步执行CMD命令，并返回结果
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public static async Task<string> ExecuteCommandAsync(string command)
        {
            LogHelper.Debug($"CommandTools::ExecuteCommandAsync |cmd = {command}");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + command,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            // .Net5以后新增API
            // https://learn.microsoft.com/zh-cn/dotnet/api/system.threading.tasks.taskcompletionsource.trysetresult?view=net-7.0
            var tcs = new TaskCompletionSource<string>();
            void outputHandler(object sender,EventArgs e)
            {
                var result = process.StandardOutput.ReadToEnd();
                LogHelper.Debug($"CommandTools::ExecuteCommandAsync |Result = {result}");
                tcs.TrySetResult(result);
                process.Exited -= outputHandler;
            };

            process.Exited += outputHandler;
            process.Start();

            return await tcs.Task;
        }
    }
}
