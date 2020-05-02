using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SMPS2ASMv2 {
	static class Program {
		// print error info and exit
		public static void error(string v) {
			if (debug) dbWr.Flush();
			Console.WriteLine(v);
			Console.ReadKey();
			Environment.Exit(-1);
		}

		public static string folder, type = "";
		public static bool pause, debug;
		private static StreamWriter dbWr;

		// check file name (special)
		public static string chkfilext2(string data, bool ret) {
			string d = Directory.GetFiles(folder, data, SearchOption.AllDirectories).FirstOrDefault();
			return ret ? d : d != null ? null : "Input file '" + data + "' does not exist!";
		}

		// check file name
		public static string chkfilext(string data, bool ret) {
			string d = folder + "\\music\\" + data;
			return ret ? d : File.Exists(d) ? null : "Input file '" + d.Replace(folder, "") + "' does not exist!";
		}

		// check folder name
		public static string chkfolext(string data, bool ret) {
			if (!data.Contains('.')) {
				type = "";
				string d = folder +"\\SMPS\\"+ data + "\\smps2asm.smpss";
				return ret ? d : File.Exists(d) ? null : "Script file '" + d.Replace(folder, "") + "' does not exist!";

			} else {
				type = data.Split('.')[1];
				string d = folder + "\\SMPS\\" + data.Split('.')[0] + "\\smps2asm.smpss";
				return ret ? d : File.Exists(d) ? null : "Script file '" + d.Replace(folder, "") + "' does not exist!";
			}
		}

		// check project name
		public static string chkname(string data, bool ret) {
			return ret ? data : 
				data.Length == 0 ? "Label must contain at least a single character!" :
				Char.IsDigit(data.ElementAt(0)) ? "Label must not start with a digit!" : 
				new Regex("^[A-Za-z0-9_\\.]+$").IsMatch(data) ? null : "Label may only contain letters a-z, numbers, underscores and dots!";
		}

		public static string quitprg() {
			Environment.Exit(0);
			return null;
		}

		public static ConsoleColor quitcl() {
			return ConsoleColor.Black;
		}

		public static string pauseprg() {
			pause = !pause;
			return null;
		}

		public static ConsoleColor pausecl() {
			return pause ? ConsoleColor.DarkGray : ConsoleColor.Black;
		}

		public static string debugprg() {
			debug = !debug;
			return null;
		}

		public static ConsoleColor debugcl() {
			return debug ? ConsoleColor.DarkGray : ConsoleColor.Black;
		}

		// timer to determine how long the program took to convert
		public static Stopwatch timer;

		[STAThread]
		static void Main(string[] args) {
			Console.Title = "SMPS2ASM/NAT  Built: " + new FileInfo(Assembly.GetExecutingAssembly().Location).LastWriteTime.ToShortDateString() + " " + new FileInfo(Assembly.GetExecutingAssembly().Location).LastWriteTime.ToShortTimeString();

			// args[input file with ext, Sound driver name, label, extra: may be used by script]
			// get the exe folder
			string[] a = args;

			//check if we have a debug option
			opcheck:
			if (args.Length > 0 && args[0] == "-d") {
				args = args.Skip(1).ToArray();
				debug = true;
				goto opcheck;
			}

			//check if we have a pause option
			if (args.Length > 0 && args[0] == "-p") {
				args = args.Skip(1).ToArray();
				pause = true;
				goto opcheck;
			}

			//check if we have a type option
			if (args.Length > 1 && args[0] == "-t") {
				type = args[1];
				args = args.Skip(2).ToArray();
				goto opcheck;
			}

			//check if a script file was dragged in
			if (args.Length > 0) {
				if(File.Exists(args[0]) && args[0].EndsWith(".smpss")) {
					folder = Environment.CurrentDirectory;
					string script = args[0];
					args = args.Skip(1).ToArray();

					// check if all arguments are gotten
					if (args.Length < 2) {
						pause = true;
						args = ConsoleArguments.Get(args, new ArgHandler[] {
							new ArgHandler("Music file name with extension:", chkfilext2),
							new ArgHandler("Project name:", chkname), }, new ButtonHandler[]{
							new ButtonHandler(ConsoleKey.Escape, "Quit the program", quitprg, quitcl),
							new ButtonHandler(ConsoleKey.F1, "Pause program at the end", pauseprg, pausecl),
							new ButtonHandler(ConsoleKey.F2, "Print debug info", debugprg, debugcl),
						});

					} else {
						args[0] = chkfilext2(args[0], true);
						args[1] = chkname(args[1], true);
					}

					string[] ax = new string[1 + args.Length];
					ax[0] = args[0];
					ax[2] = args[1];
					ax[1] = script;

					for (int i = 3;i < ax.Length;i++)
						ax[i] = args[i - 1];

					args = ax;
					goto oops;
				}
			}

			// check if all arguments are gotten
			if (args.Length < 3) {
				folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Environment.CurrentDirectory), @""));
				pause = true;
				args = ConsoleArguments.Get(args, new ArgHandler[] {
					new ArgHandler("Music file name with extension:", chkfilext),
					new ArgHandler("Sound driver folder name:", chkfolext),
					new ArgHandler("Project name:", chkname), }, new ButtonHandler[]{
					new ButtonHandler(ConsoleKey.Escape, "Quit the program", quitprg, quitcl),
					new ButtonHandler(ConsoleKey.F1, "Pause program at the end", pauseprg, pausecl),
					new ButtonHandler(ConsoleKey.F2, "Print debug info", debugprg, debugcl),
				});

			} else {
				folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Environment.CurrentDirectory), @""));
				args[0] = chkfilext(args[0], true);
				args[1] = chkfolext(args[1], true);
				args[2] = chkname(args[2], true);
			}

			// time how long this will take
			oops:
			timer = new Stopwatch();
			timer.Start();
			
			// remove bin folder from path
			//		if (folder.EndsWith("\\bin") || folder.EndsWith("\\bin\\")) folder = folder.Substring(0, folder.LastIndexOf("\\"));

			// removes the extension of input file and adds .asm as the extension of output file
			string fileout;
			if (args[0].IndexOf(".", args[0].LastIndexOf("\\")) > 0) {
				fileout = args[0].Substring(0, args[0].LastIndexOf(".")) + ".asm";

			} else {
				fileout = args[0] + ".asm";
			}

			// init debugwriter and put in debug info
			if (debug) {
				string db;
				if (args[0].IndexOf(".", args[0].LastIndexOf("\\")) > 0) {
					db = args[0].Substring(0, args[0].LastIndexOf(".")) + ".smpsd";

				} else {
					db = args[0] + ".smpsd";
				}

				//init stream
				dbWr = new StreamWriter(db);

				// write info about args
				Debug("--; args=["+ string.Join(", ", a) +"]");
				Debug("--; filein=" + args[0]);
				Debug("--; fileout="+ fileout);
				Debug("--; folder=" + folder);
				Debug("--; script=" + args[1]);
				Debug("--; lable=" + args[2]);
				Debug("--; type=" + type);
			}

			// get new SMPS object
			ConvertSMPS cvt = new ConvertSMPS(args[0], fileout, args[2]);

			// get the file for smps2asm script
			S2AScript scr = new S2AScript(args[1], args.Skip(3).ToArray(), type);

			// print timer info
			long tra = timer.ElapsedMilliseconds;
			Console.WriteLine("Script translated! Took " + tra + " ms!");
			
			// restart timer
			timer.Reset();
			timer.Start();

			// do teh conversion
			cvt.Convert(scr);

			// print timer info
			long con = timer.ElapsedMilliseconds;
			Console.WriteLine("File converted! Took " + con + " ms!");

			// restart timer
			timer.Reset();
			timer.Start();

			// write teh file
			Output.DoIt(cvt);

			// print timer info
			long pot = timer.ElapsedMilliseconds;
			Console.WriteLine("File saved to disk! Took " + pot + " ms!");
			Console.WriteLine("Conversion done! Took " + (pot + tra + con) + " ms!");

			if (debug) {
				Debug(new string('-', 80));
				Debug("--; Time for Script " + tra + " ms");
				Debug("--; Time for Convert " + con + " ms");
				Debug("--; Time for Save " + pot + " ms");
				Debug("--; Time for Total " + (pot + tra + con) + " ms");
				dbWr.Flush();
			}
			if (pause) Console.ReadKey();
		}

		public static string toHexString(double res, int zeroes) {
			long ree;
			if (Parse.DoubleToLong(res, out ree))
				return toHexString(ree, zeroes);

			return null;
		}

		public static string toHexString(long res, int zeroes) {
			return "$" + string.Format("{0:x" + zeroes + "}", res).ToUpper();
		}

		public static string toBinaryString(long res, int zeroes) {
			return "%" + Convert.ToString(res, 2).PadLeft(zeroes);
		}

		// print debug info
		public static void Debug(string text) {
			dbWr.WriteLine(text);
		}

		public static void Debug(uint pos,string text) {
			dbWr.WriteLine(toHexString(pos, 4) + ": "+ text);
		}

		public static void Debug(uint lnum, uint tabs, string text) {
			dbWr.WriteLine(lnum.ToString("D4") + ": " + new string('\t', (int)tabs) + text);
		}

		public static void Debug(uint pos, uint lnum, string id, string text) {
			dbWr.WriteLine(toHexString(pos, 4) +' '+ id +' '+ lnum + ":   \t"+ text);
		}

		public static void Debug(uint pos, uint lnum, bool str, string text) {
			dbWr.WriteLine(toHexString(pos, 4) + ' ' + str + ' ' + lnum + ":   \t" + text);
		}
	}
	
	public class OffsetString {
		public uint? offset = 0;
		public uint length = 0;
		public string line;

		public OffsetString(uint? off, uint len, string line) {
			offset = off;
			this.line = line;
			length = len;
		}
	}
}
