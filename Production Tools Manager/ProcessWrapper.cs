using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
//---------------------------------------------------------------------------------------
namespace MSZA.ProductionToolsManager
{
    public class ProcessWrapper : Process, IDisposable {
        public enum PipeType { StdOut, StdErr }
        public class Output {
            public string Message { get; set; }
            public PipeType Pipe { get; set; }
            public override string ToString() {
            	return string.Format("{0}: {1}", Pipe, Message);
            }
        }
		//-------------------------------------------------------------------------------
        private bool _hidden;
        private bool _isDisposed;
        private readonly string _args;
        private readonly string _command;
        private readonly ProcessStartInfo _startInfo = null;
        private readonly Queue<Output> _outputQueue = new Queue<Output>();
        private readonly ManualResetEvent[] _waitHandles = new ManualResetEvent[2];
        private readonly ManualResetEvent _outputSteamWaitHandle = new ManualResetEvent(false);
		//-------------------------------------------------------------------------------
        public ProcessWrapper(string startCommand, string args, bool hidden = false) {
            _command = startCommand;
            _hidden = hidden;
            _args = args;
            
            _startInfo = new ProcessStartInfo(_command, _args);
            _startInfo.UseShellExecute = false;
            _startInfo.RedirectStandardError = true;
            _startInfo.RedirectStandardInput = true;
            _startInfo.RedirectStandardOutput = true;
            if(_hidden) {
            	_startInfo.CreateNoWindow = true;
            	_startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
            StartInfo = _startInfo;
        }
		//-------------------------------------------------------------------------------
        public IEnumerable<string> GetMessages() {
            while(!_isDisposed) {
                _outputSteamWaitHandle.WaitOne();
                if(_outputQueue.Any())
                    yield return _outputQueue.Dequeue().ToString();
            }
        }
		//-------------------------------------------------------------------------------
        public void SendCommand(string command) {
            StandardInput.Write(command);
            StandardInput.Flush();
        }
		//-------------------------------------------------------------------------------
        public new int Start() {
            OutputDataReceived += delegate(object sender, DataReceivedEventArgs args){
                if (args.Data == null) {
                    _waitHandles[0].Set();
                } else if (args.Data.Length > 0) {
                    _outputQueue.Enqueue(
        				new Output {
        					Message = args.Data, Pipe = PipeType.StdOut
        				}
        			);
                    _outputSteamWaitHandle.Set();
                }
            };

            ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args) {
                if (args.Data == null) {
                    _waitHandles[1].Set();
                } else if (args.Data.Length > 0) {
                    _outputSteamWaitHandle.Set();
                    _outputQueue.Enqueue(new Output { Message = args.Data, Pipe = PipeType.StdErr });
                }
            };
            
            base.Start();
            _waitHandles[0] = new ManualResetEvent(false);
            BeginErrorReadLine();
            _waitHandles[1] = new ManualResetEvent(false);
            BeginOutputReadLine();
            
            return Id;
        }
		//-------------------------------------------------------------------------------
        public new void Dispose() {
            StandardInput.Flush();
            StandardInput.Close();
            
            if (!WaitForExit(1000))
                Kill();
            
            if (WaitForExit(1000))
                WaitHandle.WaitAll(_waitHandles);

            base.Dispose();
            _isDisposed = true;
        }
    }
}
