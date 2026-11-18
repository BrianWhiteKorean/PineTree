using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public interface IOrderSourceEditor : IOrderItemEditor
    {
        event System.EventHandler SourceViewChanged;
        int SourceRotationDegreesClockwise { get; }
        bool SourceFlipHorizontal { get; }
        void RotateSource(int clockwiseDegrees);
        void FlipSourceHorizontally();
        OrderSourceDocument SourceDocument { get; }
        void AttachSourceDocument(OrderSourceDocument document);
    }
}
