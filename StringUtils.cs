﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Input;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChineseWriter {

    class DiacriticsAdderFunc : clojure.lang.AFn {

        private static readonly int[] TONE_DIACRITICS = new int[] { 772, 769, 780, 768 };

        private static Tuple<string, string>[] DIACRITIC_VOVEL = new Tuple<string, string>[] { 
            Tuple.Create("a", "a"), Tuple.Create("e", "e"), Tuple.Create("o", "o"),
            Tuple.Create("iu", "u"), Tuple.Create("i", "i"), Tuple.Create("u", "u"),
            Tuple.Create("ü", "ü"), Tuple.Create("m", "m") };  // 呣 呣 [m4] /interjection expressing consent/um/

        public override object invoke( object pinyin ) {
            return AddDiacritics( (string)pinyin );
        }

        public static string AddDiacritics( string pinyin ) {
            return string.Join( " ", pinyin.Replace( "u:", "ü" ).Split( ' ' ).
                Select( syllable => AddToneDiacritic( syllable ) ).ToArray( ) );
        }

        /// <summary>
        /// Convert Ba4 -> Bà
        /// </summary>
        /// <param name="pinyinSyllable">Eg. Ba4</param>
        /// <param name="tone"></param>
        /// <returns>Eg. Bà</returns>
        private static string AddToneDiacritic( string pinyinSyllable ) {
            var lastChar = pinyinSyllable.TakeLast( );
            int tone;
            if (!int.TryParse( lastChar, out tone )) return pinyinSyllable; // No tone mark: literal
            var plainPinyin = pinyinSyllable.DropLast( );
            if (tone == 5) return plainPinyin; // neutral, no mark
            var diacriticInserted = InsertChar(
                original: plainPinyin,
                c: (char)TONE_DIACRITICS[tone - 1],
                pos: VovelIndex( plainPinyin.ToLower( ) ) + 1 );
            return new string( diacriticInserted ).Normalize( NormalizationForm.FormKC );
        }

        private static int VovelIndex( string plainPinyin ) {
            foreach (Tuple<string, string> pair in DIACRITIC_VOVEL) {
                if (plainPinyin.Contains( pair.Item1 )) {
                    return plainPinyin.IndexOf( pair.Item2 );
                }
            }
            throw new ApplicationException( "Invalid pinyin: " + plainPinyin );
        }

        private static char[] InsertChar( string original, char c, int pos ) {
            return original.
                ToCharArray( ).
                Take( pos ).
                Concat( new char[] { c } ).
                Concat( original.Skip( pos ) ).
                ToArray( );
        }
    }

    static class StringUtils {

        public static clojure.lang.IFn AddDiacriticsFunc = new DiacriticsAdderFunc( );

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
