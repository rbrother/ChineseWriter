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
        virtual public Color Color { get { return Colors.White; } }
    }

    public class LiteralWord : Word {
        private string _text;

        public LiteralWord( string text ) {
            _text = text;
        }

        override public string Hanyu { get { return _text; } }
        override public string Pinyin { get { return _text; } }
        override public string English { get { return _text; } }
        override public Color Color { get { return Color.FromRgb( 220, 220, 220 ); } }

        public override string ToString( ) {
            return string.Format("LiteralWord: {0}", _text);
        }
    }

    public class HanyuWord : Word {
        private readonly string _hanyu, _english;
        private readonly string _pinyin; // eg. "ma3 pa2"
        private readonly string _pinyinNoSpaces; // with spaces removed eg. "ma3pa2"
        private readonly string _pinyinNoSpacesNoTones; // eg. "mapa"
        private string _pinyinDiacritics; // with diacritics added eg. "má pà"

        override public string English { get { return _english; } }
        override public string Hanyu { get { return _hanyu; } }
        override public string Pinyin { get { return _pinyin; } }
        override public string ShortEnglish { get { return English.Split(',').First(); } }

        public bool Suggest { get; set; }

        public override string ToString( ) {
            return string.Format( "<{0}> <{1}:{2}:{3}> <{4}>", _hanyu, _pinyin, _pinyinNoSpaces, _pinyinDiacritics, English );
        }

        override public string DisplayPinyin {
            get { return _pinyinDiacritics; }
        }

        private static Regex NUMBERS = new Regex( @"\d", RegexOptions.Compiled );

        public HanyuWord( string hanyu, string pinyin, string english, bool suggest ) {
            _hanyu = hanyu;
            _pinyin = pinyin;
            _english = english;
            _pinyinNoSpaces = pinyin.Replace( " ", "" ).ToLower();
            _pinyinNoSpacesNoTones = NUMBERS.Replace( _pinyinNoSpaces, "" );
            _pinyinDiacritics = _pinyin.AddToneDiacritics( );
            Suggest = suggest;
        }

        public bool MatchesPinyin( string pinyinInput ) {
            return _pinyinNoSpaces.StartsWith( pinyinInput ) ||
                _pinyinNoSpacesNoTones.StartsWith( pinyinInput );
        }

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

        public override Color Color { get { return Colors.Yellow; } }

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
