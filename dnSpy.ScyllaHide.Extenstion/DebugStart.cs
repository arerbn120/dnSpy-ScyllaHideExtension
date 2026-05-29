using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using dnSpy.Contracts.Debugger;
using dnSpy.Contracts.Text;

namespace dnSpy.ScyllaHide
{
	[Export(typeof(IDbgManagerStartListener))]
	sealed class DebugStart : IDbgManagerStartListener
	{
		public static SynchronizationContext? MainContext;
		public static DbgManager? DbgManagerInstance;
		public static DebugStart? Instance;

		[Import]
		public ScyllaHideSettings ProgrammSettings { get; set; } = null!;

		public void OnStart(DbgManager dbgManager)
		{
			MyLogger.Instance.WriteLine(TextColor.Red, "Dbg Manager is OnStart");
			DbgManagerInstance = dbgManager;
			MainContext = SynchronizationContext.Current;
			Instance = this;

			dbgManager.IsRunningChanged += (sender, message) => { EventOnDbgManager("IsRunningChanged"); };
			dbgManager.IsDebuggingChanged += (sender, message) => { EventOnDbgManager("IsDebuggingChanged"); };
			dbgManager.DebugTagsChanged += (sender, message) => { EventOnDbgManager($"DebugTags {message.Objects?[0]}"); };
			dbgManager.DbgManagerMessage += (sender, message) => { EventOnDbgManager("DebugManagerMessage"); };
			dbgManager.CurrentRuntimeChanged += (sender, message) => { EventOnDbgManager("CurrentRuntimeChanged"); };
			dbgManager.ProcessesChanged += (sender, message) => { EventOnDbgManager("ProcessChanged"); };
			dbgManager.CurrentProcessChanged += (sender, message) => { EventOnDbgManager("CurrentProcessChanged"); };
			dbgManager.Message += (sender, args) => { MessageFromDbg(dbgManager, args); };
		}

		private void EventOnDbgManager(string text)
		{
			MyLogger.Instance.WriteLine(TextColor.Red, text);
		}

		private static int orderCounter;

		private static void MessageFromDbg(DbgManager dbgManager, DbgMessageEventArgs message)
		{
			MyLogger.Instance.WriteLine($"Message type: {message.Kind}");

			if (message.Kind == DbgMessageKind.ModuleLoaded)
			{
				if (message is DbgMessageModuleLoadedEventArgs moduleLoaded)
				{
					MyLogger.Instance.WriteLine($"ModuleLoaded: {moduleLoaded.Module.Filename}");
				}
			}

			if (Instance?.ProgrammSettings.IsEnabledOption != true)
				return;

			if (dbgManager.Processes.Length > 0)
			{
				foreach (var process in dbgManager.Processes)
				{
					int pid = process.Id;
					StartScyllaHide(pid, dbgManager, message);
					MyLogger.Instance.WriteLine(TextColor.Red, $"PointerSize = {process.PointerSize}");
				}
			}
		}

		private static void StartScyllaHide(int processId, DbgManager dbgManager, DbgMessageEventArgs message)
		{
			switch (message.Kind)
			{
				case DbgMessageKind.ProcessCreated:
					{
						string currentDirectory = Environment.CurrentDirectory;
						ScyllaHideInit(currentDirectory);
						MyLogger.Instance.WriteLine(TextColor.Red, "InitScyllaHide");

						if (message is DbgMessageProcessCreatedEventArgs processCreated)
						{
							ScyllaHideDebugLoop(1, processId, true, false);
							ScyllaHideDebugLoop(3, processId);
							MyLogger.Instance.WriteLine(TextColor.Red, $"PointerSize = {processCreated.Process.PointerSize}");
						}
						break;
					}

				case DbgMessageKind.ModuleLoaded:
					{
						if (message is DbgMessageModuleLoadedEventArgs moduleLoaded)
						{
							string filename = moduleLoaded.Module.Filename;
							if (filename.Contains(".dll", StringComparison.OrdinalIgnoreCase))
							{
								bool isNtDll = filename.Contains("ntdll.dll", StringComparison.OrdinalIgnoreCase);
								ScyllaHideDebugLoop(2, processId, false, isNtDll);
								MyLogger.Instance.WriteLine(TextColor.Red, "ScyllaHide DLL loaded");
							}
						}
						break;
					}

				case DbgMessageKind.BoundBreakpoint:
					{
						ScyllaHideDebugLoop(3, processId);
						MyLogger.Instance.WriteLine(TextColor.Red, "ScyllaHide Breakpoint");
						break;
					}

				default:
					{
						ScyllaHideDebugLoop(0, processId);
						MyLogger.Instance.WriteLine(TextColor.Red, "ScyllaHide Other Debug Message");
						break;
					}
			}
		}

		private static void InjectUsingProgram(ulong processId, string scyllaProgram, string dllPath)
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = scyllaProgram,
				Arguments = $"pid:{processId} {dllPath}",
				CreateNoWindow = true
			};

			var thread = new Thread(() =>
			{
				try
				{
					Process.Start(startInfo);
				}
				catch (Exception ex)
				{
					MyLogger.Instance.WriteLine(TextColor.Red, $"Error injecting: {ex.Message}");
				}
			});
			thread.Start();
		}

		// P/Invoke declarations for ScyllaHide
		[DllImport("ScyllaHideDnSpyPluginx64.dll", EntryPoint = "ScyllaHideInit", CallingConvention = CallingConvention.Cdecl)]
		private static extern void ScyllaHideInit([MarshalAs(UnmanagedType.LPWStr)] string directory);

		[DllImport("ScyllaHideDnSpyPluginx64.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern void ScyllaHideReset();

		[DllImport("ScyllaHideDnSpyPluginx64.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern void ScyllaHideDebugLoop(int debugEvent, int processId, [MarshalAs(UnmanagedType.Bool)] bool lpStartAddressIsNull = false, [MarshalAs(UnmanagedType.Bool)] bool lpBaseOfNtDll = false);
	}
}
