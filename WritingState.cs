using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Key = System.Windows.Input.Key;

namespace ChineseWriter {

    internal class WritingState {

        private WordDatabase _hanyuDb;
        private int _cursorPos;
        private Word[] _words;
        private HanyuWord[] _suggestions;
        private bool _english;
        private string _pinyinInput = "";

        // Observables
        public Subject<string> PinyinChanges = new Subject<string>( );
        public Subject<bool> EnglishChanges = new Subject<bool>( );
        public Subject<Word[]> SuggestionsChanges = new Subject<Word[]>( );
        public Subject<Word[]> WordsChanges = new Subject<Word[]>( );
        public Subject<int> CursorPosChanges = new Subject<int>( );

        // Observables accessors
        public bool English {
            get { return _english;  }
            set { _english = value;  EnglishChanges.OnNext( value ); }
        }
        public string PinyinInput {
            get { return _pinyinInput;  }
            set { _pinyinInput = value; PinyinChanges.OnNext( value ); }
        }
        public Word[] Words { 
            get { return _words; }
            set { 
                _words = value;
                if (_cursorPos > _words.Length) CursorPos = _words.Length;
                WordsChanges.OnNext( _words ); 
            }
        }
        public int CursorPos {
            get { return _cursorPos; }
            set { _cursorPos = value; CursorPosChanges.OnNext( value ); }
        }

        // Constructor
        public WritingState( WordDatabase wordDatabase ) {
            _hanyuDb = wordDatabase;
            PinyinChanges.CombineLatest( EnglishChanges, ( pinyin, english ) => 0 ).
                Subscribe( value => UpdateSuggestions( ) );
        }

        private void UpdateSuggestions( ) {
            _suggestions = PinyinInput == "" ? new HanyuWord[] {} :
                _hanyuDb.
                    MatchingSuggestions( WordSelector( PinyinInput, English ) ).
                    Take( 50 ).ToArray();
            SuggestionsChanges.OnNext( _suggestions );
        }

        private static Func<HanyuWord, bool> WordSelector( string textToFind, bool english ) {
            if (english) {
                var regex = new Regex( @"\b" + textToFind, RegexOptions.Compiled );
                return word => regex.IsMatch( word.English );
            } else {
                return word => word.MatchesPinyin( textToFind );
            }
        }

        public void TextEdit( Key key ) {
            switch (key ) {
                case Key.Back: BackSpace(); break;
                case Key.Delete: Delete(); break;
                case Key.Left: CursorPos--; break;
                case Key.Right: CursorPos++; break;
                case Key.Home: CursorPos = 0; break;
                case Key.End: CursorPos = Words.Length; break;
            }
            if (CursorPos < 0) CursorPos = 0;
            if (CursorPos > Words.Length) CursorPos = Words.Length;
        }

        private void Delete() {
            if (CursorPos < Words.Length) {
                Words = Words.Take(CursorPos).
                    Concat(Words.Skip(CursorPos + 1)).
                    ToArray();
            }            
        }

        private void BackSpace() {
            if (CursorPos > 0) {
                var orignalCursor = CursorPos;
                Words = Words.Take( orignalCursor - 1 ).
                    Concat( Words.Skip( orignalCursor ) ).
                    ToArray();
                CursorPos = orignalCursor - 1;
            }
        }

        internal void SelectPinyin( int n ) {
            if (n == 1 && _suggestions.Length == 0 ) {
                // Literal input
                InsertWords( _hanyuDb.HanyuToWords( PinyinInput ) );
            } else if (n > _suggestions.Length) {
                return; 
            } else {
                SelectWord( _suggestions[n - 1] );
            }
            PinyinInput = "";
        }

        internal void SelectWord( HanyuWord word ) {
            InsertWords( new Word[] { word } );
            word.Suggest = true; // Automatically add selected words for future suggestions to words.xml
            word.UsageCount++;
            PinyinInput = "";
        }

        private void InsertWords( IEnumerable<Word> newWords ) {
            Words = Words.Take( CursorPos ).
                Concat( newWords ).
                Concat( Words.Skip( CursorPos ) ).
                ToArray( );
            CursorPos = CursorPos + newWords.Count();
        }

        public string HanyiPinyinLines { 
            get {
                var pinyinLine = string.Join( "  ", Words
                    .Select( word => word.DisplayPinyin ).ToArray( ) );
                var hanyiLine = string.Join( " ", Words
                    .Select( word => word.Text ).ToArray( ) );
                return hanyiLine + "\n" + pinyinLine;
            } 
        }

        public string HanyiPinyinHtml {
            get {
                return "<table><tr>" +
                    string.Join( "", Words.Select( word => word.Hanyu ).ToArray() ) +
                    "</tr></table>";
            }
        }

        internal void Clear( ) {
            Words = new Word[] { };
            PinyinInput = "";
            CursorPos = 0;
        }

        internal string Hanyu {
            get { return string.Join( "", Words.Select( word => word.Text ).ToArray( ) ); }
        }

        internal void Reparse( ) {
            Words = _hanyuDb.HanyuToWords( Hanyu );
        }
    } // class

} // namespace
