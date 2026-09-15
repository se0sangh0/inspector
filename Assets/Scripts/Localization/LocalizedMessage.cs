using System;
using System.Linq;

/// <summary>런 기록은 번역된 문장 대신 원문과 확정 당시 인자를 보관한다. 영속 저장 스키마는 변경하지 않는다.</summary>
public sealed class LocalizedMessage
{
    public string Key { get; }
    private readonly object[] _arguments;

    public LocalizedMessage(string key, params object[] arguments)
    {
        Key = key ?? string.Empty;
        _arguments = arguments == null ? Array.Empty<object>() : (object[])arguments.Clone();
    }

    public string Render()
    {
        var args = _arguments.Select(a => a is LocalizedMessage message ? message.Render() : a).ToArray();
        return args.Length == 0 ? Loc.Tr(Key) : Loc.Tr(Key, args);
    }

    public static LocalizedMessage FromInterpolated(FormattableString message)
    {
        var args = message.GetArguments();
        for (int i = 0; i < args.Length; i++)
            if (args[i] is string source) args[i] = new LocalizedMessage(source);
        return new LocalizedMessage(message.Format, args);
    }

    public static LocalizedMessage Join(string separator, System.Collections.Generic.IEnumerable<LocalizedMessage> messages)
    {
        var values = messages.Cast<object>().ToArray();
        string format = string.Join(separator, Enumerable.Range(0, values.Length).Select(i => "{" + i + "}"));
        return new LocalizedMessage(format, values);
    }

    public override string ToString() => Render();
    public static implicit operator LocalizedMessage(string key) => new LocalizedMessage(key);
}
