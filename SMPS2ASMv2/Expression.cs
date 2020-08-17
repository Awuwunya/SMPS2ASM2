using System;
using System.Collections.Generic;
using System.Globalization;
using SMPS2ASM;
using static SMPS2ASMv2.Program;

namespace SMPS2ASMv2 {
	public class Expression {
		public static string Process(string buf) {
			try {
				List<TokenInfo> tokens = new List<TokenInfo>();

				// convert text into tokens
				{
					TokenInfoEnum current = TokenInfoEnum.None;
					int start = -1;

					// variables for strings
					char strchar = '\0';
					bool escaped = false;

					for (int i = 0;i < buf.Length;i++) {
						switch (buf[i]) {
							case '"':
							case '\'':
								// string delimiters, check what to do
								if (!escaped) {
									if (strchar == 0) {
										if (current != TokenInfoEnum.None)
											tokens.Add(new TokenInfo(current, start, i));

										strchar = buf[i];
										start = i + 1;
										current = TokenInfoEnum.String;

									} else if (strchar == buf[i]) {
										strchar = '\0';
										tokens.Add(new TokenInfo(TokenInfoEnum.String, start, i));
										current = TokenInfoEnum.None;
									}
								}
								break;

							case '\\':
								// escaping character special behaviour
								if (strchar != 0) escaped ^= true;
								else {
									if (current == TokenInfoEnum.Text) {
										tokens.Add(new TokenInfo(current, start, i));
										current = TokenInfoEnum.None;

									} else if (current != TokenInfoEnum.None)
										error("Expression error: Unexpected \\ in input!\n" + buf);

									tokens.Add(new TokenInfo(TokenInfoEnum.Backslash, i, i));
								}
								break;

							case '=':
								if (tokens.Count > 0 && tokens[tokens.Count - 1].Type == TokenInfoEnum.MathOperator && tokens[tokens.Count - 1].Start == tokens[tokens.Count - 1].End) {
									if (buf[tokens[tokens.Count - 1].End] == '!' || buf[tokens[tokens.Count - 1].End] == '=' || buf[tokens[tokens.Count - 1].End] == '<' || buf[tokens[tokens.Count - 1].End] == '>') {
										tokens[tokens.Count - 1] = new TokenInfo(TokenInfoEnum.MathOperator, tokens[tokens.Count - 1].Start, i);
										break;
									}
								}
								goto case '+';

							case '<':
								if (tokens.Count > 0 && tokens[tokens.Count - 1].Type == TokenInfoEnum.MathOperator && tokens[tokens.Count - 1].Start >= tokens[tokens.Count - 1].End - 1 && buf[tokens[tokens.Count - 1].End] == '<') {
									tokens[tokens.Count - 1] = new TokenInfo(TokenInfoEnum.MathOperator, tokens[tokens.Count - 1].Start, i);
									break;
								}
								goto case '+';

							case '>':
								if (tokens.Count > 0 && tokens[tokens.Count - 1].Type == TokenInfoEnum.MathOperator && tokens[tokens.Count - 1].Start >= tokens[tokens.Count - 1].End - 1 && buf[tokens[tokens.Count - 1].End] == '>') {
									tokens[tokens.Count - 1] = new TokenInfo(TokenInfoEnum.MathOperator, tokens[tokens.Count - 1].Start, i);
									break;
								}
								goto case '+';

							case '&':
								if (tokens.Count > 0 && tokens[tokens.Count - 1].Type == TokenInfoEnum.MathOperator && tokens[tokens.Count - 1].Start >= tokens[tokens.Count - 1].End - 1 && buf[tokens[tokens.Count - 1].End] == '&') {
									tokens[tokens.Count - 1] = new TokenInfo(TokenInfoEnum.MathOperator, tokens[tokens.Count - 1].Start, i);
									break;
								}
								goto case '+';

							case '|':
								if (tokens.Count > 0 && tokens[tokens.Count - 1].Type == TokenInfoEnum.MathOperator && tokens[tokens.Count - 1].Start >= tokens[tokens.Count - 1].End - 1 && buf[tokens[tokens.Count - 1].End] == '|') {
									tokens[tokens.Count - 1] = new TokenInfo(TokenInfoEnum.MathOperator, tokens[tokens.Count - 1].Start, i);
									break;
								}
								goto case '+';

							case '!':
							case '+':
							case '-':
							case '*':
							case '/':
							case '%':
							case '^':
							case '~':
								if (strchar == 0) {
									if (current != TokenInfoEnum.None) {
										tokens.Add(new TokenInfo(current, start, i));
										current = TokenInfoEnum.None;
									}

									tokens.Add(new TokenInfo(TokenInfoEnum.MathOperator, i, i));
								}
								break;

							case '(':
								if (strchar == 0) {
									if (current != TokenInfoEnum.None) {
										tokens.Add(new TokenInfo(current, start, i));
										current = TokenInfoEnum.None;
									}

									tokens.Add(new TokenInfo(TokenInfoEnum.OpenParen, i, i));
								}
								break;

							case ')':
								if (strchar == 0) {
									if (current != TokenInfoEnum.None) {
										tokens.Add(new TokenInfo(current, start, i));
										current = TokenInfoEnum.None;
									}

									tokens.Add(new TokenInfo(TokenInfoEnum.CloseParen, i, i));
								}
								break;

							case '[':
								if (strchar == 0) {
									if (current != TokenInfoEnum.None) {
										tokens.Add(new TokenInfo(current, start, i));
										current = TokenInfoEnum.None;
									}

									tokens.Add(new TokenInfo(TokenInfoEnum.OpenSqu, i, i));
								}
								break;

							case ']':
								if (strchar == 0) {
									if (current != TokenInfoEnum.None) {
										tokens.Add(new TokenInfo(current, start, i));
										current = TokenInfoEnum.None;
									}

									tokens.Add(new TokenInfo(TokenInfoEnum.CloseSqu, i, i));
								}
								break;

							case '.':
								if (strchar == 0) {
									if (current != TokenInfoEnum.None)
										error("Expression error: Unexpected . in input!\n" + buf);

									tokens.Add(new TokenInfo(TokenInfoEnum.Dot, i, i));
								}
								break;

							case ',':
								if (strchar == 0) {
									if (current != TokenInfoEnum.None) {
										tokens.Add(new TokenInfo(current, start, i));
										current = TokenInfoEnum.None;
									}

									tokens.Add(new TokenInfo(TokenInfoEnum.Comma, i, i));
								}
								break;

							case ' ':
							case '\t':
							case '\r':
							case '\n':
								if (strchar == 0 && current != TokenInfoEnum.None) {
									tokens.Add(new TokenInfo(current, start, i));
									current = TokenInfoEnum.None;
								}
								break;

							default:
								if (strchar == 0) {
									if (current == TokenInfoEnum.None) {
										// check if we can start a new number or text sequence
										if (buf[i] == '%' || buf[i] == '$' || (buf[i] >= '0' && buf[i] <= '9')) {
											start = i;
											current = TokenInfoEnum.Number;

										} else if (buf[i] == '_' || ((buf[i] & 0xDF) >= 'A' && (buf[i] & 0xDF) <= 'Z')) {
											start = i;
											current = TokenInfoEnum.Text;

										} else {
											tokens.Add(new TokenInfo(TokenInfoEnum.Error, i, i));
										}
									}
								}
								break;
						}
					}

					// add the final entry if it was incomplete
					if (current != TokenInfoEnum.None) {
						tokens.Add(new TokenInfo(current, start, buf.Length));
						current = TokenInfoEnum.None;
					}
				}

				// evaluate tokens
				{
					// first, find the element that has highest depth
					restart:
					int maxdep = 0, checkstart = 0, checkend = -1;
					int cdep = 0;

					for (int i = 0;i < tokens.Count;i++) {
						if (tokens[i].Type == TokenInfoEnum.OpenParen) {
							if (++cdep > maxdep) {
								maxdep = cdep;
								checkstart = i + 1;
							}

						} else if (tokens[i].Type == TokenInfoEnum.CloseParen) {
							if (maxdep == cdep && checkend <= checkstart)
								checkend = i;
							--cdep;
						}
					}

					if (checkend <= checkstart)
						checkend = tokens.Count;

					// check if there is right amount of parenthesis
					if (cdep != 0) error("Expression error: There are more ('s than )'s.\n" + buf);

					// check which to process first
					next:
					List<int> ignore = new List<int>();

					recheck:
					int maxpre = -1, prepos = 0;

					for (int i = checkstart;i < checkend - 1;i++) {
						int prec = GetPrecedence(tokens[i], ref buf);

						if (prec > maxpre && !ignore.Contains(i)) {
							maxpre = prec;
							prepos = i;
						}
					}

					// deal with this token
					switch (tokens[prepos].Type) {
						default:
							// any other token, usually indicates this is the last one
							if (checkstart - checkend - 2 > 1)
								error("Expression error: Unexpected tokens in input!\n" + buf);

							if (checkend < tokens.Count) tokens.RemoveAt(checkend);
							if (checkstart > 0) tokens.RemoveAt(checkstart - 1);
							if (tokens.Count > 1) goto restart;
							goto ret;

						case TokenInfoEnum.Error:
							error("Expression error: Invalid token " + buf.Substring(tokens[prepos].Start, tokens[prepos].End - tokens[prepos].Start) + " in input!\n" + buf);
							break;

						case TokenInfoEnum.Dot:
							// check if this is a valid operator
							if (tokens.Count > prepos + 1 && tokens[prepos + 1].Type == TokenInfoEnum.Text) {

								// convert Text into a string
								if (!(tokens[prepos + 1].Value is string res))
									res = buf.Substring(tokens[prepos + 1].Start, tokens[prepos + 1].End - tokens[prepos + 1].Start);

								// insert our cool new tokens here
								tokens.RemoveAt(prepos);
								tokens[prepos] = ConvertOperator(res);
								checkend--;

							} else error("Expression error: Unexpected . in input!\n" + buf);
							break;

						case TokenInfoEnum.Backslash:
							// check if this is a valid equate
							if (tokens.Count > prepos + 2 && tokens[prepos + 1].Type == TokenInfoEnum.Text && tokens[prepos + 2].Type == TokenInfoEnum.Backslash) {
								// convert Text into a string
								if (!(tokens[prepos + 1].Value is string res))
									res = buf.Substring(tokens[prepos + 1].Start, tokens[prepos + 1].End - tokens[prepos + 1].Start);

								// convert to equate
								Equate tr = S2AScript.GetEquate(res);
								if (tr == null) error("Expression error: Could not find equate " + res + "\n" + buf);

								// insert our cool new tokens here
								tokens.RemoveAt(prepos);
								tokens.RemoveAt(prepos);
								tokens[prepos] = tr.calculated ? new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = tr.value } : new TokenInfo(TokenInfoEnum.String, -1, -1) { Value = tr.val };
								checkend -= 2;

							} else
								error("Expression error: Unexpected \\ in input!\n" + buf);
							break;

						case TokenInfoEnum.MathOperator:
							if (tokens.Count <= prepos + 1)
								error("Expression error: Expected a value to the right side of operator, but got nothing.\n" + buf);

							// get left and right parameters
							TokenInfo right = tokens[prepos + 1];
							TokenInfo left = null;
							if (prepos > 0)
								left = tokens[prepos - 1];

							// check right parameter type
							if (right.Type == TokenInfoEnum.String) {
								if (left == null)
									error("Expression error: Expected a value on the left side of operator, but got nothing.\n" + buf);

								if (left.Type != TokenInfoEnum.String && left.Type != TokenInfoEnum.Number)
									error("Expression error: Expected a value on the left side of operator, but got something else.\n" + buf);

								// check if this is the +, == or != operators
								switch (buf[tokens[prepos].Start]) {
									case '+':
										tokens[prepos] = new TokenInfo(TokenInfoEnum.String, left.Start, right.End) { Value = ConvertArg(left, ref buf).ToString() + ConvertArg(right, ref buf).ToString() };
										break;

									case '=':
										if (buf[tokens[prepos].End] != '=')
											goto default;
										tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) {
											Value = ConvertArg(left, ref buf).ToString().Equals(ConvertArg(right, ref buf).ToString(), StringComparison.InvariantCultureIgnoreCase) ? 1d : 0d
										};
										break;

									case '!':
										if (tokens[prepos].Start == tokens[prepos].End || buf[tokens[prepos].End] != '=')
											goto default;

										tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) {
											Value = ConvertArg(left, ref buf).ToString().Equals(ConvertArg(right, ref buf).ToString(), StringComparison.InvariantCultureIgnoreCase) ? 0d : 1d
										};
										break;

									default:
										error("Expression error: Operator " + buf.Substring(tokens[prepos].Start, tokens[prepos].End - tokens[prepos].Start) + " is not valid for strings!\n" + buf);
										break;
								}

								// remove old tokens
								tokens.RemoveAt(prepos + 1);
								tokens.RemoveAt(prepos - 1);
								checkend -= 2;
								break;

							} else if (right.Type != TokenInfoEnum.Number)
								error("Expression error: Expected a value on the right side of operator, but got something else.\n" + buf);

							// check for normal operators
							char op = buf[tokens[prepos].Start];
							if (op != '+') {
								if (left == null) {
									// check if we have an unary operator
									if (op != '-' && op != '!' && op != '~')
										error("Expression error: Expected a value on the left side of operator, but got nothing.\n" + buf);
								}

								if (left.Type == TokenInfoEnum.String && op != '!' && op != '=')
									error("Expression error: Operator " + buf.Substring(tokens[prepos].Start, tokens[prepos].End - tokens[prepos].Start) + " is not valid for strings!\n" + buf);
							}

							if (left.Type != TokenInfoEnum.String && left.Type != TokenInfoEnum.Number)
								error("Expression error: Expected a value on the left side of operator, but got something else.\n" + buf);

							// execute operator code
							switch (op) {
								case '+':
									if (left == null) {
										// unary plus
										tokens.RemoveAt(prepos);
										checkend -= 1;
										break;

									} else if (left.Type == TokenInfoEnum.String) {
										tokens[prepos] = new TokenInfo(TokenInfoEnum.String, left.Start, right.End) { Value = ConvertArg(left, ref buf).ToString() + ConvertArg(right, ref buf).ToString() };

									} else tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) + (double)ConvertArg(right, ref buf) };

