using CliWrap;
using CommunityToolkit.WinUI.Notifications;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using PS3IsoLauncher;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Utils;

internal class Program
{
	[DllImport("kernel32.dll")]
	private static extern bool AllocConsole();

	[DllImport("kernel32.dll")]
	private static extern bool FreeConsole();

	[DllImport("shell32.dll", SetLastError = true)]
	static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

	public static string PathRPCS = "";

	public static string PathGame = "";

	public static string PathBackup = "";

	public static string PathRap = "";

	private static void Main(string[] args)
	{
		/*
		bool isAdminFA = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
		if (!isAdminFA)
		{
			List<string> fakeArgs = new List<string>();
			args = fakeArgs.ToArray();
		}
		*/


		if (args.Length == 0)
		{
			if (!IsAppRegistered())
			{
				string exePath = Process.GetCurrentProcess().MainModule.FileName;
				string exeDir = Path.GetDirectoryName(exePath);
				System.Diagnostics.ProcessStartInfo StartInfo = new System.Diagnostics.ProcessStartInfo
				{
					UseShellExecute = true, //<- for elevation
					Verb = "runas",  //<- for elevation
					WorkingDirectory = exeDir,
					FileName = exePath,
					Arguments = "--register"
				};
				System.Diagnostics.Process p = System.Diagnostics.Process.Start(StartInfo);

				return;
			}
			else
			{
				string exePath = Process.GetCurrentProcess().MainModule.FileName;
				string exeDir = Path.GetDirectoryName(exePath);

				System.Diagnostics.ProcessStartInfo StartInfo = new System.Diagnostics.ProcessStartInfo
				{
					UseShellExecute = true, //<- for elevation
					Verb = "runas",  //<- for elevation
					WorkingDirectory = exeDir,
					FileName = exePath,
					Arguments = "--unregister"
				};
				System.Diagnostics.Process p = System.Diagnostics.Process.Start(StartInfo);
				return;
			}
		}

		if (args.Length == 1 && args[0] == "--register")
		{
			bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
			if (isAdmin)
			{
				var r = new RegisteryManager(Process.GetCurrentProcess().MainModule.FileName);
				r.FixRegistery();

				string exePath = Process.GetCurrentProcess().MainModule.FileName;
				if (CheckTaskExist("IsoEnablerMount")) DeleteTask("IsoEnablerMount");
				RegisterTask("IsoEnablerMount", exePath, "--mountvhdx");
				if (CheckTaskExist("IsoEnablerUnmount")) DeleteTask("IsoEnablerUnmount");
				RegisterTask("IsoEnablerUnmount", exePath, "--unmountvhdx");

				SendNotification("IsoEnablerForRPCS3 Register", "rpcs3 will now accept iso");

				string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IsoEnabler");
				if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);

			}
			return;
		}

		if (args.Length == 1 && args[0] == "--unregister")
		{
			bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
			if (isAdmin)
			{
				var r = new RegisteryManager(Process.GetCurrentProcess().MainModule.FileName);
				r.DeleteAllDebuggerKeys();

				if (CheckTaskExist("IsoEnablerMount")) DeleteTask("IsoEnablerMount");
				if (CheckTaskExist("IsoEnablerUnmount")) DeleteTask("IsoEnablerUnmount");

				SendNotification("IsoEnablerForRPCS3 UnRegister", "IsoEnabler is unregistred");
			}
			return;
		}

		if (args.Length == 1 && args[0] == "--mountvhdx")
		{
			bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
			if (isAdmin)
			{
				VHDXTool.TaskMount();
			}
			return;
		}

		if (args.Length == 1 && args[0] == "--unmountvhdx")
		{
			bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
			if (isAdmin)
			{
				VHDXTool.TaskUnmount();
			}
			return;
		}

