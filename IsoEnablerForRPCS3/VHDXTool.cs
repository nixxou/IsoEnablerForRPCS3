using CreateMaps;
using DiscUtils.Iso9660;
using PS3ISORebuilder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Gaming.UI;

namespace PS3IsoLauncher
{

	public class VHDXTool
	{
		[DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
		//static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);
		static extern bool CreateHardLink(
string lpFileName,
string lpExistingFileName,
IntPtr lpSecurityAttributes
);

		public string IsoFilePath { get; set; }
		public char IsoMountDrive { get; set; }

		public static List<String> GameDirChanged { get; set; } = new List<String>();

		public VHDXTool(string isoFilePath)
		{
			IsoFilePath = isoFilePath;
			IsoMountDrive = GetIsoMountDrive();
			if (IsoMountDrive != '\0') Umount();
			//if (!IsPS3Iso()) throw new Exception("Invalid PS3 Iso");
		}

		public char GetIsoMountDrive()
		{
			string resultat = "";
			Task.Run(async () => { resultat = await ExecuteProcess($"$drive = (Get-Partition (Get-DiskImage -ImagePath \"{IsoFilePath}\").Number | Get-Volume).DriveLetter;echo $drive"); }).Wait();
			string driveLetterString = resultat.Trim('\n').Trim('\r').Trim();
			if (driveLetterString.Length == 1) return driveLetterString.ToCharArray()[0];
			else return '\0';
		}

		public bool Mount(bool AsReadOnly=false)
		{
			var listFreeDriveLetters = Enumerable.Range('A', 'Z' - 'A' + 1).Select(i => (Char)i + ":").Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
	
			string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IsoEnabler");
			string CmdMountFile = Path.Combine(ConfigDir, "mountcmd.txt");
			string FileToMount = IsoFilePath;
			if (AsReadOnly) FileToMount += ":ro";
			File.WriteAllText(CmdMountFile, IsoFilePath);

			Program.ExecuteTask("IsoEnablerMount");

			Thread.Sleep(1000);
			var listFreeDriveLetters2 = Enumerable.Range('A', 'Z' - 'A' + 1).Select(i => (Char)i + ":").Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();

			string found = "";
			foreach(var driveLetter in listFreeDriveLetters)
			{
				if (!listFreeDriveLetters2.Contains(driveLetter))
				{
					found = driveLetter;
					break;
				}
			}
			if (found != "") IsoMountDrive = found.ToCharArray()[0];
			else IsoMountDrive = '\0';

			if (IsoMountDrive == '\0') return false;
			else return true;
		}

		public bool Umount()
		{
			var listFreeDriveLetters = Enumerable.Range('A', 'Z' - 'A' + 1).Select(i => (Char)i + ":").Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
			string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IsoEnabler");
			string CmdMountFile = Path.Combine(ConfigDir, "unmountcmd.txt");
			File.WriteAllText(CmdMountFile, IsoFilePath);

			Program.ExecuteTask("IsoEnablerUnmount");

			Thread.Sleep(1000);
			var listFreeDriveLetters2 = Enumerable.Range('A', 'Z' - 'A' + 1).Select(i => (Char)i + ":").Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();

			string found = "";
			foreach (var driveLetter in listFreeDriveLetters2)
			{
				if (!listFreeDriveLetters.Contains(driveLetter))
				{
					found = driveLetter;
					break;
				}
			}
			if (found != "" && found.ToCharArray()[0] == IsoMountDrive) IsoMountDrive = '\0';

			if (IsoMountDrive == '\0') return true;
			else return false;
		}

		public static void EnableGameDirWatcher(string PathGame)
		{
			FileSystemWatcher watcher = new FileSystemWatcher();
			watcher.Path = PathGame;
			watcher.Created += new FileSystemEventHandler((sender, args) =>
			{
				string intermediateDirectory = VHDXTool.GetIntermediateDirectory(PathGame, args.FullPath);
				Console.WriteLine(intermediateDirectory);
				if (!GameDirChanged.Contains(intermediateDirectory))
				{
					GameDirChanged.Add(intermediateDirectory);
				}
			});
			watcher.EnableRaisingEvents = true;
			watcher.IncludeSubdirectories = true;
		}

		public static void RestoreDirFromBackup(string PathGame, string PathBackup)
		{
			foreach (var dir in GameDirChanged)
			{
				string PathDir = Path.Combine(PathGame, dir);
				if (Directory.Exists(PathDir)) VHDXTool.EmptyFolder(PathDir);
				if (Directory.Exists(PathDir)) Directory.Delete(PathDir);

				string ExistingData = Path.Combine(PathBackup, dir);
				if (Directory.Exists(ExistingData))
				{
					Directory.Move(ExistingData, PathDir);
				}
			}
		}

		public static void CleanJunctions(string PathGame)
		{
			var listDirSource = Directory.GetDirectories(PathGame);
			foreach (var dir in listDirSource)
			{
				string dirName = Path.GetFullPath(dir);
				if (IsDirectoryJunction(dirName))
				{
					try
					{
						JunctionPoint.Delete(dirName);
					}
					catch { }
				}
			}
		}

		private static bool IsDirectoryJunction(string folderPath)
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(folderPath);

			// Vérifier si l'attribut ReparsePoint est défini pour le dossier
			return (directoryInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
		}

		public static void TaskMount()
		{
			string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IsoEnabler");
			string CmdMountFile = Path.Combine(ConfigDir, "mountcmd.txt");
			if (File.Exists(CmdMountFile))
			{
				bool readOnlyMount = false;
				var fileToMount = File.ReadAllText(CmdMountFile);
				File.Delete(CmdMountFile);
				if (fileToMount.EndsWith(":ro"))
				{
					readOnlyMount = true;
					int lastIndex = fileToMount.LastIndexOf(":ro");
					if (lastIndex == fileToMount.Length - ":ro".Length)
					{
						fileToMount = fileToMount.Substring(0, lastIndex);
					}


				}
				if (File.Exists(fileToMount))
				{
					string resultat = "";
					System.Threading.Tasks.Task.Run(async () => { resultat = await VHDXTool.ExecuteProcess($"$drive = (Get-Partition (Get-DiskImage -ImagePath \"{fileToMount}\").Number | Get-Volume).DriveLetter;echo $drive"); }).Wait();
					string driveLetterString = resultat.Trim();
					if (driveLetterString.Length == 1)
					{
						System.Threading.Tasks.Task.Run(async () => { resultat = await VHDXTool.ExecuteProcess($"Dismount-VHD \"{fileToMount}\""); }).Wait();
						Thread.Sleep(2000);
					}


					if (readOnlyMount) System.Threading.Tasks.Task.Run(async () => { resultat = await VHDXTool.ExecuteProcess($"Mount-VHD -Path \"{fileToMount}\" -ReadOnly"); }).Wait();
					else System.Threading.Tasks.Task.Run(async () => { resultat = await VHDXTool.ExecuteProcess($"Mount-VHD -Path \"{fileToMount}\""); }).Wait();
				}
			}
		}

		public static void TaskUnmount()
		{
			string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IsoEnabler");
			string CmdMountFile = Path.Combine(ConfigDir, "unmountcmd.txt");
			if (File.Exists(CmdMountFile))
			{
				var fileToMount = File.ReadAllText(CmdMountFile);
				File.Delete(CmdMountFile);
				if (File.Exists(fileToMount))
				{
					string resultat = "";
					System.Threading.Tasks.Task.Run(async () => { resultat = await VHDXTool.ExecuteProcess($"Dismount-VHD \"{fileToMount}\""); }).Wait();
				}

			}
		}

		public static string LinkBackToGameDir(string GameDir, string SourceDir, string eboot)
		{
			bool areGameDirAvailiable = true;
			var listDirSource = Directory.GetDirectories(SourceDir);
			var filteredListDirSource = FilterDirectories(listDirSource);

			foreach (var dir in filteredListDirSource)
			{
				string dirName = Path.GetFileName(Path.GetFullPath(dir));
				string newDir = Path.Combine(GameDir, dirName);
				if (Directory.Exists(newDir)) areGameDirAvailiable = false;
			}

			if (areGameDirAvailiable)
			{
				foreach (var dir in filteredListDirSource)
				{
					string dirSource = Path.GetFullPath(dir);
					string dirName = Path.GetFileName(Path.GetFullPath(dir));
					string newDir = Path.Combine(GameDir, dirName);
					//Create Junction
					JunctionPoint.Create(newDir, dirSource, true);
				}
				if(!String.IsNullOrWhiteSpace(eboot) && File.Exists(eboot))
				{
					eboot = Path.GetFullPath(eboot);
					if (eboot.StartsWith(SourceDir))
					{
						eboot = eboot.Remove(0, SourceDir.Length);
						eboot.TrimStart('\\');
						string newEboot = Path.Combine(GameDir, eboot);
						if (File.Exists(newEboot))
						{
							eboot = newEboot;
						}

					}

				}

			}
			return eboot;
		}

		public static string FindEboot(string sourceDir, int id = 0)
		{
			sourceDir = Path.GetFullPath(sourceDir);
			string firstfile = "";
			string firstDir = "";
			var listedir = Directory.GetDirectories(sourceDir);
			var listDirFiltered = FilterDirectories(listedir);
			if(listDirFiltered.Count()>0 && !String.IsNullOrEmpty(listDirFiltered[0])) firstDir = listDirFiltered[0];

			if (!String.IsNullOrEmpty(firstDir))
			{
				if (File.Exists(Path.Combine(firstDir, "USRDIR", "EBOOT.BIN")))
				{
					firstfile = Path.Combine(firstDir, "USRDIR", "EBOOT.BIN");
				}
			}

			var files = Directory.GetFiles(sourceDir, "EBOOT.BIN",
			new EnumerationOptions
			{
				IgnoreInaccessible = true,
				RecurseSubdirectories = true,
			});
			List<string> listeboot = new List<string>();
			if(firstfile != "") listeboot.Add(firstfile);
			foreach (string file in files)
			{
				if(file != firstfile)
				{
					listeboot.Add(file);
				}
			}

			if (listeboot.Count() == 0) return "";

			if(listeboot.Count() >= id + 1)
			{
				return listeboot[id].ToString();
			}
			else return listeboot[id].ToString();

		}

		public static void CreateBackupGameDir(string PathGame, string PathBackup)
		{

			if (Directory.Exists(PathBackup))
			{
				if (Directory.Exists(PathBackup)) VHDXTool.EmptyFolder(PathBackup);
				if (Directory.Exists(PathBackup)) Directory.Delete(PathBackup);
			}
			Directory.CreateDirectory(PathBackup);

			var files = Directory.GetFiles(PathGame, "*",
			new EnumerationOptions
			{
				IgnoreInaccessible = true,
				RecurseSubdirectories = true,
				AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
			});
			foreach (string file in files)
			{
				string newfile = PathBackup + file.Remove(0, PathGame.Length);
				string newfiledir = Directory.GetParent(newfile).FullName;
				if (!Directory.Exists(newfiledir))
				{
					Directory.CreateDirectory(newfiledir);
				}
				VHDXTool.MakeLink(file, newfile);
			}
			var dirs = Directory.GetDirectories(PathGame, "*",
			new EnumerationOptions
			{
				IgnoreInaccessible = true,
				RecurseSubdirectories = true,
				AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
			});


			foreach (string dir in dirs)
			{
				string newfiledir = PathBackup + dir.Remove(0, PathGame.Length);
				if (!Directory.Exists(newfiledir))
				{
					Directory.CreateDirectory(newfiledir);
				}
			}
		}



		public static void MakeLink(string source, string target)
		{
			if (!File.Exists(source)) return;
			if (File.Exists(target)) return;

			CreateHardLink(target, source, IntPtr.Zero);
		}

		public static bool EmptyFolder(string pathName)
		{
			bool errors = false;
			DirectoryInfo dir = new DirectoryInfo(pathName);

			foreach (FileInfo fi in dir.EnumerateFiles())
			{
				try
				{
					fi.IsReadOnly = false;
					fi.Delete();

					//Wait for the item to disapear (avoid 'dir not empty' error).
					while (fi.Exists)
					{
						System.Threading.Thread.Sleep(10);
						fi.Refresh();
					}
				}
				catch (IOException e)
				{
					Debug.WriteLine(e.Message);
					errors = true;
				}
			}

			foreach (DirectoryInfo di in dir.EnumerateDirectories())
			{
				try
				{
					EmptyFolder(di.FullName);
					di.Delete();

					//Wait for the item to disapear (avoid 'dir not empty' error).
					while (di.Exists)
					{
						System.Threading.Thread.Sleep(10);
						di.Refresh();
					}
				}
				catch (IOException e)
				{
					Debug.WriteLine(e.Message);
					errors = true;
				}
			}

			return !errors;
		}
		public static void CreateVHDX(string rootPath, string target)
		{
			long totalSizeNeeded = 0;
			foreach (var dir in GameDirChanged)
			{
				var dirPath = Path.Combine(rootPath, dir);
				DirectoryInfo sourceD = new DirectoryInfo(dirPath);
				long sizeSource = DirSize(sourceD);
				long sizeDest = (long)Math.Round(((double)sizeSource * 1.5) / 1024 / 1024) + 30;
				totalSizeNeeded += sizeDest;
			}

			string resultat = "";
			var listFreeDriveLetters = Enumerable.Range('A', 'Z' - 'A' + 1).Select(i => (Char)i + ":").Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();


			Task.Run(async () => { resultat = await ExecuteProcess($"New-VHD -Path \"{target}\" -Dynamic -SizeBytes {totalSizeNeeded}MB"); }).Wait();

			if (!File.Exists(target)) throw new FileNotFoundException("error creating vhd");

			Task.Run(async () => { resultat = await ExecuteProcess($"Mount-VHD -Path \"{target}\""); }).Wait();

			Task.Run(async () => { resultat = await ExecuteProcess($"$disknum=(get-vhd -path \"{target}\").DiskNumber; echo $disknum"); }).Wait();
			resultat = resultat.Trim();
			int numdisk = -1;
			for (int i = 0; i < 10; i++)
			{
				Task.Run(async () => { resultat = await ExecuteProcess($"$disknum=(get-vhd -path \"{target}\").DiskNumber; echo $disknum"); }).Wait();
				resultat = resultat.Trim();
				if (int.TryParse(resultat, out numdisk))
				{
					break;
				}
				Thread.Sleep(1000);
			}
			if (numdisk <= 0) throw new Exception("Cant find mounted disk");

			Task.Run(async () => { resultat = await ExecuteProcess($"Initialize-Disk {numdisk}"); }).Wait();
			Thread.Sleep(1000);
			Task.Run(async () => { resultat = await ExecuteProcess($"New-Partition -AssignDriveLetter -UseMaximumSize -DiskNumber {numdisk}"); }).Wait();
			Thread.Sleep(1000);
			Task.Run(async () => { resultat = await ExecuteProcess($"$drive = (Get-Partition (Get-DiskImage -ImagePath \"{target}\").Number | Get-Volume).DriveLetter;echo $drive"); }).Wait();

			string driveLetterString = resultat.Trim();

			char driveLetter;
			if (listFreeDriveLetters.Contains(driveLetterString + ":"))
			{
				driveLetter = driveLetterString.ToCharArray()[0];
			}
			else
			{
				throw new Exception("Invalide drive letter");
				return;
			}
			Thread.Sleep(1000);

			Task.Run(async () => { resultat = await ExecuteProcess($"Format-Volume -FileSystem NTFS -DriveLetter {driveLetter}"); }).Wait();
			Thread.Sleep(1000);
			Task.Run(async () => { resultat = await ExecuteProcess($"Set-Volume -DriveLetter {driveLetter} -NewFileSystemLabel PSNGAME"); }).Wait();

			

			foreach (var dir in GameDirChanged)
			{
				string sourceDir = Path.Combine(rootPath, dir);
				string targetDir = Path.Combine(driveLetter + @":/", dir);
				Directory.CreateDirectory(targetDir);
				DirectoryCopy(sourceDir, targetDir, true);
			}

			SetDiskProperty(driveLetter.ToString(), true);
			Thread.Sleep(2000);

			Task.Run(async () => { resultat = await ExecuteProcess($"Dismount-VHD \"{target}\""); }).Wait();
			Thread.Sleep(1000);

		}

		public static void RunDiskPartScript(string scriptText)
		{
			string diskpartPath = Path.Combine(Environment.GetEnvironmentVariable("windir"), "System32", "diskpart.exe");

			if (!System.IO.File.Exists(diskpartPath))
				throw new Exception(String.Format("'{0}' doesn't exist.", diskpartPath));

			Debug.WriteLine("diskpartPath: " + diskpartPath);

			ProcessStartInfo psInfo = new ProcessStartInfo(diskpartPath);

			string scriptFilename = System.IO.Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location));
			Debug.WriteLine($"scriptFilename: '{scriptFilename}'");

