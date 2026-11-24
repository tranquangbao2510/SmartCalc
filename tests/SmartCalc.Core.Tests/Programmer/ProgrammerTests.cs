using SmartCalc.Core.Errors;
using SmartCalc.Core.Input;
using SmartCalc.Core.Lexing;
using SmartCalc.Core.Parsing;
using SmartCalc.Core.Programmer;

namespace SmartCalc.Core.Tests.Programmer;

public class ProgrammerLexerTests
{
    [Theory]
    [InlineData("FF", 16)]
    [InlineData("ff", 16)]
    [InlineData("1A2B", 16)]
    [InlineData("0B1", 16)]      // ở hệ HEX, "0B1" là số hex, không phải tiền tố 0b
    [InlineData("777", 8)]
    [InlineData("1011", 2)]
    [InlineData("255", 10)]
    [InlineData("0x1F", 10)]
    [InlineData("0b101", 8)]
    public void NumberInRadix_IsSingleToken(string input, int radix)
    {
        var tokens = Lexer.Tokenize(input, programmerMode: true, radix);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenKind.Number, tokens[0].Kind);
        Assert.Equal(input, tokens[0].Text);
    }

    [Theory]
    [InlineData("102", 2, 2)]
    [InlineData("78", 8, 1)]
    [InlineData("1A", 10, 1)]
    [InlineData("FG", 16, 1)]
    [InlineData("1.5", 10, 1)]   // không có số thực ở chế độ lập trình
    public void DigitOutsideRadix_ReportsPosition(string input, int radix, int position)
    {
        var ex = Assert.Throws<CalcException>(() => Lexer.Tokenize(input, programmerMode: true, radix));
        Assert.Equal(position, ex.Position);
    }

    [Fact]
    public void HexLettersStartNumbers_NotIdentifiers()
    {
        var tokens = Lexer.Tokenize("A&F", programmerMode: true, radix: 16);
        Assert.Equal([TokenKind.Number, TokenKind.Ampersand, TokenKind.Number, TokenKind.End], tokens.Select(t => t.Kind));
    }
}

public class ProgrammerParserTests
{
    private static string Tree(string s, int radix = 10) => AstPrinter.ToPrefix(ProgrammerParser.Parse(s, radix));

    [Theory]
    // độ ưu tiên kiểu C: * > + > << > & > ^ > |
    [InlineData("1|2^3&4", "(| 1 (xor 2 (& 3 4)))")]
    [InlineData("1&2<<3", "(& 1 (<< 2 3))")]
    [InlineData("1<<2+3", "(<< 1 (+ 2 3))")]
    [InlineData("1+2*3", "(+ 1 (* 2 3))")]
    [InlineData("8-2-1", "(- (- 8 2) 1)")]
    [InlineData("~1&2", "(& (~ 1) 2)")]
    [InlineData("-~5", "(neg (~ 5))")]
    [InlineData("(1|2)&3", "(& (| 1 2) 3)")]
    public void Precedence(string input, string expected) => Assert.Equal(expected, Tree(input));

    [Theory]
    [InlineData("1&", 2, "Thiếu toán hạng")]
    [InlineData("(1|2", 0, "Thiếu dấu ')'")]
    [InlineData("1)", 1, "Thừa dấu ')'")]
    [InlineData("1 2", 2, "Thiếu toán tử")]
    [InlineData("sin", 0, "không dùng tên")]
    [InlineData("3!", 1, "không dùng được")]
    public void Errors(string input, int position, string keyword)
    {
        var ex = Assert.Throws<CalcException>(() => ProgrammerParser.Parse(input));
        Assert.Equal(position, ex.Position);
        Assert.Contains(keyword, ex.Message);
    }
}

public class IntegerEvaluatorTests
{
    private static long Eval(string s, WordSize size = WordSize.QWord, int radix = 10) =>
        ProgrammerCalculator.Evaluate(s, radix, size);

    // ---------- Đổi hệ cơ số ----------
    [Theory]
    [InlineData("FF", 16, 255)]
    [InlineData("777", 8, 511)]
    [InlineData("1011", 2, 11)]
    [InlineData("0x10+0b11+0o7", 10, 26)]
    [InlineData("7FFFFFFFFFFFFFFF", 16, long.MaxValue)]
    [InlineData("FFFFFFFFFFFFFFFF", 16, -1)]          // 64 bit toàn 1 = -1 (bù hai)
    public void ParsesRadix(string input, int radix, long expected) =>
        Assert.Equal(expected, Eval(input, WordSize.QWord, radix));

    [Theory]
    [InlineData(255, WordSize.QWord, "FF", "255", "377", "11111111")]
    [InlineData(-1, WordSize.Byte, "FF", "-1", "377", "11111111")]
    [InlineData(-1, WordSize.Word, "FFFF", "-1", "177777", "1111111111111111")]
    [InlineData(-2, WordSize.DWord, "FFFFFFFE", "-2", "37777777776", "11111111111111111111111111111110")]
    [InlineData(0, WordSize.QWord, "0", "0", "0", "0")]
    public void FormatsAllRadixes(long value, WordSize size, string hex, string dec, string oct, string bin)
    {
        Assert.Equal(hex, ProgrammerFormatter.Format(value, 16, size));
        Assert.Equal(dec, ProgrammerFormatter.Format(value, 10, size));
        Assert.Equal(oct, ProgrammerFormatter.Format(value, 8, size));
        Assert.Equal(bin, ProgrammerFormatter.Format(value, 2, size));
    }

