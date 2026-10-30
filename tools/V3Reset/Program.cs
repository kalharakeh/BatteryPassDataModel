using System.Collections;
using V3Reset;

var environment = Environment.GetEnvironmentVariables()
    .Cast<DictionaryEntry>()
    .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value, StringComparer.OrdinalIgnoreCase);
return await App.RunAsync(args, environment, Console.In, Console.Out, $"v3-reset ({Environment.UserName})");
