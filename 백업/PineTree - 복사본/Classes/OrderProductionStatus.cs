namespace PineTree.Classes
{
    public enum OrderProductionStatus
    {
        New,
        Design,
        Printing,
        PostProcess,
        PackingWaiting,
        PackingComplete,
        DeliveryComplete,
        SettlementComplete
    }

    public static class OrderProductionStatusHelper
    {
        public static string ToDbText(OrderProductionStatus status)
        {
            switch (status)
            {
                case OrderProductionStatus.New:
                    return "신규";
                case OrderProductionStatus.Design:
                    return "디자인";
                case OrderProductionStatus.Printing:
                    return "출력중";
                case OrderProductionStatus.PostProcess:
                    return "후가공";
                case OrderProductionStatus.PackingWaiting:
                    return "포장대기";
                case OrderProductionStatus.PackingComplete:
                    return "포장완료";
                case OrderProductionStatus.DeliveryComplete:
                    return "납품완료";
                case OrderProductionStatus.SettlementComplete:
                    return "정산완료";
                default:
                    return string.Empty;
            }
        }

        public static bool TryParse(string value, out OrderProductionStatus status)
        {
            string normalized = Normalize(value);
            switch (normalized)
            {
                case "신규":
                    status = OrderProductionStatus.New;
                    return true;
                case "디자인":
                    status = OrderProductionStatus.Design;
                    return true;
                case "출력중":
                    status = OrderProductionStatus.Printing;
                    return true;
                case "후가공":
                    status = OrderProductionStatus.PostProcess;
                    return true;
                case "포장대기":
                    status = OrderProductionStatus.PackingWaiting;
                    return true;
                case "포장완료":
                    status = OrderProductionStatus.PackingComplete;
                    return true;
                case "납품완료":
                    status = OrderProductionStatus.DeliveryComplete;
                    return true;
                case "정산완료":
                    status = OrderProductionStatus.SettlementComplete;
                    return true;
                default:
                    status = OrderProductionStatus.New;
                    return false;
            }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().Replace(" ", string.Empty);
        }
    }
}