    [Fact]
    public void Format_Grouping()
    {
        Assert.Equal("1234 ABCD", ProgrammerFormatter.Format(0x1234ABCD, 16, WordSize.QWord, group: true));
        Assert.Equal("1111 1111", ProgrammerFormatter.Format(255, 2, WordSize.QWord, group: true));
        Assert.Equal("-1 234 567", ProgrammerFormatter.Format(-1234567, 10, WordSize.QWord, group: true));
        Assert.Equal("377", ProgrammerFormatter.Format(255, 8, WordSize.QWord, group: true));
    }

    [Theory]
    [InlineData(-5, 10, "−5")]
    [InlineData(-5, 16, "FB")]
    [InlineData(42, 2, "101010")]
    public void FormatForInput_RoundTrips(long value, int radix, string expected)
    {
        string text = ProgrammerFormatter.FormatForInput(value, radix, WordSize.Byte);
        Assert.Equal(expected, text);
        Assert.Equal(value, Eval(text, WordSize.Byte, radix));
    }

    // ---------- Phép bit theo kích thước word ----------
    [Theory]
    [InlineData("12&10", 8)]
    [InlineData("12|10", 14)]
    [InlineData("12^10", 6)]
    [InlineData("1<<4", 16)]
    [InlineData("256>>4", 16)]
    [InlineData("-16>>2", -4)]        // dịch phải số học: giữ bit dấu
    [InlineData("1<<64", 0)]          // dịch quá số bit
    [InlineData("-1>>100", -1)]
    [InlineData("7/2", 3)]
    [InlineData("-7/2", -3)]          // chia nguyên làm tròn về 0
    [InlineData("-7%3", -1)]
    public void Bitwise_QWord(string expr, long expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("~0", WordSize.Byte, -1)]
    [InlineData("~0", WordSize.QWord, -1)]
    [InlineData("~1", WordSize.Word, -2)]
    [InlineData("127+1", WordSize.Byte, -128)]           // tràn quay vòng
    [InlineData("32767+1", WordSize.Word, -32768)]
    [InlineData("2147483647+1", WordSize.DWord, -2147483648)]
    [InlineData("1<<7", WordSize.Byte, -128)]
    [InlineData("1<<8", WordSize.Byte, 0)]
    [InlineData("1<<15", WordSize.Word, -32768)]
    [InlineData("1<<31", WordSize.DWord, int.MinValue)]
    [InlineData("1<<31", WordSize.QWord, 2147483648)]
    [InlineData("200*2", WordSize.Byte, -112)]           // 400 mod 256 = 144 -> -112
    [InlineData("-128/-1", WordSize.Byte, -128)]         // MIN / -1 tràn -> quay vòng, không ném lỗi
    [InlineData("-128>>1", WordSize.Byte, -64)]
    public void Bitwise_ByWordSize(string expr, WordSize size, long expected) =>
        Assert.Equal(expected, Eval(expr, size));

    [Theory]
    [InlineData("FF", WordSize.Byte, -1)]
    [InlineData("80", WordSize.Byte, -128)]
    [InlineData("FFFF", WordSize.Word, -1)]
    [InlineData("FF&0F", WordSize.Byte, 15)]
    [InlineData("F0^FF", WordSize.Byte, 15)]
    [InlineData("~F0", WordSize.Byte, 15)]
    public void Hex_ByWordSize(string expr, WordSize size, long expected) =>
        Assert.Equal(expected, Eval(expr, size, 16));

    [Theory]
    [InlineData("256", WordSize.Byte, 0)]
    [InlineData("100", WordSize.Byte, 0, 16)]
    [InlineData("10000", WordSize.Word, 0, 16)]
    [InlineData("1+65536", WordSize.Word, 2)]
    [InlineData("10000000000000000", WordSize.QWord, 0, 16)]   // 65 bit
    public void Literal_TooLargeForWord_IsOverflow(string expr, WordSize size, int position, int radix = 10)
    {
        var ex = Assert.Throws<CalcException>(() => Eval(expr, size, radix));
        Assert.Equal(position, ex.Position);
        Assert.Contains("Tràn số", ex.Message);
    }

    [Theory]
    [InlineData("5/0", 1)]
    [InlineData("5%(2-2)", 1)]
    public void DivideByZero(string expr, int position)
    {
        var ex = Assert.Throws<CalcException>(() => Eval(expr));
        Assert.Equal(position, ex.Position);
    }

    [Fact]
    public void NegativeShift_IsError() => Assert.Throws<CalcException>(() => Eval("1<<-1"));

