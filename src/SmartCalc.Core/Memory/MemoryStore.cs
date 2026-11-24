using SmartCalc.Core.Evaluation;

namespace SmartCalc.Core.Memory;

/// <summary>
/// Bộ nhớ dạng ngăn xếp (giống máy tính Windows): phần tử [0] là giá trị "hiện hành".
///   MS : đẩy giá trị mới lên đầu      MR : đọc phần tử đầu
///   M+ : cộng vào phần tử đầu         M− : trừ khỏi phần tử đầu   (rỗng thì tạo mới)
///   MC : xoá hết
/// Cộng/trừ dùng decimal như Evaluator nên 0.1 + 0.2 = 0.3.
/// </summary>
public sealed class MemoryStore
{
    private readonly List<double> _values = [];

    public IReadOnlyList<double> Values => _values;
    public bool HasValue => _values.Count > 0;

    public event Action? Changed;

    public void Store(double value)
    {
        _values.Insert(0, value);
        Changed?.Invoke();
    }

    public double? Recall() => HasValue ? _values[0] : null;

    public void Add(double value) => Combine(0, value, +1);
    public void Subtract(double value) => Combine(0, value, -1);

    /// <summary>M+ / M− cho một phần tử bất kỳ (nút trên từng dòng của tab Bộ nhớ).</summary>
    public void AddAt(int index, double value) => Combine(index, value, +1);
    public void SubtractAt(int index, double value) => Combine(index, value, -1);

    public void RemoveAt(int index)
    {
        _values.RemoveAt(index);
        Changed?.Invoke();
    }

    public void Clear()
    {
        _values.Clear();
        Changed?.Invoke();
    }

    private void Combine(int index, double value, int sign)
    {
        if (!HasValue)
        {
            _values.Add(sign * value);
        }
        else
        {
            double current = _values[index];
            _values[index] = sign > 0
                ? Evaluator.Exact(current, value, (a, b) => a + b, current + value)
                : Evaluator.Exact(current, value, (a, b) => a - b, current - value);
        }
        Changed?.Invoke();
    }
}
