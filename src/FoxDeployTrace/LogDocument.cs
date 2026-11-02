using System.Text;

namespace FoxDeployTrace;

internal sealed class LogDocument
{
    private readonly string _text;
    private readonly int[] _lineStarts;

    private LogDocument(string text, int[] lineStarts)
    {
        _text = text;
        _lineStarts = lineStarts;
    }

    public int LineCount => _lineStarts.Length;

    public static Task<LogDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return Parse(text, cancellationToken);
        }, cancellationToken);
    }

    public static LogDocument Parse(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (text.Length == 0)
        {
            return new LogDocument(text, []);
        }

        var count = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if ((i & 65535) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (text[i] is not ('\r' or '\n'))
            {
                continue;
            }

            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }

            if (i + 1 < text.Length)
            {
                count++;
            }
        }

        var starts = new int[count];
        var line = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if ((i & 65535) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (text[i] is not ('\r' or '\n'))
            {
                continue;
            }

            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }

            if (i + 1 < text.Length)
            {
                starts[line++] = i + 1;
            }
        }

        return new LogDocument(text, starts);
    }

    public ReadOnlySpan<char> GetLine(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, LineCount);
        var start = _lineStarts[index];
        var end = index + 1 < LineCount ? _lineStarts[index + 1] : _text.Length;
        if (end > start && _text[end - 1] == '\n')
        {
            end--;
        }
        if (end > start && _text[end - 1] == '\r')
        {
            end--;
        }
        return _text.AsSpan(start, end - start);
    }
}
