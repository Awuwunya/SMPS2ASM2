using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using static SMPS2ASMv2.Program;

namespace SMPS2ASMv2 {
	public class LableRule {
		public static Dictionary<string, Func<int, string>> RandomRules = new Dictionary<string, Func<int, string>>() {
			{ "normal", (num) => "" + (num + 1) },
			{ "dec", (num) => "" + num },
			{ "hex", (num) => "" + num.ToString("X2") },
		};
		public static Func<int, string> GetNextRandom = RandomRules["normal"];
	}

	public class ConvertSMPS {
		// ref to current obj
		public static ConvertSMPS context;

		// here are some variables that arent used in conversion
		public string filein, fileout, baselable;
		public bool[] skipped;

		// and these are conversion
		public S2AScript scr;
		public List<OffsetString> Lables, Lines;
		public List<uint> UnunsedChk;
		public byte[] data; // data of the file
		public uint pos = 0; // current position in the script
		public bool followlables = false;// set to true, if we also want to follow new lables

		// these will be gotten from the topmost script
		public string endian = null; // 'big' or 'little'
		public uint offset = 0;    // offset of the 0th byte of the file. Usually Z80 address where file starts
		
		public byte Read(long offset) {
			if (data.Length > offset)
				return data[offset];

			throw new DataException($"Failed to read data at ${(pos + offset).ToString("X4")}!");
		}

		public void cvterror(GenericScriptItem i, string v) {
			error("smps2asm.smpss:" + (i != null ? i.line +"" : "null") + ": " + v);
		}
		
		private void AddLine(uint pos, uint len, ScriptEquate s) {
			AddLine(pos++, 1, "\b " + s.GetName());
		}

		private void AddLine(uint pos, uint len, byte val) {
			AddLine(pos++, 1, "\b " + toHexString(val, 2));
		}

		// array for tabulating different strings to a common place
		private readonly int[] LineAlign = { 8, 24, 64 };
		private void AddLine(uint pos, uint len, string[] lines) {
			string s = "";
			int last = 0;

			for(int i = 0; i < lines.Length;i++) {
				if (lines[i].Length > 0) {
					s += new string('\t', (LineAlign[i] - last - 1) / 8 + 1);    // divide by tab len. If its 4, though. Maybe I can add an option later...
					s += lines[i];
					last = LineAlign[i] + lines[i].Length;  // index of characters last in string
				}
			}

			// finally put out
			AddLine(pos, len, s);
		}

		private void AddLine(uint pos, uint len, string line) {
			Lines.Add(new OffsetString(pos + offset, len, line));
		}

		// generate valid lables from a specific rule and position
		private bool ObtainValidLable(string lable, string lastlable, uint position, out string valid, out OffsetString lab) {
			valid = null;
			lab = null;
			if (lable == "") return false;

			// check if any lable exists with same address
			foreach(OffsetString o in Lables)
				if(o.offset == position) {
					valid = o.line;
					return true;
				}

			// get num of lables that match rule, and return if no wildcard
			int num = GetLablesRule(lable, lastlable).Count;
			if (num != 0 && !lable.Contains("?")) return false;

			// build lable, add it to pool, and return
			valid = lable.Replace("£", baselable).Replace("%", lastlable).Replace("?", LableRule.GetNextRandom(num));
			lab = AddLable(position, valid);
			return true;
		}

		private OffsetString AddLable(uint pos, string valid) {
			OffsetString off = new OffsetString(pos, 0, valid);
			Lables.Add(off);
			return off;
		}

		public void SkipByte(uint addr) {
			skipped[addr] = true;
		}

		// read word, respect to the endianness
		public ushort ReadWord(uint addr) {
			if (endian == null) InitConvertVars();

			if(endian.ToLower() == "little") {
				return (ushort)((Read(addr)) | ((Read(addr + 1) << 8)));

			} else if(endian.ToLower() == "big") {
				return (ushort)((Read(addr + 1)) | ((Read(addr) << 8)));

			} else error("Endian '"+ endian +"' is not recognized!");
			return 0;
		}

