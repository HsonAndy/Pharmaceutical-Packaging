using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace App_CCD檢測機
{
    static class Program
    {
        public static Queue<string> Messages = new Queue<string>();

        static string appGuid = "{8604F243-A7AE-45C9-AB4C-D129FEA9FEE9}";
        [STAThread]
        static void Main(string[] args)
        {
            using (System.Threading.Mutex m = new System.Threading.Mutex(false, "Global\\" + appGuid))
            {
                //另一份已執行
                if (!m.WaitOne(0, false))
                {
                    MessageBox.Show("程式已在執行中");
                    return;
                }
                else
                {
                    if (args.Any())
                    {
                        Messages.Enqueue(args[0]);
                    }
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Form1());
                }
            }
        }
    }
}
