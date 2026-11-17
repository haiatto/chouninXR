using TMPro;
using UnityEngine;

namespace ChouninXR
{
    // 日本語を表示するための TMP フォント。
    // まずは OS のフォントから動的に作る（フォントをリポジトリに同梱しないため）。
    // 見た目を固定したくなったら、ライセンスの明確なフォント（Noto Sans JP 等）を同梱して差し替える。
    public static class JapaneseFont
    {
        private static TMP_FontAsset _cached;

        // (ファミリー名, スタイル名)
        private static readonly (string family, string style)[] Candidates =
        {
            // Android
            ("Noto Sans CJK JP", "Regular"),
            ("Noto Sans JP", "Regular"),
            // Windows（エディタ）
            ("Yu Gothic", "Regular"),
            ("Yu Gothic UI", "Regular"),
            ("Meiryo", "Regular"),
            ("MS Gothic", "Regular"),
            // macOS（エディタ）
            ("Hiragino Sans", "W3"),
        };

        public static TMP_FontAsset Get()
        {
            if (_cached != null)
            {
                return _cached;
            }

            foreach (var (family, style) in Candidates)
            {
                var asset = TMP_FontAsset.CreateFontAsset(family, style, 64);
                if (asset != null)
                {
                    asset.name = $"OS_{family}";
                    _cached = asset;
                    return asset;
                }
            }

            Debug.LogWarning("[Chounin] 日本語フォントを用意できませんでした");
            return null;
        }
    }
}
