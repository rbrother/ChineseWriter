using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reactive.Subjects;
using RT = clojure.lang.RT;

namespace ChineseWriter {

    internal static class WritingState {

        // Observables
        public static Subject<IDictionary<object, object>[]> WordsChanges =
            new Subject<IDictionary<object, object>[]>( );

        private static IDictionary<object, object> WritingStateData {
            get {
                return (IDictionary<object, object>) 
                    ( (clojure.lang.Atom)RT.var( "WritingState", "state" ).deref( ) ).deref( );
            }
        }

        private static IDictionary<object, object>[] Words {
            get {
                // TODO: remove array cast
                return WritingStateData.GetList( "text" ).Cast<IDictionary<object, object>>( ).ToArray( );
            }
        }

        public static int CursorPos {
            get {
                return WritingStateData.Get<int>( "cursor-pos" );
            }
        }

        public static void LoadText( ) {
            if (File.Exists( TextSaveFileName )) {
                var data = RT.var( "WritingState", "load-current-text" ).invoke( TextSaveFileName );
                WordsChanges.OnNext( Words );
            }
        }

        public static void Delete( ) {
            DeleteWordInner( CursorPos );
        }

        public static void BackSpace( ) {
            DeleteWordInner( CursorPos - 1 );
        }

        public static void DeleteWordInner( int pos ) {
            if ((bool)RT.var( "WritingState", "delete-word!" ).invoke( pos )) {
                WordsChanges.OnNext( Words );
            }
        }

        internal static void LiteralInput( string text ) {
            var words = (IEnumerable<object>)RT.var( "ParseChinese", "hanyu-to-words" ).invoke( text );
            InsertWords( words.Cast<IDictionary<object, object>>( ).ToArray( ) );
        }

        internal static void SelectWord( IDictionary<object, object> word ) {
            WordDatabase.IncreaseUsageCount( word );
            InsertWords( new IDictionary<object, object>[] { word } );            
        }

        private static void InsertWords( IDictionary<object, object>[] newWords ) {
            RT.var( "WritingState", "insert-text-words!" ).invoke( newWords );
            WordsChanges.OnNext( Words );
        }

        public static void ExpandChars( ) {
            RT.var( "WritingState", "expand-text-words!" ).invoke( );
            WordsChanges.OnNext( Words );
        }

        private static string PadHanyu( string s, int latinLength ) {
            int hanyuExtraLen = s.Replace( " ", "" ).Length / 2;
            return s.PadRight( latinLength - hanyuExtraLen );
        }

        public static string HanyiPinyinLines( bool copyPinyin, bool copyEnglish ) {
            var hanyus = Words.Select( word => word.Hanyu( ) );
            if (!copyPinyin && !copyEnglish) {
                // simple case: only hanyu
                return string.Join( " ", hanyus.ToArray( ) );
            } else { // complex case: aligned lines (use monospaced font)
                var pinyins = Words.Select( word => word.PinyinDiacritics( ) );
                var english = Words.Select( word => word.ShortEnglish( ) );
                var hanyuLengths = hanyus.Select( word => word.Length + 1 );
                var pinyinLengths = pinyins.Select( word => word.Length + 1 );
                var englishLengths = english.Select( word => word.Length + 1 );
                var lengths = hanyuLengths;
                if (copyPinyin) lengths = lengths.Zip( pinyinLengths, ( l1, l2 ) => Math.Max( l1, l2 ) );
                if (copyEnglish) lengths = lengths.Zip( englishLengths, ( l1, l2 ) => Math.Max( l1, l2 ) );
                // could not think of way to do this with the base LINQ funcs:
                var cumulativeLengths = new List<int>( );
                int cumulativeLength = 0;
                foreach (int length in lengths) {
                    cumulativeLength += length;
                    cumulativeLengths.Add( cumulativeLength );
                }
                // Chinese characters in a monospaced font are ~1.5 times as wide as Latin
                // characters in the same font, so take account into padding:
                var hanyuLine = hanyus
                    .Zip( cumulativeLengths, ( h, len ) => Tuple.Create( h, len ) )
                    .Aggregate( "", ( hanyu, tuple ) => PadHanyu( hanyu + tuple.Item1, tuple.Item2 ) );
                var pinyinLine = string.Join( "", pinyins.Zip( lengths, ( h, len ) => h.PadRight( len ) ) );
                var englishLine = string.Join( "", english.Zip( lengths, ( h, len ) => h.PadRight( len ) ) );
                return hanyuLine + ( copyPinyin ? "\n" + pinyinLine : "" ) +
                    ( copyEnglish ? "\n" + englishLine : "" );
            }
        }

        internal static void Clear( ) {
            RT.var( "WritingState", "clear-current-text!" ).invoke( );
            WordsChanges.OnNext( Words );
        }

        internal static string Hanyu {
            get { return string.Join( "", Words.Select( word => word.Get<string>("hanyu") ).ToArray( ) ); }
        }

        internal static string TextSaveFileName {
            get {
                return Path.Combine( WordDatabase.ExeDir.ToString( ), "text.clj" );
            }
        }

        internal static void SaveCurrentText( ) {
            var content = (string) RT.var( "Utils", "list-to-str" ).invoke( Words );
            File.WriteAllText( TextSaveFileName, content, Encoding.UTF8 );
        }

        internal static void Move( string dir ) {
            RT.var( "WritingState", "move-cursor!" ).invoke( dir );
            WordsChanges.OnNext( Words );
        }
    } // class

} // namespace
