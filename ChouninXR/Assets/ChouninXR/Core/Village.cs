using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChouninXR
{
    // 村 = 町人の集まり。フォルダ構成：
    //   village.json
    //   chounin/<id>/chounin.json (+ model.vrm など)
    public class Village
    {
        public VillageData data;
        public string rootDirectory;
        public readonly List<ChouninData> chounins = new List<ChouninData>();

        public static Village LoadFromDirectory(string rootDirectory)
        {
            var village = new Village { rootDirectory = rootDirectory };

            var villageJson = Path.Combine(rootDirectory, "village.json");
            village.data = File.Exists(villageJson)
                ? JsonUtility.FromJson<VillageData>(File.ReadAllText(villageJson))
                : new VillageData { id = Path.GetFileName(rootDirectory), name = Path.GetFileName(rootDirectory) };

            var chouninRoot = Path.Combine(rootDirectory, "chounin");
            if (!Directory.Exists(chouninRoot))
            {
                return village;
            }

            foreach (var dir in Directory.GetDirectories(chouninRoot))
            {
                var path = Path.Combine(dir, "chounin.json");
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    var c = JsonUtility.FromJson<ChouninData>(File.ReadAllText(path));
                    c.baseDirectory = dir;
                    if (string.IsNullOrEmpty(c.id))
                    {
                        c.id = Path.GetFileName(dir);
                    }
                    village.chounins.Add(c);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Chounin] {path} を読めませんでした: {e.Message}");
                }
            }

            return village;
        }

        public static void Save(ChouninData chounin)
        {
            if (string.IsNullOrEmpty(chounin.baseDirectory))
            {
                throw new InvalidOperationException("保存先フォルダが決まっていません");
            }
            Directory.CreateDirectory(chounin.baseDirectory);
            File.WriteAllText(
                Path.Combine(chounin.baseDirectory, "chounin.json"),
                JsonUtility.ToJson(chounin, true));
        }

        public static string ResolveVrmPath(ChouninData chounin)
        {
            var vrm = chounin.model?.vrm;
            if (string.IsNullOrEmpty(vrm))
            {
                return null;
            }
            return Path.IsPathRooted(vrm) ? vrm : Path.Combine(chounin.baseDirectory ?? "", vrm);
        }
    }
}
