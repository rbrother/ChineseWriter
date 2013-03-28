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

        public static string Pinyin( this IDictionary<object,object> word ) {
            return word.HasKeyword("pinyin") ?
                word.Get<string>( "pinyin" ) : word.Get<string>( "text" );
        }

        public static bool HasKeyword( this IDictionary<object, object> dict, string key ) {
            return dict.ContainsKey( RT.keyword( null, key ) );
        }

        public static string PinyinDiacritics( this IDictionary<object,object> word ) {
            return word.HasKeyword( "pinyin" ) ?
                word.Pinyin( ).AddDiacritics( ) : word.Get<string>( "text" );
        }

        public static string Hanyu( this IDictionary<object,object> word ) {
            return word.HasKeyword( "hanyu" ) ?
                word.Get<string>( "hanyu" ) : word.Get<string>( "text" );
        }

    }

}
