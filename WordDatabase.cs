using System;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Xml.XPath;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Reactive.Subjects;
using RT = clojure.lang.RT;
using Keyword = clojure.lang.Keyword;
using Brotherus;

namespace ChineseWriter {

    public static class WordDatabase {

        public static Dictionary<int, string> KNOWLEDGE_LEVEL_DESCR = new Dictionary<int, string>( ) { 
            { 0, "0 - Not known at all, do not learn" },
            { 1, "1 - Minimal vague passive knowledge" },
            { 2, "2 - Some knowledge, remembered 10% of times" },
            { 3, "3 - Good knowledge, remembered 90% of times" },
            { 4, "4 - Excellent knowledge, no need for training" },
        };

        public static Dictionary<string, int> KNOWLEDGE_LEVEL_VALUE =
            KNOWLEDGE_LEVEL_DESCR.ToDictionary( pair => pair.Value, pair => pair.Key );

        public static string[] KNOWLEDGE_DESCRIPTIONS = KNOWLEDGE_LEVEL_DESCR.Values.ToArray( );


        public static string SmallDictionaryFile { get { return @"C:\Google Drive\Ann\chinese study\words.clj"; } }

        public static void LoadWords( ) {
            RT.var( "WordDatabase", "load-database" )
                .invoke( Utils.FindRelativeFile( "cedict_ts.clj" ), SmallDictionaryFile );
        }

        private static IEnumerable<Word> ToWordList( object words ) {
            // Do *not* cast the list to Array here! That kills the performance since it forces
            // the lazy list to be fully evaluated
            return ( (IEnumerable<object>)words ).Cast<IDictionary<object, object>>( ).
                Select( props => new Word( props.Hanyu( ), props.Pinyin( ) ) );
        }

        public static IEnumerable<Word> Suggestions( string input, bool english ) {
            var findWords = RT.var( "WordDatabase", "find-words" );
            return findWords.isBound ?
                ToWordList( findWords.invoke( input, english ) ) :
                new Word[] { };
        }

        public static IEnumerable<Word> Characters( IHanyuPinyin word ) {
            return ToWordList( RT.var( "WordDatabase", "characters" ).invoke( word.Hanyu, word.Pinyin ) );
        }

        /// <summary>
        /// Word itself and it's characters
        /// </summary>
        public static IEnumerable<Word> BreakDown( IHanyuPinyin word ) {
            return ToWordList( RT.var( "WordDatabase", "word-breakdown" ).invoke( word.Hanyu, word.Pinyin ) );
        }

        internal static void DeleteWordInfo( IHanyuPinyin word ) {
            RT.var( "WordDatabase", "delete-word-info!" ).invoke( word.Hanyu, word.Pinyin );
        }

        internal static object GetWordProp( IHanyuPinyin word, string propName ) {
            return RT.var( "WordDatabase", "get-word-prop").invoke( word.Hanyu, word.Pinyin, propName );
        }

        internal static void SetWordProp( IHanyuPinyin word, string propName, object value ) {
            RT.var( "WordDatabase", "set-word-prop" ).invoke( word.Hanyu, word.Pinyin, propName, value );
        }

        internal static void AddNewWord( IEnumerable<IDictionary<object, object>> SelectedWords ) {
            RT.var( "WordDatabase", "add-new-combination-word" ).invoke( SelectedWords );
        }
    } // class

} // namespace
