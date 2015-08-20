﻿using Devkoes.Restup.WebServer.Helpers;
using Devkoes.Restup.WebServer.Models.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Devkoes.Restup.WebServer.Builders
{
    public class RestRequestBuilder
    {
        private const string ACCEPT_HEADERKEY = "accept:";

        private static AcceptMediaType DEFAULT_RETURN_MEDIATYPE = AcceptMediaType.JSON;

        // The order represents the priority for a response
        private static readonly IDictionary<string, AcceptMediaType> _acceptHeaderText = new Dictionary<string, AcceptMediaType>()
        {
            ["application/json"] = AcceptMediaType.JSON,
            ["text/xml"] = AcceptMediaType.XML,
            ["application/xml"] = AcceptMediaType.XML
        };

        internal RestRequest Build(string request)
        {
            if (string.IsNullOrWhiteSpace(request))
            {
                throw new ArgumentException("Request should contain the http verb and uri", "request");
            }

            string[] perLine = request.Split('\n');
            
            var verbAndUri = GetVerbAndUriFromRequest(perLine.First());
            var verb = HttpHelpers.GetVerb(verbAndUri.Item1);
            string body = GetBodyFromRequest(request);
            var accHeaders = GetAcceptHeadersFromRequest(perLine);

            return new RestRequest()
            {
                Verb = verb,
                Uri = verbAndUri.Item2,
                Body = body,
                AcceptHeaders = accHeaders
            };
        }

        private static Tuple<string,string> GetVerbAndUriFromRequest(string firstRequestLine)
        {
            string[] requestParts = firstRequestLine.Split(' ');

            if (requestParts.Length < 2)
            {
                throw new ArgumentException("Request should contain the http verb and uri", "request");
            }

            return Tuple.Create(requestParts[0], requestParts[1]);
        }

        private IEnumerable<AcceptMediaType> GetAcceptHeadersFromRequest(string[] requestLines)
        {
            // HTTP 1.1 headers are case insensitive (http://www.w3.org/Protocols/rfc2616/rfc2616.html)
            var lcLines = requestLines.Select(l => l.ToLower());
            var acceptedHeadersQuery = 
                    from line in lcLines
                    where line.StartsWith(ACCEPT_HEADERKEY)
                    from header in _acceptHeaderText
                    where line.Contains(header.Key)
                    select header.Value;

            var accHeaders = acceptedHeadersQuery.ToArray();

            // If no accept header, or no supported accept header, we add the default
            if (!accHeaders.Any())
                accHeaders = new[] { DEFAULT_RETURN_MEDIATYPE };

            return accHeaders;
        }

        private string GetBodyFromRequest(string request)
        {
            string body = null;
            var bodyIndex = request.IndexOf("\r\n\r\n");
            if (bodyIndex != -1 && request.Length > (bodyIndex + 5))
            {
                body = request.Substring(bodyIndex + 4);
            }

            return body;
        }
    }
}