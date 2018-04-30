using System;
using System.Linq;
using System.Collections.Generic;

using Xamarin.Android.Tools.Bytecode;

namespace JavaApiDiff
{
	class MainClass
	{
		static HashSet<string> methodBodyBlacklist = new HashSet<string> { "<clinit>", "hashCode", "toString", "valueOf", "<init>" };

		public static void Main (string [] args)
		{
			bool skipChanged = false;

			if (args.Length > 2) {
				if (args[0] == "--no-changed") {
					skipChanged = true;
					args = args.Skip (1).ToArray ();
				}
			}

			if (args.Length != 2) {
				PrintUsage ();
				return;
			}

			var jar1 = new ClassPath (args [0]);
			var jar2 = new ClassPath (args [1]);

			var packages1 = jar1.GetPackages ();
			var packages2 = jar2.GetPackages ();

			var commonPackages = HashSetDiffing (packages1, packages2, p => p.Key, (rt, items) => ReportConsole ("packages", rt, items));
			foreach (var package in commonPackages) {
				var commonTypes = HashSetDiffing (package.Item1.Value, package.Item2.Value,
				                                  c => c.ThisClass.Name.Value,
				                                  (rt, items) => ReportConsole ("classes in package " + package.Item1.Key, rt, items));
				foreach (var type in commonTypes) {
					Func<MethodInfo, bool> noSpecialMethods = mn => mn.Name.IndexOf ("$", StringComparison.Ordinal) == -1;
					var commonMethods = HashSetDiffing (type.Item1.Methods.Where (noSpecialMethods),
					                                    type.Item2.Methods.Where (noSpecialMethods),
					                                    m => m.Name + " :: " + m.Descriptor,
					                                    (rt, items) => ReportConsole ("methods in class " + type.Item1.ThisClass.Name.Value, rt, items));
					
					if (type.Item1.IsEnum)
						continue;

					if (!skipChanged) {
						var differingMethods = GetMethodsWithDifferingBodies (commonMethods).ToList ();
						if (differingMethods.Any ())
							ReportConsole ("methods in type " + type.Item1.ThisClass.Name.Value, ReportType.Changed, differingMethods);
					}
				}
			}
		}

		static IEnumerable<Tuple<T, T>> HashSetDiffing<T, TId> (IEnumerable<T> collection1, IEnumerable<T> collection2, Func<T, TId> keyExtractor, Action<ReportType, IEnumerable<TId>> reportAction)
		{
			var items1 = collection1.ToLookup (keyExtractor);
			var items2 = collection2.ToLookup (keyExtractor);
			var keys1 = items1.Select (i => i.Key);
			var keys2 = items2.Select (i => i.Key);

			var commonItems = new HashSet<TId> (keys1);
			commonItems.IntersectWith (keys2);

			var missingItems = new HashSet<TId> (keys1);
			missingItems.ExceptWith (keys2);
			if (missingItems.Any ())
				reportAction (ReportType.Removed, missingItems);

			var addedItems = new HashSet<TId> (keys2);
			addedItems.ExceptWith (keys1);
			if (addedItems.Any ())
				reportAction (ReportType.Added, addedItems);

			return commonItems.Select (ci => Tuple.Create (items1[ci].First (), items2[ci].First ()));
		}

		static IEnumerable<string> GetMethodsWithDifferingBodies (IEnumerable<Tuple<MethodInfo, MethodInfo>> methodPairs)
		{
			foreach (var metPair in methodPairs) {
				if (methodBodyBlacklist.Contains (metPair.Item1.Name))
					continue;
				if (metPair.Item1.Name.StartsWith ("lambda$", StringComparison.Ordinal))
					continue;
				var body1 = metPair.Item1.Attributes.OfType<CodeAttribute> ().FirstOrDefault ();
				var body2 = metPair.Item2.Attributes.OfType<CodeAttribute> ().FirstOrDefault ();

				if (body1 == null && body2 == null)
					continue;

				if ((body1 == null ^ body2 == null) || body1.Code.Length != body2.Code.Length || !Enumerable.SequenceEqual (body1.Code, body2.Code)) {
					var methodName = metPair.Item1.Name + " :: " + metPair.Item1.Descriptor;
					yield return methodName;
				}
			}
		}

		static void ReportConsole<T> (string itemName, ReportType reportType, IEnumerable<T> items)
		{
			Console.ForegroundColor = GetColorForReportType (reportType);
			Console.WriteLine ("{0} {1}:", reportType.ToString (), itemName);
			Console.ResetColor ();
			foreach (var i in items) {
				Console.Write ("\t");
				Console.WriteLine (i.ToString ());
			}
			Console.WriteLine ();
		}

		static ConsoleColor GetColorForReportType (ReportType rt)
		{
			switch (rt) {
			case ReportType.Added:
				return ConsoleColor.Green;
			case ReportType.Removed:
				return ConsoleColor.Red;
			default:
				return ConsoleColor.Cyan;
			}
		}

		static void PrintUsage ()
		{
			Console.WriteLine ("Usage: mono JavaApiDiff [--no-changed] jar-file-1 jar-file-2");
		}
	}

	enum ReportType
	{
		Added,
		Removed,
		Changed
	}
}
