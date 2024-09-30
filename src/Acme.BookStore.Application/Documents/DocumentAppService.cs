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
                var newFile = new Document(id, file.FileName, file.Length, file.ContentType, CurrentTenant.Id);
                var created = await _repository.InsertAsync(newFile);
                try
                {
                    await _blobContainer.SaveAsync(file.FileName, memoryStream.ToArray(), overrideExisting: true).ConfigureAwait(false);
                }
                catch (Exception ex)
                {

                }
                output.Add(ObjectMapper.Map<Document, DocumentDto>(newFile));
            }

            return output;
        }

        public async Task<FileResult> Download(string fileName)
        {
            var currentFile = await _repository.FirstOrDefaultAsync(x => x.FileName.Equals(fileName));
            if (currentFile != null)
            {
                var myfile = await _blobContainer.GetAllBytesOrNullAsync(currentFile.FileName);
                return new FileContentResult(myfile, currentFile.MimeType);
            }

            throw new FileNotFoundException();
        }

        public async Task<FileResult> Get(Guid id)
        {
            var currentFile = await _repository.FirstOrDefaultAsync(x => x.Id == id);
            if (currentFile != null)
            {
                var myfile = await _blobContainer.GetAllBytesOrNullAsync(currentFile.FileName);
                return new FileContentResult(myfile, currentFile.MimeType);
            }

            throw new FileNotFoundException();
        }

        public async Task<bool> Rename(string oldFilePath, string newFilePath)
        {
            // Lấy tệp cũ từ blob container
            var currentFile = await _repository.FirstOrDefaultAsync(x => x.FileName.Equals(oldFilePath));
            if (currentFile != null)
            {
                var fileBytes = await _blobContainer.GetAllBytesOrNullAsync(oldFilePath);

                if (fileBytes != null)
                {
                    // Tạo tệp mới với tên mới
                    var newFile = new Document(Guid.NewGuid(), newFilePath, currentFile.FileSize, currentFile.MimeType, currentFile.TenantId);
                    var created = await _repository.InsertAsync(newFile);
                    await _blobContainer.SaveAsync(newFilePath, fileBytes, true);

                    // Xóa tệp cũ
                    var result = await _blobContainer.DeleteAsync(oldFilePath);
                    await _repository.DeleteAsync(x => x.Id == currentFile.Id);
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
                var result = await _blobContainer.DeleteAsync(currentFile.FileName);
                if (result)
                    await _repository.DeleteAsync(x => x.Id == id);
                return result;
            }

            throw new FileNotFoundException();
        }

        public async Task<long> Size(Guid id)
        {
            var currentFile = await _repository.FirstOrDefaultAsync(x => x.Id == id);
            if (currentFile != null)
            {
                var myfile = await _blobContainer.GetAllBytesOrNullAsync(currentFile.FileName);
                return myfile!.Length;
            }

            throw new FileNotFoundException();
        }
    }

}

