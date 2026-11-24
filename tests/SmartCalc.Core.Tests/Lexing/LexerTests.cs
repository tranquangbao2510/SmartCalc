using SmartCalc.Core.Errors;
using SmartCalc.Core.Lexing;

namespace SmartCalc.Core.Tests.Lexing;

public class LexerTests
{
    private static List<Token> Lex(string input, bool programmer = false) =>
        Lexer.Tokenize(input, programmer).ToList();

    private static Token Single(string input, bool programmer = false)
    {
        var tokens = Lex(input, programmer);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenKind.End, tokens[1].Kind);
        return tokens[0];
    }

    // ---------- Số nguyên ----------
    [Theory]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("42")]
    [InlineData("007")]
    [InlineData("1234567890")]
    public void Integer_IsSingleNumberToken(string input)
    {
        var t = Single(input);
        Assert.Equal(TokenKind.Number, t.Kind);
        Assert.Equal(input, t.Text);
        Assert.Equal(0, t.Position);
    }

    // ---------- Số thực ----------
    [Theory]
    [InlineData("3.14")]
    [InlineData("0.5")]
    [InlineData(".5")]
    [InlineData("12.")]
    [InlineData("100.001")]
    public void Real_IsSingleNumberToken(string input)
    {
        var t = Single(input);
        Assert.Equal(TokenKind.Number, t.Kind);
        Assert.Equal(input, t.Text);
    }

    // ---------- Số mũ ----------
    [Theory]
    [InlineData("1.5e-3")]
    [InlineData("1.5E+3")]
    [InlineData("2e10")]
    [InlineData(".5e2")]
    [InlineData("1.e5")]
    public void Scientific_IsSingleNumberToken(string input)
    {
        var t = Single(input);
        Assert.Equal(TokenKind.Number, t.Kind);
        Assert.Equal(input, t.Text);
    }

    [Fact]
    public void Scientific_InsideExpression()
    {
        var tokens = Lex("1.5e-3*2");
        Assert.Equal(
            [TokenKind.Number, TokenKind.Star, TokenKind.Number, TokenKind.End],
            tokens.Select(t => t.Kind));
        Assert.Equal("1.5e-3", tokens[0].Text);
        Assert.Equal(6, tokens[1].Position);
    }

    [Fact]
    public void IncompleteExponent_BacksOffToLongestMatch()
    {
        // "2e" không phải số mũ hoàn chỉnh -> maximal munch lùi về "2", rồi "e" là định danh (hằng e)
        var tokens = Lex("2e");
        Assert.Equal([TokenKind.Number, TokenKind.Identifier, TokenKind.End], tokens.Select(t => t.Kind));
        Assert.Equal("2", tokens[0].Text);
        Assert.Equal("e", tokens[1].Text);

        tokens = Lex("2e-x");
        Assert.Equal(
            [TokenKind.Number, TokenKind.Identifier, TokenKind.Minus, TokenKind.Identifier, TokenKind.End],
            tokens.Select(t => t.Kind));
    }

    // ---------- Định danh ----------
    [Theory]
    [InlineData("sin")]
    [InlineData("cos")]
    [InlineData("tan")]
    [InlineData("asin")]
    [InlineData("acos")]
    [InlineData("atan")]
    [InlineData("ln")]
    [InlineData("log")]
    [InlineData("sqrt")]
    [InlineData("abs")]
    [InlineData("fact")]
    [InlineData("pi")]
    [InlineData("e")]
    [InlineData("Ans")]
    [InlineData("x")]
    public void Identifier_IsSingleToken(string input)
    {
        var t = Single(input);
        Assert.Equal(TokenKind.Identifier, t.Kind);
        Assert.Equal(input, t.Text);
    }

    [Fact]
    public void ImplicitMultiplication_NumberThenIdentifier()
    {
        var tokens = Lex("2pi");
        Assert.Equal([TokenKind.Number, TokenKind.Identifier, TokenKind.End], tokens.Select(t => t.Kind));
        Assert.Equal("pi", tokens[1].Text);
        Assert.Equal(1, tokens[1].Position);
    }

    [Fact]
    public void FunctionCall_Tokens()
    {
        var tokens = Lex("sqrt(16)");
        Assert.Equal(
            [TokenKind.Identifier, TokenKind.LParen, TokenKind.Number, TokenKind.RParen, TokenKind.End],
            tokens.Select(t => t.Kind));
    }

    // ---------- Toán tử ----------
    [Theory]
    [InlineData("+", TokenKind.Plus)]
    [InlineData("-", TokenKind.Minus)]
    [InlineData("*", TokenKind.Star)]
    [InlineData("/", TokenKind.Slash)]
    [InlineData("^", TokenKind.Caret)]
    [InlineData("%", TokenKind.Percent)]
    [InlineData("!", TokenKind.Bang)]
    [InlineData("(", TokenKind.LParen)]
    [InlineData(")", TokenKind.RParen)]
    [InlineData(",", TokenKind.Comma)]
    [InlineData("×", TokenKind.Star)]
    [InlineData("÷", TokenKind.Slash)]
    [InlineData("−", TokenKind.Minus)]
    public void Operator_IsRecognized(string input, TokenKind kind)
    {
        Assert.Equal(kind, Single(input).Kind);
    }

    [Fact]
    public void FullExpression_KindsAndPositions()
    {
        var tokens = Lex("3+4*(2-1)^2!");
        Assert.Equal(
            [
                TokenKind.Number, TokenKind.Plus, TokenKind.Number, TokenKind.Star, TokenKind.LParen,
                TokenKind.Number, TokenKind.Minus, TokenKind.Number, TokenKind.RParen, TokenKind.Caret,
                TokenKind.Number, TokenKind.Bang, TokenKind.End,
            ],
            tokens.Select(t => t.Kind));
        Assert.Equal(Enumerable.Range(0, 13), tokens.Select(t => t.Position));
    }

    // ---------- Khoảng trắng ----------
    [Fact]
    public void Whitespace_IsSkipped_PositionsKept()
    {
        var tokens = Lex("  12 +\t sin ( x )  ");
        Assert.Equal(
            [
                TokenKind.Number, TokenKind.Plus, TokenKind.Identifier, TokenKind.LParen,
                TokenKind.Identifier, TokenKind.RParen, TokenKind.End,
            ],
            tokens.Select(t => t.Kind));
        Assert.Equal([2, 5, 8, 12, 14, 16, 19], tokens.Select(t => t.Position));
    }

    [Fact]
    public void Whitespace_SeparatesNumbers()
    {
        var tokens = Lex("1 2");
        Assert.Equal([TokenKind.Number, TokenKind.Number, TokenKind.End], tokens.Select(t => t.Kind));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrBlank_OnlyEnd(string input)
    {
        var tokens = Lex(input);
        Assert.Single(tokens);
        Assert.Equal(TokenKind.End, tokens[0].Kind);
        Assert.Equal(input.Length, tokens[0].Position);
    }

    // ---------- Ký tự lạ / lỗi ----------
    [Theory]
    [InlineData("2 # 3", 2, '#')]
    [InlineData("1+2$", 3, '$')]
    [InlineData("@", 0, '@')]
    [InlineData("sin(x) ? 1", 7, '?')]
    [InlineData("2 & 3", 2, '&')]   // toán tử bit chỉ hợp lệ ở chế độ lập trình
    [InlineData("5 < 3", 2, '<')]
    public void InvalidCharacter_ReportsPosition(string input, int position, char c)
    {
        var ex = Assert.Throws<CalcException>(() => Lexer.Tokenize(input));
        Assert.Equal(position, ex.Position);
        Assert.Contains($"'{c}'", ex.Message);
        Assert.Contains("không hợp lệ", ex.Message);
    }

    [Fact]
    public void LoneDot_IsError()
    {
        var ex = Assert.Throws<CalcException>(() => Lexer.Tokenize("1 + . 2"));
        Assert.Equal(4, ex.Position);
    }

    [Theory]
    [InlineData("1.2.3", 3)]
    [InlineData("1..2", 2)]
    public void NumberWithExtraDot_IsError(string input, int position)
    {
        var ex = Assert.Throws<CalcException>(() => Lexer.Tokenize(input));
        Assert.Equal(position, ex.Position);
    }

    [Fact]
    public void ProgrammerPrefix_NotAcceptedInNormalMode()
    {
        // Ở chế độ thường "0x" = 0 nhân ngầm x
        var tokens = Lex("0x");
        Assert.Equal([TokenKind.Number, TokenKind.Identifier, TokenKind.End], tokens.Select(t => t.Kind));
    }

    // ---------- Chế độ lập trình ----------
    [Theory]
    [InlineData("0xFF")]
    [InlineData("0x1aBe")]
    [InlineData("0b1011")]
    [InlineData("0o17")]
    public void Programmer_RadixNumbers(string input)
    {
        var t = Single(input, programmer: true);
        Assert.Equal(TokenKind.Number, t.Kind);
        Assert.Equal(input, t.Text);
    }

    [Fact]
    public void Programmer_BitOperators()
    {
        var tokens = Lex("~a & b | c << 2 >> 1", programmer: true);
        Assert.Equal(
            [
                TokenKind.Tilde, TokenKind.Identifier, TokenKind.Ampersand, TokenKind.Identifier, TokenKind.Pipe,
                TokenKind.Identifier, TokenKind.ShiftLeft, TokenKind.Number, TokenKind.ShiftRight, TokenKind.Number,
                TokenKind.End,
            ],
            tokens.Select(t => t.Kind));
    }

    [Theory]
    [InlineData("0b102", 4)]
    [InlineData("0o18", 3)]
    [InlineData("0xFG", 3)]
    public void Programmer_InvalidDigit_ReportsPosition(string input, int position)
    {
        var ex = Assert.Throws<CalcException>(() => Lexer.Tokenize(input, programmerMode: true));
        Assert.Equal(position, ex.Position);
    }

    [Fact]
    public void Programmer_LoneLess_IsIncompleteSymbol()
    {
        var ex = Assert.Throws<CalcException>(() => Lexer.Tokenize("1 < 2", programmerMode: true));
        Assert.Equal(2, ex.Position);
    }

    // ---------- Kiểm tra trực tiếp bảng DFA ----------
    [Fact]
    public void Dfa_RecognizesScientificNumber_StepByStep()
    {
        var dfa = new LexerDfa();
        var s = DfaState.Start;
        var path = new List<DfaState>();
        foreach (var c in "1.5e-3")
        {
            s = dfa.Next(s, LexerDfa.Classify(c));
            path.Add(s);
        }
        Assert.Equal(
            [DfaState.Int, DfaState.IntDot, DfaState.Frac, DfaState.ExpMark, DfaState.ExpSign, DfaState.Exp],
            path);
        Assert.True(dfa.IsAccepting(DfaState.Exp));
        Assert.False(dfa.IsAccepting(DfaState.ExpMark));
        Assert.False(dfa.IsAccepting(DfaState.ExpSign));
    }
}

