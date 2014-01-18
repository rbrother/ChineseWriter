using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brotherus {

    public static class Utils {

        public static string FindRelativeFile( string fileName ) {
            return SearchUpwardFile( ExeDir, fileName );
        }

        public static DirectoryInfo ExeDir {
            get {
                var exePath = new Uri( Assembly.GetExecutingAssembly( ).CodeBase ).LocalPath;
                var exeDir = new FileInfo( exePath ).Directory;
                return exeDir;
            }
        }

        private static string SearchUpwardFile( DirectoryInfo startDir, string fileName ) {
            var theFile = startDir.GetFileSystemInfos().FirstOrDefault( file => file.Name == fileName );
            if ( theFile != null ) return theFile.FullName;
            return SearchUpwardFile( startDir.Parent, fileName );
        }

    }
}
