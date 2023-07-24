using DiscUtils.Iso9660;
using PS3ISORebuilder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PS3IsoLauncher
{
	public class PS3Status
	{
		public bool Online = false;
		public bool GameRun = false;
		public bool Mounted = false;
	}
	public class PS3Tool
	{
		public string IsoFilePath { get; set; }

		public string TrueTitle { get; private set; }
		public string TitleID { get; private set; }
		public double AppVersion { get; private set; }
		public double FirmwareVersion { get; private set; }

		public bool HavePkgDir { get; private set; }

		public SortedDictionary<string, long> pkgList { get; private set; } = new SortedDictionary<string, long>();
		public SortedDictionary<string, long> dlcList { get; private set; } = new SortedDictionary<string, long>();
		public SortedDictionary<string, long> rapList { get; private set; } = new SortedDictionary<string, long>();


		private Dictionary<string, long> _fileList = new Dictionary<string, long>();
		private PARAM_SFO _paramSfo;

		public string PKGDIR { get; private set; } = "UPDATES_AND_DLC";

		public char IsoMountDrive { get; set; }

		public PS3Tool(string isoFilePath)
		{
			IsoFilePath = isoFilePath;
			IsoMountDrive = GetIsoMountDrive();
			if (IsoMountDrive != '\0') Umount();
			try
			{
				using (FileStream fileStream = File.Open(IsoFilePath, FileMode.Open)) { }
			}
			catch (IOException)
			{
				throw new Exception($"The file {IsoFilePath} can't be open");
			}
			if (!IsPS3Iso()) throw new Exception("Invalid PS3 Iso");
		}

		public char GetIsoMountDrive()
		{
			string resultat = "";
			Task.Run(async () => { resultat = await ExecuteProcess($"$drive = (Get-DiskImage \"{IsoFilePath}\" | Get-Volume).DriveLetter;echo $drive"); }).Wait();
			string driveLetterString = resultat.Trim('\n').Trim('\r').Trim();
			if (driveLetterString.Length == 1) return driveLetterString.ToCharArray()[0];
			else return '\0';
		}

		public bool Mount()
		{
			string resultat = "";
			Task.Run(async () => { resultat = await ExecuteProcess($"Mount-DiskImage \"{IsoFilePath}\""); }).Wait();
			Thread.Sleep(500);
			IsoMountDrive = GetIsoMountDrive();
			if (IsoMountDrive == '\0') return false;
			else return true;
		}

		public bool Umount()
		{
			string resultat = "";
			Task.Run(async () => { resultat = await ExecuteProcess($"Dismount-DiskImage \"{IsoFilePath}\""); }).Wait();
			Thread.Sleep(500);
			IsoMountDrive = GetIsoMountDrive();
			if (IsoMountDrive == '\0') return true;
			else return false;
		}

		/*
		private Dictionary<string, long> GetFileData()
		{
			Dictionary<string, long> fileListResult = new Dictionary<string, long>();
			Dictionary<string, long> pkgTmpList = new Dictionary<string, long>();
			using (FileStream fs = System.IO.File.Open(IsoFilePath, FileMode.Open))
			{
				PS3ISORebuilder.IRDFile.ISO cd = new PS3ISORebuilder.IRDFile.ISO(fs);
				foreach (var f in cd.filelist)
				{
					var fullname = f.Value.entrypath.TrimStart('\\');
					fileListResult.Add(fullname, (long)f.Value.Length);
					if (f.Value.entrypath.StartsWith(@"\" + PKGDIR))
					{
						var extension = Path.GetExtension(fullname).ToLower();
						if (extension == ".rap")
						{
							rapList.Add(fullname, (long)f.Value.Length);
						}
						if (extension == ".pkg")
						{
							pkgTmpList.Add(fullname, (long)f.Value.Length);
						}
					}
				}
			}
			foreach (var pkg in pkgTmpList)
			{
				bool isDlc = false;
				foreach (var rap in rapList)
				{
					if (pkg.Key.ToLower().Contains(Path.GetFileNameWithoutExtension(rap.Key).ToLower()))
					{
						isDlc = true;
						break;
					}
				}
				if (Path.GetFileName(pkg.Key).ToLower().StartsWith("DLC--"))
				{
					isDlc = true;
				}
				if (isDlc)
				{
					dlcList.Add(pkg.Key, pkg.Value);
				}
				else
				{
					pkgList.Add(pkg.Key, pkg.Value);
				}
			}

			return fileListResult;
		}
		*/

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

		private bool IsPS3Iso()
		{
			if(IsoMountDrive == '\0')
			{
				CDBuilder builder = new CDBuilder();
				using (FileStream fs = System.IO.File.Open(IsoFilePath, FileMode.Open))
				{
					CDReader cd = new CDReader(fs, true, true);
					var ParamFile = cd.GetFileInfo("PS3_GAME\\PARAM.SFO");
					if (ParamFile.Exists)
					{
						try
						{
							_paramSfo = new PARAM_SFO(ParamFile.Open(FileMode.Open));
							TrueTitle = _paramSfo.Title.Replace("\r", "").Replace("\n", "").Replace("\r\n", "");
							TitleID = _paramSfo.TitleID.ToUpper();
							var firmwareVersionTxt = _paramSfo.Tables.SingleOrDefault(t => t.Name == "PS3_SYSTEM_VER").Value.TrimStart('0');
							AppVersion = TryParseVersion(_paramSfo.APP_VER);
							FirmwareVersion = TryParseVersion(firmwareVersionTxt);

						}
						catch
						{
							return false;
						}

						var ParamFileEboot = cd.GetFileInfo("PS3_GAME\\USRDIR\\EBOOT.BIN");
						if (ParamFileEboot.Exists)
						{
							return true;
						}
					}
				}
			}
			else
			{
				string ebootpath = Path.Combine(IsoMountDrive + ":\\", "PS3_GAME", "USRDIR", "EBOOT.BIN");
				string parampath = Path.Combine(IsoMountDrive + ":\\", "PS3_GAME", "PARAM.SFO");
				if(File.Exists(ebootpath) && File.Exists(parampath))
				{
					try
					{
						_paramSfo = new PARAM_SFO(parampath);
						TrueTitle = _paramSfo.Title.Replace("\r", "").Replace("\n", "").Replace("\r\n", "");
						TitleID = _paramSfo.TitleID.ToUpper();
						var firmwareVersionTxt = _paramSfo.Tables.SingleOrDefault(t => t.Name == "PS3_SYSTEM_VER").Value.TrimStart('0');
						AppVersion = TryParseVersion(_paramSfo.APP_VER);
						FirmwareVersion = TryParseVersion(firmwareVersionTxt);

					}
					catch
					{
						return false;
					}
					return true;
				}
			}


			return false;
		}




		private async Task<string> ExecuteProcess(string message, bool returnerror = false)
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
