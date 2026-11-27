-- Apply after OrderInfo.DeliveryAddress has been added.
ALTER TABLE OrderInfo
    ADD COLUMN ShippingDetailsJSON LONGTEXT NULL COMMENT 'Consignee and shipper details';
