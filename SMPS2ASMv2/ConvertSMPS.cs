using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static SMPS2ASMv2.Program;

namespace SMPS2ASMv2 {
	public class ConvertSMPS {
		// ref to current obj
		public static ConvertSMPS context;

		// here are some variables that arent used in conversion
		public string filein, fileout, baselable;
		public bool[] skipped;

		// and these are conversion
		public S2AScript scr;
		public List<OffsetString> Lables, Lines;
		public byte[] data; // data of the file
		public uint pos = 0; // current position in the script
		public bool followlables = false;// set to true, if we also want to follow new lables

		// these will be gotten from the topmost script
		public string endian = null; // "big" or "little"
		public uint offset = 0;    // offset of the 0th byte of the file. Usually Z80 address where file starts

		public void cvterror(GenericScriptItem i, string v) {
			error("smps2asm.smpss:" + i.line + ": " + v);
		}
		
		private void AddLine(uint pos, uint len, ScriptEquate s) {
			AddLine(pos++, 1, "\b " + s.equ);
		}

		private void AddLine(uint pos, uint len, byte val) {
			AddLine(pos++, 1, "\b " + toHexString(val, 2));
		}

		// array for tabulating different strings to a common place
		private int[] LineAlign = { 8, 24, 64 };
		private void AddLine(uint pos, uint len, string[] lines) {
			string s = "";
			int last = 0;

			for(int i = 0; i < lines.Length;i++) {
				s += new string('\t', (LineAlign[i] - last - 1) / 8 + 1);    // divide by tab len. If its 4, though. Maybe I can add an option later...
				s += lines[i];
				last = LineAlign[i] + lines[i].Length;	// index of characters last in string
			}

			// finally put out
			AddLine(pos, len, s);
		}

		private void AddLine(uint pos, uint len, string line) {
			Lines.Add(new OffsetString(pos + offset, len, line));
		}

		// generate valid lables from a specific rule and position
		private bool ObtainValidLable(string lable, uint position, out string valid, out OffsetString lab) {
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
			int num = GetLablesRule(lable).Count;
			if (num != 0 && !lable.Contains("?")) return false;

			// build lable, add it to pool, and return
			valid = lable.Replace("£", baselable).Replace("?", "" + (num + 1));
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
			if(endian.ToLower() == "\"little\"") {
				return (ushort)((data[addr]) | ((data[addr + 1] << 8)));

			} else if(endian.ToLower() == "\"big\"") {
				return (ushort)((data[addr + 1]) | ((data[addr] << 8)));

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
			Lables = new List<OffsetString>();
			Lines = new List<OffsetString>();
			data = File.ReadAllBytes(filein);
			skipped = new bool[data.Length];
			if (debug) Debug(new string('-', 80));

			// run conveter
			string[] a = null;
			if (debug) Debug("--> Start conversion with subscript ''");
			ConvertRun(scr.subscripts[""].Items, ref a, out bool asses, out string c);
			if (debug) Debug(new string('-', 80));
		}

		// helper method to collect some variables used in conversion
		private void InitConvertVars() {
			try {
				// get global subscript
				ScriptArray b = scr.subscripts[""];
				endian = b.GetEquate("endian").val;
				offset = Parse.BasicUint(b.GetEquate("offset").val);
				if (debug) Debug("--> InitConvertVars: endian="+ endian +" offset="+ toHexString(offset, 4));

			} catch (Exception e) {
				cvterror(null, e.ToString());
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

							// if any of these appear in any LUT, there is some programming error! Hopefully this never happens tho =O
							case ScriptItemType.Operation:case ScriptItemType.Condition:case ScriptItemType.Repeat:
							case ScriptItemType.Goto:case ScriptItemType.Stop:case ScriptItemType.Executable:
							case ScriptItemType.ArgMod:case ScriptItemType.LableMod:case ScriptItemType.LableDo:
							case ScriptItemType.Comment:
								cvterror(entry, "Somehow optimized look-up-table contains unoptimizable elements! Report to developer.");
								break;
						}

					eid++;
				}
			}

			return rout;
		}

		private void Convert(string label, GenericScriptItem[] LUT, List<GenericScriptItem>[] run, bool str, out string text) {
			text = null;
			// init some vars
			InitConvertVars();

			if (label != "") {
				foreach (OffsetString o in GetLablesRule(label)) {
					Convert(o, LUT, run, str, out text);
				}
			} else Convert(new OffsetString(offset, 0, null), LUT, run, str, out text);
		}

