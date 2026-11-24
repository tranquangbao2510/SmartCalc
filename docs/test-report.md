# Báo cáo kiểm thử SmartCalc

Dự án test: `tests/SmartCalc.Core.Tests` (xUnit, .NET 10). Toàn bộ logic nằm trong `SmartCalc.Core` nên được kiểm thử trực tiếp, không cần giao diện.

## Tổng quan

| Chỉ số | Giá trị |
|---|---|
| Tổng số test case | **679** |
| Kết quả | **679 đạt, 0 lỗi, 0 bỏ qua** |
| Phương thức test | 222 (141 `[Fact]` + 81 `[Theory]`, mỗi bộ dữ liệu của `[Theory]` là một case) |
| Thời gian chạy | ~1 giây |
| Build | 0 warning, 0 lỗi (Debug và Release; `TreatWarningsAsErrors` bật cho mọi project qua `Directory.Build.props`) |

Chạy lại: `dotnet test SmartCalc.sln`. Đếm lại theo lớp: `dotnet test --list-tests`.

## Các nhóm test

| # | Nhóm | Lớp test (file) | Số case | Nội dung chính |
|---|---|---|---:|---|
| 1 | **Lexer (DFA)** | `LexerTests`, `LexerSymbolAliasTests`, `LexerSymbolTokenTests` (`Lexing/LexerTests.cs`) | 78 | số nguyên, số thực, `1.5e-3`, định danh, toán tử, khoảng trắng, ký tự lạ (đúng vị trí), `π` `√` luôn tách riêng, đi từng bước trên bảng DFA |
| 2 | **Parser** | `ParserTests` (`Parsing/ParserTests.cs`) | 38 | hình dạng cây AST: thứ tự ưu tiên, kết hợp trái/phải, `-2^2`, nhân ngầm, giai thừa, vị trí nút |
| 3 | **Evaluator + lỗi** | `EvaluatorTests`, `ErrorTests` (`Evaluation/`) | 128 | `2+3*4=14`, `2^3^2=512`, DEG/RAD, `0.1+0.2=0.3`, làm tròn hiển thị; mọi loại lỗi trong SPEC kèm vị trí (ngoặc, thiếu toán hạng, chia 0, miền xác định, giai thừa, tràn số) |
| 4 | **Máy trạng thái nhập liệu** | `InputStateMachineTests`, `ExpressionEditorTests`, `DisplayModelTests` (`Input/`) | 72 | bảng phủ đủ 28 ô, 4 quy tắc SPEC (ShowingResult + số/toán tử, Error + phím bất kỳ, `=` khi Typing), xoá lùi, ±, nạp lịch sử, thay `Ans` khi ghi lịch sử, nội dung màn hình 2 dòng |
| 5 | **Chế độ Lập trình** | `ProgrammerLexerTests`, `ProgrammerParserTests`, `IntegerEvaluatorTests`, `ProgrammerSessionTests` (`Programmer/`) | 101 | số theo cơ số HEX/DEC/OCT/BIN, ưu tiên kiểu C, phép bit theo BYTE/WORD/DWORD/QWORD (bù hai, tràn quay vòng), đổi cơ số / kích thước word |
| 6 | **Đổi đơn vị** | `UnitConverterTests` (`Units/`) | 55 | nhiệt độ (affine, độ không tuyệt đối), 8 nhóm tuyến tính, đổi qua lại mọi cặp đơn vị, đọc/hiển thị số |
| 7 | **Ngày tháng** | `DateCalculatorTests` (`Dates/`) | 52 | năm nhuận (2000, 1900, 2100), khoảng cách qua 29/02, năm/tháng/tuần/ngày, cộng/trừ, đọc `dd/MM/yyyy` và ISO, thứ trong tuần |
| 8 | **Vẽ đồ thị** | `PlotFunctionTests`, `FunctionSamplerTests`, `ViewportTests` (`Graphing/`) | 52 | đánh giá f(x) tại nhiều điểm, điểm không xác định → null, ngắt nét ở tiệm cận (tan, 1/x), zoom giữ điểm dưới chuột, kéo, bước lưới |
| 9 | **Thống kê** | `StatisticsTests` (`Statistics/`) | 29 | n, tổng, trung bình, trung vị, mode (nhiều / không có), min, max, phương sai và độ lệch chuẩn mẫu & tổng thể, đọc dữ liệu |
| 10 | **Lưu trữ** | `MemoryStoreTests`, `HistoryStoreTests`, `AppSettingsTests` (`StoresTests.cs`) | 14 | bộ nhớ MC/MR/M+/M−/MS, lịch sử JSON (lưu/đọc, tối đa 100 mục), cài đặt, file hỏng |
| 11 | **Ca biên** | `EmptyInputTests`, `DeepNestingTests`, `ExtremeNumberTests`, `DateBoundaryTests`, `CorruptFileTests`, `CultureIndependenceTests` (`EdgeCaseTests.cs`) | 60 | xem mục dưới |
| | | | **679** | |