		if (args.Length >= 1 && args[0].ToLower().EndsWith("rpcs3.exe"))
		{
			bool generatevhdx = false;
			bool mountvhdxasreadonly = false;
			string isopath = "";
			string vhdxpath = "";

			PathRPCS = Path.GetFullPath(args[0]);
			PathGame = Path.GetDirectoryName(PathRPCS);
			PathGame = Path.Combine(PathGame, "dev_hdd0", "game");
			PathGame = Path.GetFullPath(PathGame);
			PathBackup = Path.Combine(Path.GetDirectoryName(PathRPCS), "GameBackup");
			PathRap = Path.Combine(Path.GetDirectoryName(PathRPCS), "dev_hdd0", "home", "00000001", "exdata");

			if (!Directory.Exists(PathGame)) Directory.CreateDirectory(PathGame);
			if (!Directory.Exists(PathRap)) Directory.CreateDirectory(PathRap);


			VHDXTool.CleanJunctions(PathGame);

			bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
			if (isAdmin && args.Length == 1)
			{

				SendNotification("IsoEnablerForRPCS3 VHDXGenerator"
					, $"RPCS3 is started as admin \n" +
					$"VHDX GENERATOR IS ENABLED !\n" +
					$"If you install pkg they will be deleted once the game close\n" +
					$"And a vhdx file will be generated");


				//SendNotification("ADMIN", "MODE CREATE VHDX ON !");
				//Thread.Sleep(20000);
				VHDXTool.CreateBackupGameDir(PathGame, PathBackup);
				generatevhdx = true;
				Thread.Sleep(1000);
			}

			List<string> filteredArgs = new List<string>();
			foreach (string arg in args)
			{
				bool hideThisArg = false;
				if (arg.ToLower().EndsWith(".iso"))
				{
					if (File.Exists(arg))
					{
						isopath = Path.GetFullPath(arg);
					}
				}
				if (arg.ToLower().EndsWith(".vhdx"))
				{
					if (File.Exists(arg))
					{
						vhdxpath = Path.GetFullPath(arg);
					}
				}
				if (arg.ToLower() == "--readonly")
				{
					hideThisArg = true;
					mountvhdxasreadonly = true;
				}

				if (!hideThisArg) filteredArgs.Add(arg);
			}
			args = filteredArgs.ToArray();

			if (isopath != "")
			{
				var targetProcess = Process.GetProcessesByName("rpcs3").FirstOrDefault(p => p.MainWindowTitle != "");
				if (targetProcess != null)
				{
					if (args[0].ToLower() == targetProcess.MainModule.FileName.ToLower())
					{
						SendNotification("IsoEnablerForRPCS3 ERROR", $"RPCS3 is already running");
						return;
					}
				}
				PS3Tool ps3tool = null;
				try
				{
					ps3tool = new PS3Tool(isopath);
				}
				catch (Exception ex)
				{
					SendNotification("IsoEnablerForRPCS3 ERROR", $"{ex.Message}");
				}

				if (ps3tool.Mount())
				{
					string ebootpath = Path.Combine(ps3tool.IsoMountDrive + ":\\", "PS3_GAME", "USRDIR", "EBOOT.BIN");
					string iconpath = Path.Combine(ps3tool.IsoMountDrive + ":\\", "PS3_GAME", "ICON0.PNG");
					if (File.Exists(ebootpath))
					{
						var arglist = new List<string>();
						foreach (string arg in args)
						{
							if (File.Exists(arg) && Path.GetFullPath(arg) == isopath) arglist.Add(ebootpath);
							else arglist.Add(arg);
						}

						SendNotification($"IsoEnablerForRPCS3 : {isopath}",
							$"TitleID = {ps3tool.TitleID}\n" +
							$"GameName = {ps3tool.TrueTitle}\n" +
							$"Firmware = {ps3tool.FirmwareVersion}\n" +
							$"AppVersion = {ps3tool.AppVersion}\n" +
							$"RPCS3 cmdLine = {ArgsToCommandLine(arglist.ToArray())}\n");

						var task = DirectLaunch(arglist.ToArray());
						task.Wait();

						if (!ps3tool.Umount())
						{
							SendNotification("IsoEnablerForRPCS3 ERROR", $"Error Unmounting {isopath}");
						}
						else
						{
							SendNotification("IsoEnablerForRPCS3", $"Unmounting {isopath}");
						}

					}
					else
					{
						SendNotification("IsoEnablerForRPCS3 ERROR", $"Can't find {ebootpath}");

					}
				}
				else
				{
					SendNotification("IsoEnablerForRPCS3 ERROR", $"Mount failed");
				}
				return;
			}


			if (vhdxpath != "")
			{
				VHDXTool vhdxtool = null;
				try
				{
					vhdxtool = new VHDXTool(vhdxpath);
				}
				catch (Exception ex)
				{
					SendNotification("IsoEnablerForRPCS3 ERROR", $"{ex.Message}");
				}

				var targetProcess = Process.GetProcessesByName("rpcs3").FirstOrDefault(p => p.MainWindowTitle != "");
				if (targetProcess != null)
				{
					if (args[0].ToLower() == targetProcess.MainModule.FileName.ToLower())
					{
						SendNotification("IsoEnablerForRPCS3 ERROR", $"RPCS3 is already running");
						return;
					}
				}

				//Thread.Sleep(10000);
				if (vhdxtool.Mount(mountvhdxasreadonly))
				{

					VHDXTool.CopyRap(PathRap, vhdxtool.IsoMountDrive + ":\\");
					string ebootpath = VHDXTool.FindEboot(vhdxtool.IsoMountDrive + ":\\");
					ebootpath = VHDXTool.LinkBackToGameDir(PathGame, vhdxtool.IsoMountDrive + ":\\", ebootpath);

					if (File.Exists(ebootpath) && ebootpath != "")
					{
						var arglist = new List<string>();
						foreach (string arg in args)
						{
							if (File.Exists(arg) && Path.GetFullPath(arg) == vhdxpath) arglist.Add(ebootpath);
							else arglist.Add(arg);
						}

						var task = DirectLaunch(arglist.ToArray());
						task.Wait();

						if (!vhdxtool.Umount())
						{
							SendNotification("IsoEnablerForRPCS3 ERROR", $"Error Unmounting {isopath}");
						}
						else
						{
							SendNotification("IsoEnablerForRPCS3", $"Unmounting {isopath}");
						}

					}
					else
					{
						SendNotification("IsoEnablerForRPCS3 ERROR", $"Can't find {ebootpath}");

					}
				}
				else
				{
					SendNotification("IsoEnablerForRPCS3 ERROR", $"Mount failed");
				}
				VHDXTool.CleanJunctions(PathGame);
				return;
			}

			if (isopath == "" && vhdxpath == "")
			{
				if (generatevhdx)
				{
					//SendNotification("IsoEnablerForRPCS3 DEBUG", $"{PathGame}");
					VHDXTool.EnableGameDirWatcher(PathGame, PathRap);
				}

				var task = DirectLaunch(args);
				task.Wait();

				var xxx = VHDXTool.GameRapChanged;
				if (generatevhdx && VHDXTool.GameDirChanged.Count() > 0)
				{
					AllocConsole();
					

					string TitreGenerate = "";
					TitreGenerate = "Generate VHDX From : \n";
					foreach(var gamedir in VHDXTool.GameDirChanged) { TitreGenerate += gamedir + "\n"; }
					foreach (var rapfile in VHDXTool.GameRapChanged) { TitreGenerate += rapfile + "\n"; }

					Console.WriteLine(TitreGenerate);

					SendNotification("IsoEnablerForRPCS3 VHDXGenerator", TitreGenerate);
					string outvhdx = Path.Combine(Path.GetDirectoryName(args[0]), "out.vhdx");

					if (File.Exists(outvhdx))
					{
						File.Delete(outvhdx);
					}
					try
					{
						VHDXTool.CreateVHDX(PathGame, outvhdx);

					}
					catch (Exception ex)
					{
						SendNotification("IsoEnablerForRPCS3 VHDXGenerator ERROR", $"{ex.Message}");
					}

					VHDXTool.RestoreDirFromBackup(PathGame, PathBackup);

					SendNotification("IsoEnablerForRPCS3 VHDXGenerator", $"VHDX Created on {outvhdx}");
					FreeConsole();
				}
				return;
			}

		}
	}



	public static void SendNotification(string title, string content)
	{
		ToastNotificationManagerCompat.History.Clear();
		var Toast = new ToastContentBuilder()
			.AddText(title)
			.AddText(content);

		Toast.SetProtocolActivation(new Uri(AppDomain.CurrentDomain.BaseDirectory + "dummy.txt"));
		Toast.Show((toast =>
		{
			toast.ExpirationTime = DateTime.Now.AddSeconds(5);
		}));

	}

	public static async System.Threading.Tasks.Task DirectLaunch(string[] args)
	{
		string JustRunExe = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "JustRun.exe");

		var ResultRPCS2 = await Cli.Wrap(JustRunExe)
			.WithArguments(args)
			.WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
			.WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardError()))
			.WithValidation(CommandResultValidation.None)
			.ExecuteAsync();
	}

	public static void RegisterExec()
	{
		string exePath = Process.GetCurrentProcess().MainModule.FileName;
		string exeDir = Path.GetDirectoryName(exePath);
		ProcessStartInfo startInfo = new ProcessStartInfo();
		startInfo.FileName = exePath;
		startInfo.Arguments = "--register";
		startInfo.WorkingDirectory = exeDir;
		startInfo.Verb = "runas";
		Process.Start(startInfo);
	}

	public static void UnregisterExec()
	{
		string exePath = Process.GetCurrentProcess().MainModule.FileName;
		string exeDir = Path.GetDirectoryName(exePath);
		ProcessStartInfo startInfo = new ProcessStartInfo();
		startInfo.FileName = exePath;
		startInfo.Arguments = "--unregister";
		startInfo.WorkingDirectory = exeDir;
		startInfo.Verb = "runas";
		Process.Start(startInfo);
	}

	public static bool IsAppRegistered()
	{
		string debuggerValue = CheckRegistryValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\rpcs3.exe", "Debugger");
		if (debuggerValue != null && debuggerValue == Process.GetCurrentProcess().MainModule.FileName) return true;

		return false;
	}
	static string CheckRegistryValue(string keyName, string valueName)
	{
		RegistryKey key = Registry.LocalMachine.OpenSubKey(keyName, false);

		if (key != null)
		{
			string value = (string)key.GetValue(valueName);
			if (value != null)
			{
				return value;
			}
		}

		return null;
	}



	public static string ArgsToCommandLine(string[] arguments)
	{
		var sb = new StringBuilder();
		foreach (string argument in arguments)
		{
			bool needsQuoting = argument.Any(c => char.IsWhiteSpace(c) || c == '\"');
			if (!needsQuoting)
			{
				sb.Append(argument);
			}
			else
			{
				sb.Append('\"');
				foreach (char c in argument)
				{
					int backslashes = 0;
					while (backslashes < argument.Length && argument[backslashes] == '\\')
					{
						backslashes++;
					}
					if (c == '\"')
					{
						sb.Append('\\', backslashes * 2 + 1);
						sb.Append(c);
					}
					else if (c == '\0')
					{
						sb.Append('\\', backslashes * 2);
						break;
					}
					else
					{
						sb.Append('\\', backslashes);
						sb.Append(c);
					}
				}
				sb.Append('\"');
			}
			sb.Append(' ');
		}
		return sb.ToString().TrimEnd();
	}

	public static bool CheckTaskExist(string taskName)
	{
		using (TaskService taskService = new TaskService())
		{
			if (taskService.GetTask(taskName) == null)
			{
				return false;
			}
			else
			{
				return true;
			}
		}
	}


	public static void RegisterTask(string taskName, string executable, string arguments)
	{

		var UsersRights = TaskLogonType.InteractiveToken;
		//UsersRights = TaskLogonType.S4U;
		using (TaskService ts = new TaskService())
		{
			TaskDefinition td = ts.NewTask();
			td.RegistrationInfo.Description = "Task as admin";
			td.Principal.RunLevel = TaskRunLevel.Highest;
			td.Principal.LogonType = UsersRights;
			// Create an action that will launch Notepad whenever the trigger fires
			td.Actions.Add(executable, arguments, null);
			// Register the task in the root folder
			ts.RootFolder.RegisterTaskDefinition(taskName, td, TaskCreation.CreateOrUpdate, Environment.GetEnvironmentVariable("USERNAME"), null, UsersRights, null);
		}

	}
	public static void DeleteTask(string taskName)
	{
		using (TaskService ts = new TaskService())
		{
			// Find the task in the root folder using its name
			var task = ts.FindTask(taskName);

			if (task != null)
			{
				ts.RootFolder.DeleteTask(taskName);
			}
		}
	}

	public static string[] CommandLineToArgs(string commandLine, bool addfakeexe = true)
	{
		string executableName;
		return CommandLineToArgs(commandLine, out executableName, addfakeexe);
	}
	public static string[] CommandLineToArgs(string commandLine, out string executableName, bool addfakeexe = true)
	{
		if (addfakeexe) commandLine = "test.exe " + commandLine;
		int argCount;
		IntPtr result;
		string arg;
		IntPtr pStr;
		result = CommandLineToArgvW(commandLine, out argCount);
		if (result == IntPtr.Zero)
		{
			throw new System.ComponentModel.Win32Exception();
		}
		else
		{
			try
			{
				// Jump to location 0*IntPtr.Size (in other words 0).  
				pStr = Marshal.ReadIntPtr(result, 0 * IntPtr.Size);
				executableName = Marshal.PtrToStringUni(pStr);
				// Ignore the first parameter because it is the application   
				// name which is not usually part of args in Managed code.   
				string[] args = new string[argCount - 1];
				for (int i = 0; i < args.Length; i++)
				{
					pStr = Marshal.ReadIntPtr(result, (i + 1) * IntPtr.Size);
					arg = Marshal.PtrToStringUni(pStr);
					args[i] = arg;
				}
				return args;
			}
			finally
			{
				Marshal.FreeHGlobal(result);
			}
		}
	}

	public static void ExecuteTask(string taskName, int delay = 2000)
	{
		string new_cmd = $@" /I /run /tn ""{taskName}""";
		var args = CommandLineToArgs(new_cmd, false);

		var TaskRun = System.Threading.Tasks.Task.Run(() =>
		Cli.Wrap("schtasks")
		.WithArguments(args)
		.WithValidation(CommandResultValidation.None)
		.ExecuteAsync()
		);
		TaskRun.Wait();


		TaskService ts = new TaskService();
		Microsoft.Win32.TaskScheduler.Task task = ts.GetTask(taskName);
		Microsoft.Win32.TaskScheduler.RunningTaskCollection instances = task.GetInstances();

		//Code a enlever si execution sans attente
		int nbrun = delay / 100;
		if (instances.Count == 0)
		{
			//MessageBox.Show("icil");
			instances = task.GetInstances();
			Thread.Sleep(100);
			int i = 0;
			while (instances.Count == 0)
			{
				i++;
				instances = task.GetInstances();
				Thread.Sleep(100);
				if (i > nbrun) break;
			}
		}
		while (instances.Count == 1)
		{
			instances = task.GetInstances();
			Thread.Sleep(100);
		}
	}

}