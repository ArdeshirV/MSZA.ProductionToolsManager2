#region Header

using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

#endregion Header

namespace ArdeshirV.ProductionToolsManagerFeeder
{
	class Program
	{
		public static void Main(string[] args)
		{			
			if(args.Length <= 0 || !File.Exists(args[0])) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Error: Opening Previous Log File as Simulator Failed. " +
				                  "Please Specify The Previous Log File-Path-Name.");
				Console.ResetColor();
			} else {
				RichTextBox rtf = new RichTextBox();
				rtf.LoadFile(args[0]);
				foreach(string line in rtf.Lines) {
					Console.WriteLine(line);
					Thread.Sleep(50);
				}
			}
			//Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
		}
	}
}
