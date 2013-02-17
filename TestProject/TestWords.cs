using System;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChineseWriter {
    [TestClass]
    public class TestWords {

        private static string INFO_STRING = @"
            <Chinese>
              <Words>
                <Word pinyin='A' hanyu='啊' />
                <Word pinyin='Ài' hanyu='爱' />
                <Word pinyin='Ài ren' hanyu='爱人' />
                <Word pinyin='Ān' hanyu='安' />
                <Word pinyin='Wǒ' hanyu='我' />
                <Word pinyin='Wǒ men' hanyu='我们' />
              </Words>
            </Chinese>
            ";

        private static string[] CC_LINES = new string[] {
            "婐 婐 [wo3] /maid/",
            "我 我 [wo3] /I/me/my/",
            "我們 我们 [wo3 men5] /we/us/ourselves/our/",
            "戒刀 戒刀 [jie4 dao1] /Buddhist monk's knife (not used for killing)/"
        };

        private static Dictionary<string /*hanyu + displaypinyin*/, XElement> INFO_DICT =
            WordDatabase.ParseInfoDict( XElement.Parse( INFO_STRING ) );

        [TestMethod]
        public void TestParseInfo( ) {
            Assert.AreEqual<int>( 6, INFO_DICT.Count );
            Assert.AreEqual( typeof( XElement ), INFO_DICT["我wǒ"].GetType( ) );
            Assert.AreEqual( typeof( XElement ), INFO_DICT["我们wǒmen"].GetType( ) ); 
        }

        [TestMethod]
        public void TestParseCC( ) {
            var words = WordDatabase.ParseCCLines( CC_LINES, INFO_DICT );
            Assert.AreEqual<int>( 4, words.Length );
            Assert.AreEqual<int>( 2, words.Where( word => word.Suggest ).Count( ) );
        }
    }
}