## Ca biên (giai đoạn 6)

| Lớp | Số case | Kiểm tra |
|---|---:|---|
| `EmptyInputTests` | 7 | chuỗi rỗng / chỉ có khoảng trắng ở mọi tầng: lexer, máy tính, lập trình, máy trạng thái, đồ thị, thống kê, đổi đơn vị, ngày |
| `DeepNestingTests` | 15 | ngoặc lồng 1, 50, 200 mức chạy đúng; 300, 5 000, 100 000 mức → lỗi "lồng quá sâu" (không tràn stack); chuỗi 50 000 dấu `-`; chuỗi `^` dài; chuỗi phẳng `1+1+…` 100 000 phần tử → "quá dài"; dán biểu thức lồng sâu vào máy trạng thái |
| `ExtremeNumberTests` | 25 | tràn số (`1e308*10`, `10^309`, `170!*171`, 400 chữ số 9) báo đúng vị trí; số lớn hữu hạn (`double.MaxValue`, vượt phạm vi `decimal`); số cực nhỏ không bị hụt về 0; biên `long` ở chế độ lập trình; thống kê / đổi đơn vị vượt `decimal` |
| `DateBoundaryTests` | 7 | khoảng cách gần năm 9999 và toàn bộ phạm vi 0001–9999; cộng số tuần/ngày/tháng/năm cực lớn; từ chối số âm |
| `CorruptFileTests` | 3 | `settings.json` có enum sai hoặc `null`; `history.json` có mục `null` hoặc thiếu trường |
| `CultureIndependenceTests` | 3 | kết quả giống nhau khi máy dùng locale `vi-VN`, `de-DE`, `fr-FR` (dấu thập phân là dấu phẩy) |

## Lỗi tìm và sửa trong giai đoạn 6

Mỗi lỗi đều có test tái hiện. Test được viết trước, chạy thấy thất bại, sau đó mới sửa.

| # | Lỗi | Hậu quả | Sửa |
|---|---|---|---|
| 1 | Parser đệ quy không giới hạn độ sâu | dán `((((…))))` lồng ~100 000 mức → **tràn stack, chương trình tắt ngay** (không bắt được bằng try/catch) | `ParserLimits`: tối đa 256 mức lồng, 1 000 ký hiệu; báo `CalcException` như lỗi thường (cả parser khoa học và lập trình) |
| 2 | Nhân/chia/luỹ thừa bằng `decimal` bị hụt về 0 | `0.1^50 = 0`, `1e-10/1e27 = 0` (sai) | kết quả `decimal` = 0 trong khi toán hạng khác 0 → dùng kết quả `double` |
| 3 | `DateCalculator.Difference` gần năm 9999 | ném `ArgumentOutOfRangeException` (vòng đếm tháng thử cộng sang năm 10000) | đếm tối đa 11 tháng, kiểm tra trước khi cộng (`TryAddMonths`) |
| 4 | `DateCalculator.Add` với số tuần lớn | `weeks * 7` tràn `int` âm thầm → ra ngày sai | tính bằng `long`, quá phạm vi → báo lỗi; số âm → từ chối |
| 5 | `settings.json` có enum không hợp lệ | theme/đơn vị góc nhận giá trị lạ (vd `Theme = 7`) | kiểm tra `Enum.IsDefined`, sai thì dùng mặc định |
| 6 | `history.json` có mục `null` / thiếu trường | danh sách lịch sử chứa `null` → lỗi khi hiển thị | bỏ các mục không hợp lệ khi đọc |
| 7 | `ToggleNegate` so sánh chuỗi theo culture của máy | hành vi có thể khác nhau tuỳ locale | `StringComparison.Ordinal` |
| 8 | Kéo đồ thị khi mất mouse capture (Alt+Tab) | đồ thị tiếp tục "dính" theo chuột | xử lý `OnLostMouseCapture` |
| 9 | Đọc clipboard khi app khác đang giữ | `COMException` không được bắt → app có thể đóng | bắt lỗi, bỏ qua lần dán đó |

Lỗi 8 và 9 nằm ở tầng giao diện (WPF) nên không có unit test; đã kiểm tra bằng đọc code.

## Phạm vi chưa kiểm thử tự động

- Tầng giao diện WPF (XAML, ViewModel, `GraphCanvas`) không có test tự động. Các giai đoạn 3–5 đã kiểm tra bằng cách chạy app thật và chụp màn hình từng chế độ, ở theme sáng/tối và cửa sổ rộng/hẹp. Mọi logic quyết định kết quả đều nằm trong Core và đã có test.