		// read long, respect to the endianness
		public uint ReadLong(uint addr) {
			return (ushort)((ReadWord(addr)) | ((ReadWord(addr + 2) << 16)));
		}

		public ushort ReadWordOff(uint addr, int off) {
			return (ushort)(ReadWord(addr) + off + pos);
		}

		public ConvertSMPS(string filebin, string fileasm, string lable) {
			context = this;

			// set files
			filein = filebin;
			fileout = fileasm;

			// check input file exists
			if (!File.Exists(filein)) {
				error("Input file '" + filein + "' does not exist!");
			}

			// set base lable
			baselable = lable;
		}

		// convert main script
		public void Convert(S2AScript scr) {
			if (debug) Debug("Prepare conversion");
			this.scr = scr;
			UnunsedChk = new List<uint>();
			Lables = new List<OffsetString>();
			Lines = new List<OffsetString>();
			data = File.ReadAllBytes(filein);
			skipped = new bool[data.Length + 1];
			skipped[data.Length] = true; // skip last byte for output
			if (debug) Debug(new string('-', 80));

			// run conveter
			string[] a = null;
			if (debug) Debug("--> Start conversion with subscript ''");
			try {
				ConvertRun(scr.subscripts[""].Items, ref a, baselable, out bool asses, out string c);
			} catch (DataException) { }

			// convert unused data
			if (scr.subscripts.ContainsKey("unused")) {
				List<GenericScriptItem> uscr = scr.subscripts["unused"].Items;

				for(int i = 0;i < UnunsedChk.Count;i ++) {
					uint p = UnunsedChk[i];

					// check if this is not used
					if (skipped[p]) continue;

					foreach(OffsetString o in Lines) {
						if (o.offset <= p && o.offset + o.length >= p)
							goto next;
					}

					// unused, deal with it
					try {
						pos = p;
						ConvertRun(uscr, ref a, baselable, out bool asses, out string  c);
					} catch(DataException) { }

					next:;
				}
			}
			if (debug) Debug(new string('-', 80));
		}

		// helper method to collect some variables used in conversion
		private void InitConvertVars() {
			try {
				// get global subscript
				endian = S2AScript.GetEquate("endian").val;
				offset = Parse.BasicUint(S2AScript.GetEquate("offset").val);
				if (debug) Debug("--> InitConvertVars: endian="+ endian +" offset="+ toHexString(offset, 4));

			} catch (Exception e) {
				cvterror(null, new Exception("Missing equates when getting convert variables! Required equates are: 'endian' and 'offset'.", e).ToString());
			}
		}

		// combine multiple script items to one array
		public GenericScriptItem[] Combine(GenericScriptItem[][] luts) {
			if (luts.Length == 0) return null;
			GenericScriptItem[] rout = new GenericScriptItem[0x100];

			foreach(GenericScriptItem[] lut in luts) {
				// entry ID
				int eid = 0;
				foreach(GenericScriptItem entry in lut) {
					if(entry != null)
						switch (entry.type) {
							case ScriptItemType.NULL:
								cvterror(entry, "Type of item is NULL! This is most likely a programming error in SMPS2ASM!");
								break;

							case ScriptItemType.Equate:
								if (rout[eid] == null) rout[eid] = entry;
								break;

							case ScriptItemType.Macro:
								if (rout[eid] == null) rout[eid] = entry;
								else if (rout[eid].type == ScriptItemType.ArrayItem) {
									(rout[eid] as ScriptArrayItem).Combine(entry as ScriptMacro, 1);
								}

								// if equate or macro, it is assumed they take up the entirity of byte, no extra bytes. If this happens despite having extra bytes, whoops =/
								break;

							case ScriptItemType.ArrayItem:
								if (rout[eid] == null) rout[eid] = entry;
								break;

							case ScriptItemType.Import:
								if (rout[eid] == null) rout[eid] = entry;
								break;

							default:
								cvterror(entry, "Somehow optimized look-up-table contains unoptimizable elements! Report to developer.");
								break;
						}

					eid++;
				}
			}

			return rout;
		}

