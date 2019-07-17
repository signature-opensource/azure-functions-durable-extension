// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class DurableHttpResponseJsonConverter : JsonConverter
    {
        private readonly Type[] types;

        public DurableHttpResponseJsonConverter(params Type[] types)
        {
            this.types = types;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanConvert(Type objectType)
        {
            bool value = this.types.Any(t => t == objectType);
            return value;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);

            int codeInt = int.Parse(jObject["StatusCode"].Value<string>());
            HttpStatusCode statusCode = (HttpStatusCode)codeInt;
            DurableHttpResponse durableHttpResponse = new DurableHttpResponse(statusCode);

            Dictionary<string, StringValues> headerDictStringValues = new Dictionary<string, StringValues>();
            Dictionary<string, IEnumerable<string>> headersDictEnumerable = jObject["Headers"].ToObject<Dictionary<string, IEnumerable<string>>>();
            foreach (var header in headersDictEnumerable)
            {
                string key = header.Key;
                string[] headerValues = header.Value.ToArray<string>();
                StringValues values = new StringValues(headerValues);
                headerDictStringValues.Add(key, values);
            }

            durableHttpResponse.Headers = headerDictStringValues;

            durableHttpResponse.Content = jObject["Content"].Value<string>();

            return durableHttpResponse;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
