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

    public class WordDatabase {

        Subject<int> _wordsChanged = new Subject<int>();

        private int _maxWordLength;

        private HanyuWord[] _words;
        private Dictionary<string /*hanyu*/, List<HanyuWord>> _wordsDict;

        private static readonly Regex NON_HANYI = new Regex( @"^[a-zA-Z0-9!！\?\？\.。,，\-\:\：\/=""]+" );

        private static readonly Regex CC_LINE = new Regex( @"(\S+)\s+(\S+)\s+\[([\w\s]+)\]\s+\/(.+)\/" );

        private static readonly SuggestionComparer _suggestionComparer = new SuggestionComparer( );

        public WordDatabase( ) {
            _words = LoadWords( );
            _wordsChanged.OnNext( _words.Length );
        }

        private int MaxWordLength {
            get {
                if (_maxWordLength == 0) {
                    _maxWordLength = _words.Max( word => word.Hanyu.Length );
                }
                return _maxWordLength;
            }
        }

        private string FilePath(string fileName) {
            return SearchUpwardFile( ExeDir, fileName );
        }

        private static DirectoryInfo ExeDir {
            get {
                var exePath = new Uri( Assembly.GetExecutingAssembly( ).CodeBase ).LocalPath;
                var exeDir = new FileInfo( exePath ).Directory;
                return exeDir;
            }
        }

        private string SearchUpwardFile( DirectoryInfo startDir, string fileName ) {
            var theFile = startDir.GetFiles( ).FirstOrDefault( file => file.Name == fileName );
            if (theFile != null) return theFile.FullName;
            return SearchUpwardFile( startDir.Parent, fileName );            
        }

        public HanyuWord[] Words { get { return _words; } }

        private Dictionary<string /*hanyu*/, List<HanyuWord>> WordsDict {
            get {
                if (_wordsDict == null) {
                    _wordsDict = new Dictionary<string /*hanyu*/, List<HanyuWord>>( 150000 );
                    foreach (HanyuWord word in Words) {
                        if (!_wordsDict.ContainsKey( word.Hanyu )) {
                            _wordsDict[word.Hanyu] = new List<HanyuWord>( );
                        }
                        _wordsDict[word.Hanyu].Add( word );
                    }
                }
                return _wordsDict;
            }
        }

        public IObservable<int> WordsChanged { get { return _wordsChanged; } }

        public static Dictionary<string /*hanyu + displaypinyin*/, XElement> ParseInfoDict( XElement infoFile ) {
            var infoWords = infoFile.XPathSelectElements( "//Word" );
            var infoDict = new Dictionary<string /*hanyu + displaypinyin*/, XElement>( );
            foreach (XElement infoWord in infoWords) {
                var key = infoWord.Attribute( "hanyu" ).Value + infoWord.Attribute( "pinyin" ).Value;
                infoDict[key.ToLower( ).Replace( " ", "" )] = infoWord;
            }
            return infoDict;
        }

        public HanyuWord[] LoadWords( ) {
            var infoDict = ParseInfoDict( XElement.Load( FilePath( "words.xml" ) ) );
            return ParseCCLines( 
                File.ReadAllLines( FilePath( "cedict_ts.u8" ), Encoding.UTF8 ),
                infoDict);
        }

        public static HanyuWord[] ParseCCLines( string[] lines, Dictionary<string /*hanyu + displaypinyin*/, XElement> infoDict ) {
            return lines.
                Where( line => !line.StartsWith( "#" ) ).
                Select( line => CC_LINE.Match( line ).Groups ).
                Select( groups => LineToWord( groups, infoDict ) ).
                ToArray( );
        }

        private static HanyuWord LineToWord( GroupCollection groups, Dictionary<string, XElement> info ) {
            var traditional = groups[1].Value;
            var simplified = groups[2].Value;
            var pinyin = groups[3].Value;
            var english = groups[4].Value;
            // TODO: Make the words.xml use same simple pinyin format with tone numbers as CCDICT
            var infoKey = ( simplified + pinyin.AddToneDiacritics( ) ).ToLower( ).Replace( " ", "" );
            var wordInfo = info.ContainsKey( infoKey ) ? info[infoKey] : null;
            return new HanyuWord( simplified, pinyin, english.Replace( "/", ", " ), wordInfo );
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
                    if (WordsDict.ContainsKey( part )) {
                        return WordForHanyu( part );
                    }
                }
                // not found
                throw new ApplicationException( "Unknown chinese: " + chinese );
            }
        }

        public Word[] MatchingSuggestions( string pinyinInput, bool english, bool allWords ) {
            // Is it possible to make this faster with some dictionary-speedups?
            // eg. dictionary keyed by first and/or first+second chars.
            return (allWords ? Words : Words.Where( word => word.Suggest )).
                Where( word => word.MatchesPinyin( pinyinInput, english ) ).
                OrderBy( word => word, _suggestionComparer ).
                ToArray();
        }

        internal Word WordForHanyu( string hanyu ) {
            var entries = WordsDict[hanyu].ToArray();
            if (entries.Length == 1) {
                return entries.First();
            } else {
                return new MultiMeaningWord(entries);
            }
        }

        internal void SaveWordsInfo( ) {
            new XElement( "Chinese",
                new XElement( "Words",
                    Words.Where( word => word.Suggest ).
                        OrderBy( word => word.Pinyin). 
                        Select( word => new XElement( "Word",
                            new XAttribute( "pinyin", word.DisplayPinyin ),
                            new XAttribute( "hanyu", word.Hanyu ) ) ) ) ).
                Save( FilePath( "words.xml" ) );

        }
    } // class

    class SuggestionComparer : IComparer<HanyuWord> {
        int IComparer<HanyuWord>.Compare( HanyuWord x, HanyuWord y ) {
            if (x.Hanyu.Length != y.Hanyu.Length) {
                return x.Hanyu.Length < y.Hanyu.Length ? -1 : 1;
            } else {
                return StringComparer.InvariantCulture.Compare( x.Pinyin, y.Pinyin );
            }
        }
    }

} // namespace
