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

		public static double ParseDouble(string val, uint? lnum) {
			try {
				return double.Parse(ParseNumber(val, lnum));

			} catch (Exception) {
				return double.NaN;
			}
		}

		public static int ParseInt(string val, uint? lnum) {
			try {
				return int.Parse(val = ParseNumber(val, lnum));

			} catch (Exception) {
				Console.WriteLine("WARNING: Possibly dangerous casting from double to int! Value of '" + val + "' is not an integer.");
				// warn: This may not be safe! Need to investigate further I think
				return (int)Double.Parse(val);
			}
		}

		public static uint ParseUint(string val, uint? lnum) {
			return uint.Parse(ParseNumber(val, lnum));
		}

		public static bool ParseBool(string val, uint? lnum) {
			return bool.Parse(ParseNumber(val, lnum));
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
						len = int.Parse(s.Substring(1, s.IndexOf('!') - 1));
						s = s.Substring(s.IndexOf('!') + 1);
					}
				} catch (Exception) {
					// if it failed, we then did not want any type afterall or it was bad type.
					// easiest thing is just to ignore it.
					type = '\0';
				}

				string exr = Expression.Process(s);

				// return the type of string requested
				switch (type) {
					// plain
					case '\0':
						return exr.ToString();

					// hex
					case '$':
						return toHexString(long.Parse(exr.ToString()) & lentbl[len], len);

					// binary
					case '%':
						return toBinaryString(long.Parse(exr.ToString()) & lentbl[len], len);

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
	}
}