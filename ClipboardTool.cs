using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChineseWriter {
    static class ClipboardTool {
        public static void CopyToClipboard( string htmlFragment, Uri sourceUrl ) {
            var enc = Encoding.UTF8;

            // Builds the CF_HTML header. See format specification here:
            // http://msdn.microsoft.com/library/default.asp?url=/workshop/networking/clipboard/htmlclipboard.asp

            // The string contains index references to other spots in the string, so we need placeholders so we can compute the offsets. 
            // The <<<<<<<_ strings are just placeholders. We'll backpatch them actual values afterwards.
            // The string layout (<<<) also ensures that it can't appear in the body of the html because the <
            // character must be escaped.
            string data =
                "Version:1.0" + Environment.NewLine +
                    "StartHTML:<<<<<<<1" + Environment.NewLine +
                    "EndHTML:<<<<<<<2" + Environment.NewLine +
                    "StartFragment:<<<<<<<3" + Environment.NewLine +
                    "EndFragment:<<<<<<<4" + Environment.NewLine +
                    "SourceURL:" + sourceUrl.ToString( ) + Environment.NewLine;

            int startHTML = enc.GetBytes( data ).Length;

            data += "<HTML><BODY>";
            int fragmentStart = enc.GetBytes( data ).Length;

            data += htmlFragment;
            int fragmentEnd = enc.GetBytes( data ).Length;

            data += @"</BODY></HTML>";
            int endHTML = enc.GetBytes( data ).Length;

            // Backpatch offsets
            data = data
                .Replace( "<<<<<<<1", To8DigitString( startHTML ) )
                .Replace( "<<<<<<<2", To8DigitString( endHTML ) )
                .Replace( "<<<<<<<3", To8DigitString( fragmentStart ) )
                .Replace( "<<<<<<<4", To8DigitString( fragmentEnd ) );

            // Finally copy to clipboard.
            Clipboard.Clear( );
            Clipboard.SetText( data, TextDataFormat.Html );
        }
        static string To8DigitString( int x ) {
            var s = x.ToString( );
            return new string( '0', 8 - s.Length ) + s;
        }

    }
}
