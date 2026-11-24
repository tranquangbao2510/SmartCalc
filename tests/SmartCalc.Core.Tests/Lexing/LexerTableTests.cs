using SmartCalc.Core.Lexing;

namespace SmartCalc.Core.Tests.Lexing;

public class LexerTableTests
{
    // Đặc tả cạnh độc lập với LexerDfa; mọi ô không liệt kê có đích Dead.
    private const string Common = """
        Start LetterE,LetterB,LetterO,LetterX,HexLetter,Letter Ident
        Ident LetterE,LetterB,LetterO,LetterX,HexLetter,Letter Ident
        Start Symbol SymbolIdent
        Start Space Space
        Space Space Space
        Start Plus Plus
        Start Minus Minus
        Start Star Star
        Start Slash Slash
        Start Caret Caret
        Start Percent Percent
        Start Bang Bang
        Start LParen LParen
        Start RParen RParen
        Start Comma Comma
        BadNumber Zero,One,Oct,Dec,Dot,LetterE,LetterB,LetterO,LetterX,HexLetter,Letter BadNumber
        """;

    private const string Decimal = """
        Start Zero Zero
        Start One,Oct,Dec Int
        Start Dot LeadDot
        Zero Zero,One,Oct,Dec Int
        Zero Dot IntDot
        Zero LetterE ExpMark
        Int Zero,One,Oct,Dec Int
        Int Dot IntDot
        Int LetterE ExpMark
        IntDot Zero,One,Oct,Dec Frac
        IntDot LetterE ExpMark
        IntDot Dot BadNumber
        LeadDot Zero,One,Oct,Dec Frac
        Frac Zero,One,Oct,Dec Frac
        Frac LetterE ExpMark
        Frac Dot BadNumber
        ExpMark Plus,Minus ExpSign
        ExpMark Zero,One,Oct,Dec Exp
        ExpSign Zero,One,Oct,Dec Exp
        Exp Zero,One,Oct,Dec Exp
        Exp Dot BadNumber
        """;

    private const string Programmer = """
        Zero Zero,One,Oct,Dec,Dot,LetterE,LetterB,LetterO,LetterX,HexLetter,Letter BadNumber
        RadixInt Zero,One,Oct,Dec,Dot,LetterE,LetterB,LetterO,LetterX,HexLetter,Letter BadNumber
        HexPrefix Zero,One,Oct,Dec,Dot,LetterE,LetterB,LetterO,LetterX,HexLetter,Letter BadNumber
        Hex Zero,One,Oct,Dec,Dot,LetterE,LetterB,LetterO,LetterX,HexLetter,Letter BadNumber
        BinPrefix Zero,One,Oct,Dec,Dot,LetterE,LetterB,LetterO,LetterX,HexLetter,Letter BadNumber
        Bin Zero,One,Oct,Dec,Dot,LetterE,LetterB,LetterO,LetterX,HexLetter,Letter BadNumber
        OctPrefix Zero,One,Oct,Dec,Dot,LetterE,LetterB,LetterO,LetterX,HexLetter,Letter BadNumber
        OctNum Zero,One,Oct,Dec,Dot,LetterE,LetterB,LetterO,LetterX,HexLetter,Letter BadNumber
        Start Zero Zero
        Zero LetterX HexPrefix
        Zero LetterO OctPrefix
        HexPrefix Zero,One,Oct,Dec,LetterE,LetterB,HexLetter Hex
        Hex Zero,One,Oct,Dec,LetterE,LetterB,HexLetter Hex
        OctPrefix Zero,One,Oct OctNum
        OctNum Zero,One,Oct OctNum
        Start Amp Amp
        Start Pipe Pipe
        Start Tilde Tilde
        Start Less Less
        Start Greater Greater
        Less Less ShiftLeft
        Greater Greater ShiftRight
        """;

