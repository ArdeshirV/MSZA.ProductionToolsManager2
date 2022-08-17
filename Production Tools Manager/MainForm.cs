#region Header

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using ArdeshirV.Forms;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MSZA.ProductionToolsManager.Properties;

#endregion Header
//---------------------------------------------------------------------------------------
namespace MSZA.ProductionToolsManager
{
	public partial class MainForm : FormBase
	{
		#region External Methods
		
		public const int SW_RESTORE = 9;
		public const int WM_CLOSE = 0x0010;
		
		[DllImport("User32.dll")]
		public static extern bool SetForegroundWindow(IntPtr handle);
		
		[DllImport("User32.dll")]
		public static extern bool ShowWindow(IntPtr handle, int nCmdShow);
		
		[DllImport("User32.dll")]
		public static extern bool IsIconic(IntPtr handle);
        
		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
		
		[DllImport("user32.dll")]
		public static extern bool GetWindowRect(IntPtr hWnd, out Rectangle lpRect);
		
		[DllImport("user32.dll")]
		public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpClassName, string lpWindowName);
		
		[DllImport("user32.dll")]
		public static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);
				
		[DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Auto)]
		public static extern uint RegisterWindowMessage(string lpString);
		
		[DllImport("user32.dll")]
		public  static extern void PostQuitMessage(int ExitCode);
		
		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
		
		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern int GetWindowTextLength(IntPtr hWnd);

		#endregion External Methods
		//-------------------------------------------------------------------------------
		#region Enumerations
		
		internal enum MachineState {
			Unknown = 0, ICT = 1, FCT = 2
		}
		
		#endregion Enumerations
		//-------------------------------------------------------------------------------
		#region Variables

		private ReactionData reactionDefault;
		private ProcessWrapper process = null;
		private bool boolIsDocumentDirty = false;
		private readonly string stringProcessArgs;
		private readonly bool IsSimulator = false;
		private System.Windows.Forms.Timer timer = null;
		private string stringDocumentName = string.Empty;
		private readonly string stringExecutableFileName;
		private List<string> barcodes = new List<string>();
		private List<ReactionData> verbs = new List<ReactionData>();
		private MachineState MachineStateCurrent = MachineState.Unknown;
		private const string stringAppName = "Production Tools Manager";
		private const string stringCloseWindow = "window has been closed!";
		private const string stringSeparator = "_______________________________________________________________________________";
        private const string _stringGithub = "";
        private const string _stringWebsite = "";
        private const string _stringEmail = "MobaddelSazan@yahoo.com";

		#endregion Variables
		//-------------------------------------------------------------------------------
		#region Constructor

		/// <summary>
		/// Initialization of Application.
		/// if there is not "BSTC.Production.Tools.exe" then run app with simulator.
		/// </summary>
		public MainForm() {
			UpdateColorMap();
			InitializeComponent();
			OpacityChanger = false;
			stringProcessArgs = "";
			IsDocumentDirty = false;
			textBoxInput.BackColor = Color.Black;
			reactionDefault = new ReactionData("", textBoxInput.ForeColor, Reaction_General);
			stringExecutableFileName = Environment.GetFolderPath(
				Environment.SpecialFolder.LocalApplicationData) +
				@"\Bstc.Production\App\" + Program.ProcessName + ".exe";
			if(!File.Exists(stringExecutableFileName)) {
				string stringMessage = string.Format(
					"Error: The file \"{0}\" doesn't exists!\n\n" +
					"Do you want to use simulator instead of \"{1}\"?<Yes>\n" +
					"Or do you want to close the app?<No>",
					stringExecutableFileName, Program.ProcessName);
				DialogResult result =
					MessageBox.Show(this, stringMessage, Text,
					                MessageBoxButtons.YesNo,
					                MessageBoxIcon.Question,
					                MessageBoxDefaultButton.Button1);
				if(result == DialogResult.No) {
					Close();
				} else if(result == DialogResult.Yes) {
					stringExecutableFileName = Application.StartupPath +
						@"\..\..\..\ArdeshirV.ProductionToolsManagerFeeder\bin\Debug\" +
						Program.ProcessName + ".exe";
					if(File.Exists(stringExecutableFileName)) {
						AppendTextToOutput(string.Format(
							"Running simulator instead of {0}...",
							Program.ProcessName));
						OpenFileDialog openFile = new OpenFileDialog();
						openFile.Title = "Browse to Find Previous Log File as ICT/FCT Simulator";
						openFile.AddExtension = true;
						openFile.DefaultExt = "log.rtf";
						openFile.SupportMultiDottedExtensions = true;
						openFile.Filter = "Log Rich Text Format(*.log.rtf)|*.log.rtf";
						if(openFile.ShowDialog() == DialogResult.OK) {
							IsSimulator = true;
							stringProcessArgs = string.Format("\"{0}\"", openFile.FileName);
							//ShowInfo(stringProcessArgs);
						} else {
							ShowError("Error: Browse to Find Previous " +
							          "Log File as ICT/FCT Simulator Failed.");
							Close();
						}
					} else {
						ShowError(string.Format(
							"Error: Failed to Find The Simulator: {0}",
							stringExecutableFileName));
						Close();
					}
				}
			}
			UpdateTitle();
		}
		
		#endregion Constructor
		//-------------------------------------------------------------------------------
		#region Reaction Handler
		
		private delegate void ReactionHandler(ReactionEventArg e);
		
		private struct ReactionEventArg {
			private string data;
			private ReactionData reactionData;
			
			public ReactionEventArg(string data, ReactionData ReactionData) {
				this.data = data;
				this.reactionData = ReactionData;
			}
			
			public string Data { get { return data; } }
			public ReactionData ReactionData { get { return reactionData; } }
		}
		//-------------------------------------------------------------------------------
		private struct ReactionData {
			private string name;
			private Color color;
			private ReactionHandler reactionAfter;
			private ReactionHandler reactionBefore;
			
			public ReactionData(string name, Color color, ReactionHandler reactionBefore = null, ReactionHandler reactionAfter = null) {
				this.name = name;
				this.color = color;
				this.reactionAfter = reactionAfter;
				this.reactionBefore = reactionBefore;
			}
			
			public string Name { get { return name; } }
			public Color Color { get { return color; } }
			public ReactionHandler ReactionAfterHandler { get { return reactionAfter; } }
			public ReactionHandler ReactionBeforeHandler { get { return reactionBefore; } }
		}
		//-------------------------------------------------------------------------------
		private void UpdateColorMap() {
			verbs.Add(new ReactionData("Error", Color.Red));
			verbs.Add(new ReactionData("error", Color.Red));
			
			verbs.Add(new ReactionData("Running simulator instead of", Color.Purple));
			
			verbs.Add(new ReactionData("ICT Requested App Version", Color.Turquoise, Reaction_ICT_RequestedAppVersion));
			verbs.Add(new ReactionData("FCT Requested App Version", Color.Turquoise, Reaction_FCT_RequestedAppVersion));
						
			verbs.Add(new ReactionData("FCT test started", Color.Green, Reaction_FCT_Started));
			verbs.Add(new ReactionData("[ ICT ]  [ Success ] => Port Open Successfuly.", Color.Green, Reaction_ICT_Started));
			
			verbs.Add(new ReactionData("=> Init Devices", Color.Teal, Reaction_InitDevices));
				verbs.Add(new ReactionData("Check Firmware CheckSum", Color.Teal, Reaction_CheckFirmwareCheckSum));
			verbs.Add(new ReactionData("End Init Devices", Color.Teal, Reaction_EndInitDevices));
			
			verbs.Add(new ReactionData("Unlock exit code is 0", Color.DarkOrange, Reaction_ProgramMicroControllerStart));
			verbs.Add(new ReactionData(stringCloseWindow, Color.Orange));
			verbs.Add(new ReactionData("STM32 exit code is 0", Color.DarkOrange, Reaction_ProgramMicroControllerEnd));
			
			verbs.Add(new ReactionData("ExitFactoryMode", Color.Green, null));
			verbs.Add(new ReactionData("test stop requested!", Color.Green, null, Reaction_StopRequested));
			verbs.Add(new ReactionData("Test Device Finished", Color.Lime, null, Reaction_FCT_TestFinished));
			verbs.Add(new ReactionData("Test Done", Color.Lime, null, Reaction_ICT_TestFinished));
			verbs.Add(new ReactionData("Exception On Open Port", Color.Lime, Reaction_FCT_ExceptionOnPort));
			
			verbs.Add(new ReactionData("Timer Ticker", Color.Olive));
		}
		//-------------------------------------------------------------------------------
		private void Reaction_ICT_RequestedAppVersion(ReactionEventArg e) {
			State = MachineState.ICT;
		}
		//-------------------------------------------------------------------------------
		private void Reaction_FCT_RequestedAppVersion(ReactionEventArg e) {
			State = MachineState.FCT;
		}
		//-------------------------------------------------------------------------------
		private void Reaction_FCT_Started(ReactionEventArg e) {
		}
		//-------------------------------------------------------------------------------
		private void Reaction_ICT_Started(ReactionEventArg e) {
		}
		//-------------------------------------------------------------------------------
		private void Reaction_ProgramMicroControllerStart(ReactionEventArg e) {
			// Ready to close warning message of "JLink IC-Programmer"
		}
		//-------------------------------------------------------------------------------
		private void Reaction_ProgramMicroControllerEnd(ReactionEventArg e) {
		}
		//-------------------------------------------------------------------------------
		private void Reaction_FCT_TestFinished(ReactionEventArg e) {
			Console.Beep();
			TestFinished();
		}
		//-------------------------------------------------------------------------------
		private void Reaction_ICT_TestFinished(ReactionEventArg e) {
			Console.Beep();
			TestFinished();
		}
		//-------------------------------------------------------------------------------
		private void Reaction_FCT_ExceptionOnPort(ReactionEventArg e) {
			Console.Beep();
			TestFinished();
		}
		//-------------------------------------------------------------------------------
		private void Reaction_StopRequested(ReactionEventArg e) {
		}
		//-------------------------------------------------------------------------------
		private void TestFinished() {
			WindowState = FormWindowState.Minimized;
			BringChromeToFront();
			AppendTextToOutput(stringSeparator);
		}
		//-------------------------------------------------------------------------------
		private void Reaction_InitDevices(ReactionEventArg e) {
			barcodes.Clear();
		}
		//-------------------------------------------------------------------------------
		private void Reaction_CheckFirmwareCheckSum(ReactionEventArg e) {
			//... Check Firmware CheckSum : 6101329245 Verified!
			int index = e.Data.LastIndexOf(":", StringComparison.Ordinal);
			if(index > 0) {
				string code = e.Data.Substring(index + 1).Split(' ')[1].Trim();
				barcodes.Add(code);
			}
		}
		//-------------------------------------------------------------------------------
		private void Reaction_EndInitDevices(ReactionEventArg e) {
			if(barcodes.Count == 2) {
				if(barcodes[0] == barcodes[1])
					ShowError("Two barcodes are equal!\nMaybe you scanned one device twice!");
			} else
				ShowError("More than two barcodes scanned!");
		}
		//-------------------------------------------------------------------------------
		private void Reaction_General(ReactionEventArg e) { }
		
		#endregion Reaction Handler
		//-------------------------------------------------------------------------------
		#region Utility Methods
		
		private void ShowError(string stringMessage) {
			MessageBox.Show(this, stringMessage,
			                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		//-------------------------------------------------------------------------------
		private void ShowInfo(string strMsg) {
			MessageBox.Show(strMsg, Text,
			                MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		//-------------------------------------------------------------------------------
		public void InitApp() {
			KillAllParallelProcesses();
            process = new ProcessWrapper(stringExecutableFileName, stringProcessArgs, true);
            process.OutputDataReceived += process_OutputDataReceived;
            process.Start();
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 300;
            timer.Tick += Timer_Ticker;
			timer.Start();
		}
		//-------------------------------------------------------------------------------
		private void BringChromeToFront() {
			Process[] processes = Process.GetProcessesByName("chrome");
			if(processes.Length > 0 && processes[0] != null) {
				BringWindowToFront(processes[0].MainWindowHandle);
			}
		}
		//-------------------------------------------------------------------------------
		private void BringWindowToFront(IntPtr wndHandle) {
		    if (IsIconic(wndHandle))
		        ShowWindow(wndHandle, SW_RESTORE);
		    SetForegroundWindow(wndHandle);
		}
		//-------------------------------------------------------------------------------
		public static string GetWindowTitle(IntPtr hWnd) {
		    var length = GetWindowTextLength(hWnd) + 1;
		    var title = new StringBuilder(length);
		    GetWindowText(hWnd, title, length);
		    return title.ToString();
		}
		//-------------------------------------------------------------------------------
		ReactionData GetDataReaction(string Data) {
			foreach(ReactionData reaction in verbs)
				if(Data.Contains(reaction.Name))
					return reaction;
			return reactionDefault;
		}
		//-------------------------------------------------------------------------------
		private void AppendTextToOutputSubThread(string Data, ReactionData r) {
			if (textBoxInput.InvokeRequired) {
				Action safeWrite = delegate { AppendTextToOutputSubThread(Data, r); };
				textBoxInput.Invoke(safeWrite);
			} else {
				if(!string.IsNullOrEmpty(Data)) {
					textBoxInput.SuspendLayout();
					try {
						textBoxInput.SelectionStart = textBoxInput.TextLength;
							textBoxInput.SelectionLength = 0;
							textBoxInput.SelectionColor = r.Color;
						textBoxInput.AppendText(Data + Environment.NewLine);
							textBoxInput.SelectionColor = textBoxInput.ForeColor;
				        textBoxInput.ScrollToCaret();
					} catch(InvalidOperationException) {}
			        textBoxInput.ResumeLayout();
					IsDocumentDirty = true; //
				}
			}
		}
		//-------------------------------------------------------------------------------
		private void AppendTextToOutput(string Data) {
			Application.DoEvents();
			ReactionData r = GetDataReaction(Data);
			var threadParameters = new ThreadStart(
				delegate { 
	    			if(r.ReactionBeforeHandler != null)
	    				r.ReactionBeforeHandler.Invoke(new ReactionEventArg(Data, r));
					AppendTextToOutputSubThread(Data, r);
	    			if(r.ReactionAfterHandler != null)
	    				r.ReactionAfterHandler.Invoke(new ReactionEventArg(Data, r));
				}
			);
    		var threadSub = new Thread(threadParameters);
    		threadSub.Start();
    		if(!IsSimulator) {
    			string DataLine = Data + Environment.NewLine;
    		}
		}
		//-------------------------------------------------------------------------------
		private void KillAllParallelProcesses() {
			Program.KillAllProductionTools();
		}
		//-------------------------------------------------------------------------------
		private void SaveToFile(string stringFileName) {
			textBoxInput.SaveFile(stringDocumentName = stringFileName,
			                      RichTextBoxStreamType.RichText);
			UpdateTitle();
			IsDocumentDirty = false;
		}
		//-------------------------------------------------------------------------------
		private void Save() {
			//if(IsDocumentDirty) {
				if(string.IsNullOrEmpty(stringDocumentName))
					SaveAs();
				else
					SaveToFile(stringDocumentName);
			//}
		}
		//-------------------------------------------------------------------------------
		private void SaveAs() {
			SaveFileDialog saveFile = new SaveFileDialog();
			saveFile.Title = Text;
			saveFile.AddExtension = true;
			saveFile.DefaultExt = "log.rtf";
			saveFile.SupportMultiDottedExtensions = true;
			saveFile.Filter = "Log Rich Text Format(*.log.rtf)|*.log.rtf";
			if(saveFile.ShowDialog(this) == DialogResult.OK)
				SaveToFile(saveFile.FileName);
		}
		//-------------------------------------------------------------------------------
		private void UpdateTitle() {
			Text = stringAppName;
			Text += (boolIsDocumentDirty)? "*": "";
			if(!string.IsNullOrEmpty(stringDocumentName))
				Text += string.Format(" - {0}", stringDocumentName);
			if(IsSimulator)
				Text += " - [Simulator]";
		}
		//-------------------------------------------------------------------------------
		private void ShowFormAbout() {
			
			string stringAssemblyProductName = Application.ProductName;

            Donations[] donations = new Donations[] {
                new Donations(
            		stringAssemblyProductName,
            		new Donation[] {
                		DefaultDonationList.CreateDonationByDefaultLogos(
            				"Bitcoin",
            				"17LQ12sTp5soZ7Phw3Tf9Qx7WEzKtCQMg3"  // Belong to MSZA
            			)
					}
				)
            };
			
			string stringCreditDesc = string.Format(
@"'{1}' has been developed in Mobadel Sazan Zharf Andish Co by ArdeshirV@protonmail.com
Company Email: {0}", _stringEmail, stringAssemblyProductName);
			
			var credits = new Credits[] {
				new Credits(stringAssemblyProductName, new Credit[] {
				            	new Credit(
				            		"MSZA(Mobade Sazan Zharf Andish Co)",
				            		stringCreditDesc, Resources.MSZA)
				            })
			};
			
			var copyrights = new Copyright[] {
				new Copyright(this, Resources.IconPNG)
			};
			
			var licenses = new License[] {
				new License(stringAssemblyProductName,
				            Resources.MSZALicense,
				            Resources.MSZA)
			};
			
        	var data = new FormAboutData(this,
			                             copyrights,
										 credits,
										 licenses,
										 donations,
										 _stringWebsite,
										 _stringEmail);
        	FormAbout.Show(data);
		}		
		
		#endregion Utility Methods
		//-------------------------------------------------------------------------------
		#region Event Handlers
		
		void MainFormLoad(object sender, EventArgs e) {
			Settings.Default.Reload();
			if(Settings.Default.MainFormLocation.X == 0 &&
			   Settings.Default.MainFormLocation.Y == 0 &&
			   Settings.Default.MainFormSize.Width == 0 &&
			   Settings.Default.MainFormSize.Height == 0) {
				int intX = Screen.PrimaryScreen.Bounds.Width / 10;
				int intWidth = intX * 8;
				int intHeight = Screen.PrimaryScreen.Bounds.Height / 10 * 6;
				Settings.Default.MainFormLocation = new Point(intX, 0);
				Settings.Default.MainFormSize = new Size(intWidth, intHeight);
			}
			const int intInitialGap = 69;
			Location = new Point(Settings.Default.MainFormLocation.X,
			    Settings.Default.MainFormLocation.Y + 
				(Settings.Default.MainFormSize.Height - intInitialGap)/ 2 - 3);
			Size = new Size(Settings.Default.MainFormSize.Width, intInitialGap);
			//Size = Settings.Default.MainFormSize;
			//Location = Settings.Default.MainFormLocation;
			InitApp();
		}
		//-------------------------------------------------------------------------------
		void MainFormShown(object sender, EventArgs e) {
			Thread.Sleep(20);
			ShrinkHeightByTime(Settings.Default.MainFormSize.Height);
		}
		//-------------------------------------------------------------------------------
		/// <summary>
		/// Code test
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void ButtonShrinkClick(object sender, EventArgs e) {
			BringChromeToFront();
		}
		//-------------------------------------------------------------------------------
		/// <summary>
		/// Timer_Ticker close the "JLink warning window" automatically. 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void Timer_Ticker(object sender, EventArgs e) {
			//AppendTextToOutput("Timer Ticker Has Been Occured. " + Environment.NewLine);
			const string strTargetName = "JLink";
			const string strWndName = "J-Link", strWndSubname = "Warning";
			Process[] processes = Process.GetProcesses();
			foreach(Process p in processes) {
				string strProcessName = p.ProcessName;
				if(strProcessName == "")
					continue;
				if(strProcessName == strTargetName) {
					var allChildWindows = new WindowHandleInfo(p.MainWindowHandle).GetAllChildHandles();
					foreach(IntPtr child in allChildWindows) {
						string childName = GetWindowTitle(child).Trim();
						if(childName == "")
							continue;
						if(childName.StartsWith(strWndName, StringComparison.Ordinal) &&
						   childName.EndsWith(strWndSubname, StringComparison.Ordinal)) {
							IntPtr childHandle = FindWindow(null, childName);
							SendMessage(childHandle, WM_CLOSE, new IntPtr(), new IntPtr());
							AppendTextToOutput("  *****" + childName +
							                   "***** " + stringCloseWindow);
							break;
						}
					}
			        break;
			    }
			}
			Application.DoEvents();
		}
		//-------------------------------------------------------------------------------
		public void Application_Idle(object sender, EventArgs e) {
		}
		//-------------------------------------------------------------------------------
		/// <summary>
		/// Reads the output of Production.Tools
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void process_OutputDataReceived(object sender, DataReceivedEventArgs e) {
			if(e != null && e.Data != null && e.Data != string.Empty) {
				string strData = e.Data.Trim();
				AppendTextToOutput(strData);
			}
		}
		//-------------------------------------------------------------------------------
		void MainFormFormClosed(object sender, FormClosedEventArgs e) {
			Settings.Default.MainFormSize = Size;
			Settings.Default.MainFormLocation = Location;
			Settings.Default.Save();
			KillAllParallelProcesses();
		}
		//-------------------------------------------------------------------------------
		void MainFormFormClosing(object sender, FormClosingEventArgs e) {
			if(IsDocumentDirty) {
				DialogResult answer =
					MessageBox.Show(this, "Do you want to save changes?", Text,
					                MessageBoxButtons.YesNoCancel,
					                MessageBoxIcon.Question,
					                MessageBoxDefaultButton.Button1
					               );
				switch(answer) {
					case DialogResult.Yes:
						Save();
						break;
					case DialogResult.Cancel:
						e.Cancel = true;
						break;
					case DialogResult.No:
						break;
				}
			}
		}
		//-------------------------------------------------------------------------------
		void NewToolStripMenuItemClick(object sender, EventArgs e) {
			InitApp();
		}
		//-------------------------------------------------------------------------------
		void SaveToolStripMenuItemClick(object sender, EventArgs e) {
			Save();
		}
		//-------------------------------------------------------------------------------
		void SaveAsToolStripMenuItemClick(object sender, EventArgs e) {
			SaveAs();
		}
		//-------------------------------------------------------------------------------
		void PrintToolStripMenuItemClick(object sender, EventArgs e) {
		}
		//-------------------------------------------------------------------------------
		void PrintPreviewToolStripMenuItemClick(object sender, EventArgs e) {
		}
		//-------------------------------------------------------------------------------
		void CustomizeToolStripMenuItemClick(object sender, EventArgs e) {
		}
		//-------------------------------------------------------------------------------
		void OptionsToolStripMenuItemClick(object sender, EventArgs e) {
		}
		//-------------------------------------------------------------------------------
		void ExitToolStripMenuItemClick(object sender, EventArgs e) {
			Close();
		}
		//-------------------------------------------------------------------------------
		void CopyToolStripMenuItemClick(object sender, EventArgs e) {
			if (textBoxInput.SelectionLength > 0)
				Clipboard.SetText(textBoxInput.SelectedText);
		}
		//-------------------------------------------------------------------------------
		void SelectAllToolStripMenuItemClick(object sender, EventArgs e)
		{
			textBoxInput.SelectAll();
		}
		//-------------------------------------------------------------------------------
		void AboutToolStripMenuItemClick(object sender, EventArgs e) {
			ShowFormAbout();
		}
		//-------------------------------------------------------------------------------
		void MainFormOnBackgroundGradientColorChange(object sender, EventArgs e) {
		}
		//-------------------------------------------------------------------------------
		void TextBoxInputTextChanged(object sender, EventArgs e) {
			//IsDocumentDirty = true;
		}
		
		#endregion Event Handlers
		//-------------------------------------------------------------------------------
		#region Properties
		
		internal MachineState State {
			get {
				return MachineStateCurrent;
			}
			set {
				MachineStateCurrent = value;
			}
		}
		//-------------------------------------------------------------------------------
		bool IsDocumentDirty {
			get {
				return boolIsDocumentDirty;
			}
			set {
				if(value != boolIsDocumentDirty) {
					boolIsDocumentDirty = value;
					UpdateTitle();
				}
			}
		}
		
		#endregion Properties
	}
}