			//save script
			System.IO.File.WriteAllText(scriptFilename, scriptText);

			psInfo.Arguments = $"/s {scriptFilename}";

			psInfo.CreateNoWindow = true;
			psInfo.RedirectStandardError = true; //redirect standard Error
			psInfo.RedirectStandardOutput = true; //redirect standard output
			psInfo.RedirectStandardInput = false;
			psInfo.UseShellExecute = false; //if True, uses 'ShellExecute'; if false, uses 'CreateProcess'
			psInfo.Verb = "runas"; //use elevated permissions
			psInfo.WindowStyle = ProcessWindowStyle.Hidden;

			//create new instance and set properties
			using (Process p = new Process() { EnableRaisingEvents = true, StartInfo = psInfo })
			{
				//subscribe to event and add event handler code
				p.ErrorDataReceived += (sender, e) =>
				{
					if (!String.IsNullOrEmpty(e.Data))
					{
						//ToDo: add desired code 
						Debug.WriteLine("Error: " + e.Data);
					}
				};

				//subscribe to event and add event handler code
				p.OutputDataReceived += (sender, e) =>
				{
					if (!String.IsNullOrEmpty(e.Data))
					{
						//ToDo: add desired code
						Debug.WriteLine("Output: " + e.Data);
					}
				};

				p.Start(); //start

				p.BeginErrorReadLine(); //begin async reading for standard error
				p.BeginOutputReadLine(); //begin async reading for standard output

				//waits until the process is finished before continuing
				p.WaitForExit();

				if (File.Exists(scriptFilename))
					File.Delete(scriptFilename); //delete file
			}
		}

