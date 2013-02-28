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

namespace ChineseWriter {

    public class WordDatabase {

        Subject<int> _wordsChanged = new Subject<int>();

        private int _maxWordLength;

        private HanyuWord[] _words;
        private Dictionary<string /*hanyu*/, List<HanyuWord>> _wordsDict;

        private static readonly Regex NON_HANYI = new Regex( @"^[a-zA-Z0-9!！\?\？\.。,，\-\:\：\/=""]+" );

        public WordDatabase( HanyuWord[] words = null) {
            _words = words == null ? LoadWords( ) : words;
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

        private static string FilePath(string fileName) {
            return SearchUpwardFile( ExeDir, fileName );
        }

        private static DirectoryInfo ExeDir {
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

        public static Dictionary<Tuple<string, string>, XElement> ParseInfoDict( XElement infoFile ) {
            var infoWords = infoFile.XPathSelectElements( "//Word" );
            var infoDict = new Dictionary<Tuple<string,string> , XElement>( );
            foreach (XElement infoWord in infoWords) {
                var key = Tuple.Create( 
                    infoWord.Attribute( "hanyu" ).Value, 
                    infoWord.Attribute( "pinyin" ).Value );
                infoDict[key] = infoWord;
            }
            return infoDict;
        }

        public static HanyuWord[] LoadWords( ) {
            var infoDict = ParseInfoDict( XElement.Load( FilePath( "words.xml" ) ) );
            return ParseCCLines( 
                File.ReadAllLines( FilePath( "cedict_ts.u8" ), Encoding.UTF8 ),
                infoDict);
        }

        private static readonly Regex VARIANT_REGEX = new Regex( "^(variant of|old variant of|archaic variant of|Japanese variant of)", RegexOptions.Compiled );

        public static HanyuWord[] ParseCCLines( string[] lines, Dictionary<Tuple<string,string>, XElement> infoDict ) {
            return lines.
                Where( line => !line.StartsWith( "#" ) ).
                Select( line => new HanyuWord( line, infoDict )).
                Where( word => !VARIANT_REGEX.IsMatch( word.English )).
                ToArray( );
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
                var rest = HanyuToWords( chinese.Substring( firstWord.Text.Length ) );
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
                        var word = WordForHanyu( part );
                        if (word is HanyuWord) {
                            return word;
                        } else {
                            var multiWord = word as MultiMeaningWord;
                            Debug.Assert( word != null );
                            var suggested = multiWord.Words.Where( w => w.Suggest );
                            if (suggested.Count() > 0) return suggested.First( );
                            return multiWord;
                        }
                    }
                }
                // not found: take first char
                return new LiteralWord(chinese.TakeFirst());
            }
        }

        // string pinyinInput, bool english
        // word => word.MatchesPinyin( pinyinInput )
        public HanyuWord[] MatchingSuggestions( Func<HanyuWord,bool> matcher ) {
            // Is it possible to make this faster with some dictionary-speedups?
            // eg. dictionary keyed by first and/or first+second chars.
            return Words.
                Where( matcher ).
                OrderBy( word => word, new SuggestionComparer() ).
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

        internal HanyuWord WordForHanyuPinyin( string hanyu, string pinyin ) {
            var matching = WordsDict[hanyu];
            if (matching.Count( ) == 0) throw new ApplicationException( "No match for hany" );
            var exactMatches = matching.Where( word => word.Pinyin == pinyin );
            if (exactMatches.Count() > 0) return exactMatches.First();
            var caselessMatches = matching.Where( word => word.Pinyin.ToLower() == pinyin.ToLower() );
            if (caselessMatches.Count( ) > 0) return caselessMatches.First( );
            // Last result: match based on Hanyi alone. This can be necessary when
            // eg. a character has changed from some tone to neutral tone when combined to a word.
            return matching.First( ); 
        }

        internal void SaveWordsInfo( ) {
            new XElement( "Chinese",
                new XElement( "Words",
                    Words.Where( word => word.Suggest ).
                        OrderBy( word => word.Pinyin). 
                        Select( word => new XElement( "Word",
                            new XAttribute( "pinyin", word.Pinyin ),
                            new XAttribute( "hanyu", word.Hanyu ),
                            word.ShortEnglishGiven ? new XAttribute("short_english",word.ShortEnglish) : null,
                            word.Known ? new XAttribute( "known", "true" ) : null,
                            word.UsageCount > 0 ? new XAttribute( "usage_count", word.UsageCount ) : null ) ) ) ).
                Save( FilePath( "words.xml" ) );
        }
    } // class

    class SuggestionComparer : IComparer<HanyuWord> {
        int IComparer<HanyuWord>.Compare( HanyuWord x, HanyuWord y ) {
            if (x.Suggest != y.Suggest) {
                return x.Suggest ? -1 : 1;
            } else if (x.UsageCount != y.UsageCount) {
                return x.UsageCount > y.UsageCount ? -1 : 1;
            } else if (x.Hanyu.Length != y.Hanyu.Length) {
                return x.Hanyu.Length < y.Hanyu.Length ? -1 : 1;
            } else {
                return StringComparer.InvariantCulture.Compare( x.Pinyin, y.Pinyin );
            }
        }
    }

} // namespace
