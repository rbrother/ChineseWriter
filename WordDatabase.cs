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
                .invoke( FilePath( "cedict_ts.clj" ), InfoFileName );
        }

        private static string InfoFileName { get { return FilePath( "words.clj" ); } }

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

        internal static void SetWordInfo( IDictionary<object,object> word, 
            string shortEnglish, bool known ) {
                RT.var( "WordDatabase", "set-word-info" ).
                    invoke( word.Hanyu( ), word.Pinyin( ), shortEnglish, known );
        }

        internal static void IncreaseUsageCount( IDictionary<object, object> word ) {
            RT.var( "WordDatabase", "inc-usage-count" ).invoke( word.Hanyu( ), word.Pinyin( ) );
        }

        internal static void SaveWordsInfo( ) {
            var word_info_string = RT.var( "WordDatabase", "word-info-string" );
            if (word_info_string.isBound) { // might be that SW is closed before words loaded
                File.WriteAllText( InfoFileName,
                    (string)word_info_string.invoke( ),
                    Encoding.UTF8 );
            }
        }
    } // class

} // namespace
