-- Additive migration. Existing order/file relationships and rows are preserved.
CREATE TABLE IF NOT EXISTS OrderProcessRules (
    OrderProcessRuleId BIGINT NOT NULL AUTO_INCREMENT,
    OrderId BIGINT NOT NULL,
    OrderItemId BIGINT NOT NULL,
    OrderFileId BIGINT NULL,
    RuleID INT NOT NULL,
    SortOrder INT NOT NULL,
    ProcessName VARCHAR(100) NOT NULL,
    Summary TEXT NOT NULL,
    StepsJSON LONGTEXT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (OrderProcessRuleId),
    UNIQUE KEY UX_OrderProcessRules_ItemSort (OrderItemId, SortOrder),
    KEY IX_OrderProcessRules_Order (OrderId),
    KEY IX_OrderProcessRules_File (OrderFileId),
    KEY IX_OrderProcessRules_Rule (RuleID),
    CONSTRAINT FK_OrderProcessRules_Order FOREIGN KEY (OrderId) REFERENCES OrderInfo(OrderId),
    CONSTRAINT FK_OrderProcessRules_Item FOREIGN KEY (OrderItemId) REFERENCES OrderItems(OrderItemId) ON DELETE CASCADE,
    CONSTRAINT FK_OrderProcessRules_File FOREIGN KEY (OrderFileId) REFERENCES OrderFiles(OrderFileId) ON DELETE SET NULL,
    CONSTRAINT FK_OrderProcessRules_Rule FOREIGN KEY (RuleID) REFERENCES ProcessRule(RuleID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
