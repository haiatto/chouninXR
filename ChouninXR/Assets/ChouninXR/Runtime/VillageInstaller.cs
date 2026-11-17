using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace ChouninXR
{
    // 実機では村データを persistentDataPath に置く。
    // 初回はアプリに同梱した村（StreamingAssets/villages）をそこへコピーする。
    // 同梱はビルド時に Editor/VillageBundler が行い、files.txt にファイル一覧を書く。
    public static class VillageInstaller
    {
        public const string BundleDir = "villages";
        public const string FileList = "files.txt";

        public static IEnumerator EnsureInstalled(string villagePath, string targetDir)
        {
#if UNITY_EDITOR
            yield break;
#else
            if (File.Exists(Path.Combine(targetDir, "village.json")))
            {
                yield break;
            }

            string root = Path.Combine(Application.streamingAssetsPath, BundleDir);
            string listText = null;
            yield return Fetch(Combine(root, FileList), bytes => listText = bytes != null ? System.Text.Encoding.UTF8.GetString(bytes) : null);
            if (listText == null)
            {
                Debug.LogWarning("[Chounin] 同梱の村データがありません");
                yield break;
            }

            // files.txt は villages/ からの相対パス（例: sample/village.json）
            string prefix = villagePath.Replace('\\', '/').TrimEnd('/');
            if (prefix.StartsWith(BundleDir + "/"))
            {
                prefix = prefix.Substring(BundleDir.Length + 1);
            }

            int copied = 0;
            foreach (var raw in listText.Split('\n'))
            {
                var rel = raw.Trim();
                if (rel.Length == 0 || !rel.StartsWith(prefix + "/"))
                {
                    continue;
                }
                byte[] data = null;
                yield return Fetch(Combine(root, rel), b => data = b);
                if (data == null)
                {
                    continue;
                }
                var dest = Path.Combine(targetDir, rel.Substring(prefix.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.WriteAllBytes(dest, data);
                copied++;
            }
            Debug.Log($"[Chounin] 同梱の村「{prefix}」を {copied} ファイルコピーしました → {targetDir}");
#endif
        }

        private static string Combine(string a, string b) => a.TrimEnd('/') + "/" + b;

        private static IEnumerator Fetch(string path, System.Action<byte[]> done)
        {
            // Android の StreamingAssets は APK の中なので File では読めない
            string url = path.Contains("://") ? path : "file://" + path;
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                done(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.data : null);
            }
        }
    }
}
