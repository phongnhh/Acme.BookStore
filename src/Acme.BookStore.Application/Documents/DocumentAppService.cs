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
                    await _blobContainer.SaveAsync(id.ToString(), memoryStream.ToArray(), overrideExisting: true).ConfigureAwait(false);
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

        public async Task<bool> Rename(Guid oldId, Guid newId)
        {
            // Lấy tệp cũ từ blob container
            var currentFile = await _repository.FirstOrDefaultAsync(x => x.Id == oldId);
            if (currentFile != null)
            {
                var fileBytes = await _blobContainer.GetAllBytesOrNullAsync(oldId.ToString());

                if (fileBytes != null)
                {
                    // Tạo tệp mới với tên mới
                    var newFile = new Document(newId, currentFile.FileSize, currentFile.MimeType, currentFile.TenantId);
                    var created = await _repository.InsertAsync(newFile);
                    await _blobContainer.SaveAsync(newId.ToString(), fileBytes, true);

                    // Xóa tệp cũ
                    var result = await _blobContainer.DeleteAsync(oldId.ToString());
                    await _blobContainer.DeleteAsync(oldId.ToString());
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> Delete(Guid id)
        {
            var currentFile = await _repository.FirstOrDefaultAsync(x => x.Id == id);
            if (currentFile != null)
            {
                var result = await _blobContainer.DeleteAsync(id.ToString());
                if (result)
                    await _repository.DeleteAsync(x => x.Id == id);
                return result;
            }

            throw new FileNotFoundException();
        }

        public async Task<long> GetSize(Guid id)
        {
            var currentFile = await _repository.FirstOrDefaultAsync(x => x.Id == id);
            if (currentFile != null)
            {
                var myfile = await _blobContainer.GetAllBytesOrNullAsync(id.ToString());
                return myfile.Length;
            }

            throw new FileNotFoundException();
        }
    }

}

