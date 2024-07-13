using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using OmniSharp.Extensions.LanguageServer.Server;

namespace WebForms;

static class LanguageServerOptionsCommandLineExtensions
{
    public static LanguageServerOptions WithCommandLineCommunicationChannel(this LanguageServerOptions options,
        string[] args)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var commandLineOptions = CommandLineOptions.Parse(args);

        switch (commandLineOptions.CommunicationChannel)
        {
            case CommunicationChannel.ConsoleInputOutput:
                options.WithInput(Console.OpenStandardInput());
                options.WithOutput(Console.OpenStandardOutput());
                break;
            case CommunicationChannel.Pipe:
                var pipe = new NamedPipeClientStream(".", commandLineOptions.PipeName,
                    PipeDirection.InOut, PipeOptions.Asynchronous);
                // TODO: Make async? How to do that inside LanguageServer.From(Action<LanguageServerOptions>)?
                //await pipe.ConnectAsync(cancellationToken);
                pipe.Connect();
                options.WithInput(pipe);
                options.WithOutput(pipe);
                break;
            default:
                Debug.Assert(commandLineOptions.CommunicationChannel == CommunicationChannel.Socket);
                var client = new TcpClient();
                options.RegisterForDisposal(client);
                // TODO: Make async? How to do that inside LanguageServer.From(Action<LanguageServerOptions>)?
                //await client.ConnectAsync(IPAddress.Loopback, commandLineOptions.Port, cancellationToken);
                client.Connect(IPAddress.Loopback, commandLineOptions.Port);
                var stream = client.GetStream();
                options.WithInput(stream);
                options.WithOutput(stream);
                break;
        }

        return options;
    }

    enum CommunicationChannel
    {
        ConsoleInputOutput,
        Pipe,
        Socket
    }

    struct CommandLineOptions
    {
        public CommunicationChannel CommunicationChannel { get; init; }

        public string PipeName { get; init; }

        public int Port { get; init; }

        public static CommandLineOptions Parse(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return new CommandLineOptions { CommunicationChannel = CommunicationChannel.ConsoleInputOutput };
            }

            if (args.Length <= 2)
            {
                var firstArgument = args[0];
                var secondArgument = args.Length > 1 ? args[1] : null;

                if (firstArgument == "--stdio" && secondArgument == null)
                {
                    return new CommandLineOptions { CommunicationChannel = CommunicationChannel.ConsoleInputOutput };
                }

                if (firstArgument.StartsWith("--pipe"))
                {
                    // TODO: Handle --pipe appropriately when not running on Windows.
                    if (firstArgument == "--pipe" && secondArgument != null && secondArgument.StartsWith(@"\\.\pipe\"))
                    {
                        return new CommandLineOptions
                        {
                            CommunicationChannel = CommunicationChannel.Pipe,
                            PipeName = secondArgument.Substring(@"\\.\pipe\".Length)
                        };
                    }
                    else if (firstArgument.StartsWith(@"--pipe=\\.\pipe\") && secondArgument == null)
                    {
                        var pipeName = firstArgument.Substring(@"--pipe=\\.\pipe\".Length);

                        return new CommandLineOptions
                        {
                            CommunicationChannel = CommunicationChannel.Pipe,
                            PipeName = pipeName
                        };
                    }
                }
                else if (firstArgument.StartsWith("--socket"))
                {
                    if (firstArgument == "--socket" && secondArgument != null)
                    {
                        return new CommandLineOptions
                        {
                            CommunicationChannel = CommunicationChannel.Socket,
                            Port = int.Parse(secondArgument, NumberStyles.None, CultureInfo.InvariantCulture)
                        };
                    }
                    else if (firstArgument.StartsWith("--socket=") && secondArgument == null)
                    {
                        var port = int.Parse(firstArgument.Substring("--socket=".Length), NumberStyles.None,
                            CultureInfo.InvariantCulture);
                        return new CommandLineOptions
                        {
                            CommunicationChannel = CommunicationChannel.Socket,
                            Port = port
                        };
                    }
                }
            }

            throw new ArgumentException("Invalid command line communication channel arguments.", nameof(args));
        }
    }
}