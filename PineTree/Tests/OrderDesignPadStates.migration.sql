-- Additive storage for the work product, separate from accessory rows.
CREATE TABLE IF NOT EXISTS OrderDesignPadStates (
    OrderId BIGINT NOT NULL,
    StateJSON LONGTEXT NOT NULL,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (OrderId),
    CONSTRAINT FK_OrderDesignPadStates_Order FOREIGN KEY (OrderId)
        REFERENCES OrderInfo(OrderId) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
