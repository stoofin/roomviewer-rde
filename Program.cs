using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace viewer
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {/*
            using (IsoReader reader = new IsoReader(@"D:\chrono\CCTools\iso\cd1.iso", IsoReader.IsoMode.Mode2352, true))
            {
                IsoReader.CCFileInfo[] ccfiles = reader.CCFileInfos;
                foreach (IsoReader.CCFileInfo file in ccfiles)
                {
                    Console.WriteLine(file.Sector + "," + file.Length);
                }
            }*/
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}