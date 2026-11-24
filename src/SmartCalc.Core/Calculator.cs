using SmartCalc.Core.Evaluation;
using SmartCalc.Core.Parsing;

namespace SmartCalc.Core;

/// <summary>Mặt tiền (facade): chuỗi -> Lexer (DFA) -> Parser (đệ quy xuống) -> AST -> Evaluator.</summary>
public static class Calculator
{
    public static double Evaluate(string expression, EvalContext? context = null)
    {
        context ??= new EvalContext();
        var ast = Parser.Parse(expression, allowVariable: context.X.HasValue);
        return new Evaluator(context).Evaluate(ast);
    }

    public static string EvaluateToString(string expression, EvalContext? context = null) =>
        NumberFormatter.Format(Evaluate(expression, context));
}
