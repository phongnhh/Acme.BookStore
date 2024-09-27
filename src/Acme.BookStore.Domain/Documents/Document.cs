using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Acme.BookStore.Documents
{
    public class Document : FullAuditedAggregateRoot<Guid>, IMultiTenant
    {
        public string FileName { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public string MimeType { get; set; } = string.Empty;

        public Guid? TenantId { get; set; }

        protected Document()
        {
        }

        public Document(
            Guid id,
            string fileName,
            long fileSize,
            string mimeType,
            Guid? tenantId
        ) : base(id)
        {
            FileName = fileName;
            FileSize = fileSize;
            MimeType = mimeType;
            TenantId = tenantId;
        }
    }
}
