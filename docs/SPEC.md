# SmartCalc — Đặc tả dự án

## Bối cảnh
Bài tập môn Kiểm chứng mô hình (Model Checking), ĐH Cần Thơ. Ứng dụng máy tính cá nhân desktop.
Điểm nhấn học thuật: xử lý biểu thức bằng LÝ THUYẾT ÔTÔMAT + PHÂN TÍCH CÚ PHÁP TOP-DOWN, và điều khiển
giao diện bằng MÁY TRẠNG THÁI. Code phải thể hiện rõ các mô hình này (không dùng DataTable.Compute,
không dùng thư viện parse biểu thức có sẵn, không dùng Regex của .NET để tách token).

## Công nghệ
- C# / .NET 10, WPF (TargetFramework: net10.0-windows), MVVM với CommunityToolkit.Mvvm
- Test: xUnit
- Solution `SmartCalc.sln` gồm:
  - `src/SmartCalc.Core` (class library, net10.0) — KHÔNG phụ thuộc UI
  - `src/SmartCalc.App` (WPF, net10.0-windows)
  - `tests/SmartCalc.Core.Tests` (xUnit)

## Kiến trúc Core
1. **Lexer = DFA tự cài đặt** (table-driven): enum State, bảng chuyển trạng thái tường minh
   (Dictionary/ma trận (State, CharClass) -> State), tập trạng thái kết thúc. Token: Number (số nguyên,
   số thực, dạng 1.5e-3), Identifier (sin, cos, tan, asin, acos, atan, ln, log, sqrt, abs, fact, pi, e, Ans),
   toán tử + - * / ^ % !, ngoặc ( ), dấu phẩy. Chế độ lập trình: số 0x.., 0b.., 0o.., toán tử bit.
   Mỗi token lưu vị trí (index) để báo lỗi.
2. **Parser = recursive descent** theo văn phạm (ghi văn phạm vào comment đầu file):
   Expr    -> Term (('+'|'-') Term)*
   Term    -> Factor (('*'|'/'|'%') Factor)*        (nhân ngầm: 2pi, 3(4+1))
   Factor  -> Unary ('^' Factor)?                    (^ kết hợp phải)
   Unary   -> ('-'|'+') Unary | Postfix
   Postfix -> Primary '!'*
   Primary -> Number | Constant | Func '(' Expr ')' | '(' Expr ')'
   Kết quả là AST (record types). Biến x được phép khi dùng cho vẽ đồ thị.
3. **Evaluator**: duyệt AST. Dùng `decimal` cho + - * / để tránh lỗi 0.1+0.2; dùng `double` cho hàm
   siêu việt. Hỗ trợ DEG/RAD. Làm tròn hiển thị hợp lý (tối đa 15 chữ số có nghĩa).
4. **Lỗi**: CalcException có Message tiếng Việt + Position. Các lỗi: ký tự lạ, thiếu/thừa ngoặc,
   thiếu toán hạng, chia cho 0, miền xác định (sqrt số âm, ln(0)...), giai thừa số không nguyên/quá lớn, tràn số.

## Máy trạng thái giao diện (InputStateMachine trong Core, có test)
Trạng thái: Empty, Typing, ShowingResult, Error.
- ShowingResult + gõ số  -> bắt đầu biểu thức mới
- ShowingResult + gõ toán tử -> nối tiếp từ "Ans"
- Error + phím bất kỳ -> xoá lỗi, xử lý phím như Empty
- '=' khi Typing -> ShowingResult hoặc Error
Cài đặt bằng bảng chuyển trạng thái tường minh, không if-else rải rác.

## Chế độ (sidebar bên trái, phím tắt Alt+1..6)
1. Cơ bản: + - × ÷ % ± ⌫ C = , ngoặc
2. Khoa học: thêm sin cos tan (và nghịch đảo qua nút 2nd), ln log x² xʸ √ n! π e 1/x, DEG/RAD
3. Lập trình: HEX/DEC/OCT/BIN hiển thị song song, AND OR XOR NOT << >>, kích thước QWORD/DWORD/WORD/BYTE
4. Vẽ đồ thị: nhập y = f(x) (tối đa 3 hàm, màu khác nhau), lưới + trục, zoom chuột, kéo để di chuyển
5. Đổi đơn vị: độ dài, khối lượng, nhiệt độ, diện tích, thể tích, tốc độ, thời gian, dữ liệu (bit/byte)
6. Thống kê: nhập dãy số -> n, tổng, trung bình, trung vị, mode, min, max, phương sai, độ lệch chuẩn (mẫu & tổng thể)

