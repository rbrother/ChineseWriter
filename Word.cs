using System;
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
    }

    public class HanyuWord : Word {
        private readonly string _hanyu, _pinyin, _english;
        private readonly string _simplePinyin; // with spaces, tone-numbers and diacritics removed
        private string _displayPinyin; // with diacritics
        private readonly string[] _englishParts;

        override public string English { get { return _english; } }
        override public string Hanyu { get { return _hanyu; } }
        override public string Pinyin { get { return _pinyin; } }
        override public string ShortEnglish { get { return EnglishParts.First( ); } }

        override public string DisplayPinyin {
            get {
                if (_displayPinyin == null) {
                    _displayPinyin = string.Join(" ", _pinyin.Split( ' ' ).
                        Select( syllable => syllable.AddToneDiacritic( ) ).ToArray( ));
                }
                return _displayPinyin;
            }
        }

        private string SimplePinyin { get { return _simplePinyin; } }
        private string[] EnglishParts { get { return _englishParts; } }

        private static readonly Regex SIMPLIFY_PINYIN = new Regex( @"[ '\d]" );

        public HanyuWord( string hanyu, string pinyin, string english) {
            _hanyu = hanyu;
            _pinyin = pinyin;
            _english = english;
            _simplePinyin = SIMPLIFY_PINYIN.Replace( pinyin, "" ); 
            _englishParts = _english == null ?
                new string[] { "" } :
                _english.ToLower( )
                    .Split( ',' )
                    .Select( word => word.Trim( ) )
                    .ToArray( );
            }

        public bool MatchesPinyin( string pinyinInput, bool useEnglish ) {
            if (useEnglish) {
                return EnglishParts.Any( part => part.StartsWith( pinyinInput ));
            } else {
                return SimplePinyin.StartsWith( pinyinInput );
            }
        }

    }

}
