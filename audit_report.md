# BÁO CÁO HIỆN TRẠNG HỆ THỐNG MỨC ĐỘ CHI TIẾT
**Dự án:** Quản lý Huấn luyện Quân sự
**Môi trường:** .NET 8 WPF & MySQL 8.0

---

### 1. BÁO CÁO DỮ LIỆU & ENTITIES (DATA & SCHEMA)

**Danh sách Entities & Bảng:**
Hệ thống hiện tại duy trì 11 Entities mapping tương ứng 1:1 với CSDL MySQL thông qua EF Core Fluent API:
- `Plan` (bảng `plan`)
- `BlackoutDate` (bảng `blackout_date`)
- `SchedulingPriorityRule` (bảng `scheduling_priority_rule`)
- `TrainingTarget` (bảng `training_target`)
- `PlanTarget` (bảng `plan_target`)
- `ProgramNodeType` (bảng `program_node_type`)
- `TimeNodeType` (bảng `time_node_type`)
- `ProgramNode` (bảng `program_node`)
- `TimeNode` (bảng `time_node`)
- `TimeAllocation` (bảng `time_allocation`)
- `ProgramNodeDecor` (bảng `program_node_decor`)

**Chi tiết các thuộc tính mở rộng:**
- **`TimeNode.cs`**: Đã tích hợp `IsManual` (Boolean, default: false) và `IsLocked` (Boolean, default: false).
- **`ProgramNode.cs`**: Đã tích hợp `TimeNodeId` (int? nullable FK tới TimeNode cascade) và `ComplexityLevel` (int, default: 0), cùng với `IsNightTraining`, `IsOutdoor`, `IsHeavyPhysical`, và `PrerequisiteNodeId`.
- **`PlanTarget.cs`**: Đã bổ sung `DaysPerWeek`, `MorningHours`, `AfternoonHours`, `NightHours` để tính định mức thời gian tuần.

**Tình trạng file Migration:**
- Có mặt file `schema.sql` chứa cấu trúc khởi tạo sạch, phản ánh đầy đủ các `ALTER TABLE` và các Trigger chống lặp/vượt rào.
- Có mặt file `migration_task7.sql` chứa kịch bản insert `ON DUPLICATE KEY UPDATE` và `ALTER TABLE` dùng để chạy bù trên môi trường Live.

---

### 2. BÁO CÁO CẤU TRÚC GIAO DIỆN & LUỒNG ĐIỀU HƯỚNG (UI & NAVIGATION)

**Layout `MainWindow.xaml`:**
- Áp dụng triệt để "App Shell Layout".
- **Cột Trái (Sidebar):** Rộng 250px chứa 4 nhóm Expander (1. Lập Kế Hoạch, 2. Lập Lịch, 3. Báo Cáo, 4. Cấu Hình).
- **Cột Phải:** Được chia làm 2 tầng với `HeaderControl` ở tầng trên và `mainFrame` để nạp Page ở tầng dưới.

**Trạng thái `HeaderControl.xaml.cs`:**
- **Tích hợp ComboBox:** Hoàn thiện 3 ComboBox (`cboPlan`, `cboTarget`, `cboActiveTimeNode`).
- **Định dạng hiển thị:** `cboActiveTimeNode` đang hiển thị dạng Indented Flat List cực mượt mà sử dụng ký tự unicode đệ quy (`├─`, `└─`, `│ `) trong font `Consolas`.
- **Event-Driven:** Bắn ra sự kiện `GlobalContextChanged` chứa class `GlobalContextEventArgs` bọc gọn `PlanId`, `PlanTargetId`, `TimeNodeId` - đẩy logic xuống MainWindow để navigate/reload Frame mượt mà.

**Danh sách Views/Pages:**
- Các trang đầy đủ logic: `PlanManagementPage`, `TrainingTargetPage`, `TimeTreeManagementPage`, `ProgramTreePage`, `CascadeAllocationPage`, `TimelineReportPage`.
- Các trang Placeholder (đang chờ hoàn thiện tính năng hiển thị chi tiết): `ProgressReportPage` và `ExcelExportPage`.
- Popups/Dialogs: `ConflictResolverWindow`.
- Hub Cấu hình (`SystemConfigHubPage`) bọc các UserControl: `TargetRuleConfigView`, `PriorityRulesConfigView`, `BlackoutDateConfigView`.

