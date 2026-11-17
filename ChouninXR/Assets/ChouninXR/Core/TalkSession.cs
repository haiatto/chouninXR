using System;

namespace ChouninXR
{
    // セリフの進行だけを持つ。表示や入力はプラットフォーム側。
    // 将来 LLM のセリフもここに流し込めるよう、行の供給元を差し替えられるようにしておく。
    public class TalkSession
    {
        private readonly string[] _lines;
        private int _index;

        public TalkSession(string[] lines)
        {
            _lines = lines ?? Array.Empty<string>();
            _index = 0;
        }

        public bool IsEmpty => _lines.Length == 0;
        public bool IsFinished => _index >= _lines.Length;
        public bool HasNext => _index + 1 < _lines.Length;
        public int Index => _index;
        public int Count => _lines.Length;
        public string Current => IsFinished ? null : _lines[_index];

        // 次の行へ。最後の行で呼ぶと終了状態になる。
        public void Advance()
        {
            if (!IsFinished)
            {
                _index++;
            }
        }

        public void Restart() => _index = 0;
    }
}
