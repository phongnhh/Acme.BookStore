using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net;
using System;
using SMBLibrary.Client;
using SMBLibrary;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using static System.Net.WebRequestMethods;
using Volo.Abp.Http;
using Volo.Abp.Timing;


namespace Acme.BookStore.Controllers.Files
{
    [RemoteService]
    [Area("app")]
    [ControllerName("File")]
    [Route("api/app/files")]

    public class FileController : AbpController
    {
        private IPAddress iPAddress;
        private string username;
        private string password;
        private string shared;
        public FileController()
        {
            iPAddress = IPAddress.Parse("172.27.199.182");
            username = "phongnhh";
            password = "12345678";
            shared = "MySambaServer";
        }

        [HttpGet("listshare")]
        public List<string> GetListShare()
        {
            List<string> shares = new List<string>();
            SMB2Client client = new SMB2Client();
            bool isConnected = client.Connect(iPAddress, SMBTransportType.DirectTCPTransport);
            if (isConnected)
            {
                NTStatus status = client.Login(String.Empty, username, password);
                if (status == NTStatus.STATUS_SUCCESS)
                {
                    shares = client.ListShares(out status);
                    client.Logoff();
                }
                client.Disconnect();
            }

            return shares;
        }

        [HttpGet("listfile")]
        public async Task<List<QueryDirectoryFileInformation>?> GetListFile()
        {
            object directoryHandle = null;
            List<QueryDirectoryFileInformation> fileList = new List<QueryDirectoryFileInformation>();

            SMB2Client client = new SMB2Client();
            bool isConnected = client.Connect(iPAddress, SMBTransportType.DirectTCPTransport);
            if (isConnected)
            {
                NTStatus status = client.Login(String.Empty, username, password);
                if (status == NTStatus.STATUS_SUCCESS)
                {
                    //
                    ISMBFileStore fileStore = client.TreeConnect(shared, out status);
                    if (status == NTStatus.STATUS_SUCCESS)
                    {
                        FileStatus fileStatus;
                        status = fileStore.CreateFile(out directoryHandle, out fileStatus, String.Empty,
                            AccessMask.GENERIC_READ, SMBLibrary.FileAttributes.Directory,
                            ShareAccess.Read | ShareAccess.Write, CreateDisposition.FILE_OPEN,
                            CreateOptions.FILE_DIRECTORY_FILE, null);
                        if (status == NTStatus.STATUS_SUCCESS)
                        {
                            status = fileStore.QueryDirectory(out fileList, directoryHandle, "*",
                                FileInformationClass.FileFullDirectoryInformation);
                            foreach (var info in fileList)
                            {
                                var check = info.FileInformationClass;
                            }
                            status = fileStore.CloseFile(directoryHandle);
                        }
                    }
                    status = fileStore.Disconnect();
                    //

                    client.Logoff();
                }
                client.Disconnect();
            }

            return fileList;
        }

        [HttpPost("download")]
        public async Task<FileResult> Download(string filePath)
        {
            byte[] myfile = new byte[0];
            string mimeType = string.Empty;
            try
            {
                SMB2Client client = new SMB2Client();
                bool isConnected = client.Connect(iPAddress, SMBTransportType.DirectTCPTransport);
                if (isConnected)
                {
                    NTStatus status = client.Login(String.Empty, username, password);
                    if (status == NTStatus.STATUS_SUCCESS)
                    {
                        //
                        ISMBFileStore fileStore = client.TreeConnect(shared, out status);
                        object fileHandle;
                        FileStatus fileStatus;
                        status = fileStore.CreateFile(out fileHandle, out fileStatus, filePath,
                            AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE, SMBLibrary.FileAttributes.Normal,
                            ShareAccess.Read, CreateDisposition.FILE_OPEN,
                            CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);

                        if (status == NTStatus.STATUS_SUCCESS)
                        {
                            System.IO.MemoryStream stream = new System.IO.MemoryStream();
                            byte[] data;
                            long bytesRead = 0;
                            while (true)
                            {
                                status = fileStore.ReadFile(out data, fileHandle, bytesRead, (int)client.MaxReadSize);
                                if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_END_OF_FILE)
                                {
                                    throw new Exception("Failed to read from file");
                                }

                                if (status == NTStatus.STATUS_END_OF_FILE || data.Length == 0)
                                {
                                    break;
                                }
                                bytesRead += data.Length;
                                stream.Write(data, 0, data.Length);
                            }
                            // Prepare to return the file content
                            stream.Position = 0; // Reset the stream position to the beginning
                            myfile = stream.ToArray(); // Convert stream to byte array
                            mimeType = GetMimeType(filePath); // Get the MIME type

                        }

                        status = fileStore.CloseFile(fileHandle);
                        status = fileStore.Disconnect();
                        //

                        client.Logoff();
                    }
                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
            }

            return new FileContentResult(myfile, mimeType);
        }

