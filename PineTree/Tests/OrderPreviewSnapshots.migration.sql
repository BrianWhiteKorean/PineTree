-- Full preview images are immutable server files; order number is the lookup key.
CREATE TABLE IF NOT EXISTS OrderPreviewSnapshots (
    OrderNo VARCHAR(32) NOT NULL,
    OrderId BIGINT NOT NULL,
    SourceFileInfo LONGTEXT NULL,
    PreviewJSON LONGTEXT NOT NULL,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (OrderNo),
    UNIQUE KEY UX_OrderPreviewSnapshots_OrderId (OrderId),
    CONSTRAINT FK_OrderPreviewSnapshots_Order FOREIGN KEY (OrderId)
        REFERENCES OrderInfo(OrderId) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