									// remove old tokens
									tokens.RemoveAt(prepos + 1);
									tokens.RemoveAt(prepos - 1);
									checkend -= 2;
									break;

								case '-':
									if (left == null) {
										// unary minus
										tokens.RemoveAt(prepos);
										tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = -(double)ConvertArg(right, ref buf) };
										checkend -= 1;

									} else {
										tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) - (double)ConvertArg(right, ref buf) };
										tokens.RemoveAt(prepos + 1);
										tokens.RemoveAt(prepos - 1);
										checkend -= 2;
									}
									break;

								case '!':
									if (left == null) {
										if (tokens[prepos].Start != tokens[prepos].End)
											error("Expression error: Expected a value on the left side of operator, but got nothing.\n" + buf);

										// unary not
										tokens.RemoveAt(prepos);
										tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(right, ref buf) == 0 ? 1d : 0d };
										checkend -= 1;

									} else if (left.Type == TokenInfoEnum.String) {
										tokens[prepos] = new TokenInfo(TokenInfoEnum.String, left.Start, right.End) {
											Value = ConvertArg(left, ref buf).ToString().Equals(ConvertArg(right, ref buf).ToString(), StringComparison.InvariantCultureIgnoreCase) ? 0d : 1d
										};
										tokens.RemoveAt(prepos + 1);
										tokens.RemoveAt(prepos - 1);
										checkend -= 2;

									} else {
										tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) == (double)ConvertArg(right, ref buf) ? 0d : 1d };
										tokens.RemoveAt(prepos + 1);
										tokens.RemoveAt(prepos - 1);
										checkend -= 2;
									}
									break;

								case '=':
									if (left.Type == TokenInfoEnum.String) {
										tokens[prepos] = new TokenInfo(TokenInfoEnum.String, left.Start, right.End) {
											Value = ConvertArg(left, ref buf).ToString().Equals(ConvertArg(right, ref buf).ToString(), StringComparison.InvariantCultureIgnoreCase) ? 1d : 0d
										};

									} else tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) == (double)ConvertArg(right, ref buf) ? 1d : 0d };

									tokens.RemoveAt(prepos + 1);
									tokens.RemoveAt(prepos - 1);
									checkend -= 2;
									break;

								case '<':
									if (tokens[prepos].Start == tokens[prepos].End)
										tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) < (double)ConvertArg(right, ref buf) ? 1d : 0d };

									else {
										switch (buf[tokens[prepos].End]) {
											case '=':
												tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) <= (double)ConvertArg(right, ref buf) ? 1d : 0d };
												break;

											case '<':
												tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)((long)ConvertArg(left, ref buf) << (int)ConvertArg(right, ref buf)) };
												break;
										}
									}

									tokens.RemoveAt(prepos + 1);
									tokens.RemoveAt(prepos - 1);
									checkend -= 2;
									break;

								case '>':
									if (tokens[prepos].Start == tokens[prepos].End)
										tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) > (double)ConvertArg(right, ref buf) ? 1d : 0d };

									else {
										switch (buf[tokens[prepos].End]) {
											case '=':
												tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) >= (double)ConvertArg(right, ref buf) ? 1d : 0d };
												break;

											case '>':
												tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)((long)ConvertArg(left, ref buf) >> (int)ConvertArg(right, ref buf)) };
												break;
										}
									}

									tokens.RemoveAt(prepos + 1);
									tokens.RemoveAt(prepos - 1);
									checkend -= 2;
									break;

								case '|':
									if (tokens[prepos].Start == tokens[prepos].End)
										tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)((long)ConvertArg(left, ref buf) | (long)ConvertArg(right, ref buf)) };

									else tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) != 0 || (double)ConvertArg(right, ref buf) != 0 ? 1d : 0d };

									tokens.RemoveAt(prepos + 1);
									tokens.RemoveAt(prepos - 1);
									checkend -= 2;
									break;

								case '&':
									if (tokens[prepos].Start == tokens[prepos].End)
										tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)((long)ConvertArg(left, ref buf) & (long)ConvertArg(right, ref buf)) };

									else tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) != 0 && (double)ConvertArg(right, ref buf) != 0 ? 1d : 0d };

									tokens.RemoveAt(prepos + 1);
									tokens.RemoveAt(prepos - 1);
									checkend -= 2;
									break;

								case '^':
									tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)((long)ConvertArg(left, ref buf) ^ (long)ConvertArg(right, ref buf)) };
									tokens.RemoveAt(prepos + 1);
									tokens.RemoveAt(prepos - 1);
									checkend -= 2;
									break;

								case '~':
									// unary not
									tokens.RemoveAt(prepos);
									tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)~(long)ConvertArg(right, ref buf) };
									checkend -= 1;
									break;

								case '*':
									tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) * (double)ConvertArg(right, ref buf) };
									tokens.RemoveAt(prepos + 1);
									tokens.RemoveAt(prepos - 1);
									checkend -= 2;
									break;

								case '/':
									tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) / (double)ConvertArg(right, ref buf) };
									tokens.RemoveAt(prepos + 1);
									tokens.RemoveAt(prepos - 1);
									checkend -= 2;
									break;

								case '%':
									tokens[prepos] = new TokenInfo(TokenInfoEnum.Number, left.Start, right.End) { Value = (double)ConvertArg(left, ref buf) % (double)ConvertArg(right, ref buf) };
									tokens.RemoveAt(prepos + 1);
									tokens.RemoveAt(prepos - 1);
									checkend -= 2;
									break;
							}
							break;
					}

					if (tokens.Count > 1) goto next;
				}

				ret:
				return ConvertArg(tokens[0], ref buf).ToString();
			} catch(Exception ex) {
				error("Expression error: "+ ex);
			}
			return null;
		}

		// convert token into an object
		private static dynamic ConvertArg(TokenInfo token, ref string buf) {
			if (token.Value != null) return token.Value;
			if (token.Type == TokenInfoEnum.String) return token.Value = buf.Substring(token.Start, token.End - token.Start);
			if (token.Type == TokenInfoEnum.Number) return token.Value = ConvertNum(buf.Substring(token.Start, token.End - token.Start));
			error("Expression error: Expected a value, but got something else.");
			return null;
		}

		// convert a number from string to double
		public static double ConvertNum(string num) {
			int offs = 0;

			switch (num[0]) {
				case '$':
					// hex number
					if (ulong.TryParse(num.Substring(1 + offs), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong value))
						return value;
					error("Expression error: Unable to parse expression as a hex number: "+ num);
					break;

				case '0':
					offs++;
					if (num.Length >= 2 && num[1] == 'x') goto case '$';
					break;

				case '%':
					// binary number
					try {
						return Convert.ToInt64(num.Substring(1), 2);
					} catch (Exception) {
						error("Expression error: Unable to parse expression as a binary number: " + num);
					}
					break;
			}

			// decimal number
			if (double.TryParse(num, NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double val))
				return val;

			error("Expression error: Unable to parse expression as a decimal number: " + num);
			return double.NaN;
		}

		// calculate token precedence based on its contents
		public static int GetPrecedence(TokenInfo token, ref string buf) {
			switch (token.Type) {
				case TokenInfoEnum.Error: return 2000;
				case TokenInfoEnum.Dot: return 1001;
				case TokenInfoEnum.Backslash: return 1000;
				case TokenInfoEnum.MathOperator:
					switch (buf[token.Start]) {
						case '|': return 5;
						case '^': return 6;
						case '&': return 7;
						case '!':
							if (token.End == token.Start) return 13;
							if (buf[token.End] == '=') return 8;
							break;
						case '=': 
							if (token.End == token.Start) error("Expression error: Assignment operator = is not supported!");
							if (buf[token.End] == '=') return 8;
							break;
						case '>': case '<':
							if (token.End == token.Start || buf[token.End] == '=') return 9;
							if (buf[token.End] == buf[token.Start]) return 10;
							break;
						case '+': case '-': return 11;
						case '*': case '/': case '%': return 12;
						case '~': return 13;
					}
					break;
			}

			return -1;
		}

		// convert operator into a token
		private static TokenInfo ConvertOperator(string s) {
			switch (s.ToLowerInvariant()) {
				case "db":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos);
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.Read(ConvertSMPS.context.pos++) };

				case "lb":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 1);
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.Read(ConvertSMPS.context.pos - 1) };

				case "nb":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos);
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.Read(ConvertSMPS.context.pos) };

				case "sb":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return new TokenInfo(TokenInfoEnum.Text, -1, -1) { Value = "" };

				case "dw":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.ReadWord(ConvertSMPS.context.pos - 2) };

				case "lw":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 2);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 1);
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.ReadWord(ConvertSMPS.context.pos - 2) };

				case "nw":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos + 1);
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.ReadWord(ConvertSMPS.context.pos) };

				case "sw":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return new TokenInfo(TokenInfoEnum.Text, -1, -1) { Value = "" };

				case "ow":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.ReadWordOff(ConvertSMPS.context.pos - 2, -1) };

				case "rw":  // fucking Ristar piece of shit
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.ReadWordOff(ConvertSMPS.context.pos - 2, 0) };

				case "dl":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.ReadLong(ConvertSMPS.context.pos) };

				case "ll":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 4);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 3);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 2);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos - 1);
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.ReadLong(ConvertSMPS.context.pos - 4) };

				case "nl":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos + 1);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos + 2);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos + 3);
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.ReadLong(ConvertSMPS.context.pos) };

				case "sl":
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					ConvertSMPS.context.SkipByte(ConvertSMPS.context.pos++);
					return new TokenInfo(TokenInfoEnum.Text, -1, -1) { Value = "" };

				case "pc":
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = (ConvertSMPS.context.offset + ConvertSMPS.context.pos) };

				case "sz":
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.data.Length };

				case "of":
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertSMPS.context.offset };

				case "an": {
						uint off = (uint)ConvertSMPS.context.data.Length + ConvertSMPS.context.offset;

						foreach (OffsetString o in ConvertSMPS.context.Lables) {
							if (o.offset > ConvertSMPS.context.pos + ConvertSMPS.context.offset && o.offset != null && o.offset < off) {
								off = (uint)o.offset;
							}
						}
						return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = off };
					}

				case "al": {
						uint off = 0;

						foreach (OffsetString o in ConvertSMPS.context.Lables) {
							if (o.offset <= ConvertSMPS.context.pos + ConvertSMPS.context.offset && o.offset != null && o.offset >= 0) {
								off = (uint)o.offset;
								break;
							}
						}
						return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = off };
					}

				case "ms":
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = timer.ElapsedMilliseconds };

				case "cc":
					return new TokenInfo(TokenInfoEnum.Text, -1, -1) { Value = ""+ Console.Read() };

				case "ci":
					if (long.TryParse(Console.ReadLine(), out long r)) return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = r };
					return new TokenInfo(TokenInfoEnum.Text, -1, -1) { Value = "" };

				case "ri":
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = (random.Next() & 0xFFFF) };

				case "rf":
					return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = random.NextDouble() };

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

			error("Expression error: Operator ."+ s +" is invalid!");
			return null;
		}

		public static Random random = new Random();
		public static string[] args = new string[0];

		private static TokenInfo GetArg(int i) {
			return new TokenInfo(TokenInfoEnum.Text, -1, -1) { Value = args.Length > i ? args[i] : "" };
		}

		private static TokenInfo GetArgN(int i) {
			if (args.Length <= i) return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = double.NaN };
			return new TokenInfo(TokenInfoEnum.Number, -1, -1) { Value = ConvertNum(args[i]) };
		}
	}
}