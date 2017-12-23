using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static SMPS2ASMv2.Program;

namespace SMPS2ASMv2 {
	public class S2AScript {
		public static S2AScript context;
		// subscripts array
		public Dictionary<string, ScriptArray> subscripts;
		// array ID for script arrays, for some simpler management
		public static uint arrayID = 0;

		// construct a script error
		public static void screrr(uint lnum, string v) {
			error("smps2asm.smpss:" + lnum + ": " + v);
		}

		public S2AScript(string script, string[] args, string type) {
			context = this;

			Console.WriteLine("Parsing script...");
			try {
				ParseScript("=type '"+ type.ToLowerInvariant() +"'\n"+ File.ReadAllText(script).Replace("\t", "").Replace("\r", ""), args);
			} catch(Exception e) {
				error(e.ToString());
			}
		}

		private void ParseScript(string data, string[] args) {
			if (debug) Debug("--; Prepare script parse");
			subscripts = new Dictionary<string, ScriptArray>();
			// create stack to push and pop items from. Allow for simpler code
			Stack<ScriptArray> stack = new Stack<ScriptArray>();
			{
				// create the default subscript allowing for code initialization. Parent is null.
				ScriptArray f = new ScriptArray(null);
				stack.Push(f);
				subscripts.Add("", f);
			}

			// chars to trim from strings
			char[] trim = new char[] { ' ', '\t' };

			// some variables to help with stuff
			Stack<ScriptCondition> co = new Stack<ScriptCondition>();
			if (debug) Debug(new string('-', 80));

			// use line num to accurately report issues
			uint lnum = 0, tabs = 0;
			foreach (string ln in data.Replace("\r", "").Split('\n')) {
				lnum++;

				// trim whitespaces and tabs from start. Also C# is gay sometimes
				string line = ln.Trim(trim);
				// ignore empty lines
				if (line.Length > 0) {
					try {
						switch (line.ElementAt(0)) {
							case '#':
								//ignore comments
								break;

							case '}':
								tabs--;
								// we need 2 entries to remove one of them, to have one entry still in stack
								if (stack.Count < 2) {
									screrr(lnum, "Stack is empty. Maybe there is an extra script block end?");
								}

								// } { -> else
								if (line.EndsWith("{")) {
									// if co not set, its a problem
									if(co.Count <= 0) screrr(lnum, "Else called when not in condition block");
									// if already in false block, error
									if (stack.Peek() == co.Peek().False) screrr(lnum, "Else called when already inside else block");

									// pop last entry and push false block in
									stack.Pop();
									stack.Push(co.Peek().False);
									if(debug) Debug(lnum, tabs++, "}{");

								} else {
									stack.Pop();
									// pop from stack
									if(co.Count > 0 && (co.Peek().True == stack.Peek() || co.Peek().False == stack.Peek())) co.Pop();
									if (debug) Debug(lnum,tabs, "}");
								}
								break;

							case '?':
								if (line.Length < 3) {
									// if line is too short, it is a major problem
									screrr(lnum, "Name not specified.");
								}

								string name = line.Substring(1, line.Length - 2).Trim(trim);
								if (name.Length <= 0) {
									// no usable name
									screrr(lnum, "Name not specified.");
								}

								if (line.EndsWith("{")) {
									// declaring subscript
									ScriptArray f = new ScriptArray(stack.Peek());
									subscripts.Add(name, f);
									stack.Push(f);
									if (debug) Debug(lnum,tabs++, '?' + name + " {");

								} else if (line.EndsWith(";")) {
									// subscript importing
									stack.Peek().Add(new ScriptImport(lnum, stack.Peek(), name));
									if (debug) Debug(lnum,tabs, '?' + name + ';');

								} else {
									// if line doesnt end with ; or {, there is an issue
									screrr(lnum, "invalid end token.");
								}
								break;

							case '/':
								// get the label only if there is a space char
								string lbl = "";
								int ind;
								if ((ind = line.IndexOf(' ')) != -1) {
									lbl = line.Substring(1, ind - 1);
									line = line.Substring(ind + 1);
								}

								// check if rest of the info exists
								if (line.Length < 2) {
									screrr(lnum, "Illegal line");
								}

								List<bool> types = new List<bool>();
								List<string> names = new List<string>();

								while (line.Length > 0) {
									// get the caller type
									types.Add(line.StartsWith("?"));
									if (!types.Last() && !line.StartsWith(">"))
										screrr(lnum, "Illegal type '"+ line.ElementAt(0) +"'");

									// get the offset of the last char
									int last = line.IndexOf(' ') + 1;
									if (last == 0) last = line.Length;

									// add the string in and remove from line
									names.Add(line.Substring(1, last - 1).Trim(trim));
									line = line.Substring(last);
								}
								
								stack.Peek().Add(new ScriptExecute(lnum, stack.Peek(), lbl, types.ToArray(), names.ToArray()));

								// write debug info
								if (debug) {
									string db = '/' + lbl;
									for(int i = 0;i < types.Count;i++) {
										db += ' ' + (types[i] ? "?" : ">") + names[i];
									}

									Debug(lnum,tabs, db);
								}
								break;

							case '=':
								int indx = line.IndexOf(' ');
								if(indx == -1) screrr(lnum, "Expected a whitespace (' '), but found none.");

								// just some sanity checks, dont worry
								string equ = line.Substring(1, indx - 1), val = line.Substring(indx + 1);
								if(equ.Length < 1) screrr(lnum, "Equate name not specified.");
								if(val.Length < 1) screrr(lnum, "Equate value not specified.");

								ScriptEquate scre = new ScriptEquate(lnum, stack.Peek(), equ, val);
								stack.Peek().Add(scre);
								if (debug) Debug(lnum,tabs, '=' + equ + ' '+ val + (scre.calculated ? " "+ scre.value : ""));
								break;

							case '!':
								// skip ! and any spaces
								line = line.Substring(1).TrimStart(trim);

								// loop til all arguments are found
								int index;
								List<string> pre = new List<string>();
								while (!line.StartsWith(">")) {
									// find the next ,
									index = line.IndexOf(',');
									// if > is earlier than , (because arguments also can have them), then use > instead
									if (line.IndexOf('>') < index || index < 1) index = line.IndexOf('>');
									if (index < 1) screrr(lnum, "Invalid or nonexistent trigger byte at macro block.");

									// create new argument and 
									pre.Add(line.Substring(0, index).Trim(trim));
									line = line.Substring(index);
									// if starts with , then remove it
									if (line.StartsWith(",")) line = line.Substring(1);
								}

								// get the name
								index = line.IndexOf(':');
								if(index == -1) screrr(lnum, "Expecting semicolon (':')");
								string nam = line.Substring(1, index - 1).Trim(trim);
								line = line.Substring(index + 1).TrimStart(trim);
								if(nam.Length < 1) screrr(lnum, "Invalid name");

								// get the arguments
								List<string> arg = new List<string>();
								while (!line.StartsWith(";") && !line.StartsWith("{")) {
									// find the next ,
									index = line.IndexOf(',');
									// if failed, then try to find the end icon
									if (index < 1) index = line.IndexOf(';');
									if (index < 1) index = line.IndexOf('{');
									if (index < 1) screrr(lnum, "Invalid or nonexistent argument at macro block.");

									// create new argument and 
									arg.Add(line.Substring(0, index).Trim(trim));
									line = line.Substring(index);
									// if starts with , then remove it
									if (line.StartsWith(",")) line = line.Substring(1);
								}

								// create a new macro
								ScriptMacro s = new ScriptMacro(lnum, stack.Peek(), nam, pre.ToArray(), arg.ToArray());
								stack.Peek().Add(s);

								// if ends with this, put shit in inner block
								if (line.EndsWith("{")) 
									stack.Push(s.Inner);

								// write debug shit
								if (debug) {
									Debug(lnum,tabs, '!' + string.Join(", ", pre) + " > "+ nam +": " + string.Join(", ", arg) +  line.ElementAt(line.Length - 1));
									if (line.EndsWith("{")) tabs++;
								}
								break;

							case '@':
								// check if there is a space
								int indie = line.IndexOf(' ');
								if (indie == -1) screrr(lnum, "Expected whitespace (' '), but found none.");
								// get the equate name and remove from line
								string eqq = line.Substring(1, indie - 1);
								line = line.Substring(indie + 1).TrimStart(trim);

								// check for next space
								indie = line.IndexOf(' ');
								if (indie == -1) screrr(lnum, "Expected whitespace (' '), but found none.");
								// get the arg number
								int argnum;
								if(!Int32.TryParse(line.Substring(0, indie), out argnum))
									screrr(lnum, "Line '"+ line.Substring(0, indie) + "' can not be parsed as a number!");

								string da;
								// check if there are arguments at this offset
								if (argnum >= args.Length) {
									// print out the string
									indie = line.IndexOf("\"") + 1;
									if (indie == 0)
										screrr(lnum, "Expected string, but found none.");
									Console.Write(line.Substring(indie, line.LastIndexOf("\"") - indie) + ": ");

									da = ConsoleArguments.Get(args, new ArgHandler[] { new ArgHandler(line.Substring(indie, line.LastIndexOf("\"") - indie) + ":", (_data, ret) => ret ? _data : null), }, new ButtonHandler[] { })[0];
									if (debug) Debug(lnum,tabs, "@? " + eqq + ' ' + argnum + ' ' + (indie > 0 ? line.Substring(indie, line.LastIndexOf("\"") - indie) : "INVALID") + ' ' + da);

								} else {
									// read argument
									da = args[argnum];
									if (debug) {
										indie = line.IndexOf("\"") + 1;
										Debug(lnum,tabs, "@ " + eqq + ' ' + argnum + ' ' + (indie > 0 ? line.Substring(indie, line.LastIndexOf("\"") - indie) : "INVALID") +' '+ da);
									}
								}

								stack.Peek().Add(new ScriptEquate(lnum, stack.Peek(), eqq, da));
								break;

							case 'c':
								// ehhhhhhh =/
								if (!line.EndsWith("{"))
									screrr(lnum, "No opening script block.");
								// create the condition block, and push true block in stack
								co.Push(new ScriptCondition(lnum, stack.Peek(), line.Substring(1, line.Length - 2).Trim(trim)));
								stack.Peek().Add(co.Peek());
								stack.Push(co.Peek().True);
								if (debug) Debug(lnum, tabs++, "¤ " + line.Substring(1, line.Length - 2).Trim(trim) + " {");
								break;

							case 'f':
								// check for { at the end of line
								int inde = line.IndexOf('{');
								if (inde == -1 || !line.EndsWith("{")) screrr(lnum, "Expected block start ('{') at end of line, but found none.");
								string counter = line.Substring(1, inde - 1).Trim(trim);
								ScriptRepeat r = new ScriptRepeat(lnum, stack.Peek(), counter);
								stack.Peek().Add(r);
								stack.Push(r.Inner);
								if (debug) Debug(lnum,tabs++, "* " + counter +" {");
								break;

							case 'w':
								// check for { at the end of line
								int ine = line.IndexOf('{');
								if (ine == -1 || !line.EndsWith("{")) screrr(lnum, "Expected block start ('{') at end of line, but found none.");
								string cond = line.Substring(1, ine - 1).Trim(trim);
								ScriptWhile w = new ScriptWhile(lnum, stack.Peek(), cond);
								stack.Peek().Add(w);
								stack.Push(w.Inner);
								if (debug) Debug(lnum, tabs++, "w " + cond + " {");
								break;

							case ':':
								// check for { at the end of line
								int inxd = line.IndexOf('{');
								if (inxd == -1 || !line.EndsWith("{")) screrr(lnum, "Expected block start ('{') at end of line, but found none.");
								string num = line.Substring(1, inxd - 1).Trim(trim);

								try {
									ScriptArgMod m = new ScriptArgMod(lnum, stack.Peek(), num);
									stack.Peek().Add(m);
									stack.Push(m.Inner);
									if (debug) Debug(lnum,tabs++, ": " + m.num + " {");

								} catch(Exception) {
									screrr(lnum, "Failed to parse '"+ num +"'!");
								}
								break;

							case '~':
								int indix = line.IndexOf(' ');
								if (indix == -1) screrr(lnum, "Expected a whitespace (' '), but found none.");

								// just some sanity checks, dont worry
								string labl = line.Substring(1, indix - 1), type = line.Substring(indix + 1);
								if (labl.Length < 1) screrr(lnum, "Lable name not specified.");
								if (type.Length < 1) screrr(lnum, "Lable type or command not specified.");

								if (type.StartsWith(":")) {
									// lable mod
									LableMod l = new LableMod(lnum, stack.Peek(), labl, type.Substring(1));
									stack.Peek().Add(l);
									if (debug) Debug(lnum,tabs, '~' + labl + " :"+ l.num);

								} else { // lable create
									stack.Peek().Add(new LableCreate(lnum, stack.Peek(), labl.Trim(trim), type));
									if (debug) Debug(lnum,tabs, '~' + labl + ' ' + type);
								}
								break;

							case '$':
								stack.Peek().Add(new ScriptOperation(lnum, stack.Peek(), line = line.Substring(1).Trim(trim)));
								if (debug) Debug(lnum,tabs, '$' + line);
								break;

							case '>':
								// if line not long enough, problem
								if(line.Length < 3) screrr(lnum, "Expected type and offset!");
								// check if valid type
								char ttype = line.ElementAt(1);
								if(ttype != 'a' && ttype != 'b' && ttype != 'f')
									screrr(lnum, "Goto type '"+ ttype +"' not recognized!");

								// arg1 = type (char), arg2 = rest of the line
								stack.Peek().Add(new ScriptGoto(lnum, stack.Peek(), ttype, line = line.Substring(2).Trim(trim)));
								if (debug) Debug(lnum,tabs, '>' + ttype +' '+ line);
								break;

							case ';':
								stack.Peek().Add(new ScriptStop(lnum, stack.Peek()));
								if (debug) Debug(lnum, tabs, ";");
								break;

							case '%':
								stack.Peek().Add(new ScriptComment(lnum, stack.Peek(), line.Substring(1)));
								if (debug) Debug(lnum,tabs, line);
								break;

							case '+':
								stack.Peek().Add(new ScriptPrint(lnum, stack.Peek(), line.Substring(1)));
								if (debug) Debug(lnum, tabs, line);
								break;

							default:
								// incase we cant figure out what command this is
								screrr(lnum, "Symbol not recognized: '" + line.ElementAt(0) + "'");
								return;
						}
					} catch (Exception e) {
						screrr(lnum, e.ToString());
					}
				}
			}

			if(debug) Debug(new string('-', 80));
		}

		// safe method for getting subscripts
		public ScriptArray GetSubscript(string name) {
			if (name == null) return null;
			if (subscripts.ContainsKey(name))
				return subscripts[name];

			return null;
		}
	}
}