using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Entropy
{
    class SteamDrives
    {
        static string steamInstall = @"C:\Program Files (x86)\Steam";
        static string[] getRootFolders()
        {
            var libraryManifest = steamInstall + @"\SteamApps\libraryfolders.vdf";
            if (File.Exists(libraryManifest))
            {
                using (var fs = File.Open(libraryManifest, FileMode.Open, FileAccess.Read))
                {
                    UTF8Encoding temp = new UTF8Encoding(true);
                    Byte[] buffer = new byte[fs.Length];
                    fs.Read(buffer, 0, buffer.Length);
                    var tempString = temp.GetString(buffer);

                    var matches = Regex.Matches(tempString, @"(""[a-zA-Z]:\\\\.*)");

                    var results = new String[matches.Count+1];
                    results[0] = steamInstall;
                    for(int i = 0; i < matches.Count; i++)
                    {
                        results[i+1] = matches[i].ToString().Substring(1, matches[i].Length-2);
                    }

                    return results;
                }
            }
            Console.WriteLine(@"Unable to find SteamApps folder at ""{0}"", Perhaps your steam install is located elsewhere?", libraryManifest);
            return null;
        }

        public static void testSteamDrives()
        {
            foreach (String s in getRootFolders())
            {
                Console.WriteLine(s);
            }
        }

        public static List<int> getAllInstalledGames()
        {
            List<String> allFiles = new List<string>();
            foreach(var path in getRootFolders())
            {
                Console.WriteLine(path + @"\SteamApps");
                allFiles.AddRange(Directory.GetFiles(path + @"\SteamApps"));
            }
            //This is basically voodoo magic. I make the substring twice to make the substring once.
            var appIDs = allFiles.Where(x => x.EndsWith(".acf")).Select(x => x.Substring(x.IndexOf("appmanifest") + 12, x.Substring(x.IndexOf("appmanifest") + 12).Length - 4));
            return appIDs.Select(x => int.Parse(x)).ToList();
        }
    }
}