---

### 3. BÁO CÁO TÍNH NĂNG & LOGIC NGHIỆP VỤ (CORE LOGIC & ENGINE)

**Khởi tạo Kế hoạch & Sinh Cây:**
- Trong `PlanManagementPage`, sự kiện `BtnSavePlans_Click` sau khi lưu kế hoạch mới sẽ mở DB Transaction (`BeginTransactionAsync`) và kích hoạt `TimeStructureGenerator.BuildCompleteTimeTreeAsync`. Quá trình sinh tự động này hoạt động độc lập, không cần bất kỳ nút bấm thủ công nào từ phía người dùng, và tạo TimeNode với cờ `IsManual = false` và `IsLocked = false`.

**Cây Nội Dung (`ProgramTreePage`):**
- Đã thiết lập `ComplexityLevel` mặc định về 0 (None/Không phân loại) thông qua giao diện ComboBox.
- Logic kiểm tra số giờ hiện mới chỉ cảnh báo vướng ràng buộc (qua Error MySQL trigger) khi nhập sai, giao diện trên cây chưa móc nối sâu hàm để query Real-time hiển thị `[Giờ đã dải / Giờ khung gốc]` dạng Text trực quan trực tiếp trên Tree.

**Phân bổ Ma trận Thác nước (`CascadeAllocationPage`):**
- Đã hoàn thiện tự động sinh Cột Động bám theo Scope Anchor của Header (`_currentTimeNodeId`). Lưới load tự động ngay khi chọn.
- Logic cập nhật tổng giờ cấp trên (`RemainingBudget`) và cảnh báo vượt trần (`dgAllocation_CellEditEnding`) được thực thi mượt mà Real-time nhờ DTO binding `INotifyPropertyChanged`.

**Hub Cấu hình (`SystemConfigHubPage`):**
- Đã đóng gói thành công thành TabControl trực quan chứa 3 UserControl tương ứng. Các module này hỗ trợ Full CRUD thao tác trực tiếp với cơ sở dữ liệu qua EF Core.

**Hệ thống Engine Auto-Scheduler:**
- Sử dụng Strategy Pattern chia luồng theo cấp thời gian (Year, Stage, Month, Week).
- Hỗ trợ Bottom-Up tính khối lượng bài phức tạp, và Top-Down để đổ ngân sách.
- Hỗ trợ Async Pause/Resume hiển thị `ConflictResolverWindow` với đầy đủ tính năng ưu tiên.
- Khấu trừ BlackoutDate thông minh bằng logic chặn thời gian cắt qua.

---

### 4. ĐÁNH GIÁ VẤN ĐỀ VÀ ĐỀ XUẤT CẦN XỬ LÝ (GAP ANALYSIS)

**Vấn đề/Bỏ ngỏ:**
1. Màn hình `WeekToDayStrategy.cs` đang ở chế độ rỗng (Fallback), trả về log nhắc nhở. Việc phân bổ cấp nhỏ nhất đang được bỏ ngỏ chờ cơ sở hạ tầng cấp Session.
2. Hàm `ConvertBack` trong `LevelToIndentConverter.cs` (chuyển đổi UI Padding) vẫn quăng `NotImplementedException()` - tuy nhiên điều này an toàn vì converter này chỉ chạy chế độ OneWay cho TextBlock.
3. Trang `ProgressReportPage` và `ExcelExportPage` hiện chỉ có giao diện tĩnh báo hiệu "Đang hoàn thiện" (Placeholder).
4. `ProgramTreePage` hiển thị cây, nhưng chưa có luồng hiển thị Label tiến độ (Giờ đã dải / Tổng khung) ngay trên từng Node - một tính năng báo cáo UX rất hữu dụng.

**Trạng thái Compile/Runtime:**
- Quá trình biên dịch .NET 8 (Build) trả về **0 Lỗi (Error) và 0 Cảnh báo (Warning)**. Hệ thống cực kỳ sạch sẽ và ổn định.