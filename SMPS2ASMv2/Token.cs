using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMPS2ASM {
	public class TokenInfo {
		public TokenInfoEnum Type;
		public int Start, End;
		public dynamic Value;

		public TokenInfo(TokenInfoEnum type, int start, int end) {
			Type = type;
			Start = start;
			End = end;
			Value = null;
		}
	}

	public enum TokenInfoEnum {
		None, MathOperator, String, Text, Number, Dot, Comma,
		OpenParen, CloseParen, OpenSqu, CloseSqu, Backslash, Error,
		
	}
}
