using CliWrap;
using CommunityToolkit.WinUI.Notifications;
using Microsoft.Win32;
using PS3IsoLauncher;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using Utils;

internal class Program
{
	private static void Main(string[] args)
	{

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
				SendNotification("IsoEnablerForRPCS3 Register", "rpcs3 will now accept iso");
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
				SendNotification("IsoEnablerForRPCS3 UnRegister", "IsoEnabler is unregistred");
			}
			return;
		}


		if (args.Length >= 1 && args[0].ToLower().EndsWith("rpcs3.exe"))
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


			string isopath = "";
			foreach (string arg in args)
			{
				if (arg.ToLower().EndsWith(".iso"))
				{
					if (File.Exists(arg))
					{
						isopath = arg;
					}
				}
			}

			if (isopath == "")
			{
				var task = DirectLaunch(args);
				task.Wait();
			}
			else
			{
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
							if (arg == isopath) arglist.Add(ebootpath);
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

	public static async Task DirectLaunch(string[] args)
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

}