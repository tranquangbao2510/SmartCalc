using System.Text.RegularExpressions;
using SmartCalc.Core.Errors;
using SmartCalc.Core.Lexing;

namespace SmartCalc.Core.Tests.Lexing;

public class NumberDifferentialTests
{
    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 2)]
    [InlineData(true, 8)]
    [InlineData(true, 10)]
    [InlineData(true, 16)]
    public void NumberDfa_MatchesRegex_OnTwentyThousandRandomStrings(bool programmer, int radix)
    {
        string pattern = !programmer
            ? @"(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+\-−]?[0-9]+)?"
            : radix == 16
                ? @"(?:0[xX][0-9a-fA-F]+|0[oO][0-7]+|[0-9a-fA-F]+)"
                : @"(?:0[xX][0-9a-fA-F]+|0[bB][01]+|0[oO][0-7]+|" +
                    (radix == 2 ? "[01]+" : radix == 8 ? "[0-7]+" : "[0-9]+") + ")";
        var regex = new Regex(@"\A" + pattern + @"\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var dfa = new LexerDfa(programmer, radix);
        var random = new Random(20261124 + radix + (programmer ? 100 : 0));
        const string alphabet = "0123456789abcdefABCDEFxXbBoOeEgG.+-−";
        int accepted = 0, rejected = 0;
        for (int sample = 0; sample < 20_000; sample++)
        {
            string text = new(Enumerable.Range(0, random.Next(0, 25)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            var state = DfaState.Start;
            foreach (char c in text) state = dfa.Next(state, LexerDfa.Classify(c));
            bool number = dfa.IsAccepting(state) && !dfa.IsSkip(state) && dfa.TokenKindOf(state) == TokenKind.Number;
            bool expected = regex.IsMatch(text);
            Assert.True(expected == number, $"DFA/Regex: '{text}', programmer={programmer}, radix={radix}, state={state}");
            bool singleNumber;
            try
            {
                var tokens = Lexer.Tokenize(text, programmer, radix);
                singleNumber = tokens.Count == 2 && tokens[0].Kind == TokenKind.Number && tokens[0].Text == text;
            }
            catch (CalcException) { singleNumber = false; }
            Assert.Equal(expected, singleNumber);
            if (number) accepted++; else rejected++;
        }
        Assert.True(accepted > 10);
        Assert.True(rejected > 10_000);
    }

    [Theory]
    [InlineData("1.2.3", false, 10, 3)]
    [InlineData("1..2", false, 10, 2)]
    [InlineData("1e2.3", false, 10, 3)]
    [InlineData("0b102", true, 10, 4)]
    [InlineData("0o18", true, 10, 3)]
    [InlineData("0xFG", true, 10, 3)]
    [InlineData("0x", true, 10, 1)]
    [InlineData("0xG", true, 10, 1)]
    [InlineData("0b", true, 2, 1)]
    [InlineData("0o", true, 8, 1)]
    [InlineData("102", true, 2, 2)]
    [InlineData("78", true, 8, 1)]
    [InlineData("1A", true, 10, 1)]
    [InlineData("FZ", true, 16, 1)]
    [InlineData("0xFFsin", true, 16, 4)]
    [InlineData("0b10pi", true, 2, 4)]
    public void BadNumbers_KeepExactMessageAndPosition(string text, bool programmer, int radix, int position)
    {
        const string prefix = "  + ";
        var ex = Assert.Throws<CalcException>(() => Lexer.Tokenize(prefix + text, programmer, radix));
        int expected = prefix.Length + position;
        Assert.Equal(expected, ex.Position);
        Assert.Equal($"Số không hợp lệ: ký tự '{text[position]}' tại vị trí {expected}", ex.Message);
    }

    [Theory]
    [InlineData("2pi", 2 * Math.PI)]
    [InlineData("3(4+1)", 15)]
    [InlineData("2sin(30)", 1)]
    public void NormalMode_KeepsImplicitMultiplication(string text, double expected) =>
        Assert.Equal(expected, Calculator.Evaluate(text), 12);

    [Fact]
    public void BadNumber_IsLongerAcceptingToken()
    {
        var dfa = new LexerDfa();
        var state = DfaState.Start;
        foreach (char c in "1.2.3abc") state = dfa.Next(state, LexerDfa.Classify(c));
        Assert.Equal(DfaState.BadNumber, state);
        Assert.Equal(TokenKind.Error, dfa.TokenKindOf(state));
    }
}
