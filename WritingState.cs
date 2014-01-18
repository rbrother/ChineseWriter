﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reactive.Subjects;
using RT = clojure.lang.RT;

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

        internal static void LiteralInput( string text ) {
            var words = (IEnumerable<object>)RT.var( "ParseChinese", "hanyu-to-words" ).invoke( text );
            InsertWords( words.Cast<IDictionary<object, object>>( ).ToArray( ) );
        }

        internal static void InsertWord( IHanyuPinyin word ) {
            InsertWords( new IDictionary<object, object>[] { 
                new Dictionary<object,object>() { 
                    { RT.keyword("","hanyu"), word.Hanyu },
                    { RT.keyword("","pinyin"), word.Pinyin } 
                } 
            } );
        }

        private static void InsertWords( IDictionary<object, object>[] newWords ) {
            RT.var( "WritingState", "insert-words!" ).invoke( newWords );
            WordsChanges.OnNext( Words );
        }

        private static string PadHanyu( string s, int latinLength ) {
            int hanyuExtraLen = s.Replace( " ", "" ).Length / 2;
            return s.PadRight( latinLength - hanyuExtraLen );
        }

        public static string HanyiPinyinLines( IEnumerable<IDictionary<object, object>> words ) {
            var finalWords = words != null ? words : Words;
            var hanyus = finalWords.Select( word => word.Hanyu( ) );
            return string.Join( " ", hanyus.ToArray( ) );
        }

        internal static void Clear( ) {
            RT.var( "WritingState", "clear-current-text!" ).invoke( );
            WordsChanges.OnNext( Words );
        }

        internal static string Hanyu {
            get { return string.Join( "", Words.Select( word => word.Get<string>( "hanyu" ) ).ToArray( ) ); }
        }

        internal static string TextSaveFileName {
            get {
                return Path.Combine( WordDatabase.ExeDir.ToString( ), "text.clj" );
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
