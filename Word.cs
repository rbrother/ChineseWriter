using System;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace ChineseWriter {

    public class Word {
        virtual public string Hanyu { get { return ""; } }
        virtual public string Pinyin { get { return ""; } }
        virtual public string DisplayPinyin { get { return Pinyin; } }
        virtual public string English { get { return ""; } }
        virtual public string ShortEnglish { get { return English; } }
        virtual public Color PanelColor { get { return Colors.White; } }
    }

    public class LiteralWord : Word {
        private string _text;

        public LiteralWord( string text ) {
            _text = text;
        }

        override public string Hanyu { get { return _text; } }
        override public string Pinyin { get { return _text; } }
        override public string English { get { return _text; } }
        override public Color PanelColor { get { return Color.FromRgb( 220, 220, 220 ); } }

        public override string ToString( ) {
            return string.Format("LiteralWord: {0}", _text);
        }
    }

    public class HanyuWord : Word {
        private readonly string _hanyu, _english;
        private readonly string _pinyin; // eg. "ma3 pa2"
        private readonly string _pinyinNoSpaces; // with spaces removed eg. "ma3pa2"
        private readonly string _pinyinNoSpacesNoTones; // eg. "mapa"
        private readonly string _pinyinDiacritics; // with diacritics added eg. "má pà"
        private readonly string _shortEnglish; // explicit short english, replacing CCDICT first part
        private bool _known; // true: hide english

        override public string English { get { return _english; } }
        override public string Hanyu { get { return _hanyu; } }
        override public string Pinyin { get { return _pinyin; } }
        public bool ShortEnglishGiven { get { return _shortEnglish != null; } }
        override public string ShortEnglish {
            get {
                return ShortEnglishGiven ? _shortEnglish : English.Split( ',' ).First( );
            }
        }
        public bool Known { get { return _known; } }
        public bool Suggest { get; set; }
        public int UsageCount;

        public override string ToString( ) {
            return string.Format( "<{0}> <{1}:{2}:{3}> <{4}>", _hanyu, _pinyin, _pinyinNoSpaces, _pinyinDiacritics, English );
        }

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
                    _known = Convert.ToBoolean( wordInfo.Attribute( "known" ).Value );
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

    }

    public class MultiMeaningWord : Word {
        private HanyuWord[] _words;

        public override Color PanelColor { get { return Colors.Yellow; } }

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
