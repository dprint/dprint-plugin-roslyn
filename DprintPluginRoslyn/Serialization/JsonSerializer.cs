using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;

namespace Dprint.Plugins.Roslyn.Serialization
{
  public class JsonSerializer
  {
    public T Deserialize<T>(string jsonText)
    {
      return JsonConvert.DeserializeObject<T>(jsonText, GetSettings()) ?? throw new Exception("Error deserializing JSON.");
    }

    public string Serialize<T>(T obj)
    {
      return JsonConvert.SerializeObject(obj, GetSettings()) ?? throw new Exception("Error serializing to JSON.");
    }

    private JsonSerializerSettings GetSettings()
    {
      return new JsonSerializerSettings
      {
        ContractResolver = new DefaultContractResolver
        {
          NamingStrategy = new CamelCaseNamingStrategy()
        }
      };
    }
  }
}
