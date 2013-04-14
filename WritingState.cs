using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Key = System.Windows.Input.Key;
using RT = clojure.lang.RT;
using Keyword = clojure.lang.Keyword;

namespace ChineseWriter {

    internal class WritingState {

        // Observables
        public Subject<IDictionary<object, object>[]> WordsChanges =
            new Subject<IDictionary<object, object>[]>( );


        private IDictionary<object, object> WritingStateData {
            get {
                return (IDictionary<object, object>) 
                    ( (clojure.lang.Atom)RT.var( "WordDatabase", "writing-state" ).deref( ) ).deref( );
            }
        }

        public IDictionary<object,object>[] Words {
            get {
                // TODO: remove array cast
                return WritingStateData.GetList( "text" ).Cast<IDictionary<object, object>>( ).ToArray( );
            }
        }

        public int CursorPos {
            get {
                return WritingStateData.Get<int>( "cursor-pos" );
            }
        }

        // Constructor
        public WritingState( ) {
        }

        public void LoadText() {
            if (File.Exists( TextSaveFileName )) {
                var data = RT.var( "WordDatabase", "load-current-text" ).invoke( TextSaveFileName );
                WordsChanges.OnNext( Words );
            }
        }

        public IEnumerable<IDictionary<object, object>> Suggestions( string input, bool english ) {
            var findWords = RT.var( "WordDatabase", "find-words" );
            if (findWords.isBound) {
                var method = english ? "find-words-english" : "find-words";
                var suggestions = (IEnumerable<object>)RT.var( "WordDatabase", method ).invoke( input );
                // Do *not* cast suggestions to list or Array here! That kills the performance since it forces
                // the lazy list to be fully evaluated
                return suggestions.Cast<IDictionary<object, object>>( );
            } else {
                return new IDictionary<object, object>[] {};
            }
        }

        public void Delete( ) {
            if (CursorPos < Words.Length) {
                DeleteWordInner( CursorPos );
            }
        }

        public void BackSpace( ) {
            if (CursorPos > 0) {
                DeleteWordInner( CursorPos - 1 );
            }
        }

        public void DeleteWordInner( int pos ) {
            RT.var( "WordDatabase", "delete-word" ).invoke( pos );
            WordsChanges.OnNext( Words );
        }

        /// <returns>New cursor position</returns>
        internal void LiteralInput( string text ) {
            var words = (IEnumerable<object>)RT.var( "WordDatabase", "hanyu-to-words" ).invoke( text );
            InsertWords( words.Cast<IDictionary<object, object>>( ).ToArray( ) );
        }

        /// <returns>New cursor position</returns>
        internal void SelectWord( IDictionary<object, object> word ) {
            WordDatabase.IncreaseUsageCount( word );
            InsertWords( new IDictionary<object, object>[] { word } );            
        }

        /// <returns>New cursor position</returns>
        private void InsertWords( IDictionary<object, object>[] newWords ) {
            RT.var( "WordDatabase", "insert-text-words!" ).invoke( newWords );
            WordsChanges.OnNext( Words );
        }

        public void ExpandChars( ) {
            RT.var( "WordDatabase", "expand-text-words" ).invoke( );
            WordsChanges.OnNext( Words );
        }

        private static string PadHanyu( string s, int latinLength ) {
            int hanyuExtraLen = s.Replace( " ", "" ).Length / 2;
            return s.PadRight( latinLength - hanyuExtraLen );
        }

        public string HanyiPinyinLines { 
            get {
                var hanyus = Words.Select( word => word.Hanyu());
                var pinyins = Words.Select( word => word.PinyinDiacritics());
                var english = Words.Select( word => word.ShortEnglish()); 
                var lengths = hanyus.Select( word => word.Length + 1 )
                    .Zip( pinyins, (l1,s2) => Math.Max(l1, s2.Length + 1))
                    .Zip( english, (l1,s2) => Math.Max(l1, s2.Length + 1));
                // could not think of way to do this with the base LINQ funcs:
                var cumulativeLengths = new List<int>();
                int cumulativeLength = 0;
                foreach( int length in lengths) {
                    cumulativeLength += length;
                    cumulativeLengths.Add( cumulativeLength );
                }
                // Chinese characters in a monospaced font are ~1.5 times as wide as Latin
                // characters in the same font, so take account into padding:
                var hanyuLine = hanyus
                    .Zip( cumulativeLengths, ( h, len ) => Tuple.Create( h, len ) )
                    .Aggregate( "", ( hanyu, tuple ) => PadHanyu( hanyu + tuple.Item1, tuple.Item2 ) );
                var pinyinLine = string.Join( "", pinyins.Zip(lengths, (h,len) => h.PadRight(len)));
                var englishLine = string.Join( "", english.Zip(lengths, (h,len) => h.PadRight(len)));
                return hanyuLine + "\n" + pinyinLine + "\n" + englishLine;
            } 
        }

        public string Html {
            get {
                return "<table style='border: 1px solid #d0d0d0; border-collapse:collapse;' cellpadding='4'>" +
                    "<tr>" + HanyuHtml + "</tr>" +
                    "<tr>" + PinyinHtml + "</tr>" +
                    "<tr>" + EnglishHtml +"</tr>" +
                    "</table>";
            }
        }

        private string HanyuHtml {
            get {
                return string.Join( "", 
                    Words.Select( word => WordCell( word.HanyuHtml(), "font-size:20pt;" ) ).ToArray() );
            }
        }

        private string PinyinHtml {
            get {
                return string.Join( "", 
                    Words.Select( word => WordCell( word.PinyinHtml() ) ).ToArray( ) );
            }
        }

        private string EnglishHtml {
            get {
                return string.Join( "",
                    Words.Select( word => WordCell( word.EnglishHtml(), "color:#808080; font-size:9pt;" ) ).ToArray( ) );
            }
        }

        public static string WordCell( string content, string attr = "" ) {
            return string.Format("<td style='{0}'>{1} </td>", attr, content);
        }

        internal void Clear( ) {
            RT.var( "WordDatabase", "clear-current-text!" ).invoke( );
        }

        internal string Hanyu {
            get { return string.Join( "", Words.Select( word => word.Get<string>("hanyu") ).ToArray( ) ); }
        }

        internal string TextSaveFileName {
            get {
                return Path.Combine( WordDatabase.ExeDir.ToString( ), "text.clj" );
            }
        }

        internal void SaveCurrentText( ) {
            var content = (string) RT.var( "Utils", "list-to-str" ).invoke( Words );
            File.WriteAllText( TextSaveFileName, content, Encoding.UTF8 );
        }

        internal void Move( string dir ) {
            RT.var( "WordDatabase", "move-cursor!" ).invoke( dir );
            WordsChanges.OnNext( Words );
        }
    } // class

} // namespace
