using SmartCalc.Core.Errors;
using SmartCalc.Core.Lexing;

namespace SmartCalc.Core.Input;

public enum ExpressionElementKind { Text, AbsoluteBar, Placeholder, Caret }

/// <summary>Một phần tử hiển thị; vị trí/độ dài luôn tính trên chuỗi gốc, không phải text đã trình bày.</summary>
public sealed record ExpressionElement(
    string Text, int SourceStart, int SourceLength, int SuperscriptLevel = 0,
    ExpressionElementKind Kind = ExpressionElementKind.Text, bool IsError = false)
{
    public double FontScale => ScaleAt(SuperscriptLevel);
    public static double ScaleAt(int level) => Math.Max(0.55, Math.Pow(0.75, level));
}

/// <summary>
/// Bố cục chỉ phục vụ hiển thị: Lexer cấp token, bộ đếm ngoặc xác định nhóm/hàm và dấu abs.
/// Không sửa biểu thức, không gọi Parser/Evaluator, chấp nhận cả biểu thức đang gõ dở.
/// </summary>
public sealed class ExpressionLayout
{
    private readonly int[] _sourceMap;
    public IReadOnlyList<ExpressionElement> Elements { get; }
    public bool IsRawText { get; }

    private ExpressionLayout(string source, List<ExpressionElement> elements, bool raw, int errorPosition,
        IReadOnlyDictionary<int, int>? hiddenTargets = null, int cursorPosition = -1,
        IReadOnlyDictionary<int, int>? cursorLevels = null)
    {
        IsRawText = raw;
        _sourceMap = MapSource(source.Length, elements, hiddenTargets);
        if (errorPosition >= 0 && errorPosition <= source.Length)
        {
            int marked = _sourceMap[errorPosition];
            if (marked < 0)
            {
                // Ký tự cuối bị ẩn hoặc lỗi ở cuối chuỗi: giữ ô lỗi trống như màn hình cũ.
                elements.Add(new(" ", source.Length, 0));
                _sourceMap = MapSource(source.Length, elements, hiddenTargets);
                marked = _sourceMap[errorPosition];
            }
            elements[marked] = elements[marked] with { IsError = true };
        }
        if (cursorPosition >= 0 && cursorPosition <= source.Length)
        {
            int at = elements.FindIndex(e => e.SourceStart >= cursorPosition);
            if (at < 0) at = elements.Count;
            int level = at < elements.Count ? elements[at].SuperscriptLevel : elements.LastOrDefault()?.SuperscriptLevel ?? 0;
            if (cursorPosition < source.Length && source[cursorPosition] == '^')
                level = at > 0 ? elements[at - 1].SuperscriptLevel : 0;
            // Ngoặc mũ bị ẩn vẫn phân biệt vị trí bên trong/bên ngoài, kể cả mũ lồng nhau.
            if (cursorLevels?.TryGetValue(cursorPosition, out int specifiedLevel) == true) level = specifiedLevel;
            elements.Insert(at, new("", cursorPosition, 0, level, ExpressionElementKind.Caret));
            for (int p = 0; p < _sourceMap.Length; p++)
                if (_sourceMap[p] >= at) _sourceMap[p]++;
        }
        Elements = elements.AsReadOnly();
    }

    /// <summary>Chỉ số phần tử ứng với vị trí gốc; ký tự ẩn ánh xạ tới phần tử ngay sau nó.</summary>
    public int FindElement(int sourcePosition) =>
        sourcePosition >= 0 && sourcePosition < _sourceMap.Length ? _sourceMap[sourcePosition] : -1;

    public static ExpressionLayout Create(string source, bool mathNotation = true, int errorPosition = -1, int cursorPosition = -1)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (mathNotation)
        {
            try
            {
                var builder = new Builder(source, Lexer.Tokenize(source));
                return new(source, builder.Build(), false, errorPosition, builder.HiddenTargets, cursorPosition, builder.CursorLevels);
            }
            catch (CalcException) { /* Lexer không nhận chuỗi: hiện nguyên text để vẫn thấy chỗ sai. */ }
            catch (LayoutLimitException) { /* Giới hạn đệ quy hiển thị khi dán chuỗi quá sâu. */ }
        }

