using System;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    internal interface IOrderCoatingEditor
    {
        string WorkOrderCoating { get; }
    }

    public interface IOrderItemEditor
    {
        event EventHandler OrderValuesChanged;
        bool IsProcessing { get; }
        decimal WidthMm { get; }
        decimal HeightMm { get; }
        decimal Quantity { get; }
        decimal AreaQuantity { get; }
        void SetOrderQuantity(decimal quantity);
        BannerOrderEditorState CaptureEditorState();
        void RestoreEditorState(BannerOrderEditorState state);
    }
}
