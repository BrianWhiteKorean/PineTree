using System;
using System.IO;
using System.Text.RegularExpressions;

namespace PineTree.Classes
{
    public sealed class OrderServerFile
    {
        public string StorageKey { get; set; } = "BANNER";
        public string FileId { get; set; }
        public string OriginalFileName { get; set; }
        public long Length { get; set; }
        public string Sha256 { get; set; }
        // Set on generated PDFs. Older generated files were saved at final print size.
        public decimal? RipScale { get; set; }

        public OrderServerFile Copy() { return (OrderServerFile)MemberwiseClone(); }

        public void Validate()
        {
            if (StorageKey != "BANNER" || string.IsNullOrEmpty(FileId)
                || !Regex.IsMatch(FileId, @"\A[a-f0-9]{64}\.(pdf|eps|ai|png|jpg|jpeg)\z")
                || string.IsNullOrEmpty(Sha256) || FileId.Split('.')[0] != Sha256 || Length <= 0
                || RipScale < 1m || string.IsNullOrWhiteSpace(OriginalFileName)
                || OriginalFileName != Path.GetFileName(OriginalFileName)
                || OriginalFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || !string.Equals(Path.GetExtension(OriginalFileName), Path.GetExtension(FileId), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("저장된 서버 파일정보가 올바르지 않습니다.");
        }
    }
}