		private GenericScriptItem[] StoredLUT = null;
		private List<GenericScriptItem>[] StoredRun = null;
		private void Convert(OffsetString o, GenericScriptItem[] LUT, List<GenericScriptItem>[] run, bool str, out string text) {
			// empty list to be ref'd later (idk =/ )
			string[] args = null;
			text = null;

			// check and set pointer for this lable
			if (o.offset == null)
				error("Pointer to the lable is null! This seems like a programming error. Report to developers.");

			pos = (uint)(o.offset - offset);
			if(o.line != null) Console.WriteLine("Parsing " + o.line + " at " + toHexString((double)o.offset, 4) + "...");
			if (debug) Debug("--: "+ o.line +" "+ toHexString((double)o.offset, 4) +" LUT="+ (LUT != null) +" run="+ (run != null));

			// if no LUT, only run sequentially
			if (LUT == null) {
				followlables = false;
				foreach (List<GenericScriptItem> en in run) {
					ConvertRun(en, ref args, out bool stop, out string c);
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

			while (true) {
				if (ProcessItem(LUT, str, out bool stop, out text)) {
					if (stop) break;

				} else if (run != null && run.Length > 0) {
					foreach (List<GenericScriptItem> en in run) {
						ConvertRun(en, ref args, out bool stop2, out string c);
						if (stop2) return;
					}
				} else {
					if (str) text = toHexString(data[pos], 2);
					else AddLine(pos, 1, data[pos]);
					SkipByte(pos++);    // skip over current byte

					if (stop) break;
				}
			}
		}

		// get a list of lables that match regex. Lables may additionally use £ to get the base lable (user input) and ? for regex anything matches.
		private List<OffsetString> GetLablesRule(string label) {
			// create regex to match all lables against. Additionally, compile it if enough lable entries.
			Regex r;
			if(Lables.Count > 25)
				r = new Regex(label.Replace("?", ".*").Replace("£", baselable), RegexOptions.IgnoreCase | RegexOptions.Compiled);
			else r = new Regex(label.Replace("?", ".*").Replace("£", baselable), RegexOptions.IgnoreCase);

			List<OffsetString> ret = new List<OffsetString>();
			foreach(OffsetString o in Lables) {
				// match line against regex. If match, add to list
				if (r.IsMatch(o.line)) ret.Add(o);
			}

			return ret;
		}

		private void ConvertRun(List<GenericScriptItem> s, ref string[] args, out bool stop, out string comment) {
			// default values
			comment = null;
			stop = false;

			foreach(GenericScriptItem i in s) {
				ProcessItem(i, ref args, out stop, out comment);
				if (stop) break;
			}
			
			return;
		}

		private bool ProcessItem(GenericScriptItem[] lut, bool str, out bool stop, out string text) {
			text = null;
			// default values
			stop = str;

			// get next byte
			byte d = data[pos];
			if (lut[d] == null) return false;

			switch (lut[d].type) {
				case ScriptItemType.Equate:
					// just write equate
					if (debug) Debug(pos + offset, lut[d].line, str, (lut[d] as ScriptEquate).equ +" "+ toHexString(d, 2));
					if (str) text = (lut[d] as ScriptEquate).equ;
					else AddLine(pos, 1, lut[d] as ScriptEquate);
					SkipByte(pos++);
					return true;

				case ScriptItemType.Macro:
					if (str) cvterror(lut[d], "Macros can not be used in Argument Modifiers!");
					SkipByte(pos++);	// skip over current byte
					ProcessMacro(lut[d] as ScriptMacro, out stop);
					return true;

				case ScriptItemType.ArrayItem:
					pos++;
					return ProcessItem((lut[d] as ScriptArrayItem).Optimized, str, out stop, out text);

				case ScriptItemType.Import:
					ScriptArray sc = scr.GetSubscript((lut[d] as ScriptImport).name);
					if (sc.Optimized == null) sc.Optimize();
					uint x = pos;
					bool ret = ProcessItem(scr.GetSubscript((lut[d] as ScriptImport).name).Optimized, str, out stop, out text);
					pos = x;
					stop = true;
					return ret;

				case ScriptItemType.Operation:case ScriptItemType.Condition:case ScriptItemType.Repeat:
				case ScriptItemType.Goto:case ScriptItemType.Stop:case ScriptItemType.Executable:
				case ScriptItemType.ArgMod:case ScriptItemType.LableMod:case ScriptItemType.LableDo:
				case ScriptItemType.Comment:
					cvterror(lut[d], "Somehow optimized look-up-table contains unoptimizable elements! Report to developer.");
					break;

				case ScriptItemType.NULL:
					cvterror(lut[d], "Type of item is NULL! This is most likely a programming error in SMPS2ASM!");
					break;
			}

			return false;
		}

		private void ProcessItem(GenericScriptItem i, ref string[] args, out bool stop, out string comment) {
			// default values
			comment = null;
			stop = false;

			try {
				// process an item needed for conversion, use switch on the type
				switch (i.type) {
					case ScriptItemType.NULL:
						cvterror(i, "Type of item is NULL! This is most likely a programming error in SMPS2ASM!");
						return;

					case ScriptItemType.Equate:
						(i as ScriptEquate).Evaluate();
						if (debug) Debug(pos + offset, i.line, i.identifier, '='+ (i as ScriptEquate).equ +' '+ (i as ScriptEquate).val +' '+ (i as ScriptEquate).value);
						break;

					case ScriptItemType.Macro:
						if(CheckMacro(i as ScriptMacro)) ProcessMacro(i as ScriptMacro, out stop);
						break;

					case ScriptItemType.Operation:
						string rsop = Parse.ParseNumber((i as ScriptOperation).operation, null, i.parent);
						if (debug) Debug(pos + offset, i.line, i.identifier, '$' + (i as ScriptOperation).operation + ' ' + rsop);
						break;

					case ScriptItemType.Condition:
						ScriptCondition cond = i as ScriptCondition;
						string[] null_ = null;

						if (Parse.ParseBool(cond.condition, cond.line, cond.parent)) {
							ConvertRun(cond.True.Items, ref null_, out stop, out comment);
							if (debug) Debug(pos + offset, i.line, i.identifier, "¤ " + cond.condition + " (true)");

						} else {
							ConvertRun(cond.False.Items, ref null_, out stop, out comment);
							if (debug) Debug(pos + offset, i.line, i.identifier, "¤ " + cond.condition + " (false)");
						}
						break;

					case ScriptItemType.Repeat:
						string[] _null = null;
						int ccc = Parse.ParseInt((i as ScriptRepeat).count, i.line, i.parent);
						if (debug) Debug(pos + offset, i.line, i.identifier, "* " + ccc + " {");

						for (int nnn = ccc;nnn > 0;nnn--) {
							ConvertRun((i as ScriptRepeat).Inner.Items, ref _null, out stop, out comment);
						}
						break;

					case ScriptItemType.Goto:
						ScriptGoto gotto = i as ScriptGoto;
						uint off = Parse.ParseUint(gotto.offset, i.line, i.parent);

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
							Convert(sex.label, Combine(opt.ToArray()), dir.ToArray(), false, out string fuck);
						}
						break;

					case ScriptItemType.Import:
						if (debug) Debug(pos + offset, i.line, i.identifier, '?' + (i as ScriptImport).name + ';');
						ConvertRun(scr.GetSubscript((i as ScriptImport).name).Items, ref args, out stop, out comment);
						break;

					case ScriptItemType.ArgMod:
						// store all kinds of variables and properly fix pos
						ScriptArgMod am = i as ScriptArgMod;
						byte[] dat = data;
						uint pos3 = pos;
						bool fol = followlables;
						pos -= (uint)((args.Length - 1) - am.num);
						data = new byte[]{ Parse.BasicByte(args[am.num]) };

						if (debug) Debug(pos + offset, i.line, i.identifier, ":" + am.num + ' ' + toHexString(data[0], 2) +' '+ toHexString(pos, 4));
						if (am.Inner.Optimized == null) am.Inner.Optimize();
						Convert("", am.Inner.Optimized, null, true, out args[am.num]);
						followlables = fol;
						pos = pos3;
						data = dat;
						break;

					case ScriptItemType.LableMod:
						LableMod lmod = i as LableMod;
						// check if this request is valid
						if (args == null)
							cvterror(i, "No macro parameters were passed in to modify!");
						if (args.Length <= lmod.num)
							cvterror(i, "Not enough macro parameters were passed in for lable mod at line "+ i.line +"! Requested parameter num was "+ lmod.num +", but only "+ args.Length +" arguments were passed in!");

						uint n = 0;
						try {
							n = Parse.BasicUint(args[lmod.num]);

						} catch (Exception) {
							cvterror(i, "Failed to convert argument to number at line " + i.line + "! Argument '"+ args[lmod.num] +"' is not a valid number!");
						}

						if(!ObtainValidLable(Parse.GetAllOperators(lmod.lable, i.parent), n, out args[lmod.num], out OffsetString lab))
							cvterror(i, "Failed to fetch a valid lable with format '"+ Parse.ParseNumber(lmod.lable.Replace("£", baselable), i.line, i.parent) + "' at line "+ i.line +": Lable already taken.");

						if (debug) Debug(pos + offset, i.line, i.identifier, '~' + args[lmod.num] +" :"+ lmod.num + ' ' + toHexString((uint)lab.offset, 4));

						if (followlables && lab != null && !skipped[(uint)lab.offset - offset]) {
							uint pos2 = pos;
							Convert(lab, StoredLUT, StoredRun, false, out string fuck);
							pos = pos2;
						}
						break;

					case ScriptItemType.LableDo:
						LableCreate lcr = i as LableCreate;
						if(!ObtainValidLable(lcr.lable, Parse.ParseUint(lcr.oper, i.line, i.parent), out string shite, out OffsetString crapp))
							cvterror(i, "Failed to create lable '"+ lcr.lable +"' with operation '"+ lcr.oper +"'!");
						if (debug) Debug(pos + offset, i.line, i.identifier, '~' + shite + ' ' + lcr.oper + ' ' + toHexString((uint)crapp.offset, 4));
						break;

					case ScriptItemType.Comment: {
							// get the comment, and replace any case of escaped { and } with temp chars
							string comm = (i as ScriptComment).comment;
							uint pos2 = pos;

							// translate all the conversion things
							while (comm.Contains("{") && comm.Contains("}")) {
								int i1 = comm.IndexOf('{'), i2 = comm.IndexOf('}');
								string arg = Parse.ParseNumber(comm.Substring(i1 + 1, i2 - i1 - 1), i.line, i.parent);
								comm = comm.Substring(0, i1) + arg + comm.Substring(i2 + 1);
							}

							if (debug) Debug(pos + offset, i.line, i.identifier, '%' + comm);
							comm = comm.Replace("\\t", "\t").Replace("\\r", "\r").Replace("\\n", "\n");
							// add the comment
							if (args == null) AddLine(pos2, 0, comm);
							else comment = comm;
						}
						break;

					case ScriptItemType.ArrayItem:
						break;

					default:
						cvterror(i, "Type of item is unknown! This is most likely a programming error in SMPS2ASM!");
						return;
				}
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
				if (data[pos + i] < rangeStart || data[pos + i] > rangeEnd)
					return false;
			}

			pos += (uint)ma.pre.Length;
			return true;
		}

		private void ProcessMacro(ScriptMacro ma, out bool stop) {
			uint pos2 = pos;
			pos -= (uint)ma.pre.Length;
			string db = "";

			// skip all bytes and create debug stuff
			for (int x = 0;x < ma.pre.Length;x++) {
				if (debug) db += ", " + toHexString(data[pos], 2);
				SkipByte(pos++);
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
				args[i] = Parse.ParseNumber(ma.arg[i], ma.line, ma.Inner);
				// try to convert args to hex
				try {
					args[i] = toHexString(Parse.BasicInt(args[i]), 2);
				} catch (Exception) { }
				if (debug) db += args[i] +", ";
				i++;
			}

			// write debug info again
			if (debug) Debug(pos2 + offset, ma.line, ma.identifier, db.Substring(0, db.Length - 2));

			// run inner shite
			ConvertRun(ma.Inner.Items, ref args, out stop, out string comment);

			// if comment is not null, add it
			if (comment != null)
				AddLine(p - (uint)ma.pre.Length, (uint)ma.pre.Length + (pos - p), new string[] { ma.name, String.Join(", ", args), comment });
			else AddLine(p - (uint)ma.pre.Length, (uint)ma.pre.Length + (pos - p), new string[] { ma.name, String.Join(", ", args) });
		}
	}
}