using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChouninXR.EditorTools
{
    // ビルドのときだけ、リポジトリ直下の villages/ を StreamingAssets/villages にコピーしてアプリに同梱する。
    // 実行時は VillageInstaller が初回起動で persistentDataPath に展開する。
    // コピー先は .gitignore 済み（村データの正本は villages/ だけ）。
    public class VillageBundler : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string DestDir = "Assets/StreamingAssets/" + VillageInstaller.BundleDir;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Bundle();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            Clean();
        }

        [MenuItem("ChouninXR/Build/村データを同梱用にコピー")]
        public static void Bundle()
        {
            var src = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "villages"));
            if (!Directory.Exists(src))
            {
                Debug.LogWarning("[ChouninXR] villages/ がありません: " + src);
                return;
            }

            Clean();
            Directory.CreateDirectory(DestDir);
            var files = Directory.GetFiles(src, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(src, f).Replace('\\', '/'))
                .Where(f => !f.EndsWith(".meta") && !Path.GetFileName(f).StartsWith("."))
                .OrderBy(f => f)
                .ToList();
            foreach (var rel in files)
            {
                var dest = Path.Combine(DestDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(Path.Combine(src, rel), dest, true);
            }
            File.WriteAllText(Path.Combine(DestDir, VillageInstaller.FileList), string.Join("\n", files));
            AssetDatabase.Refresh();
            Debug.Log($"[ChouninXR] 村データ {files.Count} ファイルを同梱用にコピーしました");
        }

        public static void Clean()
        {
            if (Directory.Exists(DestDir))
            {
                AssetDatabase.DeleteAsset(DestDir);
            }
        }
    }
}
