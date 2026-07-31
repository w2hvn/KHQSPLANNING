-- Dữ liệu mẫu cho facility_type (Loại Thao trường / Bãi tập)
INSERT INTO facility_type (id, code, name, description) VALUES
(1, 'TT_BAN', 'Thao trường bắn súng', 'Khu vực huấn luyện bắn súng đạn thật các loại'),
(2, 'BT_CHIEN_THUAT', 'Bãi tập Chiến thuật', 'Khu vực huấn luyện chiến thuật bộ binh, có hào, công sự'),
(3, 'PHONG_HOC', 'Phòng học Lý thuyết', 'Phòng học tập trung, có máy chiếu, sa bàn'),
(4, 'BVC_THE_LUC', 'Bãi vật cản / Sân thể lực', 'Khu vực huấn luyện thể dục thể thao, võ thuật, bãi vật cản'),
(5, 'BAI_TAP_DIEU_LENH', 'Bãi tập Điều lệnh', 'Sân bê tông phẳng, rộng để luyện tập điều lệnh đội ngũ');

-- Dữ liệu mẫu cho program_node_type
INSERT IGNORE INTO program_node_type (id, code, name, depth_level) VALUES
(1, 'KHOA_MUC', 'Khoa mục / Môn học', 1),
(2, 'BAI_HOC', 'Bài học', 2);

-- Dữ liệu mẫu cho plan_target (Giả định có id=1, bạn cần tạo plan & training_target tương ứng trước nếu chạy thực tế)
-- Lưu ý: Các ID được set tĩnh để dễ dàng demo.

-- Dữ liệu mẫu cho program_node
-- Cấu trúc:
-- + Môn: Giáo dục Chính trị (id=1)
--   + Bài 1: Truyền thống QĐNDVN (id=2)
-- + Môn: Huấn luyện Chiến thuật (id=3)
--   + Bài 1: Từng người trong chiến đấu tiến công (id=4)
INSERT INTO program_node (id, plan_target_id, parent_id, node_type_id, code, name, capacity, level, sort_order, complexity_level, is_night_training, is_outdoor, is_heavy_physical, requires_field, facility_type_id) VALUES
(1, 1, NULL, 1, 'CT', 'Giáo dục Chính trị', 40.00, 1, 1, 0, FALSE, FALSE, FALSE, FALSE, 3),
(2, 1, 1, 2, 'CT-01', 'Bài 1: Truyền thống Quân đội nhân dân Việt Nam', 8.00, 2, 1, 1, FALSE, FALSE, FALSE, FALSE, 3),

(3, 1, NULL, 1, 'QS', 'Huấn luyện Chiến thuật', 60.00, 1, 2, 0, FALSE, TRUE, TRUE, TRUE, 2),
(4, 1, 3, 2, 'QS-01', 'Từng người trong chiến đấu tiến công', 12.00, 2, 1, 2, FALSE, TRUE, TRUE, TRUE, 2),
(5, 1, 3, 2, 'QS-02', 'Từng người trong chiến đấu phòng ngự (Ban đêm)', 8.00, 2, 2, 2, TRUE, TRUE, TRUE, TRUE, 2);

-- Dữ liệu mẫu cho program_node_decor (Cấu hình màu sắc giao diện cho các môn)
INSERT INTO program_node_decor (program_node_id, bg_color_hex, border_color_hex, text_color_hex) VALUES
(1, '#FFEBEB', '#FF3333', '#8B0000'), -- Màu đỏ nhạt cho Chính trị
(3, '#E6F3FF', '#3399FF', '#003366'); -- Màu xanh nhạt cho Chiến thuật Quân sự
