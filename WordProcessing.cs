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

        private Dictionary<string /* hanyi */, KnownHanyu> _words;

        private readonly Regex NON_HANYI = new Regex( @"^[a-zA-Z0-9!！\?\？\.。,，\-\:\：\/=""]+" );

        private int MaxWordLength {
            get {
                if (_maxWordLength == 0) {
                    _maxWordLength = _words.Max( word => word.Value.Hanyu.Length );
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

        public Dictionary<string /* hanyi */, KnownHanyu> Words {
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

        public Dictionary<string /* hanyi */, KnownHanyu> LoadWords( ) {
            return XElement.Load( FilePath )
                .XPathSelectElements( "//Word" )
                .Select( word => WordElementToWordInfo( word ) )
                .ToDictionary( word => word.Hanyu );
        }

        public void SaveWords( ) {
            new XElement( "Chinese",
                new XElement( "Words",
                    Words.Values.OrderBy(word => word.Pinyin).Select( word =>
                        new XElement( "Word",
                            new XAttribute( "pinyin", word.Pinyin ),
                            new XAttribute( "hanyu", word.Hanyu ),
                            new XAttribute( "english", word.English )
                            )
                    ) )
                ).Save( FilePath );
        }

        private KnownHanyu WordElementToWordInfo( XElement wordElement ) {
            return new KnownHanyu( 
                wordElement.Attribute( "hanyu" ).Value,
                wordElement.Attribute( "pinyin" ).Value,
                wordElement.Attribute( "english" ).Value
            );
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
                return new UnknownHanyu( chinese.First( ).ToString( ) );
            }
        }

        public IEnumerable<Word> MatchingSuggestions( string pinyinInput, bool english ) {
            return Words.Values
                .Where( word => word.MatchesPinyin( pinyinInput, english ) )
                .OrderBy( word => word.Hanyu.Length );
        }

        public string PinyinText( IEnumerable<Word> words ) {
            return String.Join( "  ", words.Select( word => word.Pinyin ).ToArray( ) );
        }

        public void AddOrModifyWord( KnownHanyu newWord ) {
            Words[ newWord.Hanyu ] = newWord ;
            _wordsChanged.OnNext( _words.Count );
            SaveWords( );
        }

    } // class

} // namespace
