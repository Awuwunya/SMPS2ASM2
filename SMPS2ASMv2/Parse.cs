using NCalc;
using System;
using System.Linq;
using static SMPS2ASMv2.Program;

namespace SMPS2ASMv2 {
	public class Parse {
		public static string[] args = new string[0];
		public static Random random = new Random();

		// basic string to int converter. Faster than ParseNumber
		public static int BasicInt(string count) {
			int bass = 10;
			if (count.StartsWith("$")) {
				// if $, then its hex
				bass = 16;
				count = count.Substring(1);

			} else if (count.StartsWith("0x")) {
				// if 0x, then its hex
				bass = 16;
				count = count.Substring(2);
			} else if (count.StartsWith("-0x")) {
				// if -0x, then its hex
				bass = 16;
				count = '-' + count.Substring(3);
			}

			return Convert.ToInt32(count, bass);
		}

		// basic string to uint converter. Faster than ParseNumber
		public static uint BasicUint(string count) {
			int bass = 10;
			if (count.StartsWith("$")) {
				// if $, then its hex
				bass = 16;
				count = count.Substring(1);

			} else if (count.StartsWith("0x")) {
				// if 0x, then its hex
				bass = 16;
				count = count.Substring(2);
			}

			return Convert.ToUInt32(count, bass);
		}

		// basic string to byte converter. Faster than ParseNumber
		public static byte BasicByte(string count) {
			int bass = 10;
			if (count.StartsWith("$")) {
				// if $, then its hex
				bass = 16;
				count = count.Substring(1);

			} else if (count.StartsWith("0x")) {
				// if 0x, then its hex
				bass = 16;
				count = count.Substring(2);
			}

			return Convert.ToByte(count, bass);
		}

		// check if it is safe to convert double to int
		public static bool DoubleToInt(double value, out int num) {
			// pre-convert! Value can still actually be used, but its just unsafe
			num = (int)value;
			// stolen from https://stackoverflow.com/questions/2751593/how-to-determine-if-a-decimal-double-is-an-integer
			// not an expert on why this is any good idea
			return (Math.Abs(value % 1) <= (Double.Epsilon * 100));
		}

		// check if it is safe to convert double to long
		public static bool DoubleToLong(double value, out long num) {
			// pre-convert! Value can still actually be used, but its just unsafe
			num = (long)value;
			// stolen from https://stackoverflow.com/questions/2751593/how-to-determine-if-a-decimal-double-is-an-integer
			// not an expert on why this is any good idea
			return (Math.Abs(value % 1) <= (Double.Epsilon * 100));
		}

		public static double ParseDouble(string val, uint? lnum) {
			try {
				return Double.Parse(ParseNumber(val, lnum));

			} catch (Exception) {
				return Double.NaN;
			}
		}

		public static int ParseInt(string val, uint? lnum) {
			try {
				return Int32.Parse(val = ParseNumber(val, lnum));

			} catch (Exception) {
				Console.WriteLine("WARNING: Possibly dangerous casting from double to int! Value of '" + val + "' is not an integer.");
				// warn: This may not be safe! Need to investigate further I think
				return (int)Double.Parse(val);
			}
		}

		public static uint ParseUint(string val, uint? lnum) {
			return UInt32.Parse(ParseNumber(val, lnum));
		}

		public static bool ParseBool(string val, uint? lnum) {
			return Boolean.Parse(ParseNumber(val, lnum));
		}

		internal static string ParseMultiple(string val, uint? lnum) {
			while (val.Contains("{") && val.Contains("}")) {
				int i1 = val.IndexOf('{'), i2 = val.IndexOf('}');
				string arg = ParseNumber(val.Substring(i1 + 1, i2 - i1 - 1), lnum);
				val = val.Substring(0, i1) + arg + val.Substring(i2 + 1);
			}

			return val;
		}

