# DialogOrder save and restore

- Source/output files are optional. Quantity zero is valid.
- Visible OrderName is stored in the existing OrderItems.CustomName column (maximum 50 characters).
- Numeric item values retain their existing columns; zero is preserved.
- OrderItemEditorStates stores one versioned JSON snapshot per order item. It is saved in the same transaction as the order, item and file metadata.
- The snapshot includes dimensions, quantity, original/scale mode and zoom rate, fit mode, bleed, cutline, punch settings, source dimensions and the original file path.
- Opening an older order without a snapshot falls back to its existing dimensions and quantity plus editor defaults.
- Source previews reload in the background without resetting saved dimensions or quantity. Missing files do not block saving the restored editor data.

## New database deployment

Apply Tests/OrderItemEditorStates.migration.sql before deploying this version. Tests/ApplyOrderItemEditorStatesMigration.ps1 applies the additive migration using the configured database. Existing order data is preserved. The current development database has already received this migration.

## Validation

Tests/OrderSaveRoundTripRegression.cs uses the exact application save transaction body and read queries. It verifies all tables are transactional, creates a new order, checks default and edited state (including multiple items), and rolls back all order data. It then verifies that test orders and items are absent. It requires access to the configured MySQL server and the customer/item shown in the requested example.

Tests/BannerEditorStateRegression.cs checks file-backed preview restore, quantity preservation, and missing source files without changing database data.
