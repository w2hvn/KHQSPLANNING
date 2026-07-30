USE QuanLyHuanLuyen;

-- 1. Bảng time_node: Bổ sung is_manual và is_locked
ALTER TABLE time_node
ADD COLUMN is_manual BOOLEAN DEFAULT FALSE COMMENT 'TRUE: Đã tạo/chỉnh sửa thủ công bởi cán bộ; FALSE: Sinh tự động từ Engine',
ADD COLUMN is_locked BOOLEAN DEFAULT FALSE COMMENT 'TRUE: Khóa mốc thời gian, Engine không được phép tính toán/đè lại';

-- 2. Bảng program_node: Sửa complexity_level và thêm time_node_id
ALTER TABLE program_node
MODIFY COLUMN complexity_level INT DEFAULT 0 COMMENT '0: None/Không phân loại/Trung tính, 1: Cơ bản (GĐ1/Tháng đầu), 2: Nâng cao (GĐ2/Tháng cuối)',
ADD COLUMN time_node_id INT NULL COMMENT 'ID nút thời gian mỏ neo (NULL = Cả năm/Root)',
ADD CONSTRAINT fk_pn_time_node FOREIGN KEY (time_node_id) REFERENCES time_node(id) ON DELETE CASCADE;

-- 3. Bảng time_node_type: Seed đủ 4 cấp độ
INSERT INTO time_node_type (id, code, name, depth_level)
VALUES
    (1, 'YEAR', 'Năm huấn luyện', 1),
    (2, 'STAGE', 'Giai đoạn', 2),
    (3, 'MONTH', 'Tháng', 3),
    (4, 'WEEK', 'Tuần', 4)
ON DUPLICATE KEY UPDATE
    code = VALUES(code),
    name = VALUES(name),
    depth_level = VALUES(depth_level);