		public static void SetDiskProperty(string driveLetter, bool isReadOnly)
		{
			//create diskpart script text
			StringBuilder sbScript = new StringBuilder();
			sbScript.AppendLine($"select volume {driveLetter}");

			if (isReadOnly)
				sbScript.AppendLine($"att vol set readonly");
			else
				sbScript.AppendLine($"att vol clear readonly");

			//Debug.WriteLine($"Script:\n'{sbScript.ToString()}'");

			//execute script
			RunDiskPartScript(sbScript.ToString());
		}

		public static void DirectoryCopy(string sourceDirName, string destDirName,
									  bool copySubDirs)
		{
			// Get the subdirectories for the specified directory.
			DirectoryInfo dir = new DirectoryInfo(sourceDirName);

			if (!dir.Exists)
			{
				throw new DirectoryNotFoundException(
					"Source directory does not exist or could not be found: "
					+ sourceDirName);
			}

			DirectoryInfo[] dirs = dir.GetDirectories();
			// If the destination directory doesn't exist, create it.
			if (!Directory.Exists(destDirName))
			{
				Directory.CreateDirectory(destDirName);
			}

			// Get the files in the directory and copy them to the new location.
			FileInfo[] files = dir.GetFiles();
			foreach (FileInfo file in files)
			{
				string temppath = Path.Combine(destDirName, file.Name);
				file.CopyTo(temppath, false);
			}

			// If copying subdirectories, copy them and their contents to new location.
			if (copySubDirs)
			{
				foreach (DirectoryInfo subdir in dirs)
				{
					string temppath = Path.Combine(destDirName, subdir.Name);
					DirectoryCopy(subdir.FullName, temppath, copySubDirs);
				}
			}
		}