		public static string ParseNumber(string s, uint? lnun) {
			string old = s;
			try {
				char type = '\0';
				int len = 0;

				// if a type is required, check it here
				try {
					if (s.Contains("!")) {
						type = s.ElementAt(0);
						len = Int32.Parse(s.Substring(1, s.IndexOf('!') - 1));
						s = s.Substring(s.IndexOf('!') + 1);
					}
				} catch (Exception) {
					// if it failed, we then did not want any type afterall or it was bad type.
					// easiest thing is just to ignore it.
					type = '\0';
				}

				// translate all abstract symbols
				s = GetAllOperators(s);

				// replace any hex constant with decimal
				while (s.Contains("0x")) {
					int i = s.IndexOf("0x") + 2, l = FindNonNumeric(s, i) + 1;
					ulong res;

					if (l <= 0) {
						res = Convert.ToUInt64(s.Substring(i, s.Length - i), 16);
						s = s.Substring(0, i - 2) + res;

					} else {
						res = Convert.ToUInt64(s.Substring(i, l - i), 16);
						s = s.Substring(0, i - 2) + res + s.Substring(FindNonNumeric(s, i) + 1);
					}
				}

				Expression e = new Expression(s);

				// return the type of string requested
				switch (type) {
					// plain
					case '\0':
						return e.Evaluate().ToString();

					// hex
					case '$':
						return toHexString(Int64.Parse(e.Evaluate().ToString()) & lentbl[len], len);

					// binary
					case '%':
						return toBinaryString(Int64.Parse(e.Evaluate().ToString()) & lentbl[len], len);

					default:
						error("Uknown return type '" + type + "'! ");
						return null;
				}

			} catch (Exception e) {
				if (debug) {
					Debug("--! ERROR IN EXPRESSION: " + s);
					Debug("--! INITIAL EXPRESSION: " + old);
					Debug("--! " + e.ToString());
				}
			//	Console.WriteLine("'"+ s +"' "+ e);
				return "";
			}
		}

		public static long[] lentbl = { 0x0, 0xF, 0xFF, 0xFFF, 0xFFFF, 0xFFFFF, 0xFFFFFF, 0xFFFFFFF };

		private static int FindNonNumeric(string s, int i) {
			for (;i < s.Length;i++) {
				char a = s.ElementAt(i);

				if (!((a >= '0' && a <= '9') || (a >= 'a' && a <= 'f') || (a >= 'A' && a <= 'F'))) {
					return i - 1;
				}
			}

			return -1;
		}

		// this code is basically taken from the earlier version of smps2asm
		public static string GetAllOperators(string s) {
			try {
				// translate all operators
				while (s.Contains(".")) {
					int i = s.IndexOf(".") + 1;
					if (s.Length < i + 2) break;

					string tr = GetOperator(s.Substring(i, 2));
					s = s.Substring(0, i - 1) + tr + s.Substring(i + 2, s.Length - i - 2);
				}

				// translate all equates
				while (s.Contains("\\")) {
					// get equate
					int i = s.IndexOf("\\") + 1, o = s.IndexOf("\\", i), x = s.IndexOf('{', i);
					if (x >= 0 && x < o) {
						if (!s.Contains("}")) throw new Exception("Sequence contains an illegal equate! Can not translate.");

						while (s.IndexOf('}', i) > o && o >= 0) o = s.IndexOf("\\", o + 1);
						if (o < 0) throw new Exception("Sequence contains an illegal equate! Can not translate.");
					}

					// get equate name and transform it if needed
					string eq = s.Substring(i, o - i);
					if (eq.Contains("{")) eq = ParseMultiple(eq, null);
					Equate tr = S2AScript.GetEquate(eq);

					// if does not exist, error
					if (tr == null) error("Could not find equate '" + eq + "'");

					// get the proper value of the equate
					s = s.Substring(0, i - 1) + (tr.calculated ? "" + tr.value : tr.val) + s.Substring(o + 1, s.Length - o - 1);
				}

				return s;

			} catch (Exception e) {
				error("Could not convert " + s + ": "+ e.ToString());
				return null;
			}
		}

