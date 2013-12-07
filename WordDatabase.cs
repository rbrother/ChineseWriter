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

        internal static void SetWordInfo( string hanyu, string pinyin, string propName, object value ) {
                RT.var( "WordDatabase", "set-word-info-prop" ).invoke( hanyu, pinyin, propName, value );
        }

        internal static void IncreaseUsageCount( IDictionary<object, object> word ) {
            RT.var( "WordDatabase", "inc-usage-count" ).invoke( word.Hanyu( ), word.Pinyin( ) );
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

        internal static void DeleteWordInfo( IDictionary<object, object> word ) {
            RT.var( "WordDatabase", "delete-word-info!" ).
                    invoke( word.Hanyu( ), word.Pinyin( ) );
        }
    } // class

} // namespace
