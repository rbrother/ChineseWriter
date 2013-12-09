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

        private static string FilePath(string fileName) {
            return SearchUpwardFile( ExeDir, fileName );
        }

        public static void LoadWords( ) {
            RT.var( "WordDatabase", "load-database" )
                .invoke( FilePath( "cedict_ts.clj" ), 
                    @"C:\Google Drive\Ann\chinese study\words.clj" );
        }

        public static DirectoryInfo ExeDir {
            get {
                var exePath = new Uri( Assembly.GetExecutingAssembly( ).CodeBase ).LocalPath;
                var exeDir = new FileInfo( exePath ).Directory;
                return exeDir;
            }
        }

        private static string SearchUpwardFile( DirectoryInfo startDir, string fileName ) {
            var theFile = startDir.GetFiles( ).FirstOrDefault( file => file.Name == fileName );
            if (theFile != null) return theFile.FullName;
            return SearchUpwardFile( startDir.Parent, fileName );            
        }

        public static IDictionary<object, object> GetWord( string hanyu, string pinyin ) {
            return (IDictionary<object, object>) RT.var( "WordDatabase", "get-word" ).invoke( hanyu, pinyin );            
        }

        internal static void SetWordProp( string hanyu, string pinyin, string propName, object value ) {
            RT.var( "WordDatabase", "set-word-info-prop" ).invoke( hanyu, pinyin, propName, value );
        }

        internal static void IncreaseUsageCount( string hanyu, string pinyin ) {
            RT.var( "WordDatabase", "inc-usage-count" ).invoke( hanyu, pinyin );
        }

        public static IEnumerable<IDictionary<object, object>> Suggestions( string input, bool english ) {
            var findWords = RT.var( "WordDatabase", "find-words" );
            if (findWords.isBound) {
                var suggestions = (IEnumerable<object>)findWords.invoke(input, english);
                // Do *not* cast suggestions to list or Array here! That kills the performance since it forces
                // the lazy list to be fully evaluated
                return suggestions.Cast<IDictionary<object, object>>( );
            } else {
                return new IDictionary<object, object>[] { };
            }
        }

        internal static void DeleteWordInfo( string hanyu, string pinyin ) {
            RT.var( "WordDatabase", "delete-word-info!" ).invoke( hanyu, pinyin );
        }

        internal static object GetWordProp( string hanyu, string pinyin, string propName ) {
            return RT.var( "WordDatabase", "get-word-prop").invoke( hanyu, pinyin, propName );
        }
    } // class

} // namespace
