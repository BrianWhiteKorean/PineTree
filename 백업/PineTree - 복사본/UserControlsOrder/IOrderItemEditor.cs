using System;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public interface IOrderItemEditor
    {
        event EventHandler OrderValuesChanged;
        bool IsProcessing { get; }
        decimal WidthMm { get; }
        decimal HeightMm { get; }
        decimal Quantity { get; }
        decimal AreaQuantity { get; }
        BannerOrderEditorState CaptureEditorState();
        void RestoreEditorState(BannerOrderEditorState state);
    }
}
