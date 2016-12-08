using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.IO.Pipelines.Text.Primitives;

namespace System.IO.Pipelines.Samples.Http
{
    // This isn't optimized
    public class RequestHeaderDictionary : IEnumerable<KeyValuePair<string, string>>
    {
        private static readonly byte[] ContentLengthKeyBytes = Encoding.ASCII.GetBytes("CONTENT-LENGTH");
        private static readonly byte[] ContentTypeKeyBytes = Encoding.ASCII.GetBytes("CONTENT-TYPE");
        private static readonly byte[] AcceptBytes = Encoding.ASCII.GetBytes("ACCEPT");
        private static readonly byte[] AcceptLanguageBytes = Encoding.ASCII.GetBytes("ACCEPT-LANGUAGE");
        private static readonly byte[] AcceptEncodingBytes = Encoding.ASCII.GetBytes("ACCEPT-ENCODING");
        private static readonly byte[] HostBytes = Encoding.ASCII.GetBytes("HOST");
        private static readonly byte[] ConnectionBytes = Encoding.ASCII.GetBytes("CONNECTION");
        private static readonly byte[] CacheControlBytes = Encoding.ASCII.GetBytes("CACHE-CONTROL");
        private static readonly byte[] UserAgentBytes = Encoding.ASCII.GetBytes("USER-AGENT");
        private static readonly byte[] UpgradeInsecureRequests = Encoding.ASCII.GetBytes("UPGRADE-INSECURE-REQUESTS");

        private Dictionary<string, HeaderValue> _headers = new Dictionary<string, HeaderValue>(10, StringComparer.OrdinalIgnoreCase);

        public string this[string key]
        {
            get
            {
                string values;
                TryGetValue(key, out values);
                return values;
            }

            set
            {
                SetHeader(key, value);
            }
        }

        public int Count => _headers.Count;

        public void SetHeader(ref ReadableBuffer key, ref ReadableBuffer value)
        {
            string headerKey = GetHeaderKey(ref key);
            _headers[headerKey] = new HeaderValue
            {
                Raw = value.Preserve()
            };
        }

        public ReadableBuffer GetHeaderRaw(string key)
        {
            HeaderValue value;
            if (_headers.TryGetValue(key, out value))
            {
                return value.Raw.Value.Buffer;
            }
            return default(ReadableBuffer);
        }

        private string GetHeaderKey(ref ReadableBuffer key)
        {
            // Uppercase the things
            foreach (var memory in key)
            {
                var data = memory.Span;
                for (int i = 0; i < memory.Length; i++)
                {
                    var mask = IsAlpha(data[i]) ? 0xdf : 0xff;
                    data[i] = (byte)(data[i] & mask);
                }
            }

            if (EqualsIgnoreCase(ref key, AcceptBytes))
            {
                return "Accept";
            }

            if (EqualsIgnoreCase(ref key, AcceptEncodingBytes))
            {
                return "Accept-Encoding";
            }

            if (EqualsIgnoreCase(ref key, AcceptLanguageBytes))
            {
                return "Accept-Language";
            }

            if (EqualsIgnoreCase(ref key, HostBytes))
            {
                return "Host";
            }

            if (EqualsIgnoreCase(ref key, UserAgentBytes))
            {
                return "User-Agent";
            }

            if (EqualsIgnoreCase(ref key, CacheControlBytes))
            {
                return "Cache-Control";
            }

            if (EqualsIgnoreCase(ref key, ConnectionBytes))
            {
                return "Connection";
            }

            if (EqualsIgnoreCase(ref key, UpgradeInsecureRequests))
            {
                return "Upgrade-Insecure-Requests";
            }

            return key.GetAsciiString();
        }

        private bool EqualsIgnoreCase(ref ReadableBuffer key, byte[] buffer)
        {
            if (key.Length != buffer.Length)
            {
                return false;
            }

            return key.Equals(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAlpha(byte b)
        {
            return b >= 'a' && b <= 'z' || b >= 'A' && b <= 'Z';
        }

        private void SetHeader(string key, string value)
        {
            _headers[key] = new HeaderValue
            {
                Value = value
            };
        }

        public void Add(string key, string value)
        {
            SetHeader(key, value);
        }

        public void Clear()
        {
            _headers.Clear();
        }

        public bool ContainsKey(string key)
        {
            return _headers.ContainsKey(key);
        }

        public void Reset()
        {
            foreach (var pair in _headers)
            {
                pair.Value.Raw?.Dispose();
            }

            _headers.Clear();
        }

        public bool Remove(string key)
        {
            return _headers.Remove(key);
        }

        public bool TryGetValue(string key, out string value)
        {
            HeaderValue headerValue;
            if (_headers.TryGetValue(key, out headerValue))
            {
                value = headerValue.GetValue();
                return true;
            }

            value = null;
            return false;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _headers.Select(h => new KeyValuePair<string, string>(h.Key, h.Value.GetValue())).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private struct HeaderValue
        {
            public PreservedBuffer? Raw;
            public string Value;
            private bool _valueSet;

            public string GetValue()
            {
                if (!_valueSet)
                {
                    _valueSet = true;
                    if (!Raw.HasValue)
                    {
                        return null;
                    }

                    Value = Raw.Value.Buffer.GetAsciiString();
                }

                return Value;
            }
        }
    }
}