		private void Convert(string label, string lastlable, GenericScriptItem[] LUT, List<GenericScriptItem>[] run, bool str, bool single, out string text) {
			text = null;
			// init some vars
			InitConvertVars();

			if (label != "") {
				foreach (OffsetString o in GetLablesRule(label, lastlable)) {
					Convert(o, LUT, run, str, single, out text);
				}
			} else Convert(new OffsetString(pos + offset, 0, null), LUT, run, str, single, out text);
		}

		private GenericScriptItem[] StoredLUT = null;
		private List<GenericScriptItem>[] StoredRun = null;

		private void Convert(OffsetString o, GenericScriptItem[] LUT, List<GenericScriptItem>[] run, bool str, bool single, out string text) {
			// empty list to be ref'd later (idk =/ )
			string[] args = null;
			text = null;

			// check and set pointer for this lable
			if (o.offset == null)
				error("Pointer to the lable is null! This seems like a programming error. Report to developers.");

			if (!IsValidLocation(o)) return;

			pos = (uint)(o.offset - offset);
			if (o.line != null) Console.WriteLine("Parsing " + o.line + " at " + toHexString((double)o.offset, 4) + "...");
			if (debug) Debug("--: " + o.line + " " + toHexString((double)o.offset, 4) + " LUT=" + (LUT != null) + " run=" + (run != null));

			// if no LUT, only run sequentially
			if (LUT == null) {
				followlables = false;
				foreach (List<GenericScriptItem> en in run) {
					ConvertRun(en, ref args, o.line, out bool stop, out string c);
					if (stop) return;
				}
				return;
			}

			// else do LUT thing
			if (!str) {
				followlables = true;
				StoredLUT = LUT;
				StoredRun = run;
			}

			do {
				if (ProcessItem(LUT, str, o.line, out bool stop, out text)) {
					if (stop) break;

				} else if (run != null && run.Length > 0) {
					foreach (List<GenericScriptItem> en in run) {
						ConvertRun(en, ref args, o.line, out bool stop2, out string c);
						if (stop2) return;
					}
				} else {
					if (str) text = toHexString(Read(pos), 2);
					else AddLine(pos, 1, Read(pos));
					SkipByte(pos++);    // skip over current byte

					if (stop) break;
				}
			} while (!single);
		}

		// check if the string is in a valid location
		private bool IsValidLocation(OffsetString o) {
			if (o.offset == null) return false;
			if (o.offset >= offset && o.offset <= offset + data.Length) return true;

			// if invalid, put in a lable at the end
			bool pp = o.offset > offset;
			long offs = (pp ? (uint)o.offset - offset - data.Length : offset - (uint)o.offset);
			AddLine((uint)data.Length, 0, "\t; " + o.line + " at " + toHexString((uint)o.offset, 4) + " (" + toHexString(offs, 1) + ' ' + (pp ? "after end of" : "before start of") + 
				" file) can not be converted, because the data does not exist.");
			if(debug) Debug("--. Unaccessible lable found at " + toHexString((uint)o.offset, 4));
			return false;
		}

		// get a list of lables that match regex. Lables may additionally use £ to get the base lable (user input) and ? for regex anything matches.
		private List<OffsetString> GetLablesRule(string label, string lastlable) {
			// create regex to match all lables against. Additionally, compile it if enough lable entries.
			Regex r;
			if(Lables.Count > 25)
				r = new Regex(label.Replace("?", ".*").Replace("£", baselable).Replace("%", lastlable), RegexOptions.IgnoreCase | RegexOptions.Compiled);
			else r = new Regex(label.Replace("?", ".*").Replace("£", baselable).Replace("%", lastlable), RegexOptions.IgnoreCase);

			List<OffsetString> ret = new List<OffsetString>();
			foreach(OffsetString o in Lables) {
				// match line against regex. If match, add to list
				if (r.IsMatch(o.line)) ret.Add(o);
			}

			return ret;
		}

