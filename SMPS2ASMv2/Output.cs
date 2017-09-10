using System;
using System.IO;
using System.Linq;
using static SMPS2ASMv2.Program;

namespace SMPS2ASMv2 {
	public static class Output {
		public static void DoIt(ConvertSMPS cvt) {
			if (debug) Debug("--; Prepare output to "+ cvt.fileout);
			// if file exists already
			if (File.Exists(cvt.fileout)) File.Delete(cvt.fileout);
			// create new writer
			StreamWriter writer = new StreamWriter(cvt.fileout);

			// get ordered list of lables and lines.
			OffsetString[] la = cvt.Lables.OrderBy(o => o.offset).ToArray();
			OffsetString[] li = cvt.Lines.OrderBy(o => o.offset).ToArray();
			int lai = 0, lii = 0;

			// next byte to check for unused
			uint check = 0;
		//	Debug(li, la, cvt);

			// used for checking if line already used
			OffsetString last = null;

			// used for nicely formatting dc.b's
			string line = "\tdc.b ";
			int bytes = 0;
			bool unused = false; // if last was unused
			bool lastlable = true; // if last line was a lable. Essentially just omits extra newline
			if (debug) Debug(new string('-', 80));

			for (uint i = cvt.offset; i <= cvt.offset + cvt.data.Length;i++) {
				// write lables for this byte
				while(lai < la.Length && la[lai].offset <= i) {
					if (la[lai].offset < i) {
						lai++;

					} else {
						// if already unused bytes on the line, save them
						if (bytes > 0) {
							lastlable = false;
							writer.WriteLine(line.Substring(0, line.Length - 2) + (unused ? "\t; Unused" : ""));
							if (debug) Debug(i, line.Substring(0, line.Length - 2) + (unused ? "\t; Unused" : ""));
							line = "\tdc.b ";
							bytes = 0;
						}

						if (debug) Debug(i, la[lai].line + ":");
						writer.WriteLine((lastlable ? "" : "\n") + la[lai].line + ":");
						lai++;
						lastlable = true;
					}
				}

				if(check <= i) last = null;

				bool found = false;
				while (lii < li.Length && li[lii].offset <= i) {
					if (li[lii].offset < i) {
						lii++;
					} else {
						lastlable = false;
						found = true;
						if (li[lii].line.ElementAt(0) == '\b') {
							// if 'db xx', it is a byte value
							if (last != null && last.length > 0 && li[lii].length > 0 && i <= check) {
								if (last.line != li[lii].line) {
									if (debug) Debug("--% " + toHexString((double)last.offset, 4) + " '" + last.line.Replace("\b", "db") + "' <> '" + li[lii].line.Replace("\b", "db") + "'");
									Console.WriteLine("WARNING! Line '" + last.line.Replace("\b", "db") + "' conflicts with line '" + li[lii].line.Replace("\b", "db") + "' at " + toHexString((double)last.offset, 4) + "!");
									goto nowrite;

								} else goto nowrite;
							}

							// if already unused bytes on the line, save them
							if (unused) {
								if (bytes > 0) writer.WriteLine(line.Substring(0, line.Length - 2) + "\t; Unused");
								if (debug) Debug(i, line.Substring(0, line.Length - 2) + "\t; Unused");
								line = "\tdc.b ";
								bytes = 0;
							}

							// add actual data in
							last = li[lii];
							line += last.line.Substring(2) + ", ";
							if (debug) Debug("--& " + last.line.Substring(2));
							bytes++;
							unused = false;

							// if enough data, save it
							if (bytes >= 8) {
								writer.WriteLine(line.Substring(0, line.Length - 2));
								if (debug) Debug(i, line.Substring(0, line.Length - 2));
								line = "\tdc.b ";
								bytes = 0;
							}

							nowrite:;

						} else {
							// else direct string
							if (last != null && last.length > 0 && li[lii].length > 0 && i <= check) {
								if (last.line != li[lii].line) {
									//	writer.WriteLine(li[lii].line);
									if (debug) Debug("--% " + toHexString((double)last.offset, 4) + " '" + last.line.Replace("\b", "db") + "' <> '" + li[lii].line.Replace("\b", "db") + "'");
									Console.WriteLine("WARNING! Line '" + last.line.Replace("\b", "db") + "' conflicts with line '" + li[lii].line.Replace("\b", "db") + "' at " + toHexString((double)last.offset, 4) + "!");
								}

							} else {
								// if already unused bytes on the line, save them
								if (bytes > 0) {
									writer.WriteLine(line.Substring(0, line.Length - 2) + (unused ? "\t; Unused" : ""));
									if (debug) Debug(i, line.Substring(0, line.Length - 2) + (unused ? "\t; Unused" : ""));
									line = "\tdc.b ";
									bytes = 0;
								}

								// write it
								if (li[lii].length > 0) last = li[lii];
								writer.WriteLine(li[lii].line);
								if (debug) Debug(i, li[lii].line);
							}
						}
						lii++;
					}
				}

				// check if we need to do unused bytes
				if(found && last != null) check = last.length + (uint)last.offset;
				else if(!cvt.skipped[i - cvt.offset]) {
					lastlable = false;
					// if already used bytes on the line, save them
					if (!unused) {
						if (bytes > 0) writer.WriteLine(line.Substring(0, line.Length - 2));
						if (debug) Debug(i, line.Substring(0, line.Length - 2));
						line = "\tdc.b ";
						bytes = 0;
					}

					// add actual data in
					line += toHexString(cvt.data[i - cvt.offset], 2) + ", ";
					if (debug) Debug("--= " + toHexString(cvt.data[i - cvt.offset], 2));
					bytes++;
					unused = true;

					// if enough data, save it
					if (bytes >= 8) {
						writer.WriteLine(line.Substring(0, line.Length - 2) + "\t; Unused");
						if (debug) Debug(i, line.Substring(0, line.Length - 2) + "\t; Unused");
						line = "\tdc.b ";
						bytes = 0;
					}
				}
			}
			writer.Flush();
		}
	}
}