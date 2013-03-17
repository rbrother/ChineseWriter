using System;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Var = clojure.lang.Var;
using RT = clojure.lang.RT;

namespace ChineseWriter {
    [TestClass]
    public class TestWords {

        private static string INFO_STRING = @"
            <Chinese>
              <Words>
                <Word pinyin='a1' hanyu='啊' />
                <Word pinyin='ai4' hanyu='爱' />
                <Word pinyin='ai4 ren5' hanyu='爱人' />
                <Word pinyin='an1' hanyu='安' />
                <Word pinyin='wo3' hanyu='我' usage_count='5' />
                <Word pinyin='wo3 men5' hanyu='我们' known='true' short_english='we' />
              </Words>
            </Chinese>
            ";

        private static string[] CC_LINES = new string[] {
            "# Comment line",
            "啊 啊 [a1] /interjection of surprise/Ah!/Oh!/",
            "啊 啊 [a2] /interjection expressing doubt or requiring answer/Eh?/what?/to show realization/to stress/",
            "婐 婐 [wo3] /maid/",
            "我 我 [wo3] /I/me/my/",
            "㐅 㐅 [wu3] /archaic variant of 五[wu3]/",
            "我們 我们 [wo3 men5] /we/us/ourselves/our/",
            "戒刀 戒刀 [jie4 dao1] /Buddhist monk's knife (not used for killing)/",
            "女友 女友 [nu:3 you3] /girlfriend/"
        };

        [TestMethod]
        public void TestClojure( ) {
            System.Environment.SetEnvironmentVariable("CLOJURE_LOAD_PATH",
                @"C:/Google Drive/programs/clojure-clr;c:/github/ChineseWriter/Clojure");
            RT.load( "WordDatabase" );
            var words = (IList<object>)RT.var( "WordDatabase", "parse-cc-lines" ).
                invoke( CC_LINES, INFO_STRING );
            Assert.AreEqual( 7, words.Count );
        }
    }
}
