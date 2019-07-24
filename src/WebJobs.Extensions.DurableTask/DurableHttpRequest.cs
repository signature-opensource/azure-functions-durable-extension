// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Request used to make an HTTP call through Durable Functions.
    /// </summary>
    public class DurableHttpRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DurableHttpRequest"/> class.
        /// </summary>
        /// <param name="method">Method used for HTTP request.</param>
        /// <param name="uri">Uri used to make the HTTP request.</param>
        public DurableHttpRequest(HttpMethod method, Uri uri)
        {
            this.Method = method;
            this.Uri = uri;
            this.Headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// HttpMethod used in the HTTP request made by the Durable Function.
        /// </summary>
        [JsonProperty("method")]
        [JsonConverter(typeof(HttpMethodConverter))]
        public HttpMethod Method { get; set; }

        /// <summary>
        /// Uri used in the HTTP request made by the Durable Function.
        /// </summary>
        [JsonProperty("uri")]
        public Uri Uri { get; set; }

        /// <summary>
        /// Headers passed with the HTTP request made by the Durable Function.
        /// </summary>
        [JsonProperty("headers")]
        [JsonConverter(typeof(HttpHeadersConverter))]
        public IDictionary<string, StringValues> Headers { get; set; }

        /// <summary>
        /// Content passed with the HTTP request made by the Durable Function.
        /// </summary>
        [JsonProperty("content")]
        public string Content { get; set; }

        /// <summary>
        /// Information needed to get a token for a specified service.
        /// </summary>
        [JsonProperty("tokenSource", TypeNameHandling = TypeNameHandling.Auto)]
        public ITokenSource TokenSource { get; set; }

        /// <summary>
        /// Specifies whether the Durable HTTP APIs should automatically
        /// handle the asynchronous HTTP pattern.
        /// </summary>
        [JsonProperty("asynchronousPatternEnabled")]
        public bool AsynchronousPatternEnabled { get; set; } = true;

        private class HttpMethodConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(HttpMethod);
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    return new HttpMethod((string)JToken.Load(reader));
                }

                // Default for JSON that's either missing or not understood
                return HttpMethod.Get;
            }

            public override void WriteJson(
                JsonWriter writer,
                object value,
                JsonSerializer serializer)
            {
                HttpMethod method = (HttpMethod)value ?? HttpMethod.Get;
                writer.WriteValue(method.ToString());
            }
        }

        private class HttpHeadersConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(IDictionary<string, StringValues>).IsAssignableFrom(objectType);
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer)
            {
                var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

                if (reader.TokenType != JsonToken.StartObject)
                {
                    return headers;
                }

                var valueList = new List<string>();
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    string propertyName = (string)reader.Value;

                    reader.Read();

                    StringValues values;
                    if (reader.TokenType == JsonToken.String)
                    {
                        values = new StringValues((string)reader.Value);
                    }
                    else if (reader.TokenType == JsonToken.StartArray)
                    {
                        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                        {
                            valueList.Add((string)reader.Value);
                        }

                        values = new StringValues(valueList.ToArray());
                        valueList.Clear();
                    }

                    headers[propertyName] = values;
                }

                return headers;
            }

            public override void WriteJson(
                JsonWriter writer,
                object value,
                JsonSerializer serializer)
            {
                // The default serialization works for StringValues
                serializer.Serialize(writer, value);
            }
        }
    }
}