    [Theory]
    [InlineData(300, WordSize.Byte, 44)]
    [InlineData(255, WordSize.Byte, -1)]
    [InlineData(65535, WordSize.Word, -1)]
    [InlineData(-1, WordSize.QWord, -1)]
    [InlineData(4294967296 + 5, WordSize.DWord, 5)]
    public void Normalize(long value, WordSize size, long expected) =>
        Assert.Equal(expected, WordSizes.Normalize(value, size));

    [Fact]
    public void WordSize_CyclesLikeWindows()
    {
        Assert.Equal(WordSize.DWord, WordSize.QWord.Next());
        Assert.Equal(WordSize.Word, WordSize.DWord.Next());
        Assert.Equal(WordSize.Byte, WordSize.Word.Next());
        Assert.Equal(WordSize.QWord, WordSize.Byte.Next());
    }
}

public class ProgrammerSessionTests
{
    private static void Type(ProgrammerSession s, params string[] keys)
    {
        foreach (var k in keys)
            s.Machine.Process(k switch
            {
                "=" => InputEvent.EqualsKey,
                "C" => InputEvent.Clear,
                "<" => InputEvent.Backspace,
                _ => InputEvent.FromText(k),
            });
    }

    [Fact]
    public void ShowsAllRadixesInParallel()
    {
        var s = new ProgrammerSession();
        Type(s, "2", "5", "5");
        Assert.Equal(
            [(16, "FF"), (10, "255"), (8, "377"), (2, "1111 1111")],
            s.AllRadixes());
    }

    [Fact]
    public void UsesSameStateMachineTable()
    {
        var s = new ProgrammerSession(radix: 16);
        Type(s, "F", "F", "&", "0", "F", "=");
        Assert.Equal(InputState.ShowingResult, s.Machine.State);
        Assert.Equal("F", s.Machine.Result);
        Type(s, "<<", "4", "=");                   // nối tiếp từ kết quả trước (không có tên Ans)
        Assert.Equal("F<<4 =", DisplayModel.From(s.Machine).TopText);
        Assert.Equal(0xF0, s.CurrentValue);
    }

    [Fact]
    public void SetRadix_RewritesCurrentValue()
    {
        var s = new ProgrammerSession();
        Type(s, "2", "5", "5", "=");
        s.SetRadix(16);
        Assert.Equal(16, s.Radix);
        Assert.Equal("FF", s.Machine.Expression);
        Assert.Equal(InputState.ShowingResult, s.Machine.State);
        Assert.Equal(255, s.CurrentValue);

        s.SetRadix(2);
        Assert.Equal("11111111", s.Machine.Expression);
    }

    [Fact]
    public void SetRadix_WhileTypingValid_KeepsTyping()
    {
        var s = new ProgrammerSession();
        Type(s, "1", "0", "+", "6");
        s.SetRadix(16);
        Assert.Equal(InputState.Typing, s.Machine.State);
        Assert.Equal("10", s.Machine.Expression);     // 16 thập phân = 10 hex
    }

    [Fact]
    public void SetRadix_WhileTypingIncomplete_Clears()
    {
        var s = new ProgrammerSession();
        Type(s, "1", "0", "+");
        s.SetRadix(2);
        Assert.Equal(InputState.Empty, s.Machine.State);
    }

    [Fact]
    public void SetWordSize_TruncatesValue()
    {
        var s = new ProgrammerSession();
        Type(s, "3", "0", "0", "=");
        s.SetWordSize(WordSize.Byte);
        Assert.Equal(44, s.CurrentValue);
        Assert.Equal("44", s.Machine.Result);

        Type(s, "C", "2", "5", "5");
        Assert.Equal(-1, s.CurrentValue);              // BYTE: 255 = FF = -1
        Assert.Equal("FF", s.AllRadixes()[0].Text);
    }

    [Fact]
    public void NegativeValue_ContinuesCorrectly()
    {
        var s = new ProgrammerSession(size: WordSize.Byte);
        Type(s, "0", "−", "5", "=", "+", "1", "=");
        Assert.Equal(-4, s.CurrentValue);
        s.SetRadix(16);
        Assert.Equal("FC", s.Machine.Expression);
    }

    [Theory]
    [InlineData(2, "01", "23456789ABCDEF")]
    [InlineData(8, "01234567", "89ABCDEF")]
    [InlineData(10, "0123456789", "ABCDEF")]
    [InlineData(16, "0123456789ABCDEFabcdef", "GXZ")]
    public void IsDigitAllowed(int radix, string allowed, string rejected)
    {
        var s = new ProgrammerSession(radix);
        Assert.All(allowed, c => Assert.True(s.IsDigitAllowed(c), $"{c}"));
        Assert.All(rejected, c => Assert.False(s.IsDigitAllowed(c), $"{c}"));
    }

    [Fact]
    public void Error_ShowsPosition()
    {
        var s = new ProgrammerSession();
        Type(s, "8", "/", "0", "=");
        var d = DisplayModel.From(s.Machine);
        Assert.True(d.IsError);
        Assert.Equal("/", d.TopMark);
    }
}