		public static long DirSize(DirectoryInfo d)
		{
			long size = 0;
			// Add file sizes.
			FileInfo[] fis = d.GetFiles();
			foreach (FileInfo fi in fis)
			{
				size += fi.Length;
			}
			// Add subdirectory sizes.
			DirectoryInfo[] dis = d.GetDirectories();
			foreach (DirectoryInfo di in dis)
			{
				size += DirSize(di);
			}
			return size;
		}

		public static string GetIntermediateDirectory(string directoryRoot, string directoryElement)
		{
			// Obtenir le chemin relatif du répertoire élément par rapport au répertoire racine
			string relativePath = Path.GetRelativePath(directoryRoot, directoryElement);

			// Séparer les parties du chemin
			string[] pathParts = relativePath.Split(Path.DirectorySeparatorChar);

			// Le répertoire intermédiaire se trouve dans la première position (index 0) du tableau pathParts
			if (pathParts.Length >= 1)
			{
				return pathParts[0];
			}

			// Si le chemin ne contient pas suffisamment de parties pour obtenir le répertoire intermédiaire, retourner une chaîne vide ou null selon le cas.
			return string.Empty;
		}

		public static double TryParseVersion(string text)
		{
			double res = 0;
			if (String.IsNullOrEmpty(text)) return res;

			text = text.TrimStart('0');
			if (Double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out res))
			{
				return res;
			}
			return 0;
		}

		private static string[] FilterDirectories(string[] directories)
		{
			return directories.Where(dir =>
			{
				FileAttributes attributes = File.GetAttributes(dir);
				bool isSystem = (attributes & FileAttributes.Hidden) != 0 || (attributes & FileAttributes.System) != 0;
				bool containsLock = Path.GetFileName(dir).Contains("locks", StringComparison.OrdinalIgnoreCase);

				return !isSystem && !containsLock;
			}).ToArray();
		}

		public static async Task<string> ExecuteProcess(string message, bool returnerror = false)
		{
			message = message.Replace("\"", "\"\"\"");
			using (var app = new Process())
			{
				app.StartInfo.FileName = "powershell.exe";
				app.StartInfo.Arguments = message;
				app.EnableRaisingEvents = true;
				app.StartInfo.RedirectStandardOutput = true;
				app.StartInfo.RedirectStandardError = true;
				// Must not set true to execute PowerShell command
				app.StartInfo.UseShellExecute = false;

				app.StartInfo.CreateNoWindow = true;

				app.Start();

				using (var o = app.StandardError)
				{
					Console.WriteLine(await o.ReadToEndAsync());
				}

				if (returnerror)
				{
					using (var o = app.StandardError)
					{
						return await o.ReadToEndAsync();
					}
				}

				using (var o = app.StandardOutput)
				{
					return await o.ReadToEndAsync();
				}
			}
		}

	}
}