		private static string GetOperator(string s) {
			switch (s.ToLowerInvariant()) {
				case "db":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos);
					return "" + ConvertSMPS.context.data[ConvertSMPS.context.pos++];

				case "lb":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 1);
					return "" + ConvertSMPS.context.data[ConvertSMPS.context.pos - 1];

				case "nb":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos);
					return "" + ConvertSMPS.context.data[ConvertSMPS.context.pos];

				case "sb":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return "";

				case "dw":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return "" + ConvertSMPS.context.ReadWord(ConvertSMPS.context.pos - 2);

				case "lw":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 2);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 1);
					return "" + ConvertSMPS.context.ReadWord(ConvertSMPS.context.pos - 2);

				case "nw":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos + 1);
					return "" + ConvertSMPS.context.ReadWord(ConvertSMPS.context.pos);

				case "sw":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return "";

				case "ow":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return ""+ ConvertSMPS.context.ReadWordOff(ConvertSMPS.context.pos - 2, -1);

				case "rw":  // fucking Ristar piece of shit
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return "" + ConvertSMPS.context.ReadWordOff(ConvertSMPS.context.pos - 2, 0);

				case "dl":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return "" + ConvertSMPS.context.ReadLong(ConvertSMPS.context.pos);

				case "ll":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 4);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 3);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 2);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 1);
					return "" + ConvertSMPS.context.ReadLong(ConvertSMPS.context.pos - 4);

				case "nl":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos + 1);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos + 2);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos + 3);
					return "" + ConvertSMPS.context.ReadLong(ConvertSMPS.context.pos);

				case "sl":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return "";

				case "pc":
					return "" + (ConvertSMPS.context.offset + ConvertSMPS.context.pos);

				case "sz":
					return "" + ConvertSMPS.context.data.Length;

				case "of":
					return "" + ConvertSMPS.context.offset;

				case "an": {
						ulong off = (uint)ConvertSMPS.context.data.Length + ConvertSMPS.context.offset;

						foreach (OffsetString o in ConvertSMPS.context.Lables) {
							if (o.offset > ConvertSMPS.context.pos + ConvertSMPS.context.offset && o.offset != null && o.offset < off) {
								off = (uint)o.offset;
							}
						}
						return "" + off;
					}

				case "al": {
						ulong off = 0;

						foreach (OffsetString o in ConvertSMPS.context.Lables) {
							if (o.offset <= ConvertSMPS.context.pos && o.offset != null && o.offset >= 0) {
								off = (uint)o.offset;
								break;
							}
						}
						return "" + off;
					}

				case "ms":
					return "" + timer.ElapsedMilliseconds;

				case "cc":
					return ""+ Console.Read();

				case "ci":
					if (long.TryParse(Console.ReadLine(), out long r)) return "" + r;
					return "";

				case "ri":
					return "" + (random.Next() & 0xFFFF);

				case "rf":
					return "" + random.NextDouble();

				case "a0": return GetArg(0);
				case "a1": return GetArg(1);
				case "a2": return GetArg(2);
				case "a3": return GetArg(3);
				case "a4": return GetArg(4);
				case "a5": return GetArg(5);
				case "a6": return GetArg(6);
				case "a7": return GetArg(7);

				case "n0": return GetArgN(0);
				case "n1": return GetArgN(1);
				case "n2": return GetArgN(2);
				case "n3": return GetArgN(3);
				case "n4": return GetArgN(4);
				case "n5": return GetArgN(5);
				case "n6": return GetArgN(6);
				case "n7": return GetArgN(7);
			}

			error("Could not resolve argument '" + s + "'");
			return null;
		}

		private static string GetArg(int i) {
			return args.Length > i ? "'"+ args[i] + "'" : "''";
		}

		private static string GetArgN(int i) {
			if (args.Length <= i) return "NaN";
			return "" + ParseDouble(args[i].Replace("$", "0x"), null);
		}
	}
}