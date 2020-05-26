<Query Kind="Program">
  <Namespace>System</Namespace>
  <Namespace>System.Reflection</Namespace>
  <Namespace>System.Security.Policy</Namespace>
</Query>

void Main()
{
	var sasm = System.Reflection.Assembly.LoadFile(@"Z:\Desktop\git\GhostPack\SharpSploit\SharpSploit\bin\Release\net40\SharpSploit.dll");
	var lTyp = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == "SharpSploit");
	lTyp.Dump();
	Type x = lTyp.GetType("SharpSploit.Enumeration.Host");

	// list methods
	MethodInfo[] methodInfo = x.GetMethods();
	Console.WriteLine($"The methods of the {x} class are:\n");
	foreach (MethodInfo temp in methodInfo)
	{
		Console.WriteLine(temp.Name);
	}

	// withexe not dll
	//sOut = lTyp.Assembly.EntryPoint.Invoke(null, new object[] { new string[] { "C:\\" } }).ToString();
	string sOut = null;
	try
	{
		sOut = x.Assembly.GetType("SharpSploit.Enumeration.Host").InvokeMember("GetDirectoryListing", BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Static, null, null, new object[] { "C:\\" }).ToString();
	}
	catch
	{
		sOut = x.Assembly.GetType("SharpSploit.Enumeration.Host").InvokeMember("GetDirectoryListing", BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Static, null, null, null).ToString();
	}
	sOut.Dump();

}
