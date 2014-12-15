using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reactive.Subjects;
using RT = clojure.lang.RT;
using Brotherus;

namespace ChineseWriter {

    internal static class WritingState {

        public static Subject<IEnumerable<IDictionary<object, object>>> WordsChanges =
            new Subject<IEnumerable<IDictionary<object, object>>>( );

        // TODO: Don't handle the atom here, make accessors that give parts
        private static IDictionary<object, object> WritingStateData {
            get {
                return (IDictionary<object, object>)
                    ( (clojure.lang.Atom)RT.var( "WritingState", "state" ).deref( ) ).deref( );
            }
        }

        private static IEnumerable<IDictionary<object, object>> Words {
            get {
                return WritingStateData.GetList( "text" ).Cast<IDictionary<object, object>>( );
            }
        }

        /// <summary>
        /// List of pinyin-forms of all words
        /// </summary>
        public static IEnumerable<string> WordPinyins {
            get {
                var words = (IEnumerable<object>) RT.var( "WritingState", "pinyin-text" ).invoke( );
                return words.Cast<string>( );
            }
        }

        public static int CursorPos {
            get {
                return WritingStateData.Get<int>( "cursor-pos" );
            }
        }

        public static void LoadText( ) {
            if ( File.Exists( TextSaveFileName ) ) {
                RT.var( "WritingState", "load-current-text" ).invoke( TextSaveFileName );
            }
            WordsChanges.OnNext( Words );
        }

        public static void Delete( ) {
            DeleteWordInner( CursorPos );
        }

        public static void BackSpace( ) {
            // TODO: Combine these to atomic swap-operation in clojure-side
            DeleteWordInner( CursorPos - 1 );
            Move( "Left" );
        }

        public static void DeleteWordInner( int pos ) {
            RT.var( "WritingState", "delete-word!" ).invoke( pos );
            WordsChanges.OnNext( Words );
        }

        internal static void InsertWord( IHanyuPinyin word ) {
            RT.var( "WritingState", "insert-word!" ).invoke( word.Hanyu, word.Pinyin );
            WordsChanges.OnNext( Words );
        }

        internal static void LiteralInput( string text ) {
            RT.var( "WritingState", "literal-input!" ).invoke( text );
            WordsChanges.OnNext( Words );
        }

        internal static void InsertChinese( string hanyuText ) {
            RT.var( "WritingState", "insert-chinese!" ).invoke( hanyuText );
            WordsChanges.OnNext( Words );
        }

        private static string PadHanyu( string s, int latinLength ) {
            int hanyuExtraLen = s.Replace( " ", "" ).Length / 2;
            return s.PadRight( latinLength - hanyuExtraLen );
        }

        public static string HanyiText( IEnumerable<IDictionary<object, object>> words ) {
            return GetText(words, word => word.Text( ) );
        }

        public static string PinyinText( IEnumerable<IDictionary<object, object>> words ) {
            return GetText( words, word => word.PinyinDiacritics() );
        }

        internal static string GetText( IEnumerable<IDictionary<object, object>> words, Func<IDictionary<object, object>, string> getter ) {
            var finalWords = words != null ? words : Words;
            var texts = finalWords.Select( getter );
            return string.Join( " ", texts.ToArray( ) );
        }

        internal static void Clear( ) {
            RT.var( "WritingState", "clear-current-text!" ).invoke( );
            WordsChanges.OnNext( Words );
        }

        internal static string TextSaveFileName {
            get {
                return Path.Combine( Utils.ExeDir.ToString( ), "text.clj" );
            }
        }

        internal static void SaveCurrentText( ) {
            RT.var( "WritingState", "save-current-text" ).invoke( TextSaveFileName );
        }

        internal static void Move( string dir ) {
            RT.var( "WritingState", "move-cursor!" ).invoke( dir );
            WordsChanges.OnNext( Words );
        }

        internal static void SetCursorPos( int pos ) {
            RT.var( "WritingState", "reset-cursor!" ).invoke( pos );
            WordsChanges.OnNext( Words );
        }

    } // class

} // namespace
