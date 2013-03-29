using System;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using RT = clojure.lang.RT;

namespace ChineseWriter {

    public class SuggestionWord {
        public string Pinyin { get; set; }
        public string Hanyu { get; set; }
        public string UsageCountString { get; set; }
        public string English { get; set; }
    }

    public static class WordExtensions {

        public static T Get<T>( this IDictionary<object,object> dict, string key ) {
            return (T) Convert.ChangeType( dict[ RT.keyword( null, key) ], typeof(T) );
        }

        public static IList<object> GetList( this IDictionary<object,object> dict, string key ) {
            return (IList<object>)dict[RT.keyword( null, key )];
        }

        public static bool HasKeyword( this IDictionary<object, object> dict, string key ) {
            return dict.ContainsKey( RT.keyword( null, key ) );
        }

        public static string Pinyin( this IDictionary<object,object> word ) {
            return word.HasKeyword("pinyin") ?
                word.Get<string>( "pinyin" ) : word.Get<string>( "text" );
        }

        public static string ShortEnglish( this IDictionary<object,object> word ) {
            return word.HasKeyword("text") ? word.Get<string>("text") :
                word.Get<bool>( "known" ) ? "" : word.Get<string>( "short-english" );
        }

        public static string PinyinDiacritics( this IDictionary<object,object> word ) {
            return word.HasKeyword( "pinyin" ) ?
                word.Pinyin( ).AddDiacritics( ) : word.Get<string>( "text" );
        }

        public static string Hanyu( this IDictionary<object,object> word ) {
            return word.HasKeyword( "hanyu" ) ?
                word.Get<string>( "hanyu" ) : word.Get<string>( "text" );
        }

        public static string UsageCountStr( this IDictionary<object,object> word ) {
            return word.HasKeyword( "usage-count" ) ?
                Convert.ToString( word.Get<int>("usage-count") ) : "";
        }

        public static SuggestionWord ToDataWord( this IDictionary<object, object> word ) {
            return new SuggestionWord {
                Pinyin = ( word.Pinyin( ) ).AddDiacritics( ),
                Hanyu = word.Hanyu( ),
                English = word.Get<string>( "english" ),
                UsageCountString = word.UsageCountStr( )
            };
        }

    }

}
