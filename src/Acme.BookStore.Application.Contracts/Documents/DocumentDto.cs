using System;
using Volo.Abp.Application.Dtos;

namespace Acme.BookStore.Documents
{
    public class DocumentDto : EntityDto<Guid>
    {
        public long FileSize { get; set; }

        public string FileUrl { get; set; } = string.Empty;

        public string MimeType { get; set; } = string.Empty;
    }
}