		private void ConvertRun(List<GenericScriptItem> s, ref string[] args, string lastlable, out bool stop, out string comment) {
			// default values
			comment = null;
			stop = false;

			foreach(GenericScriptItem i in s) {
				ProcessItem(i, ref args, lastlable, out stop, out comment);
				if (stop) break;
			}

			UnunsedChk.Add(pos);
		}

		private bool ProcessItem(GenericScriptItem[] lut, bool str, string lastlable, out bool stop, out string text) {
			text = null;
			// default values
			stop = str;

			// get next byte
			byte d = Read(pos);
			if (lut[d] == null) return false;

			switch (lut[d].type) {
				case ScriptItemType.Equate:
					// just write equate
					if (debug) Debug(pos + offset, lut[d].line, str, (lut[d] as ScriptEquate).GetName() +" "+ toHexString(d, 2));
					if (str) text = (lut[d] as ScriptEquate).GetName();
					else AddLine(pos, 1, lut[d] as ScriptEquate);
					SkipByte(pos++);
					return true;

				case ScriptItemType.Macro:
					if (str) cvterror(lut[d], "Macros can not be used in Argument Modifiers!");
					SkipByte(pos++);	// skip over current byte
					ProcessMacro(lut[d] as ScriptMacro, lastlable, out stop);
					return true;

				case ScriptItemType.ArrayItem:
					SkipByte(pos++);
					return ProcessItem((lut[d] as ScriptArrayItem).Optimized, str, lastlable, out stop, out text);

				case ScriptItemType.Import:
					ScriptArray sc = scr.GetSubscript((lut[d] as ScriptImport).name);
					if (sc.Optimized == null) sc.Optimize();
					uint x = pos;
					bool ret = ProcessItem(scr.GetSubscript((lut[d] as ScriptImport).name).Optimized, str, lastlable, out stop, out text);
					pos = x;
					stop = true;
					return ret;

				case ScriptItemType.NULL:
					cvterror(lut[d], "Type of item is NULL! This is most likely a programming error in SMPS2ASM!");
					break;

				default:
					cvterror(lut[d], "Somehow optimized look-up-table contains unoptimizable elements! Report to developer.");
					break;
			}

			return false;
		}

