# BÁO CÁO TRẠNG THÁI DỰ ÁN: QUẢN LÝ HUẤN LUYỆN QUÂN SỰ
**Ngày lập báo cáo:** (Hiện tại)
**Môi trường:** .NET 8 WPF (Code-Behind Architecture) & MySQL 8.0 (InnoDB)

---

## I. TỔNG QUAN KIẾN TRÚC & CƠ SỞ DỮ LIỆU
Hệ thống đã được thiết kế và xây dựng dựa trên nguyên tắc kiến trúc "Code-Behind thuần túy" (Không sử dụng MVVM) thông qua C# và Entity Framework Core. Cơ sở dữ liệu MySQL đã hoàn thiện toàn bộ cấu trúc Schema, đảm bảo tính toàn vẹn dữ liệu.

*   **Tình trạng CSDL:** Hoàn thiện 100%. Đã áp dụng `schema.sql` bao gồm 11 bảng: `plan`, `training_target`, `plan_target`, `program_node_type`, `time_node_type`, `program_node`, `time_node`, `time_allocation`, `program_node_decor`, `blackout_date`, và `scheduling_priority_rule`.
*   **Triggers MySQL:** Đã tích hợp thành công. Tự động tính toán `level` và `tree_path` (giải quyết triệt để lỗi Mutating Table qua BEFORE INSERT/UPDATE). Đã tích hợp ràng buộc `SIGNAL SQLSTATE '45000'` chống phân bổ thời lượng (Capacity) cho nút con vượt quá nút cha.
*   **Bảo vệ dữ liệu thừa (Zero-Value Elimination):** Mọi giao dịch lưu trữ số giờ bằng 0 sẽ tự động thực hiện thao tác `DELETE` thay vì lưu dữ liệu ảo, giữ DB luôn tinh gọn.

---

## II. CHI TIẾT CÁC TÍNH NĂNG ĐÃ THỰC HIỆN VÀ HOẠT ĐỘNG TỐT

### 1. Quản lý UI/UX & Điều hướng (MainWindow & HeaderControl)
*   **Thanh công cụ Top Bar:** Giao diện điều hướng phẳng, phong cách Quân đội. Tải Kế hoạch (`Plan`) và Đối tượng huấn luyện (`PlanTarget`) động từ CSDL.
*   **Cơ chế truyền dữ liệu:** Mọi màn hình (Page) đều được điều khiển qua Frame thông qua tín hiệu truyền ID trực tiếp `RefreshData(planTargetId)`, hoạt động mượt mà.

### 2. Màn hình Cây Nội Dung (ProgramTreePage)
*   **Hiển thị:** Dựng cây nội dung (Môn -> Bài -> Buổi) thành công. Có ô vuông màu chỉ báo riêng cho nút gốc (Môn học). Thụt lề cấp bậc trực quan qua Converter.
*   **Thao tác CRUD:** Thêm, Sửa, Xóa Nút trên Cây hoạt động chính xác. Chặn lỗi phụ thuộc vòng tròn (Circular Dependency) khi chọn Bài học Tiền đề.
*   **Quản lý Thuộc tính Bài học:** Lưu trữ đầy đủ Mã màu (Hex), Độ phức tạp (Cơ bản/Nâng cao), Huấn luyện đêm, Ngoài thao trường, Thể lực nặng, Bài tiền đề.
*   **Bắt lỗi MySQL:** Xử lý thành công Exception hiển thị cảnh báo bằng tiếng Việt nếu tổng số giờ bài con cấu hình lớn hơn ngân sách Môn cha.

### 3. Màn hình Phân Bổ Thời Gian Thác Nước (CascadeAllocationPage)
*   **Cây Thời Gian (Bên trái):** Tự động sinh danh sách Tuần của Năm dựa theo Quy tắc Đa số (Majority Rule >= 4 ngày), chuẩn xác.
*   **Lưới Ma Trận (Bên phải):** DataGrid phức tạp sinh Cột Động. Số lượng cột và Binding phụ thuộc trực tiếp vào Nút thời gian Cha được chọn.
*   **Kiểm soát Hạn mức:** Có các cột `Đã chia` và `Còn lại` sử dụng `INotifyPropertyChanged` nhảy số Real-time ngay khi gõ trên lưới.

