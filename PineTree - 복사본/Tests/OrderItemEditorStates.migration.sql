-- Additive migration: preserves all existing orders, items, files and process rules.
CREATE TABLE IF NOT EXISTS OrderItemEditorStates (
    OrderItemId BIGINT NOT NULL,
    EditorType VARCHAR(50) NOT NULL,
    StateJSON LONGTEXT NOT NULL,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (OrderItemId),
    CONSTRAINT FK_OrderItemEditorStates_Item FOREIGN KEY (OrderItemId)
        REFERENCES OrderItems(OrderItemId) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