		private void ProcessItem(GenericScriptItem i, ref string[] args, string lastlable, out bool stop, out string comment) {
			// default values
			comment = null;
			stop = false;

			try {
				// process an item needed for conversion, use switch on the type
				switch (i.type) {
					case ScriptItemType.NULL:
						cvterror(i, "Type of item is NULL! This is most likely a programming error in SMPS2ASM!");
						return;

					case ScriptItemType.Executable: {
							ScriptExecute sex = (i as ScriptExecute);
							List<GenericScriptItem[]> opt = new List<GenericScriptItem[]>();
							List<List<GenericScriptItem>> dir = new List<List<GenericScriptItem>>();

							for (int si = 0;si < sex.names.Length;si++) {
								ScriptArray sa = scr.GetSubscript(sex.names[si]);
								if (sa == null) cvterror(i, "Execute command requested subscript '" + sex.names[si] + "', which does not exist.");

								if (sex.types[si]) {
									if (sa.Optimized == null) sa.Optimize();
									opt.Add(sa.Optimized);

								} else dir.Add(sa.Items);
							}

							if (debug) Debug(pos + offset, i.line, i.identifier, '/' + sex.label);
							Convert(sex.label, lastlable, Combine(opt.ToArray()), dir.ToArray(), false, sex.singlemode, out string fuck);
						}
						break;

					case ScriptItemType.Equate:
						(i as ScriptEquate).Evaluate();
						if (debug) Debug(pos + offset, i.line, i.identifier, '='+ (i as ScriptEquate).GetName() + ' '+ (i as ScriptEquate).val +' '+ (i as ScriptEquate).GetValue());
						break;

					case ScriptItemType.Macro:
						if(CheckMacro(i as ScriptMacro)) ProcessMacro(i as ScriptMacro, lastlable, out stop);
						break;

					case ScriptItemType.Operation:
						string rsop = Parse.ParseNumber((i as ScriptOperation).operation, null);
						if (debug) Debug(pos + offset, i.line, i.identifier, '$' + (i as ScriptOperation).operation + ' ' + rsop);
						break;

					case ScriptItemType.Condition: {
							ScriptCondition cond = i as ScriptCondition;
							bool c = Parse.ParseDouble(cond.condition, cond.line) != 0;

							if (c) {
								if (debug) Debug(pos + offset, i.line, i.identifier, "c " + cond.condition + " (true)");
								ConvertRun(cond.True.Items, ref args, lastlable, out stop, out comment);

							} else {
								if (debug) Debug(pos + offset, i.line, i.identifier, "c " + cond.condition + " (false)");
								ConvertRun(cond.False.Items, ref args, lastlable, out stop, out comment);
							}
						}
						break;

					case ScriptItemType.Repeat:
						int ccc = Parse.ParseInt((i as ScriptRepeat).count, i.line);
						if (debug) Debug(pos + offset, i.line, i.identifier, "f " + ccc + " {");

						for (int nnn = ccc;nnn > 0;nnn--) {
							ConvertRun((i as ScriptRepeat).Inner.Items, ref args, lastlable, out stop, out comment);
						}
						break;

					case ScriptItemType.While: {
							bool c;

							while (true) {
								try {
									c = Parse.ParseBool((i as ScriptWhile).cond, i.line);

								} catch (Exception) {
									c = Parse.ParseDouble((i as ScriptWhile).cond, i.line) != 0;
								}

								if (debug) Debug(pos + offset, i.line, i.identifier, "w " + c + " {");
								if (!c) break;
								ConvertRun((i as ScriptWhile).Inner.Items, ref args, lastlable, out stop, out comment);
							}
						}
						break;

					case ScriptItemType.Goto:
						ScriptGoto gotto = i as ScriptGoto;
						uint off = Parse.ParseUint(gotto.offset, i.line);

						switch (gotto.func) {
							case 'a':case 'A':
								if (debug) Debug(pos + offset, i.line, i.identifier, ">a " + off);
								pos = off;
								break;

							case 'o':case 'O':
								if (debug) Debug(pos + offset, i.line, i.identifier, ">o " + off +" -> "+ toHexString(off + offset, 4));
								pos = off + offset;
								break;

							case 'b':case 'B':
								if (debug) Debug(pos + offset, i.line, i.identifier, ">b " + off + " -> " + toHexString(pos - off + offset, 4));
								pos -= off;
								break;

							case 'f':case 'F':
								if (debug) Debug(pos + offset, i.line, i.identifier, ">f " + off + " -> " + toHexString(pos + off + offset, 4));
								pos += off;
								break;

							default:
								cvterror(i, "Go To type '"+ gotto.func +"' not recognized!");
								return;
						}
						break;

					case ScriptItemType.Stop:
						if (debug) Debug(pos + offset, i.line, i.identifier, ";");
						stop = true;
						break;

					case ScriptItemType.Import:
						if (debug) Debug(pos + offset, i.line, i.identifier, '?' + (i as ScriptImport).name + ';');
						ConvertRun(scr.GetSubscript((i as ScriptImport).name).Items, ref args, lastlable, out stop, out comment);
						break;

					case ScriptItemType.ArgMod: {
							ScriptArgMod am = i as ScriptArgMod;
							// check if this request is valid
							if (args == null) cvterror(i, "No macro parameters were passed in to modify!");
							if (args.Length <= am.num)
								cvterror(i, "Not enough macro parameters were passed in for lable mod at line " + i.line + "! Requested parameter num was " + am.num + ", but only " + args.Length + " arguments were passed in!");

							// store all kinds of variables and properly fix pos
							byte[] dat = data;
							uint pos3 = pos;
							bool fol = followlables;
							pos = 0;
							data = new byte[] { Parse.BasicByte(args[am.num]) };

							// debug and optimize array
							if (debug) Debug(pos + offset, i.line, i.identifier, ":?" + am.num + ' ' + toHexString(Read(0), 2) + ' ' + toHexString(pos, 4));
							if (am.Inner.Optimized == null) am.Inner.Optimize();

							// process request
							Convert("", lastlable, am.Inner.Optimized, null, true, false, out args[am.num]);
							followlables = fol;
							pos = pos3;
							data = dat;
							Expression.args = args;
						}
						break;

					case ScriptItemType.ArgRmv: {
							ScriptArgRmv am = i as ScriptArgRmv;
							// check if this request is valid
							if (args == null) cvterror(i, "No macro parameters were passed in to modify!");
							if (args.Length <= am.num)
								cvterror(i, "Not enough macro parameters were passed in for lable mod at line " + i.line + "! Requested parameter num was " + am.num + ", but only " + args.Length + " arguments were passed in!");

							// construct a new array and remove entry from it
							string[] a = args;
							args = new string[a.Length - 1];

							for (int i1 = 0, i2 = 0;i1 < a.Length;i1++, i2++)
								if (i1 == am.num) i2--;
								else args[i2] = a[i1];

							// debug and update args
							if (debug) Debug(pos + offset, i.line, i.identifier, ":-" + am.num + ' ' + a[am.num]);
							Expression.args = args;
						}
						break;

					case ScriptItemType.ArgEqu: {
							ScriptArgEqu am = i as ScriptArgEqu;
							// check if this request is valid
							if (args == null) cvterror(i, "No macro parameters were passed in to modify!");
							if (args.Length <= am.num)
								cvterror(i, "Not enough macro parameters were passed in for lable mod at line " + i.line + "! Requested parameter num was " + am.num + ", but only " + args.Length + " arguments were passed in!");

							string res = Parse.ParseMultiple(am.operation, am.line);
							args[am.num] = res;

							// debug and update args
							if (debug) Debug(pos + offset, i.line, i.identifier, ":=" + am.num + ' ' + res);
							Expression.args = args;
						}
						break;

					case ScriptItemType.LableMod: {
							LableMod lmod = i as LableMod;
							// check if this request is valid
							if (args == null) cvterror(i, "No macro parameters were passed in to modify!");
							if (args.Length <= lmod.num)
								cvterror(i, "Not enough macro parameters were passed in for lable mod at line " + i.line + "! Requested parameter num was " + lmod.num + ", but only " + args.Length + " arguments were passed in!");
							
							uint n = 0;
							try {
								n = Parse.BasicUint(args[lmod.num]);

							} catch (Exception) {
								cvterror(i, "Failed to convert argument to number at line " + i.line + "! Argument '" + args[lmod.num] + "' is not a valid number!");
							}

							if (!ObtainValidLable(Expression.Process(lmod.lable), lastlable, n, out args[lmod.num], out OffsetString lab))
								cvterror(i, "Failed to fetch a valid lable with format '" + Parse.ParseNumber(lmod.lable.Replace("£", baselable), i.line) + "' at line " + i.line + ": Lable already taken.");

							if (debug) Debug(pos + offset, i.line, i.identifier, '~' + args[lmod.num] + " :" + lmod.num + ' ' + (lab != null ? toHexString((uint)lab.offset, 4) : "NULL"));

							if (followlables && lab != null && (uint)lab.offset - offset < skipped.Length && !skipped[(uint)lab.offset - offset]) {
								uint pos2 = pos;
								Convert(lab, StoredLUT, StoredRun, false, false, out string fuck);
								pos = pos2;
							}

							Expression.args = args;
						}
						break;

					case ScriptItemType.LableDo:
						LableCreate lcr = i as LableCreate;
						if(!ObtainValidLable(lcr.lable, lastlable, Parse.ParseUint(lcr.oper, i.line), out string shite, out OffsetString crapp))
							cvterror(i, "Failed to create lable '"+ lcr.lable +"' with operation '"+ lcr.oper +"'!");
						if (debug) Debug(pos + offset, i.line, i.identifier, '~' + shite + ' ' + lcr.oper + ' ' + toHexString((uint)crapp.offset, 4));
						break;

					case ScriptItemType.Comment: {
							// get the comment, and replace any case of escaped { and } with temp chars
							string comm = (i as ScriptComment).comment;
							uint pos2 = pos;

							// translate all the conversion things
							comm = Parse.ParseMultiple(comm, i.line);

							if (debug) Debug(pos + offset, i.line, i.identifier, '%' + comm);
							comm = comm.Replace("\\t", "\t").Replace("\\r", "\r").Replace("\\n", "\n");
							// add the comment
							if (args == null) AddLine(pos2, 0, comm);
							else comment = comm;
						}
						break;

					case ScriptItemType.Print: {
							// get the comment, and replace any case of escaped { and } with temp chars
							string comm = (i as ScriptPrint).comment;
							uint pos2 = pos;

							// translate all the conversion things
							comm = Parse.ParseMultiple(comm, i.line);

							if (debug) Debug(pos + offset, i.line, i.identifier, '+' + comm);
							comm = comm.Replace("\\t", "\t").Replace("\\r", "\r").Replace("\\n", "\n");
							// add the comment
							Console.WriteLine(comm);
						}
						break;

					case ScriptItemType.ArrayItem:
						break;

					default:
						cvterror(i, "Type of item is unknown! This is most likely a programming error in SMPS2ASM!");
						return;
				}
			} catch (DataException e) {
				throw e;

			} catch(Exception e) {
				cvterror(i, e.ToString());
			}
		}

