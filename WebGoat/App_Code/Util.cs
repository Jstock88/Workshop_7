using System;
using System.Diagnostics;
using log4net;
using System.Reflection;
using System.IO;
using System.Threading;

namespace OWASP.WebGoat.NET.App_Code
{
    public class Util
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public static int RunProcessWithInput(string cmd, string args, string input)
        {
            // Only allow known safe client executables
            var allowedExecutables = new[] { "sqlite3", "/usr/bin/sqlite3", "C:\\sqlite3\\sqlite3.exe" };
            bool allowed = false;
            foreach (var allowedExec in allowedExecutables)
            {
                if (string.Equals(cmd, allowedExec, StringComparison.OrdinalIgnoreCase))
                {
                    allowed = true;
                    break;
                }
            }
            if (!allowed)
            {
                log.Error("Attempt to run forbidden process: " + cmd);
                throw new InvalidOperationException("Invalid client command requested.");
            }
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                WorkingDirectory = Settings.RootDir,
                FileName = cmd,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            using (Process process = new Process())
            {
                process.EnableRaisingEvents = true;
                process.StartInfo = startInfo;

                process.OutputDataReceived += (sender, e) => {
                    if (e.Data != null)
                        log.Info(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        log.Error(e.Data);
                };

                AutoResetEvent are = new AutoResetEvent(false);

                process.Exited += (sender, e) => 
                {
                    Thread.Sleep(1000);
                    are.Set();
                    log.Info("Process exited");

                };

                process.Start();

                using (StreamReader reader = new StreamReader(new FileStream(input, FileMode.Open)))
                {
                    string line;
                    string replaced;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                            replaced = line.Replace("DB_Scripts/datafiles/", "DB_Scripts\\\\datafiles\\\\");
                        else
                            replaced = line;

                        log.Debug("Line: " + replaced);

                        process.StandardInput.WriteLine(replaced);
                    }
                }
    
                process.StandardInput.Close();
    

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
    
                //NOTE: Looks like we have a mono bug: https://bugzilla.xamarin.com/show_bug.cgi?id=6291
                //have a wait time for now.
                
                are.WaitOne(10 * 1000);

                if (process.HasExited)
                    return process.ExitCode;
                else //WTF? Should have exited dammit!
                {
                    process.Kill();
                    return 1;
                }
            }
        }
    }
}

