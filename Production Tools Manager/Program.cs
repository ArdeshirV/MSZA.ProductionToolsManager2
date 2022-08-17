
using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.ComponentModel;

namespace MSZA.ProductionToolsManager
{
	/// <summary>
	/// Class with program entry point.
	/// </summary>
	internal sealed class Program
	{
		private const string stringProcessName = "Production.Tools";

		/// <summary>
		/// Program entry point.
		/// </summary>
		[STAThread]
		internal static void Main(string[] args)
		{
			try {
				KillAllProductionTools();
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				Application.Run(new MainForm());
			} finally {
				KillAllProductionTools();
			}
		}
		
		internal static void KillProcess(string pName) {
			try {
				Process[] oldProcesses = Process.GetProcessesByName(pName);
				foreach(Process oldProcess in oldProcesses)
					oldProcess.Kill();
			} catch(Win32Exception) {}
		}
		
		internal static void KillAllProductionTools() {
			KillProcess(ProcessName);
		}
		
		internal static string ProcessName {
			get { return stringProcessName; }
		}
	}
}
