using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Acme.BookStore.Documents
{
    public class Document : FullAuditedAggregateRoot<Guid>, IMultiTenant
    {
        public long FileSize { get; set; }

        public string MimeType { get; set; } = string.Empty;

        public Guid? TenantId { get; set; }

        protected Document()
        {
        }

        public Document(
            Guid id,
            long fileSize,
            string mimeType,
            Guid? tenantId
        ) : base(id)
        {
            FileSize = fileSize;
            MimeType = mimeType;
            TenantId = tenantId;
        }
    }
}
