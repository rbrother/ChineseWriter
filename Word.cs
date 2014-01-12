using System;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using RT = clojure.lang.RT;

namespace ChineseWriter {

    /// <summary>
    /// Object for data-grid inteop
    /// </summary>
    public class SuggestionWord {

        private string _hanyu, _pinyin, _pinyinDiacritics; // Immutable word ID stuff, make a copy

        public SuggestionWord( int index, IDictionary<object, object> word, string shortCut ) {
            Index = index;
            _hanyu = word.Hanyu( );
            _pinyin = word.Pinyin( );
            _pinyinDiacritics = word.PinyinDiacritics( );
            Shortcut = shortCut;
        }

        public int Index { get; set; }
        public string Shortcut { get; set; }
        public string PinyinDiacritics { get { return _pinyinDiacritics; } set { } }
        public string Pinyin { get { return _pinyin; } }
        public string Hanyu { get { return _hanyu; } set { } }
        public string Known {
            get { return WordDatabase.KNOWLEDGE_LEVEL_DESCR[ KnownLevel ]; }
            set { KnownLevel = WordDatabase.KNOWLEDGE_LEVEL_VALUE[value]; }
        }
        public int KnownLevel { 
            get { return Convert.ToInt32( Get( "known" ) ); }
            set { Set( "known", Convert.ToInt64( value ) ); }
        }
        public string ShortEnglish {
            get { return (string) Get( "short-english" ); }
            set { Set("short-english", value ); }
        }
        public string Source {
            get { return (string)Get( "source" ); }
            set { Set( "source", value ); }
        }
        public object HSKIndex {
            get { return Get( "hsk-index" ); }
            set { Set( "hsk-index", value ); }
        }
        public object HanziRarity {
            get { return Get( "hanzi-rarity" ); }
            set { Set( "hanzi-rarity", value ); }
        }
        public string English {
            get { return (string)Get("english" ); }
            set { Set("english", value ); }
        }
        public string Finnish {
            get { return (string)Get( "finnish" ); }
            set { Set( "finnish", value ); }
        }
        internal void Delete( ) {
            WordDatabase.DeleteWordInfo( _hanyu, _pinyin );
        }
        private object Get( string propName ) { 
            return WordDatabase.GetWordProp( _hanyu, _pinyin, propName );
        }
        private void Set( string propName, object value ) {
            WordDatabase.SetWordProp( _hanyu, _pinyin, propName, value );
        }
    }

    public static class WordExtensions {

        public static T Get<T>( this IDictionary<object, object> dict, string key ) {
            return (T)Convert.ChangeType( dict[RT.keyword( null, key )], typeof( T ) );
        }

        public static IList<object> GetList( this IDictionary<object, object> dict, string key ) {
            return (IList<object>)dict[RT.keyword( null, key )];
        }

        public static bool HasKeyword( this IDictionary<object, object> dict, string key ) {
            return dict.ContainsKey( RT.keyword( null, key ) );
        }

        public static string Pinyin( this IDictionary<object, object> word ) {
            return word.HasKeyword( "pinyin" ) ?
                word.Get<string>( "pinyin" ) : word.Get<string>( "text" );
        }

        public static string ShortEnglish( this IDictionary<object, object> word ) {
            return word.HasKeyword( "text" ) ? word.Get<string>( "text" ) :
                word.Known( ) ? "" : word.Get<string>( "short-english" );
        }

        public static bool Known( this IDictionary<object, object> word ) {
            return word.HasKeyword( "known" ) && word.Get<int>( "known" ) >= 2;
        }

        public static string PinyinDiacritics( this IDictionary<object, object> word ) {
            return word.HasKeyword( "pinyin" ) ? DiacriticsAdderFunc.AddDiacritics( word.Get<string>( "pinyin" ) ) :
                word.Get<string>( "text" );
        }

        public static string Hanyu( this IDictionary<object, object> word ) {
            return word.HasKeyword( "hanyu" ) ?
                word.Get<string>( "hanyu" ) : word.Get<string>( "text" );
        }

    }

}
