using System;
using System.Windows.Input;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChineseWriter {

    static class StringUtils {

        public readonly static Key[] ALPHA_KEYS = new Key[]  {
            Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J,
            Key.K, Key.L, Key.M, Key.N, Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T,
            Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z
        };

        public readonly static Key[] NUMBER_KEYS = new Key[]  {
            Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, 
            Key.D5, Key.D6, Key.D7, Key.D8, Key.D9 };

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

        public static bool IsAlphaKey( Key key ) {
            return ALPHA_KEYS.Contains( key );
        }

        internal static bool IsNumberKey( Key key ) {
            return NUMBER_KEYS.Contains( key );
        }

        internal static int NumberKeyValue( Key numberKey ) {
            return Array.IndexOf<Key>( NUMBER_KEYS, numberKey );
        }


    } // class
} // namespace
