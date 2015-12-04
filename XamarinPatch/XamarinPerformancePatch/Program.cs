using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace XamarinPatch
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			//var file2 = "../../Xamarin.Forms.Platform.Android.dll";
			//patchEnsureChildOrder (path: file2);

			patchEnsureChildOrder ();
			patchTryCatch ();
		}

		static void patchEnsureChildOrder ()
		{
			// patch to suppress EnsureChildOrder
			const string directory = @"../../../../../../Subversion";
			//const string directory = @"../../";
			string[] files = Directory.GetFiles (directory, "Xamarin.Forms.Platform.Android.dll", SearchOption.AllDirectories);
			foreach (string file in files) {
				Console.WriteLine (file);
				patchEnsureChildOrder (path: file);
			}
		}

		static void patchTryCatch ()
		{
			// patch with try catch
			const string directory = @"../../";// @"../../../../../";
			string[] files = Directory.GetFiles (directory, "Xamarin.Forms.Core.dll", SearchOption.AllDirectories);
			foreach (string file in files) {
				patchTryCatch (path: file, typeName: "Xamarin.Forms.AnimationExtensions", methodName: "HandleTweenerUpdated");
				patchTryCatch (path: file, typeName: "Xamarin.Forms.AnimationExtensions", methodName: "HandleTweenerFinished");
			}
		}

		static void patchEnsureChildOrder (string path)
		{
			var assembly = AssemblyDefinition.ReadAssembly (path);
			ModuleDefinition module = assembly.MainModule;
			TypeDefinition mainClass = module.GetType ("Xamarin.Forms.Platform.Android.VisualElementPackager");
			MethodDefinition method = mainClass.Methods.Single (m => m.Name == "EnsureChildOrder");

			FieldDefinition publicField = mainClass.Fields.FirstOrDefault (f => f.Name == "SuppressBringChildToFront");
			if (publicField == null) {
				publicField = new FieldDefinition ("SuppressBringChildToFront", FieldAttributes.Public | FieldAttributes.Static, mainClass.Module.Import (typeof(bool)));
				mainClass.Fields.Add (publicField);
			}

			var il = method.Body.GetILProcessor ();
			var instructions = method.Body.Instructions;
			Console.WriteLine ("first instruction: " + instructions.First ());
			var originalFirstInstruction = instructions.First ();
			if (instructions.First ().OpCode != OpCodes.Ldsfld) {
				var loadStaticField = il.Create (OpCodes.Ldsfld, publicField);
				var ifFalse = il.Create (OpCodes.Brfalse_S, originalFirstInstruction);
				var ret = il.Create (OpCodes.Ret);

				il.InsertBefore (originalFirstInstruction, ret);
				il.InsertBefore (ret, ifFalse);
				il.InsertBefore (ifFalse, loadStaticField);

				string pathPatched = path + ".patched.dll";
				assembly.Write (pathPatched);
				File.Copy (pathPatched, path, true);
				File.Delete (pathPatched);
			}
		}

		static void patchTryCatch (string path, string typeName, string methodName)
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
