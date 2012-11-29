using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChineseWriter {

    static class StringUtils {

        public static String RemoveDiacritics(this string s ) {
            var normalizedString = s.Normalize( NormalizationForm.FormD );
            var stringBuilder = new StringBuilder( );
            for (int i = 0; i < normalizedString.Length; i++) {
                Char c = normalizedString[i];
                if (CharUnicodeInfo.GetUnicodeCategory( c ) != UnicodeCategory.NonSpacingMark)
                    stringBuilder.Append( c );
            }
            return stringBuilder.ToString( );
        }

        public static string TakeFirst( this string s, int count = 1 ) {
            if (s == "" || count == 0) {
                return "";
            } else if (count >= s.Length) {
                return s;
            } else {
                return s.Substring( 0, count );
            }
        }

        public static string DropLast( this string s, int count = 1 ) {
            if (count >= s.Length) {
                return "";
            } else {
                return s.Substring( 0, s.Length - count );
            }
        }

//        public static string PadRight( string s, int finalLength ) {
//            return s + new string( ' ', finalLength - s.Length );
//        }

    } // class
} // namespace
