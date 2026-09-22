
using AiGateway.Application.Interfaces;
using Azure;
using System.Diagnostics.Eventing.Reader;
using System.Text;
using System.Text.Json;
namespace AiGateway.Infrastructure.Sse
{
    public sealed class HttpSseEventEmitter : ISseEventEmitter
    {
        private readonly HttpResponse _response;

        public HttpSseEventEmitter(HttpResponse response)
        {
            _response = response;
        }

        public async Task EmitAsync(string eventName, string payload)
        {
            if (_response.HttpContext.RequestAborted.IsCancellationRequested)
                return;

            var ct = CancellationToken.None;
            var optData = "";
            try
            {
                await _response.WriteAsync($"event: {eventName}\n");

                var lines = payload.Replace("\r\n", "\n").Split('\n');

                foreach (var line in lines)
                {
                    await _response.WriteAsync($"data:{line}\n");
                }

              //  await _response.WriteAsync($"data:{payload}\n");
                await _response.WriteAsync("\n");
                await _response.Body.FlushAsync();
            }
            catch
            {
                // conexão encerrada pelo cliente
            }
        }
    }

    public sealed class StreamingTextBuffer
    {
        private readonly StringBuilder _buffer = new();
        private bool _inCodeBlock = false;
        private bool _inMermaidBlock = false;

        private static readonly string[] MermaidStarters =
        {
        "graph", "flowchart", "sequenceDiagram", "classDiagram",
        "stateDiagram", "gantt", "pie", "journey",
        "requirement", "quadrantChart", "timeline"
    };

        public string? Push(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
                return null;

          
            if (_buffer.Length > 0)
            {
                var last = _buffer[^1];
                var first = chunk[0];

                if (char.IsLetterOrDigit(last) && char.IsLetterOrDigit(first))
                {
                    _buffer.Append(' ');
                }
            }

            _buffer.Append(chunk);

            DetectBlocks(chunk);

            if (_inCodeBlock || _inMermaidBlock)
                return null;

            return FlushIfSafe();
        }

        private void DetectBlocks(string chunk)
        {
            if (chunk.Contains("```"))
                _inCodeBlock = !_inCodeBlock;

            if (!_inMermaidBlock)
            {
                foreach (var m in MermaidStarters)
                {
                    if (_buffer.ToString().Contains($"\n{m}") ||
                        _buffer.ToString().StartsWith(m))
                    {
                        _inMermaidBlock = true;
                        break;
                    }
                }
            }

  
            if (_inMermaidBlock && _buffer.ToString().Contains("\n\n"))
                _inMermaidBlock = false;
        }

        private string? FlushIfSafe()
        {
            var text = _buffer.ToString();

            var lastBreak =
                Math.Max(text.LastIndexOf('\n'),
                Math.Max(text.LastIndexOf('.'),
                Math.Max(text.LastIndexOf('!'),
                         text.LastIndexOf('?'))));

            if (lastBreak < 0)
                return null;

            var safe = text[..(lastBreak + 1)];
            _buffer.Remove(0, lastBreak + 1);
            return safe;
        }

        public string FlushFinal()
        {
            var rest = _buffer.ToString();
            _buffer.Clear();
            return rest;
        
}
     

        public async Task ProcessStreamWithSmartBuffer(
       IAsyncEnumerable<string> tokens,
       HttpSseEventEmitter sse,
       StringBuilder respostaFinal)
        {
            var buffer = new StreamingTextBuffer();

            await foreach (var token in tokens)
            {
                respostaFinal.Append(token);
                await sse.EmitAsync("data", token);
              
            }

        } }
    }
