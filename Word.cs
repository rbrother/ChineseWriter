﻿using System;
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
        private readonly string _simplePinyin; // with spaces removed eg. "ma3pa2"
        private string _displayPinyin; // with diacritics added eg. "má pà"
        private readonly string[] _englishParts;
        private bool _suggest = false; // Use this word in suggestions

        override public string English { get { return _english; } }
        override public string Hanyu { get { return _hanyu; } }
        override public string Pinyin { get { return _pinyin; } }
        override public string ShortEnglish { get { return EnglishParts.First( ); } }

        public bool Suggest { get { return _suggest; } }

        public override string ToString( ) {
            return string.Format( "<{0}> <{1}:{2}:{3}> <{4}>", _hanyu, _pinyin, _simplePinyin, _displayPinyin, English );
        }

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

        public HanyuWord( string hanyu, string pinyin, string english, bool suggest) {
            _hanyu = hanyu;
            _pinyin = pinyin;
            _english = english;
            _suggest = suggest;
            _simplePinyin = pinyin.Replace( " ", "" ).ToLower(); 
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
