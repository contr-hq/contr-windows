using System.Text;
using Contr.Modules.Containers.Application.Containers;
using ContainersV1 = Contr.Sdk.Containers.V1;

namespace Contr.Modules.Containers.UI.Infrastructure;

/// <summary>Decodes one log stream's UTF-8 chunks.</summary>
/// <remarks>
/// A chunk can end inside a multi-byte sequence; each output stream keeps its own decoder, which
/// holds the partial bytes for that stream's next chunk.
/// </remarks>
internal sealed class LogDecoder
{
    private readonly Decoder _stdout = Encoding.UTF8.GetDecoder();
    private readonly Decoder _stderr = Encoding.UTF8.GetDecoder();

    public ContainerLogEvent Decode(ContainersV1.StreamLogsResponse response)
    {
        var isError = response.Stream == ContainersV1.LogStream.Stderr;
        var decoder = isError ? _stderr : _stdout;
        var bytes = response.Data.Span;
        var chars = new char[decoder.GetCharCount(bytes, flush: false)];
        decoder.GetChars(bytes, chars, flush: false);

        return new ContainerLogEvent.Output(new string(chars), isError);
    }
}
