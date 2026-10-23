using System.IO;
using UnityEditor;
using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    public class TC_Links
    {
        [MenuItem("WSM Game Studio/Train Controller/Documentation")]
        static void OpenDocumentation()
        {
            string documentationFolder = "WSM Game Studio/Train Controller_v3/Documentation/Train Controller (Railroad System) User Manual v3.4.pdf";
            DirectoryInfo info = new DirectoryInfo(Application.dataPath);
            string documentationPath = Path.Combine(info.Name, documentationFolder);
            Application.OpenURL(documentationPath);
        }

        [MenuItem("WSM Game Studio/Train Controller/Write a Review")]
        static void Review()
        {
            Application.OpenURL("https://assetstore.unity.com/packages/slug/116455");
        }
    } 
}
