using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using REPOLib.Modules;

namespace io.wispforest.textureswapper.utils;

public delegate void ExecuteFunc(bool isDebug, Arguments args);
public delegate bool AccessPredicate();

/// <summary>
/// Helper class acting as an extension for DebugCommandHandler.ChatCommand with
/// ability to declare Args that can be reused for other commands and have proper
/// type safety and validation for command args
/// </summary>
/// <param name="logger"> Logger used to indicate in the log errors when parsing args </param>
/// <param name="name"> Name of the given command used when attempting to run the command within the console </param>
/// <param name="description"> Description of what the command dose</param>
/// <param name="arguments"> Arguments required in the order given for users to enter that may or may not be required </param>
/// <param name="func"> Primary function where the command will be run if all args required are present</param>
/// <param name="predicate"> Predicate to check if the given command should be accessible/useable at the moment </param>
/// <param name="debugOnly"> If the command should only exist if debug mode for the game is enabled </param>
public class ArgumentChatCommand(
        ManualLogSource logger,
        string name,
        string description,
        List<ArgKey> arguments,
        ExecuteFunc func, 
        AccessPredicate? predicate = null,
        bool debugOnly = true) : DebugCommandHandler.ChatCommand(name, description, fromExecuteFunc(name, logger, arguments, func), createSuggestionProvider(logger, arguments), predicate == null ? (() => true) : predicate!.Invoke, debugOnly) {

    public static void createAndRegister(ManualLogSource logger, string name, string description, List<ArgKey> arguments, ExecuteFunc func, AccessPredicate? predicate = null, bool debugOnly = true) {
        var command = new ArgumentChatCommand(logger, name, description, arguments, func, predicate, debugOnly);

        Commands.RegisterCommand(command);
    }
    
    private static Action<bool, string[]> fromExecuteFunc(string name, ManualLogSource logger, List<ArgKey> arguments, ExecuteFunc func) {
        return (isDebugConsole, strArgs) => {
            var args = new Arguments().parseArgs(logger, arguments, strArgs);

            var missingArgs = args.getMissingArgs(arguments);
            
            if (missingArgs.isNotEmpty()) {
                logger.LogError($"Unable to execute '{name}' as its missing the required args: {missingArgs.toPrettyString()}");
            } else {
                func(isDebugConsole, new Arguments().parseArgs(logger, arguments, strArgs));
            }
        };
    }

    private static Func<bool, string, string[], List<string>> createSuggestionProvider(ManualLogSource logger, List<ArgKey> arguments) {
        return (_, partial, strArgs) => {
            return new Arguments()
                    .parseArgs(logger, arguments, strArgs)
                    .getMissingArgs(arguments)
                    .Select(@base => $"{@base.name}:")
                    .Where(s => s.StartsWith(partial) || s.Contains(partial))
                    .ToList();
        };
    }
}

/// <summary>
/// Holder object for all arguments parsed with asses to args via the get method
/// </summary>
public class Arguments {
    private Dictionary<ArgKey, dynamic> _arguments = [];
    
    public Arguments parseArgs(ManualLogSource logger, List<ArgKey> argKeys, string[] args) {
        foreach (var arg in args) {
            var parts = arg.Split(":");

            if (parts.Length <= 1) continue;

            var wasParsed = false;
            
            foreach (var argKey in argKeys) {
                if (!argKey.matches(parts[0])) continue;
                
                argKey.setArg(logger, _arguments, parts[1]);

                wasParsed = true;
            }

            if (!wasParsed && !String.IsNullOrWhiteSpace(parts[1])) {
                logger.LogWarning($"'{parts[0]}' is not a valid argument name.");
            }
        }

        return this;
    }

    public IList<ArgKey> getMissingArgs(List<ArgKey> argKeys) => argKeys.Where(key => !_arguments.ContainsKey(key) && !key.isDefaulted).ToList();

    public T get<T>(ArgKey<T> key) => key.getArg(_arguments);
}

public interface ArgKey {

    public static ArgKey<T> required<T>(string name, ArgumentParser<T> parser) {
        return new ArgKey<T>(name, parser, false);
    }
    
    public static ArgKey<T> optional<T>(string name, ArgumentParser<T> parser, T defaultValue = default) {
        return new ArgKey<T>(name, parser, false, defaultValue);
    }
    
    public bool isDefaulted { get; }
    
    public string name { get; }

    public bool matches(string name);

    public void setArg(ManualLogSource logger, Dictionary<ArgKey, dynamic> parsedArgs, string strValue);
}

public class ArgKey<T> : ArgKey {
    private readonly ArgumentParser<T?> _parser;
    private readonly T _defaultValue;
    
    internal ArgKey(string name, ArgumentParser<T?> parser, bool isDefaulted, T defaultValue = default) {
        _parser = parser;
        _defaultValue = defaultValue;
        this.isDefaulted = isDefaulted;
        this.name = name;
    }
    public bool isDefaulted { get; }
    public string name { get; }

    public bool matches(string name) => this.name.Equals(name);

    public void setArg(ManualLogSource logger, Dictionary<ArgKey, dynamic> parsedArgs, string strValue) {
        var value = _parser(strValue);

        if (value.hasErrored) {
            logger.LogWarning($"Argument [{name}]: {value.errorMsg}");
            
            return;
        }
        
        parsedArgs[this] = value.result!;
    }

    public T getArg(Dictionary<ArgKey, dynamic> parsedArgs) {
        var test = parsedArgs.ContainsKey(this) ? parsedArgs[this] : null;

        if (test == null && !isDefaulted) {
            throw new Exception($"Unable to get arg '{name}' as the value is not defaulted and was never parsed into arguments!");
        } 

        return !(test ?? _defaultValue);
    }
}

public delegate Result<T?> ArgumentParser<T>(string str);

public static class ArgumentParsers {
    public static ArgumentParser<int> 
        integerNum() {
        return str => of(int.TryParse(str, out var result), result, str);
    }
    
    public static ArgumentParser<float> floatNum() {
        return str => of(float.TryParse(str, out var result), result, str);
    }
    
    public static Result<T?> of<T>(bool wasParsed, T result, string str) => new (result, !wasParsed, $"Unable to parse value '{str}'");
}

public class Result<T>(T? result, bool hasErrored, string? errorMsg) {
    public T? result { get; } = result;
    public bool hasErrored { get; } = hasErrored;
    public string? errorMsg { get; } = errorMsg;
    
}