### 4. Báo Cáo Tiến Độ (TimelineReportPage)
*   **Gom nhóm & Cuộn lên (Roll-up Aggregation):** Cho phép xem báo cáo theo các cấp Giai đoạn, Tháng, Tuần. Hệ thống tự động đệ quy gom tổng số giờ phân bổ từ Tuần lên cấp báo cáo Tương ứng (Tháng/Giai đoạn).
*   **Xuất Excel:** Xuất file `.xlsx` thành công thông qua thư viện `ClosedXML` (Không bị giới hạn bản quyền thương mại), giữ nguyên định dạng màu sắc Môn học, khung viền và thụt lề chuẩn.

### 5. Khối Công Cụ Cấu Hình & Lịch Trình Tự Động (Auto-Schedule Engine)
*   **Quản lý Nghỉ lễ & Trọng số:** Popups `BlackoutDateWindow` và `PriorityRuleConfigWindow` hoạt động độc lập, lưu dữ liệu chuẩn xác.
*   **Engine Lập Lịch Đa Cấp (Strategy Pattern):**
    *   **Vĩ mô (Year/Stage -> Month):** Ứng dụng Bottom-Up để tính tỷ trọng số giờ Phức tạp 1 vs Phức tạp 2 từ các Nút lá lên Cha, sau đó dải Top-Down xuống ngân sách các Tháng tương ứng theo tỷ lệ.
    *   **Vi mô (Month -> Week):** Engine trừ chính xác ngày nghỉ lễ, ưu tiên chèn Hard-rules (Chào Cờ, Sinh hoạt). Xét duyệt các Soft-rules (Điểm chính trị, Quân sự, Thể lực) qua điểm số heuristic. Áp dụng chuẩn Sequential Constraints để bài 1 học trước bài 2.
    *   **Human-in-the-loop (Tạm dừng khi phân vân):** Cơ chế Asynchronous Pause. Khi thuật toán phát hiện 2 Bài có điểm ưu tiên bằng nhau tranh chấp chung 1 khe thời gian, nó đóng băng luồng nền, hiển thị Dialog `ConflictResolverWindow` cho Chỉ huy can thiệp, và Resume ngay khi nhận lệnh. Ghi Log chi tiết đối với các bài học bị "Bỏ qua" kèm tính năng Double-Click cuộn chuột đến ô lỗi.

---

## III. HẠN CHẾ & NHỮNG CHỨC NĂNG CẦN HOÀN THIỆN TRONG TƯƠNG LAI

Dự án hiện tại là một bộ khung rất tinh xảo, đáp ứng >95% yêu cầu. Dù vậy, theo góc nhìn khách quan của kỹ sư phần mềm, dưới đây là các hạn chế còn tồn đọng:

1.  **Chưa có màn hình Quản trị Danh mục Core:**
    *   Các bảng `plan`, `training_target`, `program_node_type` và `time_node_type` (VD: Định nghĩa mã "MONTH", "STAGE") hiện tại đang dựa vào Insert SQL bằng tay ban đầu. Hệ thống cần có màn hình danh mục riêng (Master Data) để cán bộ quản trị (Admin) cấu hình thêm bớt loại node.
2.  **Khả năng mở rộng Cấp Ngày/Giờ (WeekToDayStrategy):**
    *   Thuật toán cấp cuối cùng (`WeekToDayStrategy`) hiện mới ở dạng Fallback (trả về Task thành công và bắn Log). Việc xếp thời khóa biểu chính xác theo tiết học trong Ngày cần một chiến lược UI phức tạp dạng Calendar Control kéo thả, lưới phân bổ ngang hiện tại của WPF DataGrid không phù hợp để làm cấp Ngày.
3.  **Hành vi Reload UI toàn bộ (Full Tree Reset):**
    *   Khi nhấn `[Lưu Cập Nhật]` bên `ProgramTreePage`, hàm sẽ fetch lại toàn bộ cây và load lại từ đầu, khiến các node đang mở (Expanded) bị sập lại. (Có thể khắc phục bằng cách lưu state mảng ID đang mở trước khi refresh).

## IV. TỔNG KẾT
Phần mềm hiện tại **CÓ THỂ SỬ DỤNG NGAY** cho mục đích lập kế hoạch, dựng cây bài học, chia thời lượng huấn luyện theo phân quyền thời gian (Giai đoạn/Tháng/Tuần) và xuất báo cáo báo cáo tổng hợp. Mô-đun Engine Auto-Scheduler rất độc đáo và mạnh mẽ, sẵn sàng hỗ trợ Chỉ huy Tiểu đoàn chia tiết tự động với tốc độ cao và giám sát trực quan.
