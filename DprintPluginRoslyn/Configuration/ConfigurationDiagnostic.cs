namespace Dprint.Plugins.Roslyn.Configuration;

public class ConfigurationDiagnostic
{
  public ConfigurationDiagnostic(string propertyName, string message)
  {
    PropertyName = propertyName;
    Message = message;
  }

  public string PropertyName { get; set; }
  public string Message { get; set; }
}
