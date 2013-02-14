using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChineseWriter {

    static class StringUtils {

        private static readonly int[] TONE_DIACRITICS = new int[] { 772, 769, 780, 768 };

        /// <summary>
        /// Convert Ba4 -> Bà
        /// </summary>
        /// <param name="pinyinSyllable">Eg. Ba4</param>
        /// <param name="tone"></param>
        /// <returns>Eg. Bà</returns>
        public static string AddToneDiacritic( this string pinyinSyllable ) {
            var lastChar = pinyinSyllable.TakeLast( );
            int tone;
            if ( !int.TryParse( lastChar, out tone ) ) return pinyinSyllable; // No tone mark: literal
            var plainPinyin = pinyinSyllable.DropLast( ).ToLower();
            if (tone == 5) return plainPinyin; // neutral, no mark
            int vovelIndex =
                plainPinyin.Contains( 'a' ) ? plainPinyin.IndexOf( 'a' ) :
                plainPinyin.Contains( 'e' ) ? plainPinyin.IndexOf( 'e' ) :
                plainPinyin.Contains( 'o' ) ? plainPinyin.IndexOf( 'o' ) :
                plainPinyin.Contains( "iu" ) ? plainPinyin.IndexOf( 'u' ) :
                plainPinyin.Contains( 'i' ) ? plainPinyin.IndexOf( 'i' ) :
                plainPinyin.Contains( 'u' ) ? plainPinyin.IndexOf( 'u' ) :
                // 呣 呣 [m4] /interjection expressing consent/um/
                plainPinyin.Contains( 'm' ) ? plainPinyin.IndexOf( 'm' ) : -1;
            if (vovelIndex < 0) throw new ApplicationException( "Invalid pinyin: " + pinyinSyllable );
            var chars = plainPinyin.ToCharArray( );
            return new string( chars.Take( vovelIndex + 1 ).
                Concat( new char[] { (char)TONE_DIACRITICS[tone - 1] } ).
                Concat( chars.Skip( vovelIndex + 1 ) ).ToArray( ) ).
                Normalize( NormalizationForm.FormKC );
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

        public static string TakeLast( this string s, int count = 1 ) {
            if (count >= s.Length) {
                return s;
            } else {
                return s.Substring( s.Length - count, count );
            }
        }

    } // class
} // namespace
