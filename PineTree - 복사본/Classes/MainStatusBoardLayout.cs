using System.Collections.Generic;
using System.Drawing;

namespace PineTree.Classes
{
    public static class MainStatusBoardLayout
    {
        // Coordinates are based on the current BackMain_Sample.png canvas: 1912 x 1004.
        public static readonly IReadOnlyList<StatusBoardCountTarget> CountTargets =
            new List<StatusBoardCountTarget>
            {
                new StatusBoardCountTarget(OrderProductionStatus.New, new Rectangle(405, 230, 110, 75)),
                new StatusBoardCountTarget(OrderProductionStatus.Printing, new Rectangle(645, 230, 110, 75)),
                new StatusBoardCountTarget(OrderProductionStatus.PostProcess, new Rectangle(885, 230, 110, 75)),
                new StatusBoardCountTarget(OrderProductionStatus.PackingWaiting, new Rectangle(1125, 230, 110, 75)),
                new StatusBoardCountTarget(OrderProductionStatus.PackingComplete, new Rectangle(1365, 230, 110, 75))
            };
    }

    public class StatusBoardCountTarget
    {
        public StatusBoardCountTarget(OrderProductionStatus status, Rectangle bounds)
        {
            Status = status;
            Bounds = bounds;
        }

        public OrderProductionStatus Status { get; private set; }

        public Rectangle Bounds { get; private set; }
    }
}