        var raw = source.Select((c, i) => new ExpressionElement(c.ToString(), i, 1)).ToList();
        return new(source, raw, true, errorPosition, cursorPosition: cursorPosition);
    }

    private static int[] MapSource(int length, List<ExpressionElement> elements, IReadOnlyDictionary<int, int>? hiddenTargets)
    {
        var map = Enumerable.Repeat(-1, length + 1).ToArray();
        var firstAt = Enumerable.Repeat(-1, length + 1).ToArray();
        for (int i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            if (firstAt[element.SourceStart] < 0) firstAt[element.SourceStart] = i;
            for (int p = element.SourceStart; p < element.SourceStart + element.SourceLength; p++)
                map[p] = i;
        }
        int next = -1;
        for (int p = length; p >= 0; p--)
        {
            if (firstAt[p] >= 0) next = firstAt[p];
            if (map[p] < 0) map[p] = next;
        }
        // Placeholder của mũ trước có thể nằm cùng vị trí với dấu ^ tiếp theo (vd 2^^3).
        // Cạnh ^ ghi đích trực tiếp theo thứ tự hiển thị để không tô nhầm placeholder đó.
        if (hiddenTargets is not null)
            foreach (var (position, target) in hiddenTargets) map[position] = target;
        return map;
    }

    private sealed class LayoutLimitException : Exception { }

    private sealed class Builder
    {
        private readonly string _source;
        private readonly IReadOnlyList<Token> _tokens;
        private readonly int[] _closing;
        private readonly List<ExpressionElement> _elements = [];
        private int _index;
        private int _cursor;
        public Dictionary<int, int> HiddenTargets { get; } = [];
        public Dictionary<int, int> CursorLevels { get; } = [];
        private int Count => _tokens.Count - 1; // bỏ End

        public Builder(string source, IReadOnlyList<Token> tokens)
        {
            _source = source;
            _tokens = tokens;
            _closing = Enumerable.Repeat(-1, tokens.Count).ToArray();
            var openings = new Stack<int>();
            for (int i = 0; i < Count; i++)
            {
                if (tokens[i].Kind == TokenKind.LParen) openings.Push(i);
                else if (tokens[i].Kind == TokenKind.RParen && openings.TryPop(out int opening))
                    _closing[opening] = i;
            }
        }

        public List<ExpressionElement> Build()
        {
            Sequence(Count, 0, 0);
            TextUntil(_source.Length, 0);
            return _elements;
        }

        private void Sequence(int limit, int level, int nesting)
        {
            CheckDepth(nesting);
            while (_index < limit)
            {
                TextUntil(_tokens[_index].Position, level);
                if (_tokens[_index].Kind == TokenKind.Caret)
                {
                    int position = _tokens[_index].Position;
                    SkipToken();
                    int first = _elements.Count;
                    Power(limit, level + 1, nesting + 1);
                    HiddenTargets[position] = first;
                }
                else Atom(limit, level, nesting + 1, stripGroup: false);
            }
        }

        private void Power(int limit, int level, int nesting)
        {
            CheckDepth(nesting);
            // Dấu một ngôi thuộc số mũ, vd 2^-3; dấu trong token số mũ 1e-3 do Lexer xử lý.
            while (_index < limit && _tokens[_index].Kind is TokenKind.Plus or TokenKind.Minus)
                ShowToken(level);

            if (_index >= limit || _tokens[_index].Kind is not (TokenKind.Number or TokenKind.Identifier or TokenKind.LParen))
            {
                Placeholder(limit, level);
                return;
            }

            int firstElement = _elements.Count;
            bool complete = Atom(limit, level, nesting + 1, stripGroup: true);
            while (_index < limit && _tokens[_index].Kind == TokenKind.Bang) ShowToken(level);
            if (!complete || _elements.Count == firstElement) Placeholder(limit, level);

            // Lũy thừa kết hợp phải: phần mũ tiếp theo nâng thêm một tầng.
            if (_index < limit && _tokens[_index].Kind == TokenKind.Caret)
            {
                TextUntil(_tokens[_index].Position, level);
                int position = _tokens[_index].Position;
                SkipToken();
                int first = _elements.Count;
                Power(limit, level + 1, nesting + 1);
                HiddenTargets[position] = first;
            }
        }

        private bool Atom(int limit, int level, int nesting, bool stripGroup)
        {
            CheckDepth(nesting);
            var token = _tokens[_index];
            if (token.Kind == TokenKind.LParen)
                return Group(limit, level, nesting + 1, stripGroup, absolute: false);

            if (token.Kind == TokenKind.Identifier && _index + 1 < limit && _tokens[_index + 1].Kind == TokenKind.LParen)
            {
                if (string.Equals(token.Text, "abs", StringComparison.OrdinalIgnoreCase))
                {
                    TextUntil(token.Position, level);
                    int start = token.Position;
                    SkipToken();
                    return Group(limit, level, nesting + 1, strip: false, absolute: true, absoluteStart: start);
                }
                ShowToken(level);
                return Group(limit, level, nesting + 1, strip: false, absolute: false);
            }
            ShowToken(level);
            return true;
        }

        private bool Group(int limit, int level, int nesting, bool strip, bool absolute, int absoluteStart = -1)
        {
            CheckDepth(nesting);
            int closing = _closing[_index];
            bool complete = closing >= 0 && closing < limit;
            int innerLimit = complete ? closing : limit;
            var opening = _tokens[_index];
            if (absolute)
            {
                _elements.Add(new("|", absoluteStart, opening.Position + 1 - absoluteStart, level, ExpressionElementKind.AbsoluteBar));
                SkipToken();
            }
            else if (strip)
            {
                TextUntil(opening.Position, level);
                SkipToken();
                CursorLevels[opening.Position] = Math.Max(0, level - 1);
                CursorLevels[opening.Position + 1] = level;
            }
            else ShowToken(level);

            int contentStart = _elements.Count;
            Sequence(innerLimit, level, nesting + 1);
            TextUntil(_tokens[innerLimit].Position, level);
            if (complete && _elements.Count == contentStart)
                _elements.Add(new("", _tokens[innerLimit].Position, 0, level, ExpressionElementKind.Placeholder));
            if (complete)
            {
                if (absolute)
                {
                    _elements.Add(new("|", _tokens[_index].Position, 1, level, ExpressionElementKind.AbsoluteBar));
                    SkipToken();
                }
                else if (strip)
                {
                    int position = _tokens[_index].Position;
                    CursorLevels[position] = level;
                    CursorLevels[position + 1] = Math.Max(0, level - 1);
                    SkipToken();
                }
                else ShowToken(level);
            }
            return complete;
        }

        private void Placeholder(int limit, int level)
        {
            int position = _tokens[Math.Min(_index, limit)].Position;
            TextUntil(position, level);
            _elements.Add(new("", position, 0, level, ExpressionElementKind.Placeholder));
        }

        private void ShowToken(int level)
        {
            var token = _tokens[_index++];
            TextUntil(token.Position + token.Text.Length, level);
        }

        private void SkipToken()
        {
            var token = _tokens[_index++];
            _cursor = token.Position + token.Text.Length;
        }

        private void TextUntil(int end, int level)
        {
            while (_cursor < end)
            {
                char c = _source[_cursor];
                _elements.Add(new(c == '-' ? "−" : c.ToString(), _cursor++, 1, level));
            }
        }

        private static void CheckDepth(int nesting)
        {
            if (nesting > 128) throw new LayoutLimitException();
        }
    }
}