public class LexerSymbolAliasTests
{
    [Fact]
    public void PiAndSqrtSymbols_AreIdentifiers()
    {
        var tokens = Lexer.Tokenize("2π+√(9)");
        Assert.Equal(
            [TokenKind.Number, TokenKind.Identifier, TokenKind.Plus, TokenKind.Identifier,
             TokenKind.LParen, TokenKind.Number, TokenKind.RParen, TokenKind.End],
            tokens.Select(t => t.Kind));
        Assert.Equal(2 * Math.PI + 3, Calculator.Evaluate("2π+√(9)"), 12);
    }
}

public class LexerSymbolTokenTests
{
    [Theory]
    [InlineData("πAns", new[] { "π", "Ans" })]
    [InlineData("ππ", new[] { "π", "π" })]
    [InlineData("xπ", new[] { "x", "π" })]
    [InlineData("√√", new[] { "√", "√" })]
    public void Symbols_AreAlwaysSeparateTokens(string input, string[] texts)
    {
        var tokens = Lexer.Tokenize(input).Where(t => t.Kind != TokenKind.End).ToList();
        Assert.All(tokens, t => Assert.Equal(TokenKind.Identifier, t.Kind));
        Assert.Equal(texts, tokens.Select(t => t.Text));
    }

    [Fact]
    public void PiTimesAns_Evaluates()
    {
        Assert.Equal(Math.PI * 2, Calculator.Evaluate("πAns", new(Ans: 2)), 12);
    }
}
