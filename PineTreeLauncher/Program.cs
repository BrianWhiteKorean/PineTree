using System;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Reflection;

namespace PineTreeLauncher
{
    static class Program
    {
        /// <summary>
        /// 애플리케이션을 세션당 한 번만 실행합니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true,
                @"Local\PineTreeLauncher-5E681F56-5E72-4C94-B644-A3E16E6F9C71", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
#if RELEASE
                    ProgramUpdater.UpdateIfNeeded();
#endif
                    RunPineTree();
                }
                catch (Exception ex)
                {
                    var invocation = ex as TargetInvocationException;
                    if (invocation != null && invocation.InnerException != null)
                        ex = invocation.InnerException;
                    MessageBox.Show("프로그램을 실행하지 못했습니다.\r\n\r\n" + ex.Message,
                        "PineTree Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private static void RunPineTree()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PineTree.dll");
            if (!File.Exists(path))
                throw new FileNotFoundException("실행 폴더에 PineTree.dll 파일이 없습니다.", path);
            Assembly assembly = Assembly.LoadFrom(path);
            Type mainType = assembly.GetType("PineTree.Forms.FormMain", true);
            var mainForm = (Form)mainType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .GetValue(null, null);
            Application.Run(mainForm);
        }
    }
}
