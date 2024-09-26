using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;

namespace Acme.BookStore.Documents
{
    public class DocumentAppService : BookStoreAppService
    {
        private readonly IBlobContainer<DocumentContainer> _blobContainer;
        private readonly IRepository<Document, Guid> _repository;
        public DocumentAppService(IRepository<Document, Guid> repository, IBlobContainer<DocumentContainer> blobContainer)
        {
            _repository = repository;
            _blobContainer = blobContainer;
        }

        public async Task<List<DocumentDto>> Upload([FromForm] List<IFormFile> files)
        {
            var output = new List<DocumentDto>();
            foreach (var file in files)
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream).ConfigureAwait(false);
                var id = Guid.NewGuid();
                var newFile = new Document(id, file.Length, file.ContentType, CurrentTenant.Id);
                var created = await _repository.InsertAsync(newFile);
                try
                {
                    await _blobContainer.SaveAsync($"myfolder/{id.ToString()}", memoryStream.ToArray()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {

                }
                output.Add(ObjectMapper.Map<Document, DocumentDto>(newFile));
            }

            return output;
        }

        public async Task<FileResult> Get(Guid id)
        {
            var currentFile = await _repository.FirstOrDefaultAsync(x => x.Id == id);
            if (currentFile != null)
            {
                var myfile = await _blobContainer.GetAllBytesOrNullAsync(id.ToString());
                return new FileContentResult(myfile, currentFile.MimeType);
            }

            throw new FileNotFoundException();
        }
    }

}