## Tính năng chung
- Màn hình: dòng trên = biểu thức (chữ nhỏ, xám), dòng dưới = kết quả (chữ to). Lỗi hiện đỏ + vị trí.
- Gõ bàn phím đầy đủ: số, toán tử, Enter (=), Esc (C), Backspace, Ctrl+C/Ctrl+V.
- Lịch sử (panel phải): click để nạp lại biểu thức, nút xoá. Lưu ra file JSON trong %AppData%/SmartCalc.
- Bộ nhớ: MC MR M+ M− MS, tab Bộ nhớ trong panel phải. Nút Ans.
- Theme sáng/tối, nút ghim cửa sổ luôn trên cùng (Topmost). Lưu cài đặt.
- Cửa sổ co giãn được, nút bấm co theo.

## Quy tắc thiết kế

- **Lexer là DFA thuần:** mọi luật cú pháp số nằm trong bảng δ. Số có dấu chấm thừa, chữ số sai cơ số
  hoặc chữ dính số lập trình đi tới trạng thái kết thúc `BadNumber`, sinh `TokenKind.Error`.
  Các trạng thái tiền tố chưa đủ chữ số cũng sinh Error. Khớp dài nhất quyết định token; vòng quét không
  kiểm tra hậu tố số ngoài δ. Giữ nguyên thông báo tiếng Việt và vị trí ký tự sai; chế độ thường vẫn cho
  phép nhân ngầm `2pi`, `3(4+1)`, `2sin(30)` và lùi khớp khi số mũ chưa hoàn chỉnh.
- **Máy nhập liệu là DFA toàn phần:** `Table[(InputState, InputSymbol)] -> InputState` có đủ 4 × 10 ô.
  Bảng `ActionTable[(InputState, InputSymbol)] -> InputAction` riêng cũng đủ 40 ô. Chuẩn hoá kết quả trước
  khi tra bảng: EqualsOk/EqualsError, BackspaceToEmpty/BackspaceKeep, LoadEmpty/LoadOk.
  Mỗi ký hiệu từ Error có chuyển tường minh. Không có OnFailure, Replay hay gọi đệ quy Process;
  hành động không quyết định trạng thái kế tiếp. Hai bảng chỉ đọc sau khi khởi tạo.
- **Dùng chung DFA số:** `StatisticsCalculator.Parse` và `UnitConverter.TryParse` gọi `NumberReader`,
  chạy DFA số của Lexer để nhận toàn bộ giá trị; không dùng `decimal.TryParse` kiểm tra cú pháp.
  Dấu `+`, `-`, `−` đầu số được tách trước khi chạy DFA; `decimal.Parse` chỉ chuyển giá trị sau khi nhận.
  Giữ các dấu phân tách thống kê (khoảng trắng, xuống dòng, phẩy, chấm phẩy), dấu phẩy thập phân và
  chuẩn hoá khoảng trắng của Đổi đơn vị, cùng phạm vi/làm tròn decimal hiện có.
- **Cấu trúc dữ liệu:** HistoryStore dùng `Queue<HistoryEntry>`, mục cũ nhất ở đầu; đủ 100 mục thì
  Dequeue trước khi Enqueue. UI và JSON vẫn mới nhất trước. Xoá một mục giữ thứ tự các mục còn lại.
  Builtins giữ Dictionary tên hàm → `Func<double, EvalContext, int, double>`; Evaluator tra Dictionary
  để gọi hàm, giữ nguyên DEG/RAD, kiểm tra miền xác định và vị trí lỗi.
- **Kiểm chứng bảng:** test từng ô Lexer ở chế độ thường và lập trình với cơ số 2/8/10/16; kiểm tra
  giá trị mọi ô của hai bảng nhập liệu và các kịch bản phím cũ. Differential test dùng .NET Regex
  trong project test để đối chiếu ít nhất 20.000 chuỗi ngẫu nhiên mỗi cấu hình. Core không dùng Regex.

## Quy tắc làm việc cho agent
- Làm ĐÚNG phạm vi giai đoạn được giao, không làm lấn sang giai đoạn sau.
- Sau mỗi thay đổi: `dotnet build` phải pass, `dotnet test` phải pass hết.
- Logic nằm trong Core + có unit test; App chỉ binding/hiển thị.
- Comment ngắn bằng tiếng Việt ở các chỗ quan trọng (DFA, văn phạm, máy trạng thái) để sinh viên đọc hiểu.
- Kết thúc mỗi giai đoạn: tóm tắt đã làm gì, file nào, cách chạy thử.
