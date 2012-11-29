using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChineseWriter {

    public struct ChineseWordInfo {
        public string pinyin, hanyu, english;

        private string _simplePinyin; // with spaces and diacritics removed

        private string[] _englishParts;

        public bool IsEmpty { get { return pinyin == null; } }

        public string PinyinString { get { return pinyin == null ? "?" : pinyin; } }

        private string SimplePinyin {
            get {
                if (_simplePinyin == null) {
                    _simplePinyin = this.pinyin.RemoveDiacritics( ).ToLower( )
                        .Replace( " ", "" ).Replace( "'", "" );
                }
                return _simplePinyin;
            }
        }

        public string ShortEnglish { 
            get {
                return EnglishParts.First( );
            } 
        }

        private string[] EnglishParts {
            get {
                if (_englishParts == null) {
                    if (english == null) {
                        _englishParts = new string[] { "" };
                    } else {
                        _englishParts = english.ToLower( )
                            .Split( ',' )
                            .Select( word => word.Trim( ) )
                            .ToArray();
                    }
                }
                return _englishParts;
            }
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
