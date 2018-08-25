using System;
using System.Collections.Generic;
using static SMPS2ASMv2.S2AScript;

namespace SMPS2ASMv2 {
	public class GenericScriptItem {
		public uint line = 0;
		public string identifier;
		public ScriptItemType type;
		public ScriptArray parent;

		public GenericScriptItem(uint lnum, ScriptArray paren, string ident, ScriptItemType t) {
			parent = paren;
			line = lnum;
			identifier = ident + lnum;
			type = t;
		}
	}

	public enum ScriptItemType {
		NULL = 0, Equate, Macro, Operation,
		Condition, Repeat, While, Goto, Stop,
		Executable, Import, ArgMod, ArgRmv, ArgEqu,
		LableMod, LableDo, Comment, Print,
		ArrayItem, // SPECIAL
	}

	public class ScriptEquate : GenericScriptItem {
		public string equ, val;

		public ScriptEquate(uint lnum, ScriptArray parent, string equ, string val) : base(lnum, parent, "EQU", ScriptItemType.Equate) {
			this.equ = equ;
			this.val = val;
		}

		public string GetName() {
			if (equ.Contains("{")) {
				// if contains {, it uses dynamic names
				return Parse.ParseMultiple(equ, line);
			}

			return equ;
		}

		public bool Evaluate() {
			return Evaluate(GetName());
		}

		public bool Evaluate(string name) {
			// evaluate equate
			string v = Parse.ParseNumber(val, line);
			Equate e = GetEquate(GetName());
			e.val = v;

			if (Double.TryParse(v, out double vv)) {
				e.value = vv;
				return e.calculated = true;

			} else {
				return e.calculated = false;
			}
		}

		// used for returning value correctly. Tries to avoid returning unusable crap. But it still can.
		public string GetValue() {
			Equate e = GetEquate(GetName());

			if (e.calculated) return "" + e.value;
			return e.val;
		}

		// hopefully very quickly check if value is evaluated and evaluate if not
		public bool CheckEvaluate() {
			string n = GetName();
			Equate e = GetEquate(n);

			if (e.calculated) return true;
			return Evaluate(n);
		}
	}

	public class ScriptCondition : GenericScriptItem {
		public string condition;
		public ScriptArray False, True;

		public ScriptCondition(uint lnum, ScriptArray parent, string condition) : base(lnum, parent, "CON", ScriptItemType.Condition) {
			this.condition = condition;
			False = new ScriptArray(parent);
			True = new ScriptArray(parent);
		}
	}

	public class ScriptImport : GenericScriptItem {
		public string name;

		public ScriptImport(uint lnum, ScriptArray parent, string name) : base(lnum, parent, "IMP", ScriptItemType.Import) {
			this.name = name;
		}

		public ScriptArray getSubScript() {
			if (context.subscripts.ContainsKey(name)){
				return context.subscripts[name];
			}

			return null;
		}
	}

	public class ScriptExecute : GenericScriptItem {
		public string label;
		public bool[] types;	// true if optimizing, false if not
		public string[] names;

		public ScriptExecute(uint lnum, ScriptArray parent, string lbl, bool[] types, string[] names) : base(lnum, parent, "EXE", ScriptItemType.Executable) {
			label = lbl;
			this.types = types;
			this.names = names;
		}
	}

	public class ScriptMacro : GenericScriptItem {
		public string name;
		public string[] pre, arg;
		public ScriptArray Inner;

		public ScriptMacro(uint lnum, ScriptArray parent, string nam, string[] pre, string[] arg) : base(lnum, parent, "MAC", ScriptItemType.Macro) {
			name = nam;
			this.pre = pre;
			this.arg = arg;
			Inner = new ScriptArray(parent);
		}

		// fucking VS17 crashed when I first did this shit so sorry if its crappy af now
		public bool GetRange(int depth, out int rangeStart, out int rangeEnd) {
			// default value if we fail
			rangeStart = 0; rangeEnd = 0xFF;
			if (pre.Length <= depth) return false;
			// if "", then use full range
			if (pre[depth].Length == 0) return true;

			if (pre[depth].Contains("-")) {
				// is a range
				string[] arr = pre[depth].Split('-');
				// if len is not 2, then we fail.
				if (arr.Length != 2) return false;

				// get ranges
				rangeStart = Parse.BasicInt(arr[0]);
				rangeEnd = Parse.BasicInt(arr[1]);

				// swap if start is more than end
				if(rangeStart > rangeEnd) {
					int temp = rangeStart;
					rangeStart = rangeEnd;
					rangeEnd = temp;
				}

			} else {
				// not a range
				rangeStart = rangeEnd = Parse.BasicInt(pre[depth]);
			}

			return true;
		}
	}

	public class ScriptGoto : GenericScriptItem {
		public char func;
		public string offset;