    private static Dictionary<(DfaState, CharClass), DfaState> Specification(bool programmer, int radix)
    {
        var expected = new Dictionary<(DfaState, CharClass), DfaState>();
        void Read(string rows)
        {
            foreach (string row in rows.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = row.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (string cls in parts[1].Split(','))
                    expected[(Enum.Parse<DfaState>(parts[0]), Enum.Parse<CharClass>(cls))] = Enum.Parse<DfaState>(parts[2]);
            }
        }
        Read(Common);
        if (!programmer) Read(Decimal);
        else
        {
            Read(Programmer);
            string digits = radix switch
            {
                2 => "Zero,One", 8 => "Zero,One,Oct", 10 => "Zero,One,Oct,Dec",
                16 => "Zero,One,Oct,Dec,LetterE,LetterB,HexLetter",
                _ => throw new ArgumentOutOfRangeException(nameof(radix)),
            };
            Read($"Start {digits} RadixInt\nStart Zero Zero\nZero {digits} RadixInt\nRadixInt {digits} RadixInt\nZero LetterX HexPrefix\nZero LetterO OctPrefix");
            if (radix != 16)
                Read("Zero LetterB BinPrefix\nBinPrefix Zero,One Bin\nBin Zero,One Bin");
        }
        return expected;
    }

    public static IEnumerable<object[]> Cells()
    {
        foreach (var (programmer, radix) in new[] { (false, 10), (true, 2), (true, 8), (true, 10), (true, 16) })
        {
            var expected = Specification(programmer, radix);
            foreach (var state in Enum.GetValues<DfaState>())
                foreach (var cls in Enum.GetValues<CharClass>())
                    yield return [programmer, radix, state, cls, expected.GetValueOrDefault((state, cls), DfaState.Dead)];
        }
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void EveryCell_MatchesSpecification(bool programmer, int radix, DfaState state, CharClass cls, DfaState expected)
    {
        Assert.Equal(expected, new LexerDfa(programmer, radix).Next(state, cls));
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 2)]
    [InlineData(true, 8)]
    [InlineData(true, 10)]
    [InlineData(true, 16)]
    public void AcceptingStates_MatchSpecification(bool programmer, int radix)
    {
        var dfa = new LexerDfa(programmer, radix);
        var numbers = programmer
            ? new[] { DfaState.Zero, DfaState.RadixInt, DfaState.Hex, DfaState.Bin, DfaState.OctNum }
            : new[] { DfaState.Zero, DfaState.Int, DfaState.IntDot, DfaState.Frac, DfaState.Exp };
        var errors = programmer
            ? new[] { DfaState.BadNumber, DfaState.HexPrefix, DfaState.BinPrefix, DfaState.OctPrefix }
            : new[] { DfaState.BadNumber };
        var singles = new Dictionary<DfaState, TokenKind>
        {
            [DfaState.Ident] = TokenKind.Identifier, [DfaState.SymbolIdent] = TokenKind.Identifier,
            [DfaState.Plus] = TokenKind.Plus, [DfaState.Minus] = TokenKind.Minus,
            [DfaState.Star] = TokenKind.Star, [DfaState.Slash] = TokenKind.Slash,
            [DfaState.Caret] = TokenKind.Caret, [DfaState.Percent] = TokenKind.Percent,
            [DfaState.Bang] = TokenKind.Bang, [DfaState.LParen] = TokenKind.LParen,
            [DfaState.RParen] = TokenKind.RParen, [DfaState.Comma] = TokenKind.Comma,
        };
        if (programmer)
        {
            singles[DfaState.Amp] = TokenKind.Ampersand;
            singles[DfaState.Pipe] = TokenKind.Pipe;
            singles[DfaState.Tilde] = TokenKind.Tilde;
            singles[DfaState.ShiftLeft] = TokenKind.ShiftLeft;
            singles[DfaState.ShiftRight] = TokenKind.ShiftRight;
        }
        foreach (var state in Enum.GetValues<DfaState>())
        {
            bool skip = state == DfaState.Space;
            Assert.Equal(skip, dfa.IsSkip(state));
            bool accepting = skip || numbers.Contains(state) || errors.Contains(state) || singles.ContainsKey(state);
            Assert.Equal(accepting, dfa.IsAccepting(state));
            if (accepting && !skip)
                Assert.Equal(numbers.Contains(state) ? TokenKind.Number : errors.Contains(state) ? TokenKind.Error : singles[state], dfa.TokenKindOf(state));
        }
    }
}
