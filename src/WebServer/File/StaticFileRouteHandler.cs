﻿using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Devkoes.HttpMessage;
using Devkoes.HttpMessage.Models.Schemas;

namespace Devkoes.Restup.WebServer.File
{
    public class StaticFileRouteHandler : IRouteHandler
    {
        private readonly string _basePath;
        private readonly IFileSystem _fileSystem;

        public StaticFileRouteHandler(string basePath = null, IFileSystem fileSystem = null)
        {
            _basePath = GetAbsoluteBasePathUri(basePath ?? string.Empty);
            _fileSystem = fileSystem ?? new PhysicalFileSystem();

            if (!_fileSystem.Exists(_basePath))
                throw new Exception($"Path at {_basePath} could not be found.");
        }

        private static string GetAbsoluteBasePathUri(string relativeOrAbsoluteBasePath)
        {
            relativeOrAbsoluteBasePath = relativeOrAbsoluteBasePath.TrimStart('\\');

            if (Path.IsPathRooted(relativeOrAbsoluteBasePath))
                return relativeOrAbsoluteBasePath;

            return Path.Combine(Package.Current.InstalledLocation.Path, relativeOrAbsoluteBasePath);
        }

        public async Task<HttpServerResponse> HandleRequest(IHttpServerRequest request)
        {
            if(request.Method != HttpMethod.GET)
                return GetMethodNotAllowedResponse(request.Method);

            var localFilePath = GetFilePath(request.Uri);
            var absoluteFilePath = GetAbsoluteFilePath(localFilePath);

            // todo: add validation for invalid path characters / invalid filename characters
            IFile item;
            try
            {
                item = await _fileSystem.GetFileFromPathAsync(absoluteFilePath);
            }
            catch (FileNotFoundException)
            {
                return GetFileNotFoundResponse(localFilePath);
            }

            return await GetHttpResponse(item);
        }

        private static HttpServerResponse GetFileNotFoundResponse(string localPath)
        {
            var notFoundResponse = new HttpServerResponse(new Version(1, 1), HttpResponseStatus.NotFound)
            {
                Content = Encoding.UTF8.GetBytes($"File at {localPath} not found."),
                ContentType = "text/plain",
                ContentCharset = "utf-8"
            };
            return notFoundResponse;
        }

        private static HttpServerResponse GetMethodNotAllowedResponse(HttpMethod? method)
        {
            // https://www.w3.org/Protocols/rfc2616/rfc2616-sec10.html
            // The method specified in the Request-Line is not allowed for the resource identified by the Request-URI. The response MUST include an Allow header containing a list of valid methods for the requested resource.
            var methodNotAllowedResponse = new HttpServerResponse(new Version(1, 1), HttpResponseStatus.MethodNotAllowed)
            {
                Content = Encoding.UTF8.GetBytes($"Unsupported method {method}."),
                Allow = new [] { HttpMethod.GET },
                ContentType = "text/plain",
                ContentCharset = "utf-8"
            };
            return methodNotAllowedResponse;
        }

        private async Task<HttpServerResponse> GetHttpResponse(IFile item)
        {
            // todo: do validation on file extension, probably want to have a whitelist
            using (var inputStream = await item.OpenStreamForReadAsync())
            {
                var memoryStream = new MemoryStream();
                // slightly inefficient since we're reading the whole file into memory... Should probably expose the http response stream directly somehow
                await inputStream.CopyToAsync(memoryStream);
                return new HttpServerResponse(new Version(1, 1), HttpResponseStatus.OK)
                {
                    Content = memoryStream.ToArray(), // and make another copy of the file... for now this will do
                    ContentType = item.ContentType
                };
            }
        }

        private static string GetFilePath(Uri uri)
        {
            var uriString = uri.ToString();
            var localPath = GetLocalPath(uriString);
            
            localPath = ParseLocalPath(localPath);
            var filePath = localPath.Replace('/', '\\');
            return filePath;
        }

        private string GetAbsoluteFilePath(string localFilePath)
        {
            var absoluteFilePath = Path.Combine(_basePath, localFilePath);
            return absoluteFilePath;
        }

        private static string GetLocalPath(string uriString)
        {
            return uriString.Split('?')[0];
        }

        private static string ParseLocalPath(string localPath)
        {
            if (localPath.EndsWith("/"))
                localPath += "index.html";

            if (localPath.StartsWith("/"))
                localPath = localPath.Substring(1);
            return localPath;
        }
    }
}