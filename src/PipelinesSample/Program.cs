using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.IO.Pipelines.Networking.Sockets;
using System.IO.Pipelines.Samples;
using System.IO.Pipelines.Text.Primitives;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Formatting;
using System.Threading.Tasks;

namespace PipelinesSample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var listener = new SocketListener();
            listener.OnConnection(OnConnection);
            var endpoint = new IPEndPoint(IPAddress.Loopback, 8081);
            listener.Start(endpoint);

            Console.WriteLine($"Listening on {endpoint}");
            Console.ReadKey();
        }

        private static async Task OnConnection(SocketConnection connection)
        {
            while (true)
            {
                var httpParser = new HttpRequestParser();

                while (true)
                {
                    // Wait for data
                    var result = await connection.Input.ReadAsync();
                    var input = result.Buffer;

                    try
                    {
                        if (input.IsEmpty && result.IsCompleted)
                        {
                            // No more data
                            break;
                        }

                        // Parse the input http request
                        var parseResult = httpParser.ParseRequest(ref input);

                        switch (parseResult)
                        {
                            case HttpRequestParser.ParseResult.Incomplete:
                                if (result.IsCompleted)
                                {
                                    // Didn't get the whole request and the connection ended
                                    throw new EndOfStreamException();
                                }
                                // Need more data
                                continue;
                            case HttpRequestParser.ParseResult.Complete:
                                break;
                            case HttpRequestParser.ParseResult.BadRequest:
                                throw new Exception();
                            default:
                                break;
                        }

                        Console.WriteLine(httpParser.Method.GetAsciiString() + " " + httpParser.Path.GetAsciiString());
                        foreach (var header in httpParser.RequestHeaders)
                        {
                            Console.WriteLine(header.Key + " " + header.Value);
                        }

                        Console.WriteLine();

                        // Writing directly to pooled buffers
                        var output = connection.Output.Alloc();
                        var formatter = new OutputFormatter<WritableBuffer>(output, EncodingData.InvariantUtf8);
                        formatter.Append("HTTP/1.1 200 OK");
                        formatter.Append("\r\nContent-Length: 13");
                        formatter.Append("\r\nContent-Type: text/plain");
                        formatter.Append("\r\n\r\n");
                        formatter.Append("Hello, World!");
                        await output.FlushAsync();

                        httpParser.Reset();
                    }
                    finally
                    {
                        // Consume the input
                        connection.Input.Advance(input.Start, input.End);
                    }
                }
            }
        }
    }
}
