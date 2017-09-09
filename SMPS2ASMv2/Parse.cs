using NCalc;
using System;
using System.Linq;
using static SMPS2ASMv2.Program;

namespace SMPS2ASMv2 {
	public class Parse {
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

		public static double ParseDouble(string val, uint? lnum, ScriptArray scra) {
			try {
				return Double.Parse(ParseNumber(val, lnum, scra));

			} catch (Exception) {
				return Double.NaN;
			}
		}

		public static int ParseInt(string val, uint? lnum, ScriptArray scra) {
			return Int32.Parse(ParseNumber(val, lnum, scra));
		}

		public static uint ParseUint(string val, uint? lnum, ScriptArray scra) {
			return UInt32.Parse(ParseNumber(val, lnum, scra));
		}

		public static bool ParseBool(string val, uint? lnum, ScriptArray scra) {
			return Boolean.Parse(ParseNumber(val, lnum, scra));
		}

		public static string ParseNumber(string s, uint? lnum, ScriptArray scra) {
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
				s = GetAllOperators(s, scra);

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
		public static string GetAllOperators(string s, ScriptArray scra) {
			try {
				// translate all operators
				while (s.Contains(".")) {
					int i = s.IndexOf(".") + 1;
					string tr = GetOperator(s.Substring(i, 2));
					s = s.Substring(0, i - 1) + tr + s.Substring(i + 2, s.Length - i - 2);
				}

				// translate all equates
				while (s.Contains("\\")) {
					// get equate
					int i = s.IndexOf("\\") + 1, o = s.IndexOf("\\", i);
					if (scra != null) {
						ScriptEquate tr = scra.GetEquate(s.Substring(i, o - i));

						// if does not exist, error
						if (tr == null) error("Could not find equate '" + s.Substring(i, o - i) + "'");

						// if not calculated, calculate. Also set a, so to either get string of value
						bool a;
						if (!tr.calculated)
							a = tr.Evaluate(scra);
						else a = true;

						// now plop in the value
						s = s.Substring(0, i - 1) + (a ? tr.value + "" : tr.val) + s.Substring(o + 1, s.Length - o - 1);
					} else
						// now plop in the value
						s = s.Substring(0, i - 1) +  "null" + s.Substring(o + 1, s.Length - o - 1);
				}

				return s;

			} catch (Exception e) {
				error("Could not convert '" + s + "': "+ e.ToString());
				return null;
			}
		}

		private static string GetOperator(string s) {
			switch (s) {
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
			}

			error("Could not resolve argument '" + s + "'");
			return null;
		}
	}
}