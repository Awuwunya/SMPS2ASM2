using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SMPS2ASMv2 {
	public class ConsoleArguments {
		// functions to help with string manipulation
		private static string CutStr(string s, int start, int end) {
			return s.Substring(0, start) + s.Substring(end, s.Length - end);
		}

		private static string CopyStr(string s, int start, int end) {
			return s.Substring(start, end - start);
		}

		private static string PasteStr(string s, int off, string ch) {
			return s.Substring(0, off) + ch + s.Substring(off, s.Length - off);
		}

		private static bool ctrlc = false;

		[STAThread]
		public static string[] Get(string[] args, ArgHandler[] ah, ButtonHandler[] bh) {
			if(Program.timer != null) Program.timer.Stop();
			consoleHandler = new HandlerRoutine(ConsoleCtrlCheck);
			SetConsoleCtrlHandler(consoleHandler, true);
			string[] a = new string[ah.Length];

			// copy args to a
			if (args != null)
				for (int i = 0;i < args.Length;i++) {
					a[i] = args[i];
				}

			// intitialize rest of a
			for(int i = args != null ? args.Length : 0; i < a.Length;i++) {
				a[i] = "";
			}

			// init some variables
			int index = 0, cposs = 0, cpose = 0, txtlen = 0, btnlen = 8, linebase = Console.CursorTop;

			// find the longest argument string
			foreach(ArgHandler ar in ah)
				if(ar.text.Length > txtlen - 1)
					txtlen = ar.text.Length + 1;

			// find the longest button string
			foreach (ButtonHandler br in bh)
				if (br.key.ToString().Length > btnlen - 1)
					btnlen = br.key.ToString().Length + 1;

			redrawall: {
				// clear and setup colors
				Console.ForegroundColor = ConsoleColor.Gray;
				// write arg fields
				int cy = linebase;
				foreach (ArgHandler ar in ah)
					WriteAt(ar.text, 0, cy++);

				// write controls
				cy += 3;
				WriteAt("Controls:", 0, cy++);
				WriteAt(ConsoleColor.Black, "Up/Down", "Move cursor up and down", btnlen, 0, cy++);
				WriteAt(ConsoleColor.Black, "Enter", "Confirm input and continue to next screen", btnlen, 0, cy++);
				foreach (ButtonHandler br in bh)
					WriteAt(br.color(), br.key.ToString(), br.text, btnlen, 0, cy++);

				// reset color
				Console.BackgroundColor = ConsoleColor.Black;
			}

			renderalltext: {
				int cy = 0;
				foreach (ArgHandler ar in ah) {
					string s = ar.check(a[cy], false);
					TextOkayColor(s == null);
					WriteAt(a[cy], txtlen, linebase + cy++);
				}

				Console.BackgroundColor = ConsoleColor.Black;
			}

			selectionchanged:
			Console.SetCursorPosition(txtlen, index + linebase);
			WriteAt(a[index], 0, Math.Min(cposs, cpose), ConsoleColor.Gray, ConsoleColor.Black);
			WriteAt(a[index], Math.Min(cposs, cpose), Math.Max(cposs, cpose), ConsoleColor.Black, ConsoleColor.Gray);
			WriteAt(a[index], Math.Max(cposs, cpose), a[index].Length, ConsoleColor.Gray, ConsoleColor.Black);
			int zzz = Console.BufferWidth - a[index].Length - txtlen;
			if(zzz > 0) WriteAt(new string(' ', Console.BufferWidth - a[index].Length - txtlen), ConsoleColor.Gray, ConsoleColor.Black);
			Console.SetCursorPosition(Math.Min(txtlen + cposs, Console.BufferWidth - 1), linebase + index);

			main:
			while (!Console.KeyAvailable) {
				Thread.Sleep(10);

				// hack: CTRL+C handler
				if (ctrlc) {
					ctrlc = false;
					Clipboard.SetText(CopyStr(a[index], Math.Min(cposs, cpose), Math.Max(cposs, cpose)));
				}
			}

			ConsoleKeyInfo c = Console.ReadKey(true);
			// check if any key is bound
			int p = a.Length + 6 + linebase;
			foreach (ButtonHandler br in bh) {
				if (br.key == c.Key) {
					// if key is same, call press handler
					string s = br.press();
					// rewrite with new bg color
					WriteAt(br.color(), br.key.ToString(), br.text, btnlen, 0, p);
					if (s == null) goto selectionchanged;
					// write error msg
					WriteAt(s, 0, a.Length + 1 + linebase, ConsoleColor.DarkRed, ConsoleColor.Gray);
				}
				p++;
			}

			// if not, check defaults
			switch (c.Key) {
					case ConsoleKey.Enter:
						goto chkinput;   // verify input

				case ConsoleKey.Escape:case ConsoleKey.Tab:case ConsoleKey.Home:
				case ConsoleKey.End:case ConsoleKey.PageDown:case ConsoleKey.PageUp:
				case ConsoleKey.F1:case ConsoleKey.F2:case ConsoleKey.F3:
				case ConsoleKey.F4:case ConsoleKey.F5:case ConsoleKey.F6:
				case ConsoleKey.F7:case ConsoleKey.F8:case ConsoleKey.F9:
				case ConsoleKey.F10:case ConsoleKey.F11:case ConsoleKey.F12:
				case ConsoleKey.F13:case ConsoleKey.F14:case ConsoleKey.F15:
				case ConsoleKey.F16:case ConsoleKey.F17:case ConsoleKey.F18:
				case ConsoleKey.F19:case ConsoleKey.F20:case ConsoleKey.F21:
				case ConsoleKey.F22:case ConsoleKey.F23:case ConsoleKey.F24:
					goto main;

				case ConsoleKey.UpArrow:
					WriteCurr(ah[index], a[index], txtlen, index + linebase, a.Length + 1 + linebase);
					index--;
					if (index < 0)
						index = a.Length - 1;

					cposs = a[index].Length;
					cpose = cposs;
					goto selectionchanged;   // go up 1 line

				case ConsoleKey.DownArrow:
					WriteCurr(ah[index], a[index], txtlen, index + linebase, a.Length + 1 + linebase);
					index++;
					if (index >= a.Length)
						index = 0;

					cposs = a[index].Length;
					cpose = cposs;
					goto selectionchanged;   // go down 1 line

				case ConsoleKey.LeftArrow:
					if (cposs > 0) {
						cposs--;
						if ((c.Modifiers & ConsoleModifiers.Shift) == 0) {
							cpose = cposs;
						}
					}
					goto selectionchanged;   // go up 1 line

				case ConsoleKey.RightArrow:
					if (cposs < a[index].Length) {
						cposs++;
						if ((c.Modifiers & ConsoleModifiers.Shift) == 0) {
							cpose = cposs;
						}
					}
					goto selectionchanged;   // go up 1 line

				case ConsoleKey.Delete:
					a[index] = "";
					cposs = 0;
					cpose = 0;
					goto selectionchanged;   // del all text

				case ConsoleKey.Backspace:
					if (cposs != cpose) {
						a[index] = CutStr(a[index], Math.Min(cposs, cpose), Math.Max(cposs, cpose));

					} else if (cposs > 0) {   // remove char
						a[index] = CutStr(a[index], cposs - 1, cposs);
						cposs--;
					}

					if (cposs > a[index].Length)
						cposs = a[index].Length;

					cpose = cposs;
					goto selectionchanged;

				default:
					if ((c.Modifiers & ConsoleModifiers.Control) != 0) {
						if (c.Key == ConsoleKey.V) {
							// pasting support
							if (Clipboard.ContainsText()) {
								if (cposs != cpose) {
									a[index] = CutStr(a[index], Math.Min(cposs, cpose), Math.Max(cposs, cpose));

									if (cposs > a[index].Length)
										cposs = a[index].Length;
								}

								string t = Clipboard.GetText(TextDataFormat.Text);
								a[index] = PasteStr(a[index], cposs, t);
								cposs += t.Length;
								cpose = cposs;
							}
							goto selectionchanged;

						} else if (c.Key == ConsoleKey.X) {
							// cutting support
							if (cposs != cpose) {
								Clipboard.SetText(CopyStr(a[index], Math.Min(cposs, cpose), Math.Max(cposs, cpose)));
								a[index] = CutStr(a[index], Math.Min(cposs, cpose), Math.Max(cposs, cpose));

								if (cposs > a[index].Length)
									cposs = a[index].Length;

								cpose = cposs;
								goto selectionchanged;
							}
							goto main;

						} else if (c.Key == ConsoleKey.A) {
							// select all
							cpose = 0;
							cposs = a[index].Length;
							goto main;
						}
					}

					// else its normal typing
					if (cposs != cpose) {
						// if highlighted area, cut the string out first
						a[index] = CutStr(a[index], Math.Min(cposs, cpose), Math.Max(cposs, cpose));

						if (cposs > a[index].Length)
							cposs = a[index].Length;
					}

					// type in char
					a[index] = PasteStr(a[index], cposs, "" + c.KeyChar);
					cposs++;
					cpose = cposs;
					goto selectionchanged;
			}

			chkinput:
			// check all arguments for validity
			string[] b = new string[a.Length];
			int x = 0;
			foreach (ArgHandler ar in ah) {
				// check if valid string, and if not, display onscreen
				if (!WriteCurr(ar, a[x], txtlen, x + linebase, a.Length + 1 + linebase)) goto main;

				// if all is good, save to array
				b[x] = ar.check(a[x], true);
				x++;
			}

			// fix up console cols
			Console.BackgroundColor = ConsoleColor.Black;
			Console.ForegroundColor = ConsoleColor.Gray;

			// clear the area which we've written to, that will get overwritten
			string fill = new string(' ', Console.BufferWidth);
			for (int y = linebase + a.Length;y < linebase + a.Length + 6 + bh.Length;y++)
				WriteAt(fill, 0, y);

			// reset cursor and continue timing
			Console.SetCursorPosition(0, linebase + a.Length + 1);
			if (Program.timer != null) Program.timer.Start();
			return b;
		}

		private static bool WriteCurr(ArgHandler ah, string text, int x, int y, int y2) {
			string s = ah.check(text, false);
			TextOkayColor(s == null);
			if (s != null) WriteAt(s + (Console.BufferWidth - s.Length > 0 ? new string(' ', Console.BufferWidth - s.Length) : ""), 0, y2);
			WriteAt(text, x, y);
			Console.BackgroundColor = ConsoleColor.Black;

			return s == null;
		}

		private static void TextOkayColor(bool v) {
			Console.BackgroundColor = v ? ConsoleColor.Black : ConsoleColor.DarkRed;
		}

		private static void WriteAt(ConsoleColor c, string key, string text, int common, int x, int y) {
			Console.BackgroundColor = c;
			WriteAt(' ' + key + new string(' ', common - key.Length) + "- " + text, x, y);
		}

		// write console color
		private static void WriteAt(string text, int start, int end, ConsoleColor fg, ConsoleColor bg) {
			Console.ForegroundColor = fg;
			Console.BackgroundColor = bg;
			Console.Write(CopyStr(text, start, end));
		}

		// write console color
		private static void WriteAt(string text, ConsoleColor fg, ConsoleColor bg) {
			Console.ForegroundColor = fg;
			Console.BackgroundColor = bg;
			Console.Write(text);
		}

		// set cursor position and write string
		private static void WriteAt(string text, int x, int y) {
			Console.SetCursorPosition(x, y);
			Console.Write(text);
		}

		private static readonly Mutex mutex = new Mutex(true, Assembly.GetExecutingAssembly().GetName().CodeBase);
		private static bool _userRequestExit = false;
		private static bool _doIStop = false;
		static HandlerRoutine consoleHandler;

		[DllImport("Kernel32")]
		public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

		// A delegate type to be used as the handler routine for SetConsoleCtrlHandler.
		public delegate bool HandlerRoutine(CtrlTypes CtrlType);

		// An enumerated type for the control messages sent to the handler routine.
		public enum CtrlTypes {
			CTRL_C_EVENT = 0,
			CTRL_BREAK_EVENT,
			CTRL_CLOSE_EVENT,
			CTRL_LOGOFF_EVENT = 5,
			CTRL_SHUTDOWN_EVENT
		}

		private static bool ConsoleCtrlCheck(CtrlTypes ctrlType) {
			// Put your own handler here
			switch (ctrlType) {
				case CtrlTypes.CTRL_C_EVENT:
					_userRequestExit = false;
					ctrlc = true;
					break;

				case CtrlTypes.CTRL_BREAK_EVENT:
				case CtrlTypes.CTRL_CLOSE_EVENT:
				case CtrlTypes.CTRL_LOGOFF_EVENT:
				case CtrlTypes.CTRL_SHUTDOWN_EVENT:
					_userRequestExit = true;
					break;
			}

			return true;
		}
	}

	public struct ButtonHandler {
		public ConsoleKey key;
		public string text;
		public Func<string> press;
		public Func<ConsoleColor> color;

		public ButtonHandler(ConsoleKey key, string text, Func<string> press, Func<ConsoleColor> color) {
			this.key = key;
			this.text = text;
			this.press = press;
			this.color = color;
		}
	}

	public struct ArgHandler {
		public string text;
		public Func<string, bool, string> check;

		public ArgHandler(string text, Func<string, bool, string> check) {
			this.text = text;
			this.check = check;
		}
	}
}