		public bool CheckMacro(ScriptMacro ma) {
			int rangeStart, rangeEnd;
			for(int i = 0;i < ma.pre.Length;i++) {
				// if resolving failed, return
				if (!ma.GetRange(i, out rangeStart, out rangeEnd))
					return false;

				// check if in range
				if (Read(pos + i) < rangeStart || Read(pos + i) > rangeEnd)
					return false;
			}

			pos += (uint)ma.pre.Length;
			return true;
		}

		private void ProcessMacro(ScriptMacro ma, string lastlable, out bool stop) {
			uint pos2 = pos;
			pos -= (uint)ma.pre.Length;
			string db = "";

			// skip all bytes and create debug stuff
			for (int x = 0;x < ma.pre.Length;x++) {
				if (debug) db += ", " + toHexString(Read(pos), 2);
				pos++;
			}

			// fix debug stuff
			if (debug) {
				if (db.Length >= 2) db = '!' + db.Substring(2) + " > " + ma.name + ": ";
				else db = '!' + db + " > " + ma.name + ": ";
			}

			stop = false;
			string[] args = new string[ma.arg.Length];
			uint p = pos;

			// parse all arguments
			int i = 0;
			foreach(string s in ma.arg) {
				args[i] = Parse.ParseNumber(ma.arg[i], ma.line);
				// try to convert args to hex
				try {
					args[i] = toHexString(Parse.BasicInt(args[i]), 2);
				} catch (Exception) { }
				if (debug) db += args[i] +", ";
				i++;
			}

			// write debug info again
			Expression.args = args;
			if (debug) Debug(pos2 + offset, ma.line, ma.identifier, db.Substring(0, db.Length - 2));

			// run inner shite
			ConvertRun(ma.Inner.Items, ref args, lastlable, out stop, out string comment);

			// if comment is not null, add it
			if (comment != null)
				AddLine(p - (uint)ma.pre.Length, (uint)ma.pre.Length + (pos - p), new string[] { ma.name, String.Join(", ", args), comment });
			else AddLine(p - (uint)ma.pre.Length, (uint)ma.pre.Length + (pos - p), new string[] { ma.name, String.Join(", ", args) });

			// remove arguments!
			Expression.args = new string[0];
		}
	}
	
	internal class DataException : Exception {
		public DataException() {
		}

		public DataException(string message) : base(message) {
		}
	}
}