using System;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace ChineseWriter {

    public abstract class Word {
        abstract public string Text { get; }
        virtual public string Html { get { return Text; } }
        abstract public string DisplayPinyin { get; }
    }

    public class LiteralWord : Word {
        private string _text;

        public LiteralWord( string text ) {
            _text = text;
        }

        public override string Text { get { return _text; } }

        public override string DisplayPinyin { get { return Text; } }

    }

    public abstract class ChineseEnglishWord : Word {
        public override string Text { get { return Hanyu; } }
        abstract public string Hanyu { get; }
        abstract public string Pinyin { get; }
        abstract public string English { get; }
        abstract public string ShortEnglish { get; }
    }

    public class HanyuWord : ChineseEnglishWord {
        private readonly string _hanyu, _english;
        private readonly string _pinyin; // eg. "ma3 pa2"
        private readonly string _pinyinNoSpaces; // with spaces removed eg. "ma3pa2"
        private readonly string _pinyinNoSpacesNoTones; // eg. "mapa"
        private readonly string _pinyinDiacritics; // with diacritics added eg. "má pà"
        private string _shortEnglish; // explicit short english, replacing CCDICT first part

        override public string English { get { return _english; } }
        override public string Hanyu { get { return _hanyu; } }
        override public string Pinyin { get { return _pinyin; } }
        public bool ShortEnglishGiven { get { return _shortEnglish != null; } }
        override public string ShortEnglish {
            get {
                return ShortEnglishGiven ? _shortEnglish : English.Split( ',' ).First( );
            }
        }
        public void SetShortEnglish(string value) {
            _shortEnglish = value;
        }
        public bool Known { get; set; } // true: hide english
        public bool Suggest { get; set; }
        public int UsageCount;

        override public string DisplayPinyin {
            get { return _pinyinDiacritics; }
        }

        private static readonly Regex NUMBERS = new Regex( @"\d", RegexOptions.Compiled );

        private static readonly Regex CC_LINE = new Regex( @"(\S+)\s+(\S+)\s+\[([\w\:\s]+)\]\s+\/(.+)\/" );

        public HanyuWord( string line, Dictionary<Tuple<string, string>, XElement> info ) {
            var groups = CC_LINE.Match( line ).Groups;
            //var traditional = groups[1].Value;
            _hanyu = groups[2].Value;
            _pinyin = groups[3].Value;
            _english = groups[4].Value.Replace( "/", ", " );
            _pinyinNoSpaces = _pinyin.Replace( " ", "" ).Replace( ":", "" ).ToLower( );
            _pinyinNoSpacesNoTones = NUMBERS.Replace( _pinyinNoSpaces, "" );
            _pinyinDiacritics = _pinyin.AddDiacritics( );
            var infoKey = Tuple.Create( _hanyu, _pinyin );
            if (info.ContainsKey( infoKey )) {
                var wordInfo = info[infoKey];
                Suggest = true;
                if (wordInfo.Attribute( "short_english" ) != null) {
                    _shortEnglish = wordInfo.Attribute( "short_english" ).Value;
                }
                if (wordInfo.Attribute( "known" ) != null) {
                    Known = Convert.ToBoolean( wordInfo.Attribute( "known" ).Value );
                }
                if (wordInfo.Attribute( "usage_count" ) != null) {
                    UsageCount = Convert.ToInt32( wordInfo.Attribute( "usage_count" ).Value );
                }
            }
        }

        public bool MatchesPinyin( string pinyinInput ) {
            return _pinyinNoSpaces.StartsWith( pinyinInput ) ||
                _pinyinNoSpacesNoTones.StartsWith( pinyinInput );
        }

        // TODO: Make word compose of character-objects (or sub-word objects)
        public Tuple<string, string>[] Characters {
            get {
                return _hanyu.
                    ToCharArray( ).
                    Select( c => c.ToString() ).
                    Zip( _pinyin.Split( ' ' ), (hanyu, pinyin) => Tuple.Create(hanyu, pinyin) ).
                    ToArray();
            }
        }

        public override string Html {
            get {
                return LineDiv( HanyuHtml, "font-size: 20pt;" ) +
                    LineDiv( PinyinHtml ) + 
                    LineDiv( Known ? "&nbsp;" : ShortEnglish );
            }
        }

        private string HanyuHtml {
            get {
                return string.Join( "", 
                    Characters.Select( c => 
                        string.Format( "<span style='color: {0};'>{1}</span>",
                            HtmlColor( c.Item2 ), c.Item1 ) ).ToArray( ) );
            }
        }

        private string PinyinHtml {
            get {
                return string.Join( " ",
                    Characters.Select( c =>
                        string.Format( "<span style='color: {0};'>{1}</span>",
                            HtmlColor( c.Item2 ), c.Item2.AddDiacritics() ) ).ToArray( ) );
            }
        }

        private static string HtmlColor( string pinyin ) {
            var color = WordPanel.ToneColor( pinyin ).ToString();
            // remove the alpha value
            return "#" + color.Substring( 3 );
        }

        private string LineDiv( string content, string attr = "" ) {
            return string.Format( @"<div style='margin: 4px; text-align: center; height: 24pt; {0}'>{1}</div>", 
                attr, content );
        }

    }

    public class MultiMeaningWord : ChineseEnglishWord {
        private HanyuWord[] _words;

        override public string Hanyu { get { return _words.First().Hanyu; } }

        override public string Pinyin { get { return JoinBy( word => word.Pinyin ); } }

        override public string DisplayPinyin { get { return JoinBy( word => word.DisplayPinyin ); } }

        override public string English { get { return JoinBy( word => word.English ); } }

        override public string ShortEnglish { get { return JoinBy( word => word.ShortEnglish ); } }

        public HanyuWord[] Words { get { return _words; } }

        private string JoinBy( Func<HanyuWord, string> selector ) {
            return string.Join( " / ", _words.Select( selector ).ToArray() );
        }

        public MultiMeaningWord( HanyuWord[] words ) {
            _words = words;
        }
    }
}