        private string GetMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".txt" => "text/plain",
                ".jpg" => "image/jpeg",
                ".png" => "image/png",
                ".pdf" => "application/pdf",
                ".json" => "application/json",
                // Add more mappings as needed
                _ => "application/octet-stream", // Default binary type
            };
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                SMB2Client client = new SMB2Client();
                bool isConnected = client.Connect(iPAddress, SMBTransportType.DirectTCPTransport);
                if (isConnected)
                {
                    NTStatus status = client.Login(String.Empty, username, password);
                    if (status == NTStatus.STATUS_SUCCESS)
                    {
                        //
                        ISMBFileStore fileStore = client.TreeConnect(shared, out status);
                        string filePath = file.FileName;
                        object fileHandle;
                        FileStatus fileStatus;
                        status = fileStore.CreateFile(out fileHandle, out fileStatus, filePath,
                            AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE, SMBLibrary.FileAttributes.Normal,
                            ShareAccess.None, CreateDisposition.FILE_CREATE,
                            CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
                        if (status == NTStatus.STATUS_SUCCESS)
                        {
                            int numberOfBytesWritten;
                            byte[] data = await ConvertToByteArray(file);
                            status = fileStore.WriteFile(out numberOfBytesWritten, fileHandle, 0, data);
                            if (status != NTStatus.STATUS_SUCCESS)
                            {
                                throw new Exception("Failed to write to file");
                            }
                            status = fileStore.CloseFile(fileHandle);
                        }
                        status = fileStore.Disconnect();
                        //

                        client.Logoff();
                    }
                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
            }

            return Ok();
        }