		public ScriptGoto(uint lnum, ScriptArray parent, char type, string offset) : base(lnum, parent, "GOT", ScriptItemType.Goto) {
			func = type;
			this.offset = offset;
		}
	}

	public class ScriptRepeat : GenericScriptItem {
		public string count;
		public ScriptArray Inner;

		public ScriptRepeat(uint lnum, ScriptArray parent, string count) : base(lnum, parent, "REP", ScriptItemType.Repeat) {
			this.count = count;
			Inner = new ScriptArray(parent);
		}
	}

	public class ScriptWhile : GenericScriptItem {
		public string cond;
		public ScriptArray Inner;

		public ScriptWhile(uint lnum, ScriptArray parent, string cond) : base(lnum, parent, "WHL", ScriptItemType.While) {
			this.cond = cond;
			Inner = new ScriptArray(parent);
		}
	}

	public class ScriptArgMod : GenericScriptItem {
		public int num;
		public ScriptArray Inner;

		public ScriptArgMod(uint lnum, ScriptArray parent, string count) : base(lnum, parent, "AGM", ScriptItemType.ArgMod) {
			num = Parse.BasicInt(count);
			Inner = new ScriptArray(parent);
		}
	}

	public class ScriptArgRmv : GenericScriptItem {
		public int num;

		public ScriptArgRmv(uint lnum, ScriptArray parent, string count) : base(lnum, parent, "AGR", ScriptItemType.ArgRmv) {
			num = Parse.BasicInt(count);
		}
	}

	public class ScriptArgEqu : GenericScriptItem {
		public int num;
		public string operation;

		public ScriptArgEqu(uint lnum, ScriptArray parent, string count, string oper) : base(lnum, parent, "AGE", ScriptItemType.ArgEqu) {
			num = Parse.BasicInt(count);
			operation = oper;
		}
	}

	public class ScriptOperation : GenericScriptItem {
		public string operation;

		public ScriptOperation(uint lnum, ScriptArray parent, string oper) : base(lnum, parent, "OPR", ScriptItemType.Operation) {
			operation = oper;
		}
	}

	public class ScriptComment : GenericScriptItem {
		public string comment;

		public ScriptComment(uint lnum, ScriptArray parent, string comment) : base(lnum, parent, "COM", ScriptItemType.Comment) {
			this.comment = comment;
		}
	}

	public class ScriptPrint : GenericScriptItem {
		public string comment;

		public ScriptPrint(uint lnum, ScriptArray parent, string comment) : base(lnum, parent, "PRI", ScriptItemType.Print) {
			this.comment = comment;
		}
	}

	public class ScriptStop : GenericScriptItem {
		public ScriptStop(uint lnum, ScriptArray parent) : base(lnum, parent, "STP", ScriptItemType.Stop) { }
	}

	public class LableMod : GenericScriptItem {
		public string lable;
		public int num;

		public LableMod(uint lnum, ScriptArray parent, string lable, string count) : base(lnum, parent, "LAM", ScriptItemType.LableMod) {
			this.lable = lable;
			num = Parse.BasicInt(count);
		}
	}

	public class LableCreate : GenericScriptItem {
		public string lable, oper;

		public LableCreate(uint lnum, ScriptArray parent, string lable, string oper) : base(lnum, parent, "LAC", ScriptItemType.LableDo) {
			this.lable = lable;
			this.oper = oper;
		}
	}

	public class ScriptArray {
		// parent of this scriptarray
		ScriptArray parent;

		// normal list of items
		public List<GenericScriptItem> Items;
		// if available, this is a pre-optimized array. Only for actual values, 00-FF
		public GenericScriptItem[] Optimized;

		public ScriptArray(ScriptArray parent) {
			this.parent = parent;
			Items = new List<GenericScriptItem>();
		}

		public void Add(GenericScriptItem i) {
			Items.Add(i);
		}

