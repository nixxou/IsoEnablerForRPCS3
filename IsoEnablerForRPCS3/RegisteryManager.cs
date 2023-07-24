using Microsoft.Win32;

namespace Utils
{
	public class RegisteryManager
	{
		public string BigBoxProfileExeFullPath { get; set; }

		public string BigBoxProfileExeWithoutDir = "";
		public RegisteryManager(string bigBoxProfileExeFullPath)
		{
			BigBoxProfileExeFullPath = bigBoxProfileExeFullPath;
			BigBoxProfileExeWithoutDir = Path.GetFileName(BigBoxProfileExeFullPath);
		}

		public List<string> GetRegisteryKeysWithDebugger(bool withExactExePath)
		{
			string searchValue = BigBoxProfileExeWithoutDir;
			if (withExactExePath) searchValue = BigBoxProfileExeFullPath;


			string keyName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\";
			string searchName = "Debugger";

			List<string> matchingKeys = new List<string>();

			using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyName))
			{
				if (key != null)
				{
					foreach (string subKeyName in key.GetSubKeyNames())
					{
						using (RegistryKey subKey = key.OpenSubKey(subKeyName))
						{
							if (subKey != null)
							{
								object value = subKey.GetValue(searchName);
								if (value != null && value.ToString().Contains(searchValue))
								{
									matchingKeys.Add(subKeyName);

								}
							}
						}
					}
				}
			}
			return matchingKeys;
		}


		public bool CheckIfActionIsNeeded()
		{
			var listeExe = new List<string>();
			listeExe.Add("rpcs3.exe");

			var listeReg = GetRegisteryKeysWithDebugger(true);
			var listeRegSouple = GetRegisteryKeysWithDebugger(false);

			foreach (var exe in listeExe)
			{
				bool found = false;
				foreach (var reg in listeReg)
				{
					if (reg.ToLower() == exe.ToLower())
					{
						found = true; break;
					}
				}
				if (!found) return true;
			}

			foreach (var reg in listeRegSouple)
			{
				bool found = false;
				foreach (var exe in listeExe)
				{
					if (reg.ToLower() == exe.ToLower())
					{
						found = true; break;
					}
				}
				if (!found) return true;
			}
			return false;
		}


		public void FixRegistery()
		{
			if (CheckIfActionIsNeeded())
			{

				var listeExe = new List<string>();
				listeExe.Add("rpcs3.exe");

				var listeReg = GetRegisteryKeysWithDebugger(true);
				var listeRegSouple = GetRegisteryKeysWithDebugger(false);

				foreach (var exe in listeExe)
				{
					bool found = false;
					foreach (var reg in listeReg)
					{
						if (reg.ToLower() == exe.ToLower())
						{
							found = true; break;
						}
					}
					if (!found) AddDebuggerKey(exe);
				}

				foreach (var reg in listeRegSouple)
				{
					bool found = false;
					foreach (var exe in listeExe)
					{
						if (reg.ToLower() == exe.ToLower())
						{
							found = true; break;
						}
					}
					if (!found) DeleteDebuggerKey(reg);
				}

			}

		}

		public void DeleteAllDebuggerKeys()
		{
			var listecle = GetRegisteryKeysWithDebugger(false);
			foreach (var cle in listecle)
			{
				DeleteDebuggerKey(cle);
			}
		}

		public void DeleteDebuggerKey(string cle)
		{
			string keyName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\" + cle;
			int valueCount = 0;
			bool isValid = false;
			using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyName, true))
			{
				if (key != null)
				{
					string[] valueNames = key.GetValueNames();
					if (valueNames.Length > 0)
					{
						valueCount = valueNames.Length;
					}

					object value = key.GetValue("Debugger");
					if (value != null && value.ToString().Contains(BigBoxProfileExeWithoutDir))
					{
						isValid = true;
						key.DeleteValue("Debugger");
					}
				}
			}
			if (isValid && valueCount == 1)
			{
				Registry.LocalMachine.DeleteSubKeyTree(keyName);
			}
		}

		public void AddDebuggerKey(string cle)
		{
			RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options", true);
			RegistryKey subkey = key.CreateSubKey(cle);

			subkey.SetValue("Debugger", BigBoxProfileExeFullPath);

		}

		public bool HasDebuggerKey(string cle)
		{
			string keyName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\" + cle;
			using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyName))
			{
				if (key != null)
				{
					object value = key.GetValue("Debugger");
					if (value != null && value.ToString().Contains(BigBoxProfileExeWithoutDir))
					{
						return true;
					}
				}
			}
			return false;
		}


	}
}
