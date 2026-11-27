using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using PineTree.PdfEngine;

namespace PineTree.Classes
{
    internal sealed class WorkOrderPage
    {
        public Image Image { get; set; }
        public int PageCount { get; set; }
    }

    // Keep PDF engine types out of UI method signatures and async state machines.
    // Costura assembly loading and first-call JIT must happen on a worker thread too.
    internal static class WorkOrderPreviewService
    {
        public static Task WarmUpAsync(CancellationToken token)
        {
            return Task.Run(() => WarmUpCoreAsync(token), token);
        }

        public static Task<WorkOrderPage> RenderAsync(string source, int page, int pixels, CancellationToken token)
        {
            return Task.Run(() => RenderCoreAsync(source, page, pixels, token), token);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Task WarmUpCoreAsync(CancellationToken token)
        {
            return OrderDocumentService.PrepareRuntimeAsync(token);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<WorkOrderPage> RenderCoreAsync(string source, int page, int pixels, CancellationToken token)
        {
            var result = await OrderDocumentService.PreviewAsync(source, page, pixels, true, token).ConfigureAwait(false);
            return new WorkOrderPage { Image = result.PreviewImage, PageCount = result.PageCount };
        }
    }
}
