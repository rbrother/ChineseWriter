using System;
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

        private Dictionary<string /* hanyi */, ChineseWordInfo> _words;

        private readonly Regex NON_HANYI = new Regex( @"^[a-zA-Z0-9!！\?\？\.。,，\-\:\：\/=""]+" );

        private int MaxWordLength {
            get {
                if (_maxWordLength == 0) {
                    _maxWordLength = _words.Max( word => word.Value.hanyu.Length );
                }
                return _maxWordLength;
            }
        }

        private string FileName { get { return "words.xml"; } }

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

        public Dictionary<string /* hanyi */, ChineseWordInfo> Words {
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

        public Dictionary<string /* hanyi */, ChineseWordInfo> LoadWords( ) {
            return XElement.Load( FilePath )
                .XPathSelectElements( "//Word" )
                .Select( word => WordElementToWordInfo( word ) )
                .ToDictionary( word => word.hanyu );
        }

        public void SaveWords( ) {
            new XElement( "Chinese",
                new XElement( "Words",
                    Words.Values.OrderBy(word => word.PinyinString).Select( word =>
                        new XElement( "Word",
                            new XAttribute( "pinyin", word.pinyin ),
                            new XAttribute( "hanyu", word.hanyu ),
                            new XAttribute( "english", word.english )
                            )
                    ) )
                ).Save( FilePath );
        }

        private ChineseWordInfo WordElementToWordInfo( XElement wordElement ) {
            return new ChineseWordInfo {
                pinyin = wordElement.Attribute( "pinyin" ).Value,
                hanyu = wordElement.Attribute( "hanyu" ).Value,
                english = wordElement.Attribute( "english" ).Value
            };
        }

        /// <summary>
        /// Splits string of Hanyu chars to words.
        /// Unknown chars are marked as null:s
        /// </summary>
        /// <param name="chinese"></param>
        /// <example>很抱歉.我这里没有信号 -> ["很","抱歉",".",null,"没有","信号"]</example>
        /// <returns></returns>
        public IEnumerable<ChineseWordInfo> HanyuToWords( string chinese ) {
            if (chinese == "") {
                return new ChineseWordInfo[] { };
            } else {
                var firstWord = FirstWord( chinese );
                var rest = HanyuToWords( chinese.Substring( firstWord.hanyu.Length ) );
                var result = ( new ChineseWordInfo[] { firstWord } ).Concat( rest ).ToArray( );
                return result;
            }
        }

        /// <summary>
        /// Find first word of chinestText. This can be one or multiple
        /// characters. It is found by taking longest sequence of characters
        /// that matches some word in the word list.
        /// </summary>
        /// <param name="chineseText"></param>
        /// <returns></returns>
        private ChineseWordInfo FirstWord( string chinese ) {
            var nonHanyiMatch = NON_HANYI.Match( chinese );
            if (nonHanyiMatch.Success) {
                var match = nonHanyiMatch.Value;
                return new ChineseWordInfo { hanyu = match, pinyin = match, english = match };
            } else {
                // Find longest match from the beginning
                for ( int wordLength = MaxWordLength; wordLength >= 1; wordLength--) {
                    var part = chinese.TakeFirst( wordLength );
                    if (Words.ContainsKey( part )) return Words[part];
                }
                // not found
                return new ChineseWordInfo { hanyu = chinese.First( ).ToString( ) };
            }
        }

        public IEnumerable<ChineseWordInfo> MatchingSuggestions( string pinyinInput, bool english ) {
            return Words.Values
                .Where( word => word.MatchesPinyin( pinyinInput, english ) )
                .OrderBy( word => word.hanyu.Length );
        }

        public string PinyinText( IEnumerable<ChineseWordInfo> words ) {
            return String.Join( "  ", words.Select( word => word.PinyinString ).ToArray( ) );
        }

        public void AddOrModifyWord( ChineseWordInfo newWord ) {
            Words[ newWord.hanyu ] = newWord ;
            _wordsChanged.OnNext( _words.Count );
            SaveWords( );
        }

        internal string HanyiPinyinLines( string chinese ) {
            var words = HanyuToWords( chinese );
            var pinyinLine = string.Join( "  ", words
                .Select( word => word.PinyinString ).ToArray() );
            var hanyiLine = string.Join( " ", words
                .Select( word => word.hanyu ).ToArray() );
            return hanyiLine + "\n" + pinyinLine;
        }
    } // class

} // namespace
