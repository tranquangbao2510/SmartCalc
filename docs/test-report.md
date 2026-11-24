# Báo cáo kiểm thử SmartCalc

Project: `tests/SmartCalc.Core.Tests` (xUnit, .NET 10). Số liệu lấy từ kết quả chạy test thực tế;
mỗi bộ dữ liệu Theory/MemberData được tính là một test case.

## Kết quả

| Chỉ số | Giá trị |
|---|---:|
| Test ban đầu | 679 |
| Nền sau mục 1 | 620 |
| Test bổ sung tại mục 6 | 5.648 |
| Test bổ sung bố cục biểu thức | 88 |
| Tổng cuối | **6.356** |
| Đạt / lỗi / bỏ qua | **6.356 / 0 / 0** |
| Build | **0 warning, 0 lỗi** |

Sau mỗi mục 1–5 đều chạy `dotnet build` (0 warning) và `dotnet test` (620/620 đạt).
Sau mục 6 và mục 7: build 0 warning, 6.268/6.268 test đạt.
Sau thay đổi trình bày biểu thức: build 0 warning, 6.356/6.356 test đạt.

Chạy lại:

```powershell
dotnet build SmartCalc.sln
dotnet test SmartCalc.sln --logger "trx;LogFileName=verification.trx"
```

TRX ở `tests/SmartCalc.Core.Tests/TestResults/verification.trx` (đầu ra sinh tự động, được gitignore).

## Số test theo nhóm

Các nhóm dưới đây không trùng nhau; test dùng chung số được tách riêng khỏi Lexer/Thống kê/Đổi đơn vị.

| Nhóm | Số case | Lớp test / nội dung |
|---|---:|---|
| Lexer, bảng δ và differential | **5.618** | LexerTests 72, LexerSymbolAliasTests 1, LexerSymbolTokenTests 5, LexerTableTests 5.515, NumberDifferentialTests 25 |
| Đọc số dùng chung | **31** | SharedNumberReaderTests: dấu đầu, số mũ, Unicode, biên decimal, làm tròn/hụt số, lỗi và vị trí, dấu phân tách, chuẩn hoá Đổi đơn vị |
| Parser | **38** | ParserTests: AST, ưu tiên, kết hợp, nhân ngầm, giai thừa, vị trí |
| Evaluator và lỗi | **128** | EvaluatorTests 73, ErrorTests 55: decimal/double, DEG/RAD, hàm Builtins, miền xác định, tràn số, lỗi tiếng Việt |
| Máy nhập liệu và bố cục hiển thị | **233** | InputStateMachineTests 46, InputTableTests 73, ExpressionEditorTests 21, DisplayModelTests 5, ExpressionLayoutTests 88 |
| Lập trình | **101** | ProgrammerLexerTests 15, ProgrammerParserTests 14, IntegerEvaluatorTests 60, ProgrammerSessionTests 12 |
| Đổi đơn vị | **55** | UnitConverterTests: 8 nhóm, affine, độ không tuyệt đối, đọc/hiển thị số |
| Đồ thị | **52** | PlotFunctionTests 31, FunctionSamplerTests 9, ViewportTests 12 |
| Thống kê | **29** | StatisticsTests: tổng, trung bình, trung vị, mode, phương sai, độ lệch chuẩn |
| Lưu trữ | **18** | MemoryStoreTests 5, HistoryStoreTests 5, HistoryQueueTests 4, AppSettingsTests 4 |
| Ca biên | **53** | EmptyInputTests 7, DeepNestingTests 15, ExtremeNumberTests 25, CorruptFileTests 3, CultureIndependenceTests 3 |
| **Tổng** | **6.356** | |

## Phủ bảng và differential

- **Lexer:** 38 trạng thái × 29 lớp ký tự × 5 cấu hình = **5.510 test từng ô δ**.
  Kết quả mong đợi lấy từ đặc tả cạnh riêng trong test, không đọc ngược bảng Core.
  Bao gồm Dead, BadNumber và các trạng thái không tới được ở từng cấu hình.
  Thêm 5 test kiểm toàn bộ tập kết thúc, loại token và trạng thái bỏ qua.
- **Differential:** 5 test × **20.000 chuỗi ngẫu nhiên** = **100.000 chuỗi** trên bảng chữ cái
  số/chữ hex/tiền tố/số mũ/dấu chấm/dấu cộng trừ; độ dài 0–24, seed cố định.
  Oracle là .NET Regex neo toàn chuỗi, độc lập cho chế độ thường và lập trình 2/8/10/16.
  Mỗi chuỗi đối chiếu cả DFA trực tiếp và kết quả Lexer có đúng một Number.
  Regex chỉ dùng trong project test.
- **Lỗi và nhân ngầm:** 16 test giữ nguyên chính xác thông báo tiếng Việt và Position, kể cả
  số sai sau tiền tố, tiền tố thiếu chữ số và offset trong biểu thức; 3 test nhân ngầm;
  1 test xác nhận BadNumber là kết thúc Error dài hơn Number.
- **Máy nhập liệu:** 40 test kiểm cặp giá trị δ/λ tại mọi ô, tức **80 ô của hai bảng**;
  32 kịch bản phím/nạp từ cả bốn trạng thái; 1 test số lần Evaluate/Commit/Evaluated và lỗi.
  Các kịch bản cũ vẫn chạy; test đủ ô dựa trên enum phát hiện thiếu cột/hàng khi mở rộng.
- **History Queue:** tràn nhiều lần, giữ đúng 100 mục, thứ tự UI/JSON, đọc file cũ quá dài,
  xoá giữa hàng, xoá mục bằng nhau, sự kiện Changed và Clear.
- **Bố cục biểu thức:** 30 ví dụ số mũ/nhóm/hàm/abs, 30 test kiểm ánh xạ mọi vị trí gốc
  (kể cả cuối chuỗi), 18 vị trí lỗi cụ thể, 4 fallback Lexer, 3 chế độ text thô,
  3 test cỡ chữ tối thiểu, chuỗi lồng quá sâu và giữ nguyên biểu thức/sự kiện lịch sử.

## Phạm vi xác minh

`TreatWarningsAsErrors` bật qua `Directory.Build.props`. Build gồm cả Core, WPF App và test.
Kiểm thử tự động xác minh logic Core. Phần hiển thị mới còn được dựng ảnh bằng WPF và kiểm tra
binding màn hình/lịch sử/Lập trình, copy/paste và thay theme trong chương trình kiểm tra tạm;
chưa chạy kiểm thử thao tác chuột/bàn phím trên cửa sổ ứng dụng thật.
