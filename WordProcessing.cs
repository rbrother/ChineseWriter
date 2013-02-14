using System;
using System.Text;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Xml.XPath;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using System.Reactive.Concurrency;

namespace ChineseWriter {

    class WordDatabase {

        Subject<int> _wordsChanged = new Subject<int>();

        private int _maxWordLength;

        private string _filePath;

        private Dictionary<string /* hanyi */, HanyuWord> _words;

        private readonly Regex NON_HANYI = new Regex( @"^[a-zA-Z0-9!！\?\？\.。,，\-\:\：\/=""]+" );

        private int MaxWordLength {
            get {
                if (_maxWordLength == 0) {
                    _maxWordLength = _words.Max( word => word.Value.Hanyu.Length );
                }
                return _maxWordLength;
            }
        }

        private string FileName { get { return "cedict_ts.u8"; } }

        private string FilePath { 
            get {
                if (_filePath == null) {
                    _filePath = SearchUpwardFile( ExeDir );
                }
                return _filePath; 
            } 
        }

        private static DirectoryInfo ExeDir {
            get {
                var exePath = new Uri( Assembly.GetExecutingAssembly( ).CodeBase ).LocalPath;
                var exeDir = new FileInfo( exePath ).Directory;
                return exeDir;
            }
        }

        private string SearchUpwardFile( DirectoryInfo startDir ) {
            var theFile = startDir.GetFiles( ).FirstOrDefault( file => file.Name == FileName );
            if (theFile != null) return theFile.FullName;
            return SearchUpwardFile( startDir.Parent );            
        }

        public Dictionary<string /* hanyi */, HanyuWord> Words {
            get {
                if (_words == null) {
                    _words = LoadWords( );
                    _wordsChanged.OnNext( _words.Count );
                }
                return _words;
            }
            set {
                _words = value;
            }
        }

        public IObservable<int> WordsChanged { get { return _wordsChanged; } }

        public Dictionary<string /* hanyi */, HanyuWord> LoadWords( ) {
            var words = File.ReadAllLines( FilePath, Encoding.UTF8 ).
                Where( line => !line.StartsWith( "#" ) ).
                Select( line => LineToWord( line ) );
            var dict = new Dictionary<string, HanyuWord>( );
            // TODO: Following kills duplicates, try to retain
            foreach (HanyuWord word in words) dict[word.Hanyu] = word;
            return dict;
        }

        private readonly Regex CC_LINE = new Regex( @"(\S+)\s+(\S+)\s+\[([\w\s]+)\]\s+\/(.+)\/" );

        private HanyuWord LineToWord( string line ) {
            var match = CC_LINE.Match( line );
            var traditional = match.Groups[1].Value;
            var simplified = match.Groups[2].Value;
            var pinyin = match.Groups[3].Value;
            var english = match.Groups[4].Value;
            return new HanyuWord( simplified, pinyin, english.Replace("/", ", "));
        }

        /// <summary>
        /// Splits string of Hanyu chars to words.
        /// Unknown chars are marked as null:s
        /// </summary>
        /// <param name="chinese"></param>
        /// <example>很抱歉.我这里没有信号 -> ["很","抱歉",".",null,"没有","信号"]</example>
        /// <returns></returns>
        public Word[] HanyuToWords( string chinese ) {
            if (chinese == "") {
                return new Word[] { };
            } else if (chinese.StartsWith( " " )) {
                return HanyuToWords( chinese.Substring( 1 ) ); // skip spaces
            } else {
                var firstWord = FirstWord( chinese );
                var rest = HanyuToWords( chinese.Substring( firstWord.Hanyu.Length ) );
                return ( new Word[] { firstWord } ).Concat( rest ).ToArray( );
            }
        }

        /// <summary>
        /// Find first word of chinestText. This can be one or multiple
        /// characters. It is found by taking longest sequence of characters
        /// that matches some word in the word list.
        /// </summary>
        /// <param name="chineseText"></param>
        /// <returns></returns>
        private Word FirstWord( string chinese ) {
            var nonHanyiMatch = NON_HANYI.Match( chinese );
            if (nonHanyiMatch.Success) {
                return new LiteralWord(nonHanyiMatch.Value);
            } else {
                // Find longest match from the beginning
                for ( int wordLength = MaxWordLength; wordLength >= 1; wordLength--) {
                    var part = chinese.TakeFirst( wordLength );
                    if (Words.ContainsKey( part )) return Words[part];
                }
                // not found
                throw new ApplicationException( "Unknown chinese: " + chinese );
            }
        }

        public IEnumerable<Word> MatchingSuggestions( string pinyinInput, bool english ) {
            return Words.Values
                .Where( word => word.MatchesPinyin( pinyinInput, english ) )
                .OrderBy( word => word.Pinyin );
        }

        internal HanyuWord WordForHanyu( string hanyu ) {
            return Words[hanyu];
        }
    } // class

} // namespace
