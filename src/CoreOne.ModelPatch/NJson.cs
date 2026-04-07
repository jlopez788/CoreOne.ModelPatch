using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreOne.ModelPatch;

internal static class NJson
{
    private static readonly NewtonSettings _settings = new() {
        TypeNameHandling = TypeNameHandling.None
    };

    public static T? Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, _settings) ?? default(T);

    public static string Serialize<T>(T model) => JsonConvert.SerializeObject(model, _settings);
}