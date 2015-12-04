using System;
using Mono.Cecil;
using System.Linq;
using Mono.Cecil.Cil;
using System.IO;

namespace XamarinPatch
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			const string directory = @"../../";// @"../../../../../";
			string[] files = Directory.GetFiles (directory, "Xamarin.Forms.Core.dll", SearchOption.AllDirectories);
			foreach (string file in files) {
				patchXamarinFormsCoreDll (path: file, typeName: "Xamarin.Forms.AnimationExtensions", methodName: "HandleTweenerUpdated");
				patchXamarinFormsCoreDll (path: file, typeName: "Xamarin.Forms.AnimationExtensions", methodName: "HandleTweenerFinished");
			}
		}

		static void patchXamarinFormsCoreDll (string path, string typeName, string methodName)
		{
			var assembly = AssemblyDefinition.ReadAssembly (path);
			ModuleDefinition module = assembly.MainModule;
			TypeDefinition mainClass = module.GetType (typeName);
			MethodDefinition method = mainClass.Methods.Single (m => m.Name == methodName);

			var printPath = Path.GetFileName (path.Replace ("\\", "/").Replace ("../", ""));
			Console.WriteLine (string.Format ("Patch {0}.dll: {1}: {2}", printPath, methodName, method.Body.ExceptionHandlers.Count > 0 ? "already done" : "patch now"));
			if (method.Body.ExceptionHandlers.Count == 0) {

				var il = method.Body.GetILProcessor ();

				var write = il.Create (OpCodes.Call, module.Import (typeof(Console).GetMethod ("WriteLine", new [] { typeof(object) })));
				var ret = il.Create (OpCodes.Ret);
				var leave = il.Create (OpCodes.Leave, ret);

				il.InsertAfter (method.Body.Instructions.Last (), write);
				il.InsertAfter (write, leave);
				il.InsertAfter (leave, ret);

				var handler = new ExceptionHandler (ExceptionHandlerType.Catch) {
					TryStart = method.Body.Instructions.First (),
					TryEnd = write,
					HandlerStart = write,
					HandlerEnd = ret,
					CatchType = module.Import (typeof(Exception)),
				};

				method.Body.ExceptionHandlers.Add (handler);

				string pathPatched = path + ".patched.dll";
				assembly.Write (pathPatched);
				File.Copy (pathPatched, path, true);
				File.Delete (pathPatched);
			}
		}
	}
}
