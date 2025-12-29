using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;

namespace io.wispforest.textureswapper.utils;

public class ArgumentChatCommand : DebugCommandHandler.ChatCommand {
    public ArgumentChatCommand(
            ManualLogSource logger,
            string name, 
            string description, 
            List<ArgKeyBase> arguments,
            Action<bool, Arguments> execute, // isDebugConsole, args
            Func<bool> isEnabled = null, 
            bool debugOnly = true) : base(name, description, (isDebugConsole, strArgs) => {
        execute(isDebugConsole, new Arguments().parseArgs(logger, arguments, strArgs));
    }, (_, partial, strArgs) => {
        return new Arguments().parseArgs(logger, arguments, strArgs)
                .getMissingArgs(arguments).Select(@base => $"{@base.name}:")
                .Where(s => s.StartsWith(partial) || s.Contains(partial))
                .ToList();
    }, isEnabled, debugOnly) {
        
    }
}

public class Arguments {
    private Dictionary<ArgKeyBase, dynamic> _arguments = [];
    
    public Arguments parseArgs(ManualLogSource logger, List<ArgKeyBase> argKeys, string[] args) {
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

    public IEnumerable<ArgKeyBase> getMissingArgs(List<ArgKeyBase> argKeys) {
        return argKeys.Where(@base => !_arguments.ContainsKey(@base)).ToList();
    }

    public T get<T>(ArgKey<T> key) => key.getArg(_arguments);
}

public interface ArgKeyBase {
    public string name { get; }

    public bool matches(string name);

    public void setArg(ManualLogSource logger, Dictionary<ArgKeyBase, dynamic> parsedArgs, string strValue);
}

public class ArgKey<T> : ArgKeyBase {
    private readonly Func<string, (string? msg, T t)> parser;
    private readonly T? defaultValue;
      
    public ArgKey(string name, Func<string, (string msg, T t)> parser, T? defaultValue = default) {
        this.name = name;
        this.parser = parser;
        this.defaultValue = defaultValue;
    }

    public string name { get; }

    public bool matches(string name) => this.name.Equals(name);

    public void setArg(ManualLogSource logger, Dictionary<ArgKeyBase, dynamic> parsedArgs, string strValue) {
        var value = parser(strValue);

        if (value.msg != null) {
            logger.LogWarning($"Argument [{name}]: {value.msg}");
            
            return;
        }
         
        parsedArgs[this] = value.t;
    }

    public T getArg(Dictionary<ArgKeyBase, dynamic> parsedArgs) => (parsedArgs.ContainsKey(this) ? parsedArgs[this] : null) ?? defaultValue;
}