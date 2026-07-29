
CREATE TABLE plan (
    id INT AUTO_INCREMENT PRIMARY KEY,
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    year INT NOT NULL,
    status VARCHAR(20) DEFAULT 'DRAFT',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE training_target (
    id INT AUTO_INCREMENT PRIMARY KEY,
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    sort_order INT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE plan_target (
    id INT AUTO_INCREMENT PRIMARY KEY,
    plan_id INT NOT NULL,
    target_id INT NOT NULL,
    UNIQUE KEY uk_plan_target (plan_id, target_id),
    FOREIGN KEY (plan_id) REFERENCES plan(id) ON DELETE CASCADE,
    FOREIGN KEY (target_id) REFERENCES training_target(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE program_node_type (
    id INT AUTO_INCREMENT PRIMARY KEY,
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(100) NOT NULL,
    depth_level INT NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE time_node_type (
    id INT AUTO_INCREMENT PRIMARY KEY,
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(100) NOT NULL,
    depth_level INT NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE program_node (
    id INT AUTO_INCREMENT PRIMARY KEY,
    plan_target_id INT NOT NULL,
    parent_id INT,
    node_type_id INT NOT NULL,
    code VARCHAR(50),
    name VARCHAR(255) NOT NULL,
    capacity DECIMAL(8,2) DEFAULT 0.00,
    level INT DEFAULT 1,
    tree_path VARCHAR(500),
    sort_order INT,
    FOREIGN KEY (plan_target_id) REFERENCES plan_target(id) ON DELETE CASCADE,
    FOREIGN KEY (parent_id) REFERENCES program_node(id) ON DELETE CASCADE,
    FOREIGN KEY (node_type_id) REFERENCES program_node_type(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE time_node (
    id INT AUTO_INCREMENT PRIMARY KEY,
    plan_id INT NOT NULL,
    parent_id INT,
    node_type_id INT NOT NULL,
    code VARCHAR(50),
    name VARCHAR(255) NOT NULL,
    start_date DATE,
    end_date DATE,
    level INT DEFAULT 1,
    tree_path VARCHAR(500),
    sort_order INT,
    FOREIGN KEY (plan_id) REFERENCES plan(id) ON DELETE CASCADE,
    FOREIGN KEY (parent_id) REFERENCES time_node(id) ON DELETE CASCADE,
    FOREIGN KEY (node_type_id) REFERENCES time_node_type(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE time_allocation (
    id INT AUTO_INCREMENT PRIMARY KEY,
    program_node_id INT NOT NULL,
    time_node_id INT NOT NULL,
    allocated_hours DECIMAL(8,2) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_program_time (program_node_id, time_node_id),
    FOREIGN KEY (program_node_id) REFERENCES program_node(id) ON DELETE CASCADE,
    FOREIGN KEY (time_node_id) REFERENCES time_node(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE program_node_decor (
    program_node_id INT PRIMARY KEY,
    bg_color_hex VARCHAR(10) DEFAULT '#FFFFFF',
    border_color_hex VARCHAR(10) DEFAULT '#0066CC',
    text_color_hex VARCHAR(10) DEFAULT '#000000',
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (program_node_id) REFERENCES program_node(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

DELIMITER //

-- Trigger BEFORE INSERT cho bảng program_node
CREATE TRIGGER trg_program_node_before_insert
BEFORE INSERT ON program_node
FOR EACH ROW
BEGIN
    DECLARE parent_level INT;
    DECLARE parent_path VARCHAR(500);
    DECLARE children_sum DECIMAL(8,2);
    DECLARE parent_capacity DECIMAL(8,2);

    -- Xử lý level và tree_path
    IF NEW.parent_id IS NULL THEN
        SET NEW.level = 1;
        SET NEW.tree_path = '/';
    ELSE
        SELECT level, tree_path INTO parent_level, parent_path FROM program_node WHERE id = NEW.parent_id;
        SET NEW.level = parent_level + 1;
        IF parent_path = '/' THEN
            SET NEW.tree_path = CONCAT('/', NEW.parent_id, '/');
        ELSE
            SET NEW.tree_path = CONCAT(parent_path, NEW.parent_id, '/');
        END IF;

        -- Kiểm tra ràng buộc capacity cho node cha
        SELECT SUM(capacity) INTO children_sum FROM program_node WHERE parent_id = NEW.parent_id;
        SELECT capacity INTO parent_capacity FROM program_node WHERE id = NEW.parent_id;

        IF IFNULL(children_sum, 0) + NEW.capacity > parent_capacity THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Total capacity of child nodes exceeds parent capacity';
        END IF;
    END IF;
END //

-- Trigger BEFORE UPDATE cho bảng program_node
CREATE TRIGGER trg_program_node_before_update
BEFORE UPDATE ON program_node
FOR EACH ROW
BEGIN
    DECLARE parent_level INT;
    DECLARE parent_path VARCHAR(500);
    DECLARE children_sum DECIMAL(8,2);
    DECLARE parent_capacity DECIMAL(8,2);

    -- Xử lý level và tree_path nếu parent_id thay đổi
    IF NEW.parent_id IS NULL THEN
        SET NEW.level = 1;
        SET NEW.tree_path = '/';
    ELSE
        IF OLD.parent_id IS NULL OR NEW.parent_id != OLD.parent_id THEN
            SELECT level, tree_path INTO parent_level, parent_path FROM program_node WHERE id = NEW.parent_id;
            SET NEW.level = parent_level + 1;
            IF parent_path = '/' THEN
                SET NEW.tree_path = CONCAT('/', NEW.parent_id, '/');
            ELSE
                SET NEW.tree_path = CONCAT(parent_path, NEW.parent_id, '/');
            END IF;
        END IF;
    END IF;

    -- Kiểm tra ràng buộc capacity
    -- 1. Nếu cha thay đổi, hoặc capacity tăng lên so với ban đầu
    IF NEW.parent_id IS NOT NULL AND (OLD.parent_id IS NULL OR NEW.parent_id != OLD.parent_id OR NEW.capacity > OLD.capacity) THEN
        -- Tính tổng capacity của các node con CÙNG CHA, ngoại trừ chính node đang được cập nhật
        SELECT SUM(capacity) INTO children_sum FROM program_node WHERE parent_id = NEW.parent_id AND id != NEW.id;
        SELECT capacity INTO parent_capacity FROM program_node WHERE id = NEW.parent_id;

        IF IFNULL(children_sum, 0) + NEW.capacity > parent_capacity THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Total capacity of child nodes exceeds parent capacity';
        END IF;
    END IF;

    -- 2. Kiểm tra nếu capacity của chính nó giảm xuống thấp hơn tổng của các con của nó (Trường hợp node này đang làm cha)
    IF NEW.capacity < OLD.capacity THEN
        SELECT SUM(capacity) INTO children_sum FROM program_node WHERE parent_id = NEW.id;
        IF children_sum IS NOT NULL AND children_sum > NEW.capacity THEN
             SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'New capacity is smaller than the total capacity of its children';
        END IF;
    END IF;
END //

DELIMITER ;

DELIMITER //

-- Trigger BEFORE INSERT cho bảng time_node
CREATE TRIGGER trg_time_node_before_insert
BEFORE INSERT ON time_node
FOR EACH ROW
BEGIN
    DECLARE parent_level INT;
    DECLARE parent_path VARCHAR(500);

    -- Xử lý level và tree_path
    IF NEW.parent_id IS NULL THEN
        SET NEW.level = 1;
        SET NEW.tree_path = '/';
    ELSE
        SELECT level, tree_path INTO parent_level, parent_path FROM time_node WHERE id = NEW.parent_id;
        SET NEW.level = parent_level + 1;
        IF parent_path = '/' THEN
            SET NEW.tree_path = CONCAT('/', NEW.parent_id, '/');
        ELSE
            SET NEW.tree_path = CONCAT(parent_path, NEW.parent_id, '/');
        END IF;
    END IF;
END //

-- Trigger BEFORE UPDATE cho bảng time_node
CREATE TRIGGER trg_time_node_before_update
BEFORE UPDATE ON time_node
FOR EACH ROW
BEGIN
    DECLARE parent_level INT;
    DECLARE parent_path VARCHAR(500);

    -- Xử lý level và tree_path nếu parent_id thay đổi
    IF NEW.parent_id IS NULL THEN
        SET NEW.level = 1;
        SET NEW.tree_path = '/';
    ELSE
        IF OLD.parent_id IS NULL OR NEW.parent_id != OLD.parent_id THEN
            SELECT level, tree_path INTO parent_level, parent_path FROM time_node WHERE id = NEW.parent_id;
            SET NEW.level = parent_level + 1;
            IF parent_path = '/' THEN
                SET NEW.tree_path = CONCAT('/', NEW.parent_id, '/');
            ELSE
                SET NEW.tree_path = CONCAT(parent_path, NEW.parent_id, '/');
            END IF;
        END IF;
    END IF;
END //

DELIMITER ;

-- Bảng Ngày nghỉ lễ bắt buộc
CREATE TABLE blackout_date (
    id INT AUTO_INCREMENT PRIMARY KEY,
    plan_id INT NOT NULL,
    holiday_name VARCHAR(255) NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    description VARCHAR(500),
    FOREIGN KEY (plan_id) REFERENCES plan(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Bảng Quy tắc Ưu tiên Xếp lịch
CREATE TABLE scheduling_priority_rule (
    id INT AUTO_INCREMENT PRIMARY KEY,
    plan_id INT NOT NULL,
    rule_code VARCHAR(50) NOT NULL,
    rule_name VARCHAR(255) NOT NULL,
    priority_score INT DEFAULT 50,
    is_active BOOLEAN DEFAULT TRUE,
    FOREIGN KEY (plan_id) REFERENCES plan(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
