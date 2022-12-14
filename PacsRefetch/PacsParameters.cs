using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PacsRefetch;

public class PacsParameters
{
    public string Hostname { get; set; } = null!;
    public ushort RemotePort { get; set; }
    public string RemoteName { get; set; } = null!;
    public ushort LocalPort { get; set; }
    public string LocalName { get; set; } = null!;
    public bool UseTls { get; set; }

    public override string ToString()
    {
        return new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()
            .Serialize(this);
    }

    public void Save(string file)
    {
        File.WriteAllText(file, this.ToString());
    }
    public static PacsParameters Load(string file)
    {
        return new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()
            .Deserialize<PacsParameters>(File.ReadAllText(file));
    }
}