        private static async Task<byte[]> ConvertToByteArray(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return new byte[0];
            }

            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }


        [HttpPost("rename")]
        public async Task<bool> Rename(string oldFilePath, string newFilePath)
        {
            try
            {
                SMB2Client client = new SMB2Client();
                bool isConnected = client.Connect(iPAddress, SMBTransportType.DirectTCPTransport);
                if (isConnected)
                {
                    NTStatus status = client.Login(String.Empty, username, password);
                    if (status == NTStatus.STATUS_SUCCESS)
                    {
                        //
                        ISMBFileStore fileStore = client.TreeConnect(shared, out status);
                        object fileHandle;
                        FileStatus fileStatus;
                        status = fileStore.CreateFile(out fileHandle, out fileStatus, oldFilePath,
                            AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE, SMBLibrary.FileAttributes.Normal,
                            ShareAccess.Read, CreateDisposition.FILE_OPEN,
                            CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);

                        if (status == NTStatus.STATUS_SUCCESS)
                        {
                            System.IO.MemoryStream stream = new System.IO.MemoryStream();
                            byte[] data;
                            long bytesRead = 0;
                            while (true)
                            {
                                status = fileStore.ReadFile(out data, fileHandle, bytesRead, (int)client.MaxReadSize);
                                if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_END_OF_FILE)
                                {
                                    throw new Exception("Failed to read from file");
                                }

                                if (status == NTStatus.STATUS_END_OF_FILE || data.Length == 0)
                                {
                                    break;
                                }
                                bytesRead += data.Length;
                                stream.Write(data, 0, data.Length);
                            }
                            // Prepare to return the file content
                            stream.Position = 0; // Reset the stream position to the beginning
                            data = stream.ToArray(); // Convert stream to byte array


                            // upload file to new position
                            object newFileHandle;
                            FileStatus newFileStatus;
                            status = fileStore.CreateFile(out newFileHandle, out newFileStatus, newFilePath,
                                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE, SMBLibrary.FileAttributes.Normal,
                                ShareAccess.None, CreateDisposition.FILE_CREATE,
                                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
                            if (status == NTStatus.STATUS_SUCCESS)
                            {
                                int numberOfBytesWritten;
                                status = fileStore.WriteFile(out numberOfBytesWritten, newFileHandle, 0, data);
                                if (status != NTStatus.STATUS_SUCCESS)
                                {
                                    throw new Exception("Failed to write to file");
                                }
                                status = fileStore.CloseFile(newFileHandle);
                            }
                            //

                        }

                        status = fileStore.Disconnect();
                        //

                        client.Logoff();
                    }
                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                return false;
            }

            return await Delete(oldFilePath);
        }

        [HttpPost("getsize")]
        public async Task<long> GetSize(string filePath)
        {
            byte[] myfile = new byte[0];
            string mimeType = string.Empty;
            try
            {
                SMB2Client client = new SMB2Client();
                bool isConnected = client.Connect(iPAddress, SMBTransportType.DirectTCPTransport);
                if (isConnected)
                {
                    NTStatus status = client.Login(String.Empty, username, password);
                    if (status == NTStatus.STATUS_SUCCESS)
                    {
                        //
                        ISMBFileStore fileStore = client.TreeConnect(shared, out status);
                        object fileHandle;
                        FileStatus fileStatus;
                        status = fileStore.CreateFile(out fileHandle, out fileStatus, filePath,
                            AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE, SMBLibrary.FileAttributes.Normal,
                            ShareAccess.Read, CreateDisposition.FILE_OPEN,
                            CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);

                        if (status == NTStatus.STATUS_SUCCESS)
                        {
                            System.IO.MemoryStream stream = new System.IO.MemoryStream();
                            byte[] data;
                            long bytesRead = 0;
                            while (true)
                            {
                                status = fileStore.ReadFile(out data, fileHandle, bytesRead, (int)client.MaxReadSize);
                                if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_END_OF_FILE)
                                {
                                    throw new Exception("Failed to read from file");
                                }

                                if (status == NTStatus.STATUS_END_OF_FILE || data.Length == 0)
                                {
                                    break;
                                }
                                bytesRead += data.Length;
                                stream.Write(data, 0, data.Length);
                            }
                            // Prepare to return the file content
                            stream.Position = 0; // Reset the stream position to the beginning
                            myfile = stream.ToArray(); // Convert stream to byte array
                        }

                        status = fileStore.CloseFile(fileHandle);
                        status = fileStore.Disconnect();
                        //

                        client.Logoff();
                    }
                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
            }

            return myfile.LongLength;
        }

        [HttpGet("delete")]
        public async Task<bool> Delete(string filePath)
        {
            bool deleteSucceeded = false;
            SMB2Client client = new SMB2Client();
            bool isConnected = client.Connect(iPAddress, SMBTransportType.DirectTCPTransport);
            if (isConnected)
            {
                NTStatus status = client.Login(String.Empty, username, password);
                if (status == NTStatus.STATUS_SUCCESS)
                {
                    //
                    ISMBFileStore fileStore = client.TreeConnect(shared, out status);
                    object fileHandle;
                    FileStatus fileStatus;
                    status = fileStore.CreateFile(out fileHandle, out fileStatus, filePath,
                        AccessMask.GENERIC_WRITE | AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                        SMBLibrary.FileAttributes.Normal, ShareAccess.None, CreateDisposition.FILE_OPEN,
                        CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);

                    if (status == NTStatus.STATUS_SUCCESS)
                    {
                        FileDispositionInformation fileDispositionInformation = new FileDispositionInformation();
                        fileDispositionInformation.DeletePending = true;
                        status = fileStore.SetFileInformation(fileHandle, fileDispositionInformation);
                        deleteSucceeded = (status == NTStatus.STATUS_SUCCESS);
                        status = fileStore.CloseFile(fileHandle);
                    }
                    status = fileStore.Disconnect();
                    //

                    client.Logoff();
                }
                client.Disconnect();
            }

            return deleteSucceeded;
        }

    }
}