		// function to calculate the optimized arrays of items or array items to quickly return items for certain types of requests
		public GenericScriptItem[] Optimize() {
			Optimized = new GenericScriptItem[0x100];

			foreach (GenericScriptItem entry in Items) {
				switch (entry.type) {
					case ScriptItemType.NULL:
						screrr(entry.line, "Type of item is NULL! This is most likely a programming error in SMPS2ASM!");
						break;

					case ScriptItemType.Equate:
						ScriptEquate eq = (entry as ScriptEquate);
						// only pre-calculated equates are possible to be used
						if (!eq.CheckEvaluate()) S2AScript.screrr(entry.line, "Equates that are being optimized into a look-up-table must be possible to be pre-calculated! Equate with contents '" + eq.val + "' failed to be pre-calculated.");
						// get offset
						int v;
						if (!Parse.DoubleToInt(GetEquate(eq.equ).value, out v))
							screrr(entry.line, "Equate value can not be accurately converted from double floating point to int! Equate with contents '" + eq.val + "' failed to be conveted to 32-bit signed integer.");

						// save entry or throw error.
						if (Optimized[v] == null) Optimized[v] = entry;
						else screrr(entry.line, "Entity " + entry.identifier + " conflicts with " + Optimized[v].identifier + " at line " + Optimized[v].line +
							", both trying to occupy the value " + v + " (0x" + v.ToString("X2") + ")! Optimization requires no such conflicts.");
						break;

					case ScriptItemType.Macro:
						ScriptMacro ma = (entry as ScriptMacro);
						// collect range
						int rangeStart, rangeEnd;
						if (!ma.GetRange(0, out rangeStart, out rangeEnd))
							S2AScript.screrr(entry.line, "Unable to parse first level macro range. Macro range of '" + (ma.pre.Length > 0 ? ma.pre[0] : "") + "' is not valid.");

						// if true, there is only 1 level to this macro
						bool onlylevel = ma.pre.Length == 1;
						for (int i = rangeStart;i <= rangeEnd;i++) {
							if (onlylevel) {
								if (Optimized[i] == null) Optimized[i] = ma;

								else if (Optimized[i].type == ScriptItemType.ArrayItem)
									(Optimized[i] as ScriptArrayItem).CombineFree(ma);

								else screrr(entry.line, "Entity " + entry.identifier + " conflicts with " + Optimized[i].identifier + " at line " + Optimized[i].line +
										", both trying to occupy the value " + i + " (0x" + i.ToString("X2") + ")! Optimization requires no such conflicts.");
							} else {
								if (Optimized[i] == null) {
									Optimized[i] = new ScriptArrayItem(parent);
									(Optimized[i] as ScriptArrayItem).Combine(ma, 1);

								} else if (Optimized[i].type == ScriptItemType.ArrayItem)
									(Optimized[i] as ScriptArrayItem).Combine(ma, 1);

								else screrr(entry.line, "Entity " + entry.identifier + " conflicts with " + Optimized[i].identifier + " at line " + Optimized[i].line +
									 ", both trying to occupy the value " + i + " (0x" + i.ToString("X2") + ")! Optimization requires no such conflicts.");
							}
						}
						break;

					case ScriptItemType.ArrayItem:
						screrr(entry.line, "Unoptimized list contains a pre-occupied technical element that may not be interpreted. This is likely a programming error, please report to devs!");
						break;

					case ScriptItemType.Import:
						ScriptArray sc = context.GetSubscript((entry as ScriptImport).name);
						if (sc.Optimized == null) sc.Optimize();
						Optimized = ConvertSMPS.context.Combine(new GenericScriptItem[][] { Optimized, sc.Optimized });
						break;

					// all these items are invalid inside the LUT.
					default:
						screrr(entry.line, "Optimized look-up-table may only contain unoptimizable elements! Look-up-tables may contain either Equates, or macros.");
						break;
				}
			}
			return Optimized;
		}
	}

	public class ScriptArrayItem : GenericScriptItem {
		public GenericScriptItem[] Optimized;

		public ScriptArrayItem(ScriptArray parent) : base(arrayID++, parent, "SAI", ScriptItemType.ArrayItem) {
			Optimized = new GenericScriptItem[0x100];
		}

		// this combine method is for multi-level macros
		public GenericScriptItem[] Combine(ScriptMacro macro, int depth) {
			// if null, we dont even need to bother
			if (macro == null) return Optimized;
			// if too deep, just return normally

			// combine this layer
			int RangeStart, RangeEnd;
			if (!macro.GetRange(depth, out RangeStart, out RangeEnd))
				// if too deep, just return normally
				return Optimized;

			// loop for all
			for (int i = RangeStart;i <= RangeEnd;i++) {
				if (Optimized[i] == null) {
					// nothing here
					if (depth < macro.pre.Length - 1) {
						Optimized[i] = new ScriptArrayItem(parent);
						(Optimized[i] as ScriptArrayItem).Combine(macro, depth + 1);

					} else {
						// if last level, just put macro in there
						Optimized[i] = macro;
					}

				} else if (Optimized[i].type == ScriptItemType.ArrayItem) {
					// if another array, see if we can insert us there
					(Optimized[i] as ScriptArrayItem).Combine(macro, depth + 1);
				}
			}

			// and finally at the end of the day, we can rest assured everything is sorted. Phew.
			return Optimized;
		}

		// this combine method is for single-level macros, where another level already exists
		public void CombineFree(ScriptMacro ma) {
			// loop for all
			for (int i = 0;i < 0x100;i++) {
				if (Optimized[i] == null)
					// nothing here
					Optimized[i] = ma;

				// if not free, put nothing there
			}
		}
	}
}