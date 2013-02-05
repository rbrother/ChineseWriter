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
        virtual public string English { get { return ""; } }
        virtual public string ShortEnglish { get { return English; } }
        virtual public Color Color { get { return Colors.Transparent; } }
    }

    public class LiteralWord : Word {
        private string _text;

        public LiteralWord( string text ) {
            _text = text;
        }

        override public string Hanyu { get { return _text; } }
        override public string Pinyin { get { return _text; } }
        override public string English { get { return _text; } }
        override public Color Color { get { return Color.FromArgb( 128, 255, 255, 255 ); } }
    }

    public class UnknownHanyu : Word {
        private string _hanyu;

        public UnknownHanyu( string hanyu ) {
            _hanyu = hanyu;
        }

        override public string Hanyu { get { return _hanyu; } }
        override public string Pinyin { get { return "?"; } }
        override public string English { get { return "?"; } }
        override public Color Color { get { return Color.FromArgb(128,255,0,0); } }
    }

    public class KnownHanyu : Word {
        private readonly string _hanyu, _pinyin, _english;
        private readonly string _simplePinyin; // with spaces and diacritics removed
        private readonly string[] _englishParts;

        override public string English { get { return _english; } }
        override public string Hanyu { get { return _hanyu; } }
        override public string Pinyin { get { return _pinyin; } }
        override public string ShortEnglish { get { return EnglishParts.First( ); } }

        private string SimplePinyin { get { return _simplePinyin; } }
        private string[] EnglishParts { get { return _englishParts; } }

        public KnownHanyu( string hanyu, string pinyin = null, string english = null) {
            _hanyu = hanyu;
            _pinyin = pinyin;
            _english = english;
            _simplePinyin = this._pinyin.RemoveDiacritics( ).ToLower( )
                .Replace( " ", "" ).Replace( "'", "" );